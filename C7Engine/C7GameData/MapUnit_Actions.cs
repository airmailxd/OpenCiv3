using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7GameData.AIData;

namespace C7GameData;

public partial class MapUnit {
	public void OnBeginTurn(bool skipTurn = false) {
		int maxMP = MaxMovementPoints();
		bool restedLastTurn = movementPoints.remaining >= maxMP || heldWithoutMoving;
		if (restedLastTurn && !skipTurn) {
			// The members of an army heal when the army rests, since they don't
			// move by themselves.
			if (IsArmy()) {
				foreach (MapUnit member in Passengers())
					member.Heal();
			}
			if (!IsInArmy())
				Heal();
		}
		heldWithoutMoving = false;

		if (skipTurn) {
			movementPoints.skipTurn();
		} else {
			movementPoints.reset(maxMP);
		}

		defensiveBombardsRemaining = 1;
		hasAttackedThisTurn = false;

		if (isSentried && location.neighbors.Values.Any(t => t.unitsOnTile.Any(ShouldWakeSentryFor))) {
			Wake();
		}
	}

	private void Heal() {
		int maxHP = maxHitPoints;
		if (hitPointsRemaining < maxHP)
			hitPointsRemaining += HealRateAt(location);
		if (hitPointsRemaining > maxHP)
			hitPointsRemaining = maxHP;
	}

	public void OnEnterTile(Tile tile) {
		//Add to player knowledge of tiles
		owner.tileKnowledge.AddTilesToKnown(tile);

		// Disperse barb camp
		if (tile.hasBarbarianCamp && !owner.isBarbarians) {
			EngineStorage.gameData.map.barbarianCamps.Remove(tile);
			tile.hasBarbarianCamp = false;
			animate(MapUnit.AnimatedAction.VICTORY);

			// TODO: make this configurable
			owner.gold += 25;
			if (owner.isHuman) {
				new MsgShowMilitaryAdvisorPopup(owner, $"We cleared a barbarian encampment and earned 25 gold!", happy: true).send();
			}
		}

		// Capture the enemy city on the tile unless we're the barbarians,
		// in which case we'll just take some gold.
		if (tile.HasCity() && !owner.IsAtPeaceWith(tile.cityAtTile.owner)) {
			if (owner.isBarbarians) {
				// TODO: Add rules for how much gold is taken.
				int goldTaken = tile.cityAtTile.owner.gold / 4;
				tile.cityAtTile.owner.gold -= goldTaken;
				this.RemoveFromPlay();
				if (tile.cityAtTile.owner.isHuman) {
					new MsgShowMilitaryAdvisorPopup(tile.cityAtTile.owner, $"Barbarians have stolen {goldTaken} gold from our cities!\nWe need a stronger military.", happy: false).send();
				}
			} else {
				CityInteractions.CaptureCity(tile.cityAtTile, owner);
			}
		}

		// Check to see if we've discovered a new civ.
		//
		// TODO: this should really be based on interactions with our "visible"
		// tiles. Also civ3 only counts border-based discovery from rank 1
		// tiles, not rank 2+.
		foreach (Tile t in tile.neighbors.Values) {
			if (t.unitsOnTile.Count > 0 && owner != t.unitsOnTile[0].owner) {
				owner.EnsureRelationshipExists(t.unitsOnTile[0].owner);
			}
			if (t.owningCity != null && owner != t.owningCity.owner) {
				owner.EnsureRelationshipExists(t.owningCity.owner);
			}
		}
	}

	public void Fortify() {
		ResetFacingDirection();
		isFortified = true;
		animate(MapUnit.AnimatedAction.FORTIFY);
	}

	public void Wake() {
		isFortified = false;
		isSentried = false;
		sentryEnemyOnly = false;
	}

	public void Sentry(bool enemyOnly) {
		ResetFacingDirection();
		isSentried = true;
		sentryEnemyOnly = enemyOnly;
	}

