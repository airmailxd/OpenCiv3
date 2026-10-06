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
	// refuses, and we declare war on it. Military allies are welcome.
	public static class TerritoryDemands {
		private static ILogger log = Log.ForContext(typeof(TerritoryDemands));

		// A human civ that agrees to leave and comes back this many times,
		// each within MEMORY_TURNS of the last demand, gets war instead of
		// another warning. AIs are only ever warned: their units cross
		// borders in the ordinary course of things, and an AI that wants a
		// war plans one.
		public const int WITHDRAWALS_BEFORE_WAR = 3;
		public const int MEMORY_TURNS = 20;

		public static async Task MakeDemands(Player us, GameData gameData) {
			foreach (KeyValuePair<Player, List<MapUnit>> trespass in FindTrespassers(us, gameData)) {
				Player them = trespass.Key;
				// Units with nowhere to go can't be told to leave, so they
				// neither count against their civ nor give us a reason for war.
				List<MapUnit> units = trespass.Value.FindAll(u => u.CanWithdraw());
				if (units.Count == 0) {
					continue;
				}
				PlayerRelationship ourView = us.playerRelationships[them.id];

				// Forget promises the other civ kept for a good while.
				if (ourView.lastWithdrawalDemandTurn < 0
					|| gameData.turn - ourView.lastWithdrawalDemandTurn > MEMORY_TURNS) {
					ourView.recentWithdrawals = 0;
				}
				bool repeatOffense = ourView.recentWithdrawals > 0;
				ourView.lastWithdrawalDemandTurn = gameData.turn;

				if (them.isHuman && ourView.recentWithdrawals >= WITHDRAWALS_BEFORE_WAR) {
					log.Information("{Us} has had enough of {Them} trespassing and declares war", us, them);
					DeclareWar(us, them);
					continue;
				}

				bool withdraw = await AskToWithdraw(us, them, units.Count, repeatOffense);
				if (withdraw) {
					int withdrawn = Withdraw(units);
					if (withdrawn > 0) {
						++ourView.recentWithdrawals;
					}
					log.Information("{Them} withdrew {Count} units from {Us}'s territory", them, withdrawn, us);
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
		// and right of passage, military alliances and permanent alliances
		// let units in.
		private static bool MustStayOutOf(Player them, Player us, GameData gameData) {
			if (them == us || them.isBarbarians || them.defeated || !them.isIncludedInGame) {
				return false;
			}
			if (!TryGetRelationship(us, them, out PlayerRelationship relationship) || relationship.AtWar()) {
				return false;
			}
			return !HaveActiveRightOfPassage(us, them) && !HaveMilitaryAlliance(relationship)
				&& !gameData.AreInLockedPeace(us, them);
		}

		private static bool HaveMilitaryAlliance(PlayerRelationship relationship) {
			return relationship.multiTurnDeals.Exists(d => d.dealSubType == DealSubType.MilitaryAlliance);
		}

		private static async Task<bool> AskToWithdraw(Player us, Player them, int unitCount, bool repeatOffense) {
			// AIs back down rather than start a war over it. A civ planning a
			// war will declare it when it's ready.
			if (!them.isHuman) {
				return true;
			}
			// A LAN guest who has left can't answer, and is taken to agree.
			if (!EngineStorage.IsPlayerReachable(them.id)) {
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
			// A LAN host closes the screen for a guest who leaves or rejoins
			// while it is up, so this always ends.
			await EngineStorage.WaitForDiplomacyCompleted(them.id);
			EngineStorage.diplomacyPlayerID = null;

			// Closing the screen without answering is taken as agreeing, so a
			// stray key press can't start a war.
			bool withdraw = EngineStorage.territoryDemandAnswer ?? true;
			EngineStorage.territoryDemandAnswer = null;
			return withdraw;
		}

		// Returns how many of the units moved out.
		private static int Withdraw(List<MapUnit> units) {
			int withdrawn = 0;
			foreach (MapUnit unit in units) {
				if (unit.WithdrawToNearestFreeTile()) {
					++withdrawn;
				}
			}
			return withdrawn;
		}

		private static void DeclareWar(Player aggressor, Player defender) {
			aggressor.DeclareWarOn(defender, EngineStorage.gameData.turn);
			new MsgWarDeclaration(aggressor, defender).send();
		}
	}
}
