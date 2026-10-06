using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using C7GameData;
using Serilog;

namespace C7Engine {

	public class TurnHandling {
		private static ILogger log = Log.ForContext<TurnHandling>();

		public static void OnBeginTurn() {
			GameData gameData = EngineStorage.gameData;
			log.Information("\n*** Beginning turn {Turn} ***", gameData.turn);
		}

		public static void OnEndTurn(Player player) {
			GameData gameData = EngineStorage.gameData;

			// The player's own list of units, rather than every unit in the
			// game. Copied, in case finishing a job changes it.
			foreach (MapUnit busyWorker in player.units.ToArray())
				if (busyWorker.WorkerJob != null)
					EngineStorage.ObserveTask(busyWorker.PerformEndOfTurnAction(), nameof(MapUnit.PerformEndOfTurnAction));
		}

		public static void InitTurnData(Player player = null, bool skipTurn = false) {
			GameData gameData = EngineStorage.gameData;
			if (player == null) {
				foreach (MapUnit mapUnit in gameData.mapUnits)
					mapUnit.OnBeginTurn(skipTurn);
			} else {
				foreach (MapUnit mapUnit in player.units)
					mapUnit.OnBeginTurn(skipTurn);
			}
		}

		// Implements the game loop. This method is called when the game is started and when the player signals that they're done moving.
		public static async Task AdvanceTurn() {
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			GameData gameData = EngineStorage.gameData;

			// Loop ends with a function return once we reach the UI controller during the movement phase
			while (true) {
				// The loop hands control back at a human, or at the player
				// being observed. With neither left, the AIs would play
				// against each other forever with nothing for the UI to do.
				if (!gameData.players.Any(p => !p.defeated && (p.isHuman || p.id == EngineStorage.activePlayerID))) {
					log.Information("No human players remain, ending the game");
					gameData.gameOver = true;
					new MsgNoHumansRemain().send();
					return;
				}

				bool firstTurn = GetTurnNumber() == 0;

				// Movement phase
				if (await PlayPlayerTurns(gameData, firstTurn)) {
					stopwatch.Stop();
					log.Debug("Turn time took {Milliseconds} milliseconds", stopwatch.ElapsedMilliseconds);
					return;
				}

				// Clear all wait queue, so if a player ended the turn without handling all waited units, they are selected
				// at the same place in the order. Confirmed this is what Civ3 does.
				UnitInteractions.ClearWaitQueue();

				BarbarianInteractions.SpawnBarbarians(gameData);

				gameData.turn++;
				foreach (Player player in gameData.players) {
					player.MaybeSpawnBonusUnits(gameData);
					player.DecrementCityUnhappinessPenalties(gameData);
					player.UpdateWarWeariness(gameData);
					player.RecalculateCitizenMoods(gameData, goIntoDisorderIfUnhappy: true);
					player.DoCorruptionCalculations(gameData);

					// Do the financial updates before updating the cities, so
					// that a newly produced unit won't put a player over the
					// unit support cap and cause them to lose gold unexpectedly.
					player.DoPerTurnFinanceUpdates(gameData);
					player.DoPerTurnScienceUpdates(gameData);

					// Free units and upgrades from wonders like the Statue of
					// Zeus and Leonardo's Workshop. Before city production, so
					// a wonder finished this turn starts counting next turn.
					WonderUnits.DoPerTurnUpdates(player, gameData);
					player.DoGreatLibraryUpdates(gameData);

					// Note that we do growth after calculating citizen moods,
					// to ensure that the player has a chance to deal with the
					// unhappiness of a new citizen during their turn.
					log.Information("\n*** City growth/production for turn {Turn}, player {Player} ***", gameData.turn, player);
					player.HandleCityUpdates(gameData);

					player.UpdateHistory(gameData);
					player.AdvanceGoldenAge();
				}

				// The United Nations votes once everyone has played the
				// voting turn, and may elect a winner.
				UnitedNations.ProcessEndOfRound(gameData);

				CheckVictory(gameData);

				// Now that the turn is ending, do all the bookkeeping for the
				// start of the next turn. We don't put the "hasPlayedThisTurn"
				// logic in OnBeginTurn because OnBeginTurn is called when a
				// save game is loaded, and that would erase the saved information
				// about which players have played.
				OnBeginTurn();
				InitTurnData();
				foreach (Player player in gameData.players) {
					player.hasPlayedThisTurn = false;
				}
			}
		}