	private bool ShouldWakeSentryFor(MapUnit other) {
		if (other.owner == owner) {
			return false;
		}
		return !sentryEnemyOnly || !owner.IsAtPeaceWith(other.owner);
	}

	// Wakes sentries next to a tile this unit just entered.
	private void WakeNearbySentries(Tile tile) {
		foreach (Tile t in tile.neighbors.Values) {
			foreach (MapUnit u in t.unitsOnTile) {
				if (u.isSentried && u.ShouldWakeSentryFor(this)) {
					u.Wake();
				}
			}
		}
	}

	public void Automate() {
		Wake();
		isAutomated = true;
		WorkerAIData? maybeAiData = WorkerAI.MakeAiData(this, owner);
		if (maybeAiData == null) {
			log.Information($"Could not find anything to automate for {this} owned by {owner}");
			isAutomated = false;
			return;
		}
		currentAI = new WorkerAI(maybeAiData);
		PlayAutomatedTurn();
	}

	public void Explore() {
		Wake();
		isAutomated = true;
		ExplorerAIData? maybeAiData = ExplorerAI.MaybeMakeAiData(this, owner);
		if (maybeAiData == null) {
			log.Information($"Could not find anything to explore for {this} owned by {owner}");
			isAutomated = false;
			return;
		}
		currentAI = new ExplorerAI(maybeAiData);
		PlayAutomatedTurn();
	}

	public void SkipTurn() {
		// Holding uses up the unit's movement points, but a unit that holds
		// without having moved has still rested and should heal.
		if (movementPoints.remaining >= MaxMovementPoints()) {
			heldWithoutMoving = true;
		}
		movementPoints.skipTurn();
	}

	public async Task Disband() {
		await EngineStorage.gameData.DisbandUnit(this);
	}

	public void RemoveFromPlay() {
		EngineStorage.gameData.RemoveUnit(this);
	}

	public async Task MoveAlongPath() {
		while (movementPoints.canMove && path?.PathLength() > 0) {
			TileDirection dir = location.DirectionTo(path.Next());
			await Move(dir, true); //TODO: don't wait on last move animation?
		}
	}

	public async Task SetUnitPath(TilePath path) {
		this.path = path;
		await MoveAlongPath();
	}

	public async void PlayAutomatedTurn() {
		if (currentAI == null) {
			// TODO: handle giving automated workers from loaded saves the
			// proper unit ai.
			isAutomated = false;
			return;
		}
		UnitAI.Result result = await currentAI.PlayTurn(owner, this);
		if (result == UnitAI.Result.Done) {
			if (currentAI is WorkerAI) {
				Automate();
			} else if (currentAI is ExplorerAI) {
				Explore();
			}
		}

		// Do nothing after an error so control returns to the player, and
		// nothing after an progress result, so that next turn continues the
		// AI action.
	}

	public async Task PerformBusyAction() {
		if (isFortified) {
			return;
		}

		if (path != null && path.PathLength() > 0) {
			await MoveAlongPath();
			return;
		}

		if (isAutomated) {
			// workers contribute their work at the end of the turn, not when assigned
			if (this.unitType.isWorker && WorkerJob != null) {
				return;
			}
			PlayAutomatedTurn();
			return;
		}
	}

	public async Task PerformEndOfTurnAction() {
		// Busy Worker
		if (WorkerJob != null) {
			WorkerProgressTowardsJob += workerSpeed();
			movementPoints.onConsumeAll();

			// See if this worker finished the job.
			if ((int)SumWorkerProgress(location, WorkerJob) >= GetWorkerJobCost(location, WorkerJob)) {
				location.FinishWorkerJob(WorkerJob);
			}
		}
	}

