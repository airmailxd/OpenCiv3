using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using C7GameData.AIData;
using Serilog;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI.UnitAI {
	// Airdrops by the AI (see MapUnit.Airdrop): a land unit with the BIQ's
	// airdrop, in a city of ours with an airport (or on an airfield), flies
	// to land within its operational range, using its turn. Before this the
	// AI never airdropped, though Modern gives it the Settler (range 9), the
	// Modern Trooper, the RPG Trooper and the Super Trooper.
	// - A settler drops at (or next to) the site it was going to found a
	//   city on, when that is at least MinSettlerDropDistance tiles off or
	//   over water: the distant land settled at once rather than after a
	//   long walk or a voyage. Only onto land no enemy can reach next turn.
	// - At war, paratroopers not in a war campaign drop next to an enemy
	//   city within range that they, with our units already next to it,
	//   can expect to take (WarCampaign.ExpectedHits against the defenders
	//   we see, or the campaign's guess), onto tiles no enemy stands on:
	//   where ours stand if any, else the best defended. They attack from
	//   there on their next turn (see CombatAI). Helicopters, which carry
	//   their passengers, are left to the player.
	public static class AirdropAI {
		private static readonly ILogger log = Log.ForContext(typeof(AirdropAI));

		// Lets the League AI games count airdrops. Null outside them.
		internal static Action<MapUnit, Tile> onAirdrop;

		// HEURISTIC: a settler drops for a site at least this far off.
		internal const int MinSettlerDropDistance = 4;
		// HEURISTIC: the hits the paratroopers must expect to deal, over the
		// defenders' hit points, before they drop next to a city.
		internal const double DropMargin = 1.2;

		public static bool CanAirdrop(UnitPrototype type) {
			return type.IsLandUnit() && type.actions.Contains(UnitAction.Airdrop) && type.operationalRange > 0;
		}

		// Whether the player has, or knows the advance for, a land unit that
		// airdrops, so an airport is a base for it (see BuildingEffects).
		public static bool HasAirdroppers(Player player) {
			if (player.units.Any(u => CanAirdrop(u.unitType))) {
				return true;
			}
			GameData gameData = EngineStorage.gameData;
			return gameData != null && gameData.unitPrototypes.Any(p => CanAirdrop(p) && !p.isKing
				&& (p.requiredTech == null || player.HasTech(p.requiredTech.id)));
		}

		// HEURISTIC: how many of a civ's cities want an airport to drop
		// from: its most productive ones.
		internal const int AirBases = 2;

		// Whether the city is one the civ would drop from.
		public static bool WantsAirBase(Player player, City city) {
			if (!HasAirdroppers(player)) {
				return false;
			}
			return player.cities.OrderByDescending(c => c.CurrentProductionYield().useful).ThenBy(c => c.id.ToString())
				.Take(AirBases).Contains(city);
		}

		public static void PlayTurn(Player player, GameData gameData) {
			if (player.isHuman || player.IsIndependent || player.cities.Count == 0) {
				return;
			}
			List<MapUnit> droppers = player.units.Where(u => CanAirdrop(u.unitType) && u.CanAirdropFrom()).ToList();
			if (droppers.Count == 0) {
				return;
			}
			AIThreat threat = AIThreat.Assess(player);
			foreach (MapUnit u in droppers.Where(u => u.unitType.actions.Contains(UnitAction.BuildCity))) {
				DropSettler(player, u, threat);
			}
			List<MapUnit> troopers = droppers.Where(u => !u.unitType.actions.Contains(UnitAction.BuildCity) && u.unitType.attack > 0
				&& u.currentAI is not WarCampaign.MemberAI && PlayerAI.IsStillOurs(u, player) && u.CanAirdropFrom()).ToList();
			if (troopers.Count > 0 && IsInAnyWar(player, gameData.players)) {
				DropTroopers(player, gameData, troopers);
			}
		}

		private static void Drop(MapUnit u, Tile tile, string why) {
			log.Information("{Player} airdrops {Unit} from {From} to {To} ({Why})", u.owner, u, u.location, tile, why);
			u.Airdrop(tile);
			onAirdrop?.Invoke(u, tile);
		}

		private static void DropSettler(Player player, MapUnit u, AIThreat threat) {
			if (u.currentAI is not SettlerAI s || s.data == null || s.data.goal != SettlerAIData.SettlerGoal.BUILD_CITY) {
				return;
			}
			Tile site = s.data.destination;
			if (site == null || site == Tile.NONE || site == u.location) {
				return;
			}
			bool overWater = site.continent != u.location.continent;
			if (!overWater && u.location.DistanceTo(site) < MinSettlerDropDistance) {
				return;
			}
			Tile best = null;
			int bestDistance = int.MaxValue;
			foreach (Tile t in site.neighbors.Values.Append(site)) {
				if (t == Tile.NONE || t.continent != site.continent || !u.CanAirdropTo(t) || threat.EnemyCanReach(t)) {
					continue;
				}
				int d = t.DistanceTo(site);
				if (d < bestDistance) {
					best = t;
					bestDistance = d;
				}
			}
			if (best == null) {
				return;
			}
			Drop(u, best, $"to found a city at {site}");
			// The settler finds its way (and its site) again from where it
			// landed.
			u.currentAI = null;
		}

		private static void DropTroopers(Player player, GameData gameData, List<MapUnit> troopers) {
			// The enemy cities in reach of a trooper, the nearest first.
			List<City> targets = gameData.cities.Where(c => c.owner != player && !c.owner.IsAtPeaceWith(player)
					&& !c.owner.isBarbarians && AIFogOfWar.KnowsTile(player, c.location)
					&& troopers.Any(u => u.location.DistanceTo(c.location) <= u.OperationalRange() + 1))
				.OrderBy(c => troopers.Min(u => u.location.DistanceTo(c.location))).ThenBy(c => c.id.ToString()).ToList();
			foreach (City city in targets) {
				List<MapUnit> able = troopers.Where(u => PlayerAI.IsStillOurs(u, player) && u.CanAirdropFrom()).ToList();
				if (able.Count == 0) {
					return;
				}
				int defenderHitPoints = AIFogOfWar.SeesUnitsOn(player, city.location)
					? WarCampaign.DefenderHitPoints(player, city.location)
					: 3 * WarCampaign.EstimateDefenders(player, city);
				double needed = DropMargin * Math.Max(1, defenderHitPoints);
				// Our attackers already next to it count too.
				double hits = city.location.neighbors.Values.Where(t => t != Tile.NONE).SelectMany(t => t.unitsOnTile)
					.Where(x => x.owner == player && x.IsLandUnit() && x.unitType.attack > 0).Sum(x => Hits(x, city));
				List<(MapUnit u, Tile tile)> plan = new();
				foreach (MapUnit u in able) {
					if (hits >= needed) {
						break;
					}
					Tile tile = DropTile(player, u, city);
					if (tile == null) {
						continue;
					}
					plan.Add((u, tile));
					hits += Hits(u, city);
				}
				if (plan.Count == 0 || hits < needed) {
					continue;
				}
				foreach ((MapUnit u, Tile tile) in plan) {
					if (u.CanAirdropTo(tile)) {
						Drop(u, tile, $"to take {city.name}");
						troopers.Remove(u);
					}
				}
			}
		}

		private static double Hits(MapUnit u, City city) {
			MapUnit defender = city.location.FindTopDefender(u);
			return MapUnit.IsMapUnitValid(defender) ? WarCampaign.ExpectedHits(u, defender) : 3;
		}

		// The tile next to the city the unit would drop onto: one it can,
		// where ours already stand if any, else the best defended.
		private static Tile DropTile(Player player, MapUnit u, City city) {
			return city.location.neighbors.Values.Where(t => t != Tile.NONE && t.IsLand() && u.CanAirdropTo(t))
				.OrderByDescending(t => t.unitsOnTile.Count(x => x.owner == player))
				.ThenByDescending(t => t.overlayTerrainType.defenseBonus.amount)
				.ThenBy(t => t.DistanceTo(u.location)).FirstOrDefault();
		}
	}
}
