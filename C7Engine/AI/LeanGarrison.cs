using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using C7GameData;

namespace C7Engine.AI {
	// Trial switch "lean-garrison" (ZUR-71, the owner's idea 5 from
	// watching Advanced games, doc/ai-roadmap.md): Advanced AI only. Each
	// city keeps the defenders its place calls for, no more: an interior
	// city 1 (2 at war or with an enemy close), a border city 2 (3 at war or
	// with an enemy close). The owner saw 20 Infantry in a city far from any
	// enemy. The defenders it no longer wants become attackers (see
	// ChooseProducible.StockGarrisonBonus), and the surplus already in an
	// interior city walks to a city short of them (see
	// DefenderAI.FindAtRiskCityToDefend and PlayerAI's fortified units).
	// Nothing is disbanded. The Standard AI keeps Civ3's 2 defenders and 1
	// attacker per city.
	//
	// IsInterior is the shared "far from any rival" test, for lean-buildings
	// (ZUR-59) to reuse.
	public static class LeanGarrison {
		public const string Switch = "lean-garrison";

		public static bool On(Player player) => player != null && player.IsAdvancedAi && AiExperiments.Has(player, Switch);

		// UNVERIFIED: all the sizes below are guesses, not Civ3 rules.
		// A city is interior with no other civ's land it knows of within
		// ForeignTerritoryRadius tiles, no rival city it knows of within
		// DevelopmentAI's border distance (6), and, if it is on the coast,
		// none within CoastalExposureRadius (a landing could come by sea).
		public const int ForeignTerritoryRadius = 4;
		public const int CoastalExposureRadius = 12;
		// An enemy land unit we can see within this many tiles counts as
		// close (a few turns' march).
		public const int EnemyNearRadius = 3;
		public const int InteriorDefenders = 1;
		public const int InteriorDefendersThreatened = 2;
		public const int BorderDefenders = 2;
		public const int BorderDefendersThreatened = 3;
		// What a defensive unit loses in a city that already has its
		// defenders (about a defender's worth of score).
		public const float ExtraDefenderPenalty = 15;
		// What being short of defenders adds to a city's pull on a
		// defender with nowhere it must be (see
		// DefenderAI.FindAtRiskCityToDefend): enough to draw it some 30
		// tiles.
		public const float ShortCityPull = 30;

		// Whether the city is far from any rival: see the radii above.
		public static bool IsInterior(Player player, City city) {
			if (DevelopmentAI.NearRivalCity(player, city)) {
				return false;
			}
			foreach (Tile t in city.location.GetTilesWithinRankDistance(ForeignTerritoryRadius)) {
				Player owner = t.OwningPlayer();
				if (owner != null && owner != player && !owner.isBarbarians && AIFogOfWar.KnowsTile(player, t)) {
					return false;
				}
			}
			if (city.location.NeighborsOcean()) {
				foreach (Player other in EngineStorage.gameData.players) {
					if (other == player || other.IsIndependent || other.defeated) {
						continue;
					}
					foreach (City theirs in other.cities) {
						if (theirs.location.map == city.location.map && city.location.DistanceTo(theirs.location) <= CoastalExposureRadius
								&& AIFogOfWar.KnowsTile(player, theirs.location)) {
							return false;
						}
					}
				}
			}
			return true;
		}

		// Whether a hostile land unit (a civ we are at war with, or the
		// barbarians) we can see stands within EnemyNearRadius of the city.
		public static bool EnemyNear(Player player, City city) {
			foreach (Player other in EngineStorage.gameData.players) {
				if (!AIThreat.IsHostile(player, other)) {
					continue;
				}
				foreach (MapUnit u in other.units) {
					Tile t = u.location;
					if (t == null || t == Tile.NONE || !u.IsLandUnit() || !u.CanAttack() || u.IsLoaded()
							|| t.DistanceTo(city.location) > EnemyNearRadius
							|| !AIFogOfWar.SeesUnitsOn(player, t) || !u.IsSeenBy(player)) {
						continue;
					}
					return true;
				}
			}
			return false;
		}