	/// <summary>
	/// Moves the unit in the given direction
	/// </summary>
	/// <param name="unit"></param>
	/// <param name="dir">Which direction to move, e.g. northeast, west, etc.</param>
	/// <param name="wait">Whether the method should wait to return until animations complete</param>
	/// <returns>True if the unit is alive after the movement, false otherwise</returns>
	/// <exception cref="Exception"></exception>
	public async Task<bool> Move(TileDirection dir, bool wait = false) {
		(int dx, int dy) = dir.ToCoordDiff();

		Tile newLoc = EngineStorage.gameData.map.tileAt(dx + location.XCoordinate, dy + location.YCoordinate);

		var canMove = (newLoc != Tile.NONE) && this.CanEnter(newLoc) && (movementPoints.canMove);
		if (!canMove) return false;

		facingDirection = dir;
		Wake();

		// Trigger combat if the tile we're moving into has an enemy  Or if this unit can't fight, do nothing.
		MapUnit defender = newLoc.FindTopDefender(this);
		bool enemyOnTile = defender != MapUnit.NONE && !owner.IsAtPeaceWith(defender.owner);
		// Units that can't attack, including empty armies, don't start fights,
		// and without Blitz a unit only attacks once per turn.
		if (enemyOnTile && (!CanAttack() || !CanAttackAgainThisTurn())) {
			return true;
		}

		if (enemyOnTile && !HasArmedEnemyDefender(newLoc)) {
			// Units that can't defend themselves, like workers and settlers,
			// are captured rather than fought. Fighting them would always win,
			// and could be farmed for promotions.
			CaptureDefencelessUnits(newLoc);
		} else if (enemyOnTile) {
			CombatResult combatResult = await Fight(defender);
			hasAttackedThisTurn = true;
			this.path = TilePath.NONE;
			// If we were killed then of course there's nothing more to do. If the combat couldn't happen for whatever
			// reason, just give up on trying to move.
			if (combatResult == CombatResult.AttackerKilled) {
				return false;
			}
			if (combatResult == CombatResult.Impossible) {
				return true;
			}

			// If the enemy was defeated, check if there is another enemy on the tile. If so we can't complete the move
			// but still pay one movement point for the combat. Otherwise we move in below, paying only for the move.
			if (combatResult == CombatResult.DefenderKilled || combatResult == CombatResult.DefenderRetreated) {
				if (newLoc.FindTopDefender(this) != MapUnit.NONE) {
					this.movementPoints.onUnitMove(1);
					this.facingDirection = this.facingDirection.Reversed();
					return true;
				}

				// Similarly if we retreated, pay one MP for the combat but don't move.
			} else if (combatResult == CombatResult.AttackerRetreated) {
				this.movementPoints.onUnitMove(1);
				this.facingDirection = this.facingDirection.Reversed();
				return true;
			}
		}

		facingDirection = dir;
		float movementCost = TilePath.GetMovementCost(this.owner, location, dir, newLoc);
		List<MapUnit> zoneOfControlAttackers = FindZoneOfControlAttackers(location, newLoc);

		RelocateTo(newLoc);
		WakeNearbySentries(newLoc);

		if (wait)
			await animateAsync(MapUnit.AnimatedAction.RUN);
		else
			animate(MapUnit.AnimatedAction.RUN);

		movementPoints.onUnitMove(movementCost);

		foreach (MapUnit zocUnit in zoneOfControlAttackers) {
			await zocUnit.ZoneOfControlAttack(this);
		}

		return true;
	}

	// Moves the unit, and anything it carries, onto a neighboring tile. This
	// doesn't check whether the move is allowed, start combat, or use
	// movement points; callers handle those.
	private void RelocateTo(Tile newLoc) {
		// Leave old tile
		if (!location.unitsOnTile.Remove(this))
			throw new System.Exception("Failed to remove unit from tile it's supposed to be on");

		// Move transported units, too
		CarryPassengersTo(newLoc);

		TryBoardingTransportOnTile(newLoc);
		TryUnboardingTransportToTile(newLoc);

		// Enter new tile
		// Make sure the unit is on the new location before claiming we have entered the tile
		newLoc.unitsOnTile.Add(this);
		location = newLoc;
		OnEnterTile(newLoc);
	}

