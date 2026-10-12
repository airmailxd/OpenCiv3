using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine.Pathing;
using C7GameData;
using C7GameData.AIData;
using Serilog;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI.UnitAI {
	// Plays an AI's army. An army's strength
	// is the hit points of all its members, fighting one after the other, so
	// it's the unit most likely to take a defended city and survive doing so.
	// It's wasted if it attacks half empty or half dead, so:
	//  - in one of our cities it takes in our offensive units there, and calls
	//    in those nearby, until it's full;
	//  - badly hurt, it goes back to a city and rests until its members heal;
	//  - at war, full and healthy, it attacks like any other attacker (see
	//    CombatAI), but only where it's likely to win;
	//  - at peace it waits in a city;
	//  - a war campaign takes it to lead its attack (see WarCampaign).
	// Leaders, which only elite units produce, are rare, so armies are too.
	public class ArmyAI : C7GameData.UnitAI {
		private static ILogger log = Log.ForContext<ArmyAI>();

		// Below this share of its hit points the army goes back to heal, and
		// it stays there until it has this much back.
		private const double RetreatHealth = 0.5;
		private const double ReadyHealth = 0.9;

		// Below this estimated chance of winning, the army doesn't attack.
		// Higher than for single units: losing an army loses all its members.
		private const double MinChanceToAttack = 0.6;

		// How far away (in tiles) units are called in to join the army.
		private const int RecruitRange = 6;

		// The current attack, while there is one.
		private CombatAI attack;

		// The path to the city the army is going back to, while it is.
		private TilePath pathHome;

		// Whether the player's armies are played by ArmyAI: an AI's are.
		public static bool IsEnabled(Player player) {
			return !player.isHuman && !player.isBarbarians;
		}

		// Whether a leader in one of our cities should form an army there
		// rather than hurry the city's production. An army is worth most at
		// war, with
		// offensive units nearby to fill it; at peace, only if we have none
		// yet and the units for one.
		internal static bool ShouldFormArmy(MapUnit leader, Player player) {
			if (!IsEnabled(player) || !leader.CanFormArmy()) {
				return false;
			}
			Tile tile = leader.location;
			int recruits = player.units.Count(u => IsRecruit(u, player) && u.location.DistanceTo(tile) <= RecruitRange);
			if (IsInAnyWar(player, EngineStorage.gameData.players)) {
				return recruits >= 2;
			}
			return recruits >= 3 && player.ArmyCount() == 0;
		}

		// The score of building an army in the city, or null not to. Only at
		// war, when the empire
		// can support another, with offensive units nearby to fill it.
		internal static float? ScoreArmy(City city, Player player, UnitPrototype unit, bool atWar) {
			if (!IsEnabled(player) || !atWar || !player.CanSupportAnotherArmy()) {
				return null;
			}
			int recruits = player.units.Count(u => IsRecruit(u, player) && u.location.DistanceTo(city.location) <= RecruitRange);
			if (recruits < 2) {
				return null;
			}
			return 25 - city.TurnsToProduce(unit);
		}

		// The extra score of the Military Academy (which builds armies) or
		// the Pentagon (larger armies), or null if it isn't worth building.
		internal static float? ScoreArmyWonder(Player player, Building building, bool atWar) {
			if (!IsEnabled(player)) {
				return null;
			}
			if (building.allowsBuildArmy) {
				return atWar ? 20 : 8;
			}
			return player.ArmyCount() >= 2 ? 10 : null;
		}

		// Has the leader form an army and fills it from the units at hand.
		internal static MapUnit FormArmy(MapUnit leader, Player player) {
			MapUnit army = leader.FormArmy();
			if (army == null) {
				return null;
			}
			army.currentAI = new ArmyAI();
			Recruit(army, player);
			return army;
		}

		public C7GameData.UnitAI.MoveResult PlayTurnImpl(Player player, MapUnit army) {
			int members = army.PassengerCount();
			City city = army.location.cityAtTile;
			bool inOurCity = city != null && city.owner == player;
			if (inOurCity && members < army.Capacity()) {
				Recruit(army, player);
				members = army.PassengerCount();
			}

			double health = HealthOf(army);
			bool atWar = IsInAnyWar(player, EngineStorage.gameData.players);
			bool hurt = health < RetreatHealth || (pathHome != null && health < ReadyHealth);

			// An empty army has nothing to fight with: it waits in a city for
			// members, as does one that's still filling up in peacetime.
			bool readyToFight = members >= Math.Min(2, army.Capacity()) && !hurt && atWar
				&& (members == army.Capacity() || !RecruitsOnTheWay(army, player));

			if (readyToFight) {
				pathHome = null;
				return Attack(army, player);
			}
			attack = null;

			if (inOurCity) {
				pathHome = null;
				// Resting heals the members (see MapUnit.OnBeginTurn).
				army.SkipTurn();
				return C7GameData.UnitAI.Result.InProgress;
			}
			return GoHome(army, player);
		}

		// The army's hit points as a share of what its members have when
		// healthy.
		private static double HealthOf(MapUnit army) {
			int max = army.CompositeMaxHitPoints();
			return max > 0 ? (double)army.CompositeHitPoints() / max : 0;
		}

		private C7GameData.UnitAI.MoveResult Attack(MapUnit army, Player player) {
			if (attack == null) {
				CombatAIData target = CityToAttack(army, player) ?? CombatAI.MakeAiData(army, player);
				if (target == null || !WorthAttacking(army, target.destination)) {
					// Nothing worth an army to attack: wait where we are, or
					// in a city.
					if (army.location.cityAtTile?.owner == player) {
						army.SkipTurn();
						return C7GameData.UnitAI.Result.InProgress;
					}
					return GoHome(army, player);
				}
				attack = new CombatAI(target);
				log.Information("{Army} with {Members} members at {Health:0%} attacking {Target}", army, army.PassengerCount(), HealthOf(army), target.destination);
			}

			// Check the odds again before each attack: the defenders may have
			// changed, and the army may be hurt.
			Tile next = attack.data.path?.PeekNext() ?? Tile.NONE;
			if (next != Tile.NONE && !WorthAttacking(army, next)) {
				attack = null;
				army.SkipTurn();
				return C7GameData.UnitAI.Result.InProgress;
			}

			C7GameData.UnitAI.MoveResult result = attack.PlayTurnImpl(player, army);
			if (result.Result != C7GameData.UnitAI.Result.InProgress) {
				attack = null;
				// Plan again on the next step, unless that was the last of our
				// moves.
				return army.movementPoints.canMove ? C7GameData.UnitAI.Result.InProgress : result;
			}
			return result;
		}

		// How far (in tiles) the army looks for an enemy city to take.
		private const int CityRange = 10;

		// The nearest enemy city within reach that the army is likely to
		// take: a city is what an army is best at attacking, its hit points
		// lasting through several defenders. A city whose defenders we can't
		// see now is tried, and the odds checked again on arrival.
		private static CombatAIData CityToAttack(MapUnit army, Player player) {
			AIFogOfWar.Refresh(player);
			List<Tile> cities = new();
			foreach (City c in EngineStorage.gameData.cities) {
				Tile t = c.location;
				if (c.owner == player || !AtWar(player, c.owner) || t.DistanceTo(army.location) > CityRange
						|| !player.tileKnowledge.isTileKnown(t)) {
					continue;
				}
				if (AIFogOfWar.SeesUnitsOn(player, t) && !WorthAttacking(army, t)) {
					continue;
				}
				cities.Add(t);
			}
			if (cities.Count == 0) {
				return null;
			}
			cities = cities.OrderBy(t => t.DistanceTo(army.location)).ToList();
			int index = PathingAlgorithmChooser.GetAlgorithm(army).FindFirstReachable(army.location, cities, army, out TilePath path);
			if (index < 0 || path == null) {
				return null;
			}
			return new CombatAIData() { destination = cities[index], path = path };
		}

		// Whether the army should move onto the tile: there's no enemy there
		// we'd likely lose to.
		private static bool WorthAttacking(MapUnit army, Tile tile) {
			if (tile.unitsOnTile.Count == 0 || army.owner.IsAtPeaceWith(tile.unitsOnTile[0].owner)) {
				return true;
			}
			MapUnit defender = tile.FindTopDefender(army);
			if (!MapUnit.IsMapUnitValid(defender)) {
				return true;
			}
			return CombatAI.EstimateChanceToWin(army, defender) >= MinChanceToAttack;
		}

		// Heads for the nearest of our cities we can reach, to heal or wait
		// there.
		private C7GameData.UnitAI.MoveResult GoHome(MapUnit army, Player player) {
			if (pathHome == null || pathHome.PathLength() == 0) {
				List<Tile> cities = player.cities.Select(c => c.location)
					.OrderBy(t => t.DistanceTo(army.location)).Take(8).ToList();
				int index = PathingAlgorithmChooser.GetAlgorithm(army).FindFirstReachable(army.location, cities, army, out TilePath path);
				if (index < 0 || path == null || path.PathLength() == 0) {
					army.SkipTurn();
					return C7GameData.UnitAI.Result.InProgress;
				}
				pathHome = path;
				log.Information("{Army} with {Members} members at {Health:0%} heading back to {City}", army, army.PassengerCount(), HealthOf(army), cities[index]);
			}
			C7GameData.UnitAI.MoveResult result = this.TryToMoveAlongPath(army, ref pathHome);
			if (result.Result == C7GameData.UnitAI.Result.Error) {
				pathHome = null;
				army.SkipTurn();
				return C7GameData.UnitAI.Result.InProgress;
			}
			return result;
		}

		// Whether a unit should join an army: one of ours, meant for attack,
		// and not marching with a war campaign.
		private static bool IsRecruit(MapUnit u, Player player) {
			return u.owner == player && !u.IsLoaded() && !u.IsArmy() && u.unitType.CanJoinArmy() && u.currentAI is not WarCampaign.MemberAI
				&& u.unitType.attack > u.unitType.defense && u.hitPointsRemaining > 0;
		}

		// Loads our offensive units in the army's city into it, strongest
		// first, keeping a defender in the city, and calls in those nearby to
		// fill the remaining places.
		internal static void Recruit(MapUnit army, Player player) {
			Tile tile = army.location;
			int free = army.Capacity() - army.PassengerCount();
			if (free <= 0) {
				return;
			}

			List<MapUnit> here = tile.unitsOnTile.Where(u => IsRecruit(u, player))
				.OrderByDescending(u => u.unitType.attack).ThenByDescending(u => u.hitPointsRemaining).ToList();
			foreach (MapUnit u in here) {
				if (free <= 0) {
					break;
				}
				// The city keeps someone else to defend it.
				bool otherDefender = tile.unitsOnTile.Any(d => d != u && d != army && !d.IsLoaded() && d.CanDefendOnLand());
				if (!otherDefender) {
					continue;
				}
				u.Wake();
				u.currentAI = null;
				u.BoardTransport(army);
				if (u.IsLoadedIn(army)) {
					log.Information("{Unit} joined {Army}", u, army);
					--free;
				}
			}

			free -= CountRecruitsOnTheWay(army, player);
			if (free <= 0) {
				return;
			}

			// Call in the strongest offensive units nearby that we can get
			// here.
			IEnumerable<MapUnit> nearby = player.units
				.Where(u => IsRecruit(u, player) && u.location != tile && u.location.DistanceTo(tile) <= RecruitRange
					&& !(u.currentAI is DefenderAI d && d.data.destination == tile))
				.OrderByDescending(u => u.unitType.attack).ThenBy(u => u.location.DistanceTo(tile)).ToList();
			foreach (MapUnit u in nearby) {
				if (free <= 0) {
					break;
				}
				TilePath path = PathingAlgorithmChooser.GetAlgorithm(u).PathFrom(u.location, tile, u);
				if (path == null || path.PathLength() == 0) {
					continue;
				}
				u.Wake();
				u.currentAI = new DefenderAI(new DefenderAIData() {
					goal = DefenderAIData.DefenderGoal.DEFEND_CITY,
					destination = tile,
					defender = u,
					pathToDestination = path,
				});
				log.Information("Calling {Unit} to join {Army}", u, army);
				--free;
			}
		}

		// Our units heading for the army's tile to join it.
		private static int CountRecruitsOnTheWay(MapUnit army, Player player) {
			int count = 0;
			foreach (MapUnit u in player.units) {
				if (u.location != army.location && u.currentAI is DefenderAI d && d.data.destination == army.location && IsRecruit(u, player)) {
					++count;
				}
			}
			return count;
		}

		private static bool RecruitsOnTheWay(MapUnit army, Player player) {
			return army.location.cityAtTile?.owner == player && CountRecruitsOnTheWay(army, player) > 0;
		}

		public string SummarizePlan() {
			return attack != null ? "ArmyAI attacking: " + attack.SummarizePlan() : "ArmyAI";
		}

		public void UpdateOnDeath() { }
	}
}
