using System;
using System.Threading.Tasks;
using C7Engine.AI.UnitAI;
using C7GameData;
using C7GameData.AIData;

namespace C7Engine;

internal abstract class BaseStrategy : IBarbarianStrategy {
	// TODO: Determine how barbarian AI is implemented in Civ3
	// TODO: What are the key parameters influencing barbarian activity levels in Civ3?

	/// <summary>
	/// Observe - Orient - Decide - Act.
	/// 
	/// Note: This approach may or may not have anything to do with how Civ3 implements barbarian AI.
	/// </summary>
	public async Task PlayUnitTurn(Player player, MapUnit unit) {
		// "Observe: Collect data and information from the environment through senses and feedback."

		// Wake up the unit if there's a reason to do so
		if (ShouldWake(player, unit))
			unit.Wake();

		// Skip units that didn't wake up
		if (unit.isFortified)
			return;

		var orientation = await Orient(player, unit);
		var plan = await Decide(player, unit, orientation);

		// Keep the plan on the unit, so that a plan in progress (such as
		// exploring towards a tile) can be continued next turn, and so that
		// the unit counts as busy with it (e.g. for exploration targets).
		unit.currentAI = plan;
		var result = await Act(player, unit, plan);
		if (result != UnitAI.Result.InProgress && unit.currentAI == plan) {
			unit.currentAI = null;
		}
	}

	/// <summary>
	/// Wake the unit if a foreign unit or the borders of a civ are in sight. 
	/// </summary>
	private static bool ShouldWake(Player player, MapUnit unit) {
		var tiles = player.tileKnowledge.GetTilesVisibleToUnit(unit.location);
		foreach (Tile t in tiles) {
			if (t.unitsOnTile.Count > 0 && t.unitsOnTile[0].owner != player)
				return true;

			if (t.OwningPlayer() != null)
				return true;
		}

		return false;
	}

	/// <summary>
	/// "Orient: Analyze and synthesize data to form a mental perspective, considering experience,
	/// culture, and new information. This is considered the most important phase of the OODA loop."
	/// </summary>
	protected Task<Orientation> Orient(Player player, MapUnit unit) {
		// Combat intel requires a target search, so it's only computed if the
		// decision actually depends on it.
		return Task.FromResult(new Orientation(() => CombatAI.MakeAiData(unit, player)) {
			IsLastUnitInCamp = unit.location.hasBarbarianCamp && unit.location.unitsOnTile.Count == 1,
		});
	}

	/// <summary>
	/// "Decide: Formulate a plan or course of action based on the orientation."
	/// </summary>
	protected async Task<UnitAI> Decide(Player player, MapUnit unit, Orientation orientation) {
		// Barbarians defend their camp if it is unguarded.
		if (orientation.IsLastUnitInCamp)
			return new DefenderAI(DefenderAI.MakeAiDataForDefendInPlace(unit, player));

		// Otherwise a unit keeps to a plan in progress while it is still
		// worth following, rather than rolling the dice afresh every turn.
		// An attack gives up by itself once its target is gone.
		if (unit.currentAI is CombatAI currentAttack)
			return currentAttack;
		if (unit.currentAI is ExplorerAI currentExploration && IsStillExploring(player, unit, currentExploration))
			return currentExploration;

		// Decide whether to engage enemy units
		if (MayEngage && orientation.CanEngage() && DecideToEngage(player, unit, orientation))
			return new CombatAI(orientation.CombatIntel);

		// Decide whether to explore
		if (DecideToExplore(player, unit, orientation)) {
			var maybeAiData = ExplorerAI.MaybeMakeAiData(unit, player);
			if (maybeAiData != null)
				return new ExplorerAI(maybeAiData);
		}

		// Defend otherwise
		return new DefenderAI(DefenderAI.MakeAiDataForDefendInPlace(unit, player));
	}

	/// <summary>
	/// "Act: Implement the decision, which creates new data and feeds back into the observation phase."
	/// </summary>
	protected async Task<UnitAI.Result> Act(Player player, MapUnit unit, UnitAI plan) {
		return await plan.PlayTurn(player, unit);
	}

	private static bool IsStillExploring(Player player, MapUnit unit, ExplorerAI explorer) {
		Tile destination = explorer.data?.destination;
		return destination != null
			&& destination != unit.location
			&& !player.tileKnowledge.isTileKnown(destination);
	}

	internal class Orientation {
		private readonly Func<CombatAIData> combatIntelSource;
		private bool combatIntelComputed;
		private CombatAIData combatIntel;

		public Orientation(Func<CombatAIData> combatIntelSource) {
			this.combatIntelSource = combatIntelSource;
		}

		public bool IsLastUnitInCamp { get; set; }

		// Computed on first use.
		public CombatAIData CombatIntel {
			get {
				if (!combatIntelComputed) {
					combatIntel = combatIntelSource?.Invoke();
					combatIntelComputed = true;
				}
				return combatIntel;
			}
		}

		public bool CanEngage() => CombatIntel != null;
	}

	// False for strategies that never engage, so we can skip gathering combat
	// intel for them.
	protected virtual bool MayEngage => true;

	protected abstract bool DecideToEngage(Player player, MapUnit unit, Orientation orientation);

	protected abstract bool DecideToExplore(Player player, MapUnit unit, Orientation orientation);
}