	// Moves everything loaded on this unit, and anything loaded on that (like
	// an army aboard a ship), to the new tile along with it.
	private void CarryPassengersTo(Tile newLoc) {
		foreach (MapUnit passenger in Passengers()) {
			passenger.CarryPassengersTo(newLoc);
			if (!location.unitsOnTile.Remove(passenger))
				throw new System.Exception("Failed to remove unit from tile during transport move");
			newLoc.unitsOnTile.Add(passenger);
			passenger.location = newLoc;
		}
	}

	// True if an enemy unit on the tile can actually put up a fight against
	// this unit, as opposed to only workers, settlers, empty armies and the
	// like. Units in an army fight through the army.
	private bool HasArmedEnemyDefender(Tile tile) {
		return tile.unitsOnTile.Any(u => !owner.IsAtPeaceWith(u.owner)
			&& !u.IsInArmy()
			&& u.CanDefendAgainst(this)
			&& u.CombatBaseStrength(CombatRole.Defense) > 0);
	}

	// Captures the enemy workers on the tile, turns enemy settlers into two
	// slave workers, and destroys any other enemy units there that can't be
	// captured. Barbarians don't take captives.
	private void CaptureDefencelessUnits(Tile tile) {
		GameData gameData = EngineStorage.gameData;
		foreach (MapUnit enemy in tile.unitsOnTile.Where(u => !owner.IsAtPeaceWith(u.owner)).ToList()) {
			if (owner.isBarbarians) {
				gameData.RemoveUnit(enemy);
			} else if (enemy.unitType.isSettler) {
				gameData.CaptureSettler(enemy, owner);
			} else if (enemy.unitType.isWorker) {
				gameData.CaptureUnit(enemy, owner);
			} else {
				gameData.RemoveUnit(enemy);
			}
		}
	}

	// Enemy units with a zone of control that are next to both tiles of a
	// move get a free attack on the moving unit. Land units only watch land
	// units, and ships only watch ships.
	public List<MapUnit> FindZoneOfControlAttackers(Tile from, Tile to) {
		List<MapUnit> result = new();
		foreach (Tile t in from.neighbors.Values) {
			if (t == to || !to.neighbors.ContainsValue(t)) {
				continue;
			}
			foreach (MapUnit u in t.unitsOnTile) {
				if (u.unitType.hasZoneOfControl && u.unitType.attack > 0 && !u.IsLoaded()
					&& u.IsLandUnit() == IsLandUnit() && u.IsWaterUnit() == IsWaterUnit()
					&& u.owner != owner && !owner.IsAtPeaceWith(u.owner)) {
					result.Add(u);
				}
			}
		}
		return result;
	}

	// A single round of combat against a unit moving through our zone of
	// control. It can wound the unit but never kills it.
	public async Task ZoneOfControlAttack(MapUnit target) {
		if (target.hitPointsRemaining <= 1) {
			return;
		}

		double attackStrength = StrengthVersus(target, CombatRole.Attack, location.DirectionTo(target.location));
		double defenseStrength = target.StrengthVersus(this, CombatRole.Defense, location.DirectionTo(target.location));
		var originalDirection = facingDirection;
		facingDirection = GetAttackAnimationDirection(location.DirectionTo(target.location));
		await animateAsync(AnimatedAction.ATTACK1);
		facingDirection = originalDirection;

		if (GameData.rng.NextDouble() < attackStrength / (attackStrength + defenseStrength)) {
			target.hitPointsRemaining -= 1;
			log.Information($"{this} hit {target} moving through its zone of control");
		}
	}