		public static int DefendersWanted(Player player, City city, bool atWar) {
			bool threatened = atWar || EnemyNear(player, city);
			if (IsInterior(player, city)) {
				return threatened ? InteriorDefendersThreatened : InteriorDefenders;
			}
			return threatened ? BorderDefendersThreatened : BorderDefenders;
		}

		public static int DefendersWanted(Player player, City city) =>
			DefendersWanted(player, city, PlayerRelationship.IsInAnyWar(player, EngineStorage.gameData.players));

		// The city's defenders, as Civ3's AI strategy counts them (units
		// whose BIQ strategy is defence), not counting units marching with
		// a war campaign, which will leave, nor `except`.
		public static int Defenders(Player player, City city, MapUnit except = null) {
			int n = 0;
			foreach (MapUnit u in city.location.unitsOnTile) {
				if (u != except && u.owner == player && StockWarPlotting.IsDefensive(u) && u.currentAI is not WarCampaign.MemberAI) {
					++n;
				}
			}
			return n;
		}

		// The defenders all our cities want.
		public static int Need(Player player, bool atWar) {
			int wanted = 0;
			foreach (City c in player.cities) {
				wanted += DefendersWanted(player, c, atWar);
			}
			return wanted;
		}

		// Production: a city that already holds the defenders it wants
		// builds attackers or settlers instead. A city short of them
		// builds its own (or a surplus defender walks in, see
		// ShouldLeave). (Waiving this while any city of ours was short
		// kept it from ever applying while the civ expands: a new city
		// is always short.)
		internal static float DefenderAdjustment(ChooseProducible.ProducibleStats stats, City city, Player player, UnitPrototype unit) {
			if (!StockWarPlotting.IsDefensive(unit) || !On(player)) {
				return 0;
			}
			if (Defenders(player, city) < DefendersWanted(player, city, stats.atWar)) {
				return 0;
			}
			return -ExtraDefenderPenalty;
		}

		// Fortified defenders in a city that has more than it wants leave
		// for a city short of them, one a turn for each short city.
		// Worked out once a turn: the cities still short, and how many
		// each still needs.
		private static readonly ConditionalWeakTable<Player, Shortfall> shortfalls = new();

		private sealed class Shortfall {
			public int turn;
			public Dictionary<City, int> cityShort;
			public Dictionary<City, int> spare;
		}

		// Whether this fortified unit should wake and find a new post.
		internal static bool ShouldLeave(MapUnit unit, Player player) {
			if (!On(player) || !StockWarPlotting.IsDefensive(unit) || unit.location.cityAtTile?.owner != player
					|| unit.currentAI is WarCampaign.MemberAI) {
				return false;
			}
			int turn = EngineStorage.gameData?.turn ?? 0;
			if (!shortfalls.TryGetValue(player, out Shortfall s) || s.turn != turn) {
				bool atWar = PlayerRelationship.IsInAnyWar(player, EngineStorage.gameData.players);
				s = new Shortfall() { turn = turn, cityShort = new(), spare = new() };
				foreach (City c in player.cities) {
					int d = Defenders(player, c) - DefendersWanted(player, c, atWar);
					if (d < 0) {
						s.cityShort[c] = -d;
					} else if (d > 0) {
						s.spare[c] = d;
					}
				}
				shortfalls.AddOrUpdate(player, s);
			}
			City here = unit.location.cityAtTile;
			if (!s.spare.TryGetValue(here, out int spare) || spare <= 0) {
				return false;
			}
			// The nearest short city on our landmass takes it.
			City to = s.cityShort.Where(kv => kv.Value > 0 && kv.Key.location.continent == here.location.continent)
				.Select(kv => kv.Key).OrderBy(c => c.location.DistanceTo(here.location)).ThenBy(c => c.id.ToString()).FirstOrDefault();
			if (to == null) {
				return false;
			}
			s.spare[here] = spare - 1;
			s.cityShort[to] -= 1;
			return true;
		}
	}
}
