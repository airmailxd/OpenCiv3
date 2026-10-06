using System.Collections.Generic;
using System.Threading.Tasks;
using C7GameData;
using Serilog;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI {
	// As in Civ3, a civ doesn't put up with another civ's units in its
	// territory. Each turn it tells every civ it is at peace with, and that
	// has no right of passage, to take its units out of its borders. The
	// other civ either withdraws them, and they are moved out at once, or
	// refuses, and we declare war on it.
	public static class TerritoryDemands {
		private static ILogger log = Log.ForContext(typeof(TerritoryDemands));

		// A civ that agrees to leave and comes back this many times, each
		// within MEMORY_TURNS of the last demand, gets war instead of another
		// warning.
		public const int WITHDRAWALS_BEFORE_WAR = 3;
		public const int MEMORY_TURNS = 20;

		public static async Task MakeDemands(Player us, GameData gameData) {
			foreach (KeyValuePair<Player, List<MapUnit>> trespass in FindTrespassers(us, gameData)) {
				Player them = trespass.Key;
				List<MapUnit> units = trespass.Value;
				PlayerRelationship ourView = us.playerRelationships[them.id];

				// Forget promises the other civ kept for a good while.
				if (ourView.lastWithdrawalDemandTurn < 0
					|| gameData.turn - ourView.lastWithdrawalDemandTurn > MEMORY_TURNS) {
					ourView.recentWithdrawals = 0;
				}
				bool repeatOffense = ourView.recentWithdrawals > 0;
				ourView.lastWithdrawalDemandTurn = gameData.turn;

				if (ourView.recentWithdrawals >= WITHDRAWALS_BEFORE_WAR) {
					log.Information("{Us} has had enough of {Them} trespassing and declares war", us, them);
					DeclareWar(us, them);
					continue;
				}

				bool withdraw = await AskToWithdraw(us, them, units.Count, repeatOffense);
				if (withdraw) {
					++ourView.recentWithdrawals;
					Withdraw(units);
					log.Information("{Them} withdrew {Count} units from {Us}'s territory", them, units.Count, us);
				} else {
					// Then we'll throw them out by force.
					log.Information("{Them} refused to leave {Us}'s territory", them, us);
					DeclareWar(us, them);
				}
			}
		}

		// The units of each civ we'd tell to leave that are in our territory,
		// all of which we can see. Units aboard a transport go wherever it
		// goes, so only the transport is listed.
		public static Dictionary<Player, List<MapUnit>> FindTrespassers(Player us, GameData gameData) {
			Dictionary<Player, List<MapUnit>> result = new();
			foreach (Player them in gameData.players) {
				if (!MustStayOutOf(them, us, gameData)) {
					continue;
				}
				foreach (MapUnit unit in them.units) {
					Tile tile = unit.location;
					if (unit.IsLoaded() || tile == null || tile == Tile.NONE || tile.OwningPlayer() != us) {
						continue;
					}
					if (!result.TryGetValue(them, out List<MapUnit> units)) {
						result[them] = units = new();
					}
					units.Add(unit);
				}
			}
			return result;
		}

		// Whether us would tell them to stay out: civs at war fight instead,
		// and right of passage and permanent alliances let units in.
		private static bool MustStayOutOf(Player them, Player us, GameData gameData) {
			if (them == us || them.isBarbarians || them.defeated || !them.isIncludedInGame) {
				return false;
			}
			if (!TryGetRelationship(us, them, out PlayerRelationship relationship) || relationship.AtWar()) {
				return false;
			}
			return !HaveActiveRightOfPassage(us, them) && !gameData.AreInLockedPeace(us, them);
		}

		private static async Task<bool> AskToWithdraw(Player us, Player them, int unitCount, bool repeatOffense) {
			// AIs back down rather than start a war over it. A civ planning a
			// war will declare it when it's ready.
			if (!them.isHuman) {
				return true;
			}

			// The human answering takes the UI. In a hotseat game they may
			// not be the player at the screen.
			if (EngineStorage.uiFollowsActivePlayer) {
				EngineStorage.uiControllerID = them.id;
			}
			EngineStorage.diplomacyPlayerID = them.id;
			EngineStorage.territoryDemandAnswer = null;
			new MsgShowTerritoryDemand(us, them, unitCount, repeatOffense).send();
			await EngineStorage.WaitForMessageToEngine<MsgDiplomacyCompleted>();
			EngineStorage.diplomacyPlayerID = null;

			// Closing the screen without answering is taken as agreeing, so a
			// stray key press can't start a war.
			bool withdraw = EngineStorage.territoryDemandAnswer ?? true;
			EngineStorage.territoryDemandAnswer = null;
			return withdraw;
		}

		private static void Withdraw(List<MapUnit> units) {
			foreach (MapUnit unit in units) {
				// A unit with nowhere to go stays put, and will be asked
				// about again.
				unit.WithdrawToNearestFreeTile();
			}
		}

		private static void DeclareWar(Player aggressor, Player defender) {
			aggressor.DeclareWarOn(defender, EngineStorage.gameData.turn);
			new MsgWarDeclaration(aggressor, defender).send();
		}
	}
}