	public async Task<CombatResult> Fight(MapUnit defender) {
		var attacker = this;

		// Armies fight with one member at a time; for other units the
		// combatant is the unit itself. See Combatant().
		MapUnit attackingMember = attacker.Combatant(CombatRole.Attack);
		MapUnit defendingMember = defender.Combatant(CombatRole.Defense);

		// Set combat animation facing. We'll restore the defender's original facing direction at the end of the battle.
		TileDirection attackerAttackDirection = attacker.location.DirectionTo(defender.location);
		TileDirection defenderDefenseDirection = attackerAttackDirection.Reversed();
		var defenderOriginalDirection = defender.facingDirection;
		attacker.facingDirection = attacker.GetAttackAnimationDirection(attackerAttackDirection);
		defender.facingDirection = defender.GetAttackAnimationDirection(defenderDefenseDirection);

		IEnumerable<StrengthBonus> attackBonuses  = attacker.ListStrengthBonusesVersus(defender, CombatRole.Attack , attackerAttackDirection),
								   defenseBonuses = defender.ListStrengthBonusesVersus(attacker, CombatRole.Defense, attackerAttackDirection);
		double attackMultiplier  = StrengthBonus.ListToMultiplier(attackBonuses),
			   defenseMultiplier = StrengthBonus.ListToMultiplier(defenseBonuses);

		double attackerStrength = attackingMember.unitType.attack  * attackMultiplier,
			   defenderStrength = defendingMember.unitType.defense * defenseMultiplier;

		log.Information($"Combat log: {attacker} ({attackerStrength}) attacking {defender} ({defenderStrength})");
		log.Information($"\tAttacker: {attackingMember.unitType.name}, base strength {attackingMember.unitType.BaseStrength(CombatRole.Attack)}");
		foreach (StrengthBonus bonus in attackBonuses)
			log.Information($"\t\t+{100.0 * bonus.amount}%\t{bonus.description}");
		log.Information($"\tDefender: {defendingMember.unitType.name}, base strength {defendingMember.unitType.BaseStrength(CombatRole.Defense)}");
		foreach (StrengthBonus bonus in defenseBonuses)
			log.Information($"\t\t+{100.0 * bonus.amount}%\t{bonus.description}");

		CombatResult result = CombatResult.Impossible;

		double attackerOdds = attackerStrength / (attackerStrength + defenderStrength);
		if (Double.IsNaN(attackerOdds))
			return result;

		// When an army rotates in another member, the odds change with it.
		void UpdateOdds() {
			attackerStrength = attackingMember.unitType.attack * attackMultiplier;
			defenderStrength = defendingMember.unitType.defense * defenseMultiplier;
			attackerOdds = attackerStrength / (attackerStrength + defenderStrength);
			if (Double.IsNaN(attackerOdds))
				attackerOdds = 0.5;
		}

		// Defensive bombard
		MapUnit defensiveBombarder = MapUnit.NONE;
		double defensiveBombarderStrength = 0.0;
		foreach (MapUnit candidate in defender.location.unitsOnTile.Where(u => u != defender && !u.IsInArmy() && !u.owner.IsAtPeaceWith(attacker.owner) && u.defensiveBombardsRemaining > 0)) {
			double strength = candidate.StrengthVersus(attacker, CombatRole.DefensiveBombard, defenderDefenseDirection);
			if (strength > defensiveBombarderStrength) {
				defensiveBombarder = candidate;
				defensiveBombarderStrength = strength;
			}
		}
		// In the original game, defensive bombard does not trigger against attackers with 1 HP. See:
		// https://github.com/C7-Game/Prototype/pull/250#discussion_r893051111
		// Against an army it hits the member with the most hit points, so it
		// never kills one either.
		MapUnit bombardedUnit = attacker.IsArmy()
			? attacker.Passengers().OrderByDescending(m => m.hitPointsRemaining).FirstOrDefault() ?? attacker
			: attacker;
		if (defensiveBombarder != MapUnit.NONE && bombardedUnit.hitPointsRemaining > 1) {
			var dBOriginalDirection = defensiveBombarder.facingDirection;
			TileDirection defensiveBombardDirection = defenderDefenseDirection;
			defensiveBombarder.facingDirection = defensiveBombarder.GetAttackAnimationDirection(defensiveBombardDirection);

			await defensiveBombarder.animateAsync(MapUnit.AnimatedAction.ATTACK1);

			// dADB = defense Against Defensive Bombard
			double dADB = attacker.StrengthVersus(defensiveBombarder, CombatRole.DefensiveBombardDefense, defensiveBombardDirection);
			if (GameData.rng.NextDouble() < defensiveBombarderStrength / (defensiveBombarderStrength + dADB))
				bombardedUnit.hitPointsRemaining -= 1;

			defensiveBombarder.defensiveBombardsRemaining -= 1;
			defensiveBombarder.facingDirection = dBOriginalDirection;

			attackingMember = attacker.Combatant(CombatRole.Attack);
			UpdateOdds();
		}

		bool defenderEligibleToRetreat = defender.CompositeHitPoints() > 1 && ! defender.location.HasCity();

		// Do combat rounds
		while (true) {
			defender.animate(MapUnit.AnimatedAction.ATTACK1);
			await attacker.animateAsync(MapUnit.AnimatedAction.ATTACK1);
			if (GameData.rng.NextDouble() < attackerOdds) {
				if (defenderEligibleToRetreat &&
					defender.CompositeHitPoints() == 1 &&
					GameData.rng.NextDouble() < defender.RetreatChance(attacker, false)) {
					// TODO: Defender retreat behavior requires some more work. There's an issue for it here:
					// https://github.com/C7-Game/Prototype/issues/274
					//
					// Retreat straight onto the tile behind the defender. It has
					// to be one we can enter peacefully, so the retreat can't
					// start another battle, and we move the defender directly
					// rather than with Move(), which needs movement points the
					// defender may not have during the attacker's turn.
					if (defender.location.neighbors.TryGetValue(attackerAttackDirection, out Tile retreatDestination)
						&& retreatDestination != Tile.NONE
						&& defender.CanEnterPeacefully(retreatDestination)) {
						defender.facingDirection = attackerAttackDirection;
						defender.RelocateTo(retreatDestination);
						await defender.animateAsync(MapUnit.AnimatedAction.RUN);
						result = CombatResult.DefenderRetreated;
						break;
					}
				}
				if (defender.AbsorbCombatHit(defendingMember)) {
					result = CombatResult.DefenderKilled;
					break;
				}
				defendingMember = defender.Combatant(CombatRole.Defense);
				UpdateOdds();
			} else {
				if (attacker.CompositeHitPoints() == 1 &&
					GameData.rng.NextDouble() < attacker.RetreatChance(defender, true)) {
					result = CombatResult.AttackerRetreated;
					break;
				}
				if (attacker.AbsorbCombatHit(attackingMember)) {
					result = CombatResult.AttackerKilled;
					break;
				}
				attackingMember = attacker.Combatant(CombatRole.Attack);
				UpdateOdds();
			}
		}

		if ((result == CombatResult.AttackerKilled) || (result == CombatResult.DefenderKilled)) {
			var (dead, alive) = (result == CombatResult.AttackerKilled) ? (attacker, defender) : (defender, attacker);

			// In an army, the member that won the last round gets the chance
			// to be promoted, and the army plays the victory animation.
			MapUnit survivingMember = (alive == attacker) ? attackingMember : defendingMember;
			survivingMember.RollToPromote(dead, alive);

			// Winning a battle with an army is what lets a civ build the
			// Military Academy.
			if (alive.IsArmy())
				alive.owner.hasVictoriousArmy = true;

			if (!dead.owner.isBarbarians && !alive.owner.isBarbarians) {
				dead.owner.AddWarWearinessForLostUnit(diedAttacking: dead == attacker);
			}

			// A unique unit beating another civ (not barbarians) starts a golden age.
			if (survivingMember.unitType.startsGoldenAge && !dead.owner.isBarbarians) {
				alive.owner.StartGoldenAge(EngineStorage.gameData, $"Our {survivingMember.unitType.name} has won a great victory.");
			}
			await dead.animateAsync(MapUnit.AnimatedAction.DEATH);
			dead.RemoveFromPlay();
		}

		if (result.DefenderWon())
			defender.facingDirection = defenderOriginalDirection;

		return result;
	}

