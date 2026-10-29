using System.Collections.Generic;
using C7GameData;
using Serilog;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI {
	// As in Civ3, a civ doesn't put up with another civ's units in its
	// territory. Each turn it tells every AI civ it is at peace with, and
	// that has no right of passage, to take its units out of its borders, if
	// they are enough of a threat (see IsThreatening). Unlike Civ3, humans
	// are never told (see MakeDemands). The other civ withdraws them, and
	// they are moved out at once. Military allies are welcome.
	public static class TerritoryDemands {
		private static ILogger log = Log.ForContext(typeof(TerritoryDemands));

		// How long a civ's promises to keep out are remembered.
		public const int MEMORY_TURNS = 20;

		public static void MakeDemands(Player us, GameData gameData) {
			Dictionary<Player, List<MapUnit>> trespassers = FindTrespassers(us, gameData);
			// With the AI fog of war, only the trespassers we can see.
			if (AIFogOfWar.EnabledFor(us)) {
				AIFogOfWar.Refresh(us);
				foreach (Player them in new List<Player>(trespassers.Keys)) {
					trespassers[them].RemoveAll(u => !AIFogOfWar.SeesUnitsOn(us, u.location));
					if (trespassers[them].Count == 0) {
						trespassers.Remove(them);
					}
				}
			}
			// A lone unit that left on its own starts the count over if it
			// comes back.
			foreach (Player them in gameData.players) {
				if (!trespassers.ContainsKey(them) && us.playerRelationships.TryGetValue(them.id, out PlayerRelationship view)) {
					view.loneTrespasserSinceTurn = -1;
				}
			}

			foreach (KeyValuePair<Player, List<MapUnit>> trespass in trespassers) {
				Player them = trespass.Key;
				// Not Civ3: an AI never tells a human to leave, nor goes to
				// war over their units in its territory (owner's decision,
				// 2026-10-22). AIs still warn each other.
				if (them.isHuman) {
					continue;
				}
				// Units with nowhere to go can't be told to leave, so they
				// neither count against their civ nor give us a reason for war.
				List<MapUnit> units = trespass.Value.FindAll(u => u.CanWithdraw());
				PlayerRelationship ourView = us.playerRelationships[them.id];
				if (!IsThreatening(units, ourView, gameData.turn)) {
					continue;
				}

				// Forget promises the other civ kept for a good while.
				if (ourView.lastWithdrawalDemandTurn < 0
					|| gameData.turn - ourView.lastWithdrawalDemandTurn > MEMORY_TURNS) {
					ourView.recentWithdrawals = 0;
				}
				ourView.lastWithdrawalDemandTurn = gameData.turn;

				// AIs back down rather than start a war over it. A civ
				// planning a war will declare it when it's ready.
				int withdrawn = Withdraw(units);
				if (withdrawn > 0) {
					++ourView.recentWithdrawals;
				}
				log.Information("{Them} withdrew {Count} units from {Us}'s territory", them, withdrawn, us);
			}
		}

		// How many turns a lone combat unit may stay in our territory before
		// we tell its civ to take it out: one of these, picked at random.
		public const int MIN_LONE_TRESPASSER_TURNS = 2;
		public const int MAX_LONE_TRESPASSER_TURNS = 3;

		// Whether the units a civ has in our territory are worth a demand.
		// Only combat units count, so workers, settlers and scouts are let
		// be. Two or more land units, two or more ships, or a land unit and
		// a ship are told to leave at once. A single land unit is put up
		// with for a couple of turns, and a single ship for good.
		public static bool IsThreatening(List<MapUnit> units, PlayerRelationship ourView, int turn) {
			int land = units.FindAll(u => u.IsCombatUnit() && u.IsLandUnit()).Count;
			int sea = units.FindAll(u => u.IsCombatUnit() && u.IsWaterUnit()).Count;

			if (land != 1 || sea != 0) {
				ourView.loneTrespasserSinceTurn = -1;
				return land >= 2 || sea >= 2 || (land >= 1 && sea >= 1);
			}

			if (ourView.loneTrespasserSinceTurn < 0) {
				ourView.loneTrespasserSinceTurn = turn;
				ourView.loneTrespasserPatience = GameData.rng.Next(MIN_LONE_TRESPASSER_TURNS, MAX_LONE_TRESPASSER_TURNS + 1);
			}
			if (turn - ourView.loneTrespasserSinceTurn + 1 < ourView.loneTrespasserPatience) {
				return false;
			}
			ourView.loneTrespasserSinceTurn = -1;
			return true;
		}

		// The units of each civ we'd tell to leave that are in our territory,
		// all of which we can see. Units aboard a transport go wherever it
		// goes, so only the transport is listed.
		public static Dictionary<Player, List<MapUnit>> FindTrespassers(Player us, GameData gameData) {
			Dictionary<Player, List<MapUnit>> result = new();
			foreach (Player them in gameData.players) {
				List<MapUnit> units = TrespassersOf(us, them, gameData);
				if (units.Count > 0) {
					result[them] = units;
				}
			}
			return result;
		}

		// Like FindTrespassers, for a single civ.
		private static List<MapUnit> TrespassersOf(Player us, Player them, GameData gameData) {
			List<MapUnit> result = new();
			if (!MustStayOutOf(them, us, gameData)) {
				return result;
			}
			foreach (MapUnit unit in them.units) {
				Tile tile = unit.location;
				if (unit.IsLoaded() || tile == null || tile == Tile.NONE || tile.OwningPlayer() != us) {
					continue;
				}
				result.Add(unit);
			}
			return result;
		}

		// The units us could tell them to take out of its territory, leaving
		// out any that have nowhere to go.
		public static List<MapUnit> UnitsToWithdraw(Player us, Player them, GameData gameData) {
			return TrespassersOf(us, them, gameData).FindAll(u => u.CanWithdraw());
		}

		// A human tells an AI to take its units out of the human's territory,
		// from the talk screen, as in Civ3. The AI withdraws them, unless it
		// is the stronger of the two, in which case it refuses and declares
		// war on the human. Returns whether the AI withdrew, or null if it
		// had nothing to withdraw.
		public static bool? DemandFromHuman(Player human, Player ai, GameData gameData) {
			List<MapUnit> units = UnitsToWithdraw(human, ai, gameData);
			if (ai.isHuman || units.Count == 0) {
				return null;
			}

			if (ai.CompareMilitaryStrengthTo(human) == Player.MilitaryStrength.StrongTo && PeaceAI.WouldWinWarWith(gameData, ai, human)) {
				log.Information("{Ai} refused to leave {Human}'s territory", ai, human);
				// Having been warned, it goes to war openly, even though its
				// units are inside the human's borders.
				ai.DeclareWarOn(human, gameData.turn, afterWarning: true);
				MsgWarDeclaration.Announce(ai, human);
				return false;
			}

			int withdrawn = Withdraw(units);
			log.Information("{Ai} withdrew {Count} units from {Human}'s territory", ai, withdrawn, human);
			return true;
		}

		// Whether us would tell them to stay out: civs at war fight instead,
		// and right of passage, military alliances and permanent alliances
		// let units in.
		private static bool MustStayOutOf(Player them, Player us, GameData gameData) {
			if (them == us || them.IsIndependent || them.defeated || !them.isIncludedInGame) {
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
	}
}