		/// <summary>
		/// Plays the turns for all the players in the game (including barbarians).
		/// </summary>
		/// <param name="gameData"></param>
		/// <param name="firstTurn"></param>
		/// <returns>true when it is time for the human to take control again</returns>
		private static async Task<bool> PlayPlayerTurns(GameData gameData, bool firstTurn) {
			foreach (Player player in PlayersInTurnOrder(gameData)) {
				if (player.hasPlayedThisTurn || player.defeated) {
					continue;
				}

				if (firstTurn && player.SitsOutFirstTurn()) {
					continue;
				}

				if (player.isBarbarians) {
					await BarbarianAI.PlayTurn(player, gameData);
				} else if (!player.isHuman) {
					await PlayerAI.PlayTurn(player, gameData);
				}

				// Each human player takes the turn in order. In a hotseat game
				// the UI goes with them; in a LAN game it is their machine's turn.
				if (player.isHuman) {
					EngineStorage.activePlayerID = player.id;
					if (EngineStorage.uiFollowsActivePlayer) {
						EngineStorage.uiControllerID = player.id;
					}
				}

				//Human player check. Let the human see what's going on even if they are in observer mode.
				if (player.id == EngineStorage.activePlayerID) {
					if (player.isHuman) {
						// TODO: Before we call this method to automatically end obsolete deals, we could make this more versatile.
						// For example unless we have a good reason, as a human, receiving luxuries, gpt,
						// or having an active RoP, doesn't hurt us.
						PlayerRelationship.CheckForObsoleteDeals(player, gameData.players, gameData.turn);
					}
					new MsgStartTurn(player).send();
					UnitedNations.AskHumanToVote(gameData, player);
					return true;
				}

				OnEndTurn(player);
				player.hasPlayedThisTurn = true;
			}
			return false;
		}

		// Order players: Humans -> AI -> Barbarian AI. The sort is stable, so
		// humans play in the order they appear in the player list.
		internal static List<Player> PlayersInTurnOrder(GameData gameData) {
			return gameData.players.OrderByDescending(p => !p.isBarbarians).ThenByDescending(p => p.isHuman).ToList();
		}

		// Returns the human player who should control the UI when a game is
		// loaded: the first human who has yet to play this turn, or the first
		// human if they all have.
		internal static Player FirstHumanToPlay(GameData gameData) {
			List<Player> humans = PlayersInTurnOrder(gameData).Where(p => p.isHuman).ToList();
			return humans.FirstOrDefault(p => !p.hasPlayedThisTurn && !p.defeated) ?? humans.FirstOrDefault();
		}

		// True when more than one human shares this computer.
		public static bool IsHotseat(GameData gameData) {
			return gameData.players.Count(p => p.isHuman && !p.defeated) > 1;
		}

		///Eventually we'll have a game year or month or whatever, but for now this provides feedback on our progression
		public static int GetTurnNumber() {
			return EngineStorage.gameData.turn;
		}

		internal static void CheckVictory(GameData gameData) {
			if (gameData.gameOver)
				return; // Game is already over

			List<Tuple<Player, IVictory>> winners = [];

			foreach (Player player in gameData.players) {
				if (player.isBarbarians || player.defeated)
					continue;

				foreach (IVictory victory in gameData.victories) {
					VictoryStatus status = victory.Evaluate(player, gameData);
					if (victory.HasVictory(status)) {
						winners.Add(new Tuple<Player, IVictory>(player, victory));
						break;
					}
				}
			}

			if (winners.Count == 1) {
				DeclareVictory(winners[0].Item1, winners[0].Item2, gameData);
			} else if (winners.Count > 1) {
				var topScoring = winners.OrderByDescending(pv => {
					var player = pv.Item1;
					HistTurnRecord lastTurn = gameData.history[player.id.ToString()].LastOrDefault();
					return lastTurn?.Score ?? 0;
				}).First();
				DeclareVictory(topScoring.Item1, topScoring.Item2, gameData);
			}
		}

		private static void DeclareVictory(Player winner, IVictory victory, GameData gameData) {
			Log.Information("Player {Winner} has wone a {Victory} victory!",
				winner, victory.Header());

			gameData.winner = winner;
			gameData.gameOver = true;

			new MsgVictory(winner, victory).send();
		}
	}
}