	public async Task<City?> BuildCity(string cityName) {
		if (!canBuildCity()) {
			log.Warning($"can't build city at {location}");
			return null;
		}

		await animateAsync(MapUnit.AnimatedAction.BUILD);

		// TODO: Need to check somewhere that this unit is allowed to build a city on its current tile. Either do that here or in every caller
		// (probably best to just do it here).
		City city = CityInteractions.BuildCity(location, owner, cityName);
		this.RemoveFromPlay();

		return city;
	}

	// entry point for "manual" job assignment
	public void PerformTerraformAction(Terraform terraform) {
		if (!CanPerformTerraformAction(terraform)) {
			log.Warning($"can't perform {terraform.Name} by {this}");
			return;
		}
		WorkerJob = terraform;

		if (terraform.Animation is AnimatedAction animation)
			animate(animation, AnimationEnding.Repeat);

		movementPoints.onConsumeAll();

		// See if this worker finished the job.
		var terraformProgress = this.SumWorkerProgress(this.location, this.WorkerJob);
		var turnProgress = this.location.GetCurrentUnaccountedJobProgress(terraform);
		var totalCost = (float)GetWorkerJobCost(this.location, this.WorkerJob);

		// Use >= rather than ==, since faster (e.g. Industrious) workers can
		// overshoot the cost.
		if (terraformProgress + turnProgress >= totalCost) {
			location.FinishWorkerJob(WorkerJob);
		}

		Wake();
		_ = PerformBusyAction();
	}

	public void BoardTransport(MapUnit t) {
		if (t == null) {
			// TODO: throw new System.Exception("Failed to find a transport to move to");
			log.Warning("Failed to find a transport to board");
			return;
		}
		if (!t.CanCarryUnits() || !t.CanLoad(this)) {
			log.Warning($"{this} can't board {t}");
			return;
		}
		t.Board(this);
		isFortified = true;
		ResetFacingDirection();
		if (this.owner.isHuman)
			new MsgUnitMoved(this).send();
	}

	public void UnboardTransport(MapUnit t) {
		if (t == null) {
			// TODO: throw new System.Exception("Failed to find the transport to unboard from");
			log.Warning("Failed to find a transport to unboard");
			return;
		}
		t.Unboard(this);
		Wake();
		if (this.owner.isHuman)
			new MsgUnitMoved(this).send();
	}

	// Loads this unit, on the player's order, onto a transport or army on its
	// own tile.
	public void LoadOntoTransportHere() {
		if (IsLoaded() || !CanBoardTransportOnTile(location, explicitLoad: true))
			return;
		BoardTransport(SelectTransportToBoard(location, explicitLoad: true));
	}

	public void TryBoardingTransportOnTile(Tile newLoc) {
		var enteringCity = newLoc.HasCity() && newLoc != location;
		if (enteringCity || !CanBoardTransportOnTile(newLoc))
			return;

		var t = SelectTransportToBoard(newLoc);
		BoardTransport(t);
	}

	public void TryUnboardingTransportToTile(Tile newLoc) {
		if (!CanUnboardTransportToTile(newLoc))
			return;

		var t = FindTransportToUnboard(this.location, this.loadedOnUnitId);
		UnboardTransport(t);
	}

	/// <summary>
	/// Boards unit into this transport
	/// </summary>
	/// <param name="mapUnit">The unit to load on a transport</param>
	private void Board(MapUnit mapUnit) {
		mapUnit.loadedOnUnitId = this.id;
		// TODO: consume moves?
	}

	/// <summary>
	/// Unloads a unit from this transport
	/// </summary>
	/// <param name="mapUnit">The unit to unload from a transport</param>
	private void Unboard(MapUnit mapUnit) {
		mapUnit.loadedOnUnitId = null;
		// TODO: consume moves?
	}
}
