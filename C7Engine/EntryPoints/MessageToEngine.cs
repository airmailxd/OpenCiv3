using System.Linq;
using System.Text.Json.Serialization;
using C7Engine.AI;
using Serilog;

namespace C7Engine {
	using System;
	using C7GameData;

	// Messages from the UI to the engine. Each one must be serializable (see
	// C7Engine.Network.NetSerialization), because a LAN client sends its
	// messages to the host's engine rather than its own.
	public abstract class MessageToEngine {
		private static ILogger log = Log.ForContext<MessageToEngine>();

		// The player who sent this message. send() fills in the player at this
		// machine's UI unless it's set already (a hotseat player answering a
		// deal at someone else's screen), and a LAN host overwrites it with
		// the player of the connection it arrived on.
		public ID playerID;

		protected Player Sender => EngineStorage.gameData.GetPlayer(playerID);

		// Settings of this machine's engine, like animations, that are never
		// sent to a LAN host.
		[JsonIgnore]
		public virtual bool IsLocal => false;

		public void send() {
			playerID ??= EngineStorage.uiControllerID;
			EngineStorage.SendToEngine(this);
		}

		// Returns false if the sender may not send this message now.
		public bool process() {
			if (!IsLocal && !IsAllowed()) {
				log.Warning("Ignoring {Message} from {Player}, who may not send it now", GetType().Name, playerID);
				return false;
			}
			ProcessAllowed();
			return true;
		}

		protected abstract void ProcessAllowed();

		// Called on a LAN host for messages from clients, to drop anything
		// only this machine's own players may claim.
		public virtual void DistrustRemoteSender() { }

		// Players may act on their own turn, and answer an AI that is waiting
		// for them to respond to a trade offer.
		protected virtual bool IsAllowed() {
			return Sender != null
				&& (playerID == EngineStorage.activePlayerID || playerID == EngineStorage.diplomacyPlayerID);
		}

		// The sender's unit with the given ID, or null if they have no such unit.
		protected MapUnit SendersUnit(ID unitID) {
			MapUnit unit = EngineStorage.gameData.GetUnit(unitID);
			return unit != null && unit.owner == Sender ? unit : null;
		}

		protected bool IsSendersCity(City city) {
			return city != null && city.owner == Sender && EngineStorage.gameData.GetCity(city.id) == city;
		}
	}

	public class MsgShutdownEngine : MessageToEngine {
		private ILogger log = Log.ForContext<MsgShutdownEngine>();

		public override bool IsLocal => true;

		protected override void ProcessAllowed() {
			log.Information("Engine received shutdown message.");
		}
	}

	public class MsgSetFortification : MessageToEngine {
		public ID unitID;
		public bool fortifyElseWake;

		public MsgSetFortification(ID unitID, bool fortifyElseWake) {
			this.unitID = unitID;
			this.fortifyElseWake = fortifyElseWake;
		}

		protected override void ProcessAllowed() {
			MapUnit unit = SendersUnit(unitID);

			// Simply do nothing if we weren't given a valid ID. A LAN host
			// rejects actions on units the sender doesn't own the same way.
			if (unit != null) {
				if (fortifyElseWake)
					unit.Fortify();
				else
					unit.Wake();
			}
		}
	}

	public class MsgUpgradeUnit : MessageToEngine {
		public ID unitID;

		public MsgUpgradeUnit(ID unitID) {
			this.unitID = unitID;
		}

		protected override void ProcessAllowed() {
			MapUnit unit = SendersUnit(unitID);
			if (unit != null && unit.Upgrade() && unit.owner.isHuman) {
				new MsgUnitMoved(unit).send();
			}
		}
	}

	public class MsgSentry : MessageToEngine {
		public ID unitID;
		public bool enemyOnly;

		public MsgSentry(ID unitID, bool enemyOnly) {
			this.unitID = unitID;
			this.enemyOnly = enemyOnly;
		}

		protected override void ProcessAllowed() {
			MapUnit unit = SendersUnit(unitID);
			if (unit != null && unit.unitType.actions.Contains(UnitAction.Sentry)) {
				unit.Sentry(enemyOnly);
				if (unit.owner.isHuman) {
					new MsgUnitMoved(unit).send();
				}
			}
		}
	}

	public class MsgPillage : MessageToEngine {
		public ID unitID;

		public MsgPillage(ID unitID) {
			this.unitID = unitID;
		}

		protected override void ProcessAllowed() {
			MapUnit unit = SendersUnit(unitID);
			if (unit != null && unit.Pillage() && unit.owner.isHuman) {
				new MsgUnitMoved(unit).send();
			}
		}
	}

	public class MsgMoveUnit : MessageToEngine {
		public ID unitID;
		public TileDirection dir;

		public MsgMoveUnit(ID unitID, TileDirection dir) {
			this.unitID = unitID;
			this.dir = dir;
		}

		protected override async void ProcessAllowed() {
			try {
				MapUnit unit = SendersUnit(unitID);
				if (unit == null) return;

				await unit.Move(dir, true);
			} catch (Exception e) {
				EngineStorage.ReportUnhandledException(e, GetType().Name);
			}
		}
	}

	public class MsgSetUnitPath : MessageToEngine {
		public ID unitID;
		public TilePath path;

		public MsgSetUnitPath(ID unitID, TilePath path) {
			this.unitID = unitID;
			this.path = path;
		}

		protected override async void ProcessAllowed() {
			try {
				MapUnit unit = SendersUnit(unitID);
				if (unit == null || path == null) return;
				// The path may come from another machine: every step has to be to
				// a neighboring tile on the map.
				if (!unit.IsFollowablePath(path)) {
					Log.Warning("Ignoring a path for {Unit} that leaves the map or skips tiles", unit);
					return;
				}

				await unit.SetUnitPath(path);
			} catch (Exception e) {
				EngineStorage.ReportUnhandledException(e, GetType().Name);
			}
		}
	}

	public class MsgBombard : MessageToEngine {
		public ID unitID;
		public Tile tile;

		public MsgBombard(ID unitID, Tile tile) {
			this.unitID = unitID;
			this.tile = tile;
		}

		protected override async void ProcessAllowed() {
			try {
				MapUnit unit = SendersUnit(unitID);
				if (unit == null || tile == null || tile == Tile.NONE) return;

				await unit.Bombard(tile);
			} catch (Exception e) {
				EngineStorage.ReportUnhandledException(e, GetType().Name);
			}
		}
	}

	public class MsgLoadToTransport : MessageToEngine {
		public ID unitID;
		public ID transportUnitId;

		public MsgLoadToTransport(ID unitID, ID transportUnitId = null) {
			this.unitID = unitID;
			this.transportUnitId = transportUnitId;
		}

		protected override void ProcessAllowed() {
			MapUnit unit = SendersUnit(unitID);
			if (unit == null) return;

			if (this.transportUnitId != null) {
				MapUnit transportUnit = EngineStorage.gameData.GetUnit(transportUnitId);
				if (transportUnit != null)
					unit.BoardTransport(transportUnit);
			} else
				unit.LoadOntoTransportHere();
		}
	}

	public class MsgUnloadTransport : MessageToEngine {
		public ID transportUnitId;

		public MsgUnloadTransport(ID transportUnitId) {
			this.transportUnitId = transportUnitId;
		}

		protected override void ProcessAllowed() {
			// TODO: more selective unload, let human player choose
			MapUnit transportUnit = SendersUnit(transportUnitId);
			// Armies can only be unloaded when the game option allows it.
			if (transportUnit == null || !transportUnit.CanTransport())
				return;
			foreach (MapUnit unit in transportUnit.Passengers()) {
				unit.UnboardTransport(transportUnit);
			}
			new
				MsgTransportUnloaded(transportUnit).send();
		}
	}

	// Simple orders the player gives a unit.
	public class MsgUnitCommand : MessageToEngine {
		public enum Command {
			SkipTurn,
			Disband,
			Explore,
			Automate,
			// A great leader forms an army, or hurries its city's production.
			FormArmy,
			HurryProduction,
		}

		public ID unitID;
		public Command command;

		public MsgUnitCommand(ID unitID, Command command) {
			this.unitID = unitID;
			this.command = command;
		}

		protected override async void ProcessAllowed() {
			try {
				MapUnit unit = SendersUnit(unitID);
				if (unit == null) return;

				switch (command) {
					case Command.SkipTurn:
						unit.SkipTurn();
						break;
					case Command.Disband:
						await unit.Disband();
						break;
					case Command.Explore:
						unit.Explore();
						break;
					case Command.Automate:
						unit.Automate();
						break;
					case Command.FormArmy:
						MapUnit army = unit.FormArmy();
						if (army != null && army.owner.isHuman)
							new MsgUnitMoved(army).send();
						break;
					case Command.HurryProduction:
						unit.HurryProductionAsLeader();
						break;
				}
			} catch (Exception e) {
				EngineStorage.ReportUnhandledException(e, GetType().Name);
			}
		}
	}

	// The player selected a unit, which takes it out of automation, cancels
	// its path and wakes it up.
	public class MsgSelectUnit : MessageToEngine {
		public ID unitID;

		public MsgSelectUnit(ID unitID) {
			this.unitID = unitID;
		}

		protected override void ProcessAllowed() {
			MapUnit unit = SendersUnit(unitID);
			if (unit == null) return;

			if ((unit.path?.PathLength() ?? -1) > 0) {
				unit.path = TilePath.NONE;
			}
			if (unit.WorkerJob != null) {
				return;
			}
			if (unit.isAutomated) {
				unit.isAutomated = false;
				unit.currentAI = null;
			}
			if (unit.movementPoints.canMove) {
				unit.Wake();
			}
		}
	}

	// Switches the player government to anarchy and determines when the player
	// can exit anarchy.
	public class StartGovernmentTransitionMsg : MessageToEngine {
		protected override void ProcessAllowed() {
			GameData gD = EngineStorage.gameData;
			Player player = Sender;
			// Already in anarchy; don't restart the clock.
			if (player.government.transitionType) return;
			Government transitionGovt = gD.governments.Find(x => x.transitionType);
			player.government = transitionGovt;
			player.ApplyGovernmentRateCap();
			player.inAnarchyUntilTurn = gD.turn + player.GetTurnsOfAnarchyForTransition(gD);

			// Update the domestic advisor once we know how long the anarchy is.
			new MsgUpdateUiAfterDomesticChange().send();
		}
	}

	public class SelectGovernmentMsg : MessageToEngine {
		public Government government;

		public SelectGovernmentMsg(Government government) {
			this.government = government;
		}

		protected override void ProcessAllowed() {
			GameData gD = EngineStorage.gameData;
			Player player = Sender;
			if (government == null || !player.GetAvailableGovernments(gD).Contains(government)) {
				return;
			}
			// A new government can only be picked once the anarchy period is over.
			if (!player.government.transitionType || gD.turn < player.inAnarchyUntilTurn) {
				return;
			}
			player.government = government;
			player.ApplyGovernmentRateCap();
		}
	}

	// A Class that allows the UI to have the game engine run some
	// terraform action.
	public class MsgStartWorkerJob : MessageToEngine {
		public ID unitID;
		public Terraform action;

		public MsgStartWorkerJob(ID unitID, Terraform action) {
			this.unitID = unitID;
			this.action = action;
		}

		protected override void ProcessAllowed() {
			MapUnit unit = SendersUnit(unitID);
			if (action != null)
				unit?.PerformTerraformAction(action);
		}
	}

	public class MsgChooseProduction : MessageToEngine {
		public ID cityID;
		public string producibleName;

		public MsgChooseProduction(ID cityID, string producibleName) {
			this.cityID = cityID;
			this.producibleName = producibleName;
		}

		protected override void ProcessAllowed() {
			City city = EngineStorage.gameData.GetCity(cityID);
			if (IsSendersCity(city)) {
				foreach (IProducible producible in city.ListProductionOptions(EngineStorage.gameData)) {
					if (producible.name == producibleName) {
						city.ChangeProduction(producible);
						new MsgCityChanged(city).send();
						break;
					}
				}
			}
		}
	}

	// Adds an item to the end of a city's production queue.
	public class MsgEnqueueProduction : MessageToEngine {
		public ID cityID;
		public string producibleName;

		public MsgEnqueueProduction(ID cityID, string producibleName) {
			this.cityID = cityID;
			this.producibleName = producibleName;
		}

		protected override void ProcessAllowed() {
			City city = EngineStorage.gameData.GetCity(cityID);
			if (IsSendersCity(city)) {
				foreach (IProducible producible in city.ListProductionOptions(EngineStorage.gameData)) {
					if (producible.name == producibleName) {
						city.EnqueueProduction(producible);
						new MsgCityChanged(city).send();
						break;
					}
				}
			}
		}
	}

	public class MsgClearProductionQueue : MessageToEngine {
		public ID cityID;

		public MsgClearProductionQueue(ID cityID) {
			this.cityID = cityID;
		}

		protected override void ProcessAllowed() {
			City city = EngineStorage.gameData.GetCity(cityID);
			if (IsSendersCity(city)) {
				city.ClearProductionQueue();
				new MsgCityChanged(city).send();
			}
		}
	}

	// The player clicked a tile on the city screen to move a citizen.
	public class MsgReassignCitizen : MessageToEngine {
		public City city;
		public Tile tile;

		public MsgReassignCitizen(City city, Tile tile) {
			this.city = city;
			this.tile = tile;
		}

		protected override void ProcessAllowed() {
			if (!IsSendersCity(city) || tile == null || tile == Tile.NONE) return;

			CityInteractions.ReassignCitizen(EngineStorage.gameData, city, tile);
			new MsgCityChanged(city).send();
		}
	}

	// The player clicked a specialist on the city screen to change its kind.
	public class MsgCycleSpecialist : MessageToEngine {
		public City city;
		public int residentIndex;

		public MsgCycleSpecialist(City city, int residentIndex) {
			this.city = city;
			this.residentIndex = residentIndex;
		}

		protected override void ProcessAllowed() {
			if (!IsSendersCity(city)) return;

			CityInteractions.CycleSpecialist(EngineStorage.gameData, city, residentIndex);
			new MsgCityChanged(city).send();
		}
	}

	public class MsgAbandonCity : MessageToEngine {
		public City city;

		public MsgAbandonCity(City city) {
			this.city = city;
		}

		protected override void ProcessAllowed() {
			if (!IsSendersCity(city)) return;

			CityInteractions.DestroyCity(city);
		}
	}

	public class MsgChooseResearch : MessageToEngine {
		public Tech tech;
		public AdvisorState advisorState;
		public SelectionMode selectionMode;

		public enum AdvisorState : byte {
			DontShow,
			Show,
		}
		public enum SelectionMode : byte {
			Single,
			Multi,
		}

		public MsgChooseResearch(Tech tech, AdvisorState advisorState, SelectionMode selectionMode = SelectionMode.Single) {
			this.tech = tech;
			this.advisorState = advisorState;
			this.selectionMode = selectionMode;
		}

		protected override void ProcessAllowed() {
			Player player = Sender;
			if (tech == null) return;

			bool isTechEraBeyondPlayerEra = EraUtils.GetEraIndex(tech.EraCivilopediaName) > EraUtils.GetEraIndex(player.eraCivilopediaName);
			if (player.knownTechs.Contains(tech.id) || isTechEraBeyondPlayerEra)
				return;
			if (player.currentlyResearchedTech == tech.id && player.ResearchQueue.Count == 1) {
				return;
			}

			// Start the tech queueing process
			// and start researching the first tech in the queue
			// or append a new queue to the current one if it's a multiselection process
			if (selectionMode == SelectionMode.Single) {
				player.CalculateFreshTechQueueAndAssignNewCurrent(tech);
			} else {
				player.CalculateTechQueueAndAppendToCurrentQueue(tech);

			}

			// update the UI
			if (advisorState == AdvisorState.Show) {

				new MsgShowScienceAdvisor().send();
			} else if (player.currentlyResearchedTech == null
					&& player.GetAvailableTechsToResearch(EngineStorage.gameData.techs).Count > 0) {
				// The choice was learned at once with a free tech (such as
				// Philosophy's), so ask again.
				new MsgShowScienceSelection(player, player.lastDiscoveredTech).send();
			}
		}
	}

	// Sent by the UI at the start of the player's turn: asks them what to
	// research next if they have nothing to research.
	public class MsgAskWhatToResearch : MessageToEngine {
		protected override void ProcessAllowed() {
			Sender.AskWhatToResearch(EngineStorage.gameData);
		}
	}

	// Picks something to research for a player who hasn't chosen, so their
	// science isn't wasted if they dismiss the science selection popup. A free
	// tech the player has coming is left for them to choose.
	public class MsgPickDefaultResearch : MessageToEngine {
		protected override void ProcessAllowed() {
			Player player = Sender;
			GameData gameData = EngineStorage.gameData;
			if (player.currentlyResearchedTech == null && player.freeTechsRemaining == 0
					&& player.GetAvailableTechsToResearch(gameData.techs).Count > 0) {
				PlayerAI.MaybePickTechToResearch(player, gameData.techs);
			}
		}
	}

	public class MsgChangeSliders : MessageToEngine {
		public enum DomesticPolicyChoice {
			MoreScience,
			LessScience,
			MoreLuxury,
			LessLuxury,
		}

		public DomesticPolicyChoice policyChoice;

		public MsgChangeSliders(DomesticPolicyChoice policyChoice) {
			this.policyChoice = policyChoice;
		}

		protected override void ProcessAllowed() {
			Player player = Sender;

			switch (policyChoice) {
				case DomesticPolicyChoice.MoreScience:
					MoreScience(player);
					break;
				case DomesticPolicyChoice.LessScience:
					LessScience(player);
					break;
				case DomesticPolicyChoice.MoreLuxury:
					MoreLuxury(player);
					break;
				case DomesticPolicyChoice.LessLuxury:
					LessLuxury(player);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			// Update the ui to reflect our changes.
			new MsgUpdateUiAfterDomesticChange().send();
		}

		// Increase our science rate, taking away from tax rate if we can,
		// otherwise decrease the luxury rate.
		private void MoreScience(Player player) {
			if (player.scienceRate == player.maxScienceRate) {
				return;
			}

			player.scienceRate++;
			if (player.taxRate > 0) {
				player.taxRate--;
			} else {
				player.luxuryRate--;
			}
		}

		// Ditto for luxury.
		private static void MoreLuxury(Player player) {
			if (player.luxuryRate == player.maxLuxuryRate) {
				return;
			}

			player.luxuryRate++;
			if (player.taxRate > 0) {
				player.taxRate--;
			} else {
				player.scienceRate--;
			}
		}

		// Decreasing is easier, we decrease the requested slider and bump
		// up the tax rate, or the other slider if tax is at the government's
		// rate cap.
		private static void LessScience(Player player) {
			if (player.scienceRate == player.minScienceRate) {
				return;
			}

			if (player.taxRate < player.maxRate) {
				player.taxRate++;
			} else if (player.luxuryRate < player.maxLuxuryRate) {
				player.luxuryRate++;
			} else {
				return;
			}
			player.scienceRate--;
		}

		private static void LessLuxury(Player player) {
			if (player.luxuryRate == player.minLuxuryRate) {
				return;
			}

			if (player.taxRate < player.maxRate) {
				player.taxRate++;
			} else if (player.scienceRate < player.maxScienceRate) {
				player.scienceRate++;
			} else {
				return;
			}
			player.luxuryRate--;
		}
	}

	public class MsgEndTurn : MessageToEngine {
		// Only the player whose turn it is can end it.
		protected override bool IsAllowed() {
			return Sender != null && playerID == EngineStorage.activePlayerID;
		}

		protected override async void ProcessAllowed() {
			try {
				Player controller = Sender;

				TurnHandling.OnEndTurn(controller);

				// Reorder the unit list so that non-busy units will be selected
				// first.
				controller.units.Sort((x, y) => x.IsBusy().CompareTo(y.IsBusy()));

				controller.hasPlayedThisTurn = true;

				// A deal left unanswered by the end of the turn is refused.
				MsgProposeDeal deal = EngineStorage.pendingDeal;
				if (deal != null) {
					EngineStorage.pendingDeal = null;
					new MsgDealResult(deal.Proposer, deal.opponent, false).send();
				}

				// What happens during the other players' turns isn't a reply to
				// this player.
				EngineStorage.processingSenderID = null;
				await TurnHandling.AdvanceTurn();
			} catch (Exception e) {
				EngineStorage.ReportUnhandledException(e, GetType().Name);
			}
		}
	}

	public class MsgPerformUnitAction : MessageToEngine {
		public MapUnit unit;
		public MsgPerformUnitAction(MapUnit unit) {
			this.unit = unit;
		}

		protected override async void ProcessAllowed() {
			try {
				if (unit == null || unit.owner != Sender) return;

				await unit.PerformBusyAction();
			} catch (Exception e) {
				EngineStorage.ReportUnhandledException(e, GetType().Name);
			}
		}
	}

	public class MsgDoHurryProduction : MessageToEngine {
		public City city;
		public MsgDoHurryProduction(City city) {
			this.city = city;
		}

		protected override void ProcessAllowed() {
			if (!IsSendersCity(city)) return;

			city.HurryProduction();
			new MsgCityChanged(city).send();
		}
	}

	public class MsgDoStopWorkerAction : MessageToEngine {
		public MapUnit worker;
		public MsgDoStopWorkerAction(MapUnit worker) {
			this.worker = worker;
		}

		protected override void ProcessAllowed() {
			if (worker == null || worker.owner != Sender) return;

			this.worker.resetWorkerJob();
			this.worker.isAutomated = false;
			if (!this.worker.movementPoints.canMove)
				new MsgShowTemporaryPopup("This unit has already moved.", this.worker.location).send();
		}
	}

	public class MsgSetAnimationsEnabled : MessageToEngine {
		public bool enabled;

		public MsgSetAnimationsEnabled(bool enabled) {
			this.enabled = enabled;
		}

		public override bool IsLocal => true;

		protected override void ProcessAllowed() {
			EngineStorage.animationsEnabled = enabled;
		}
	}

	public class MsgToggleAnimationsEnabled : MessageToEngine {
		public override bool IsLocal => true;

		protected override void ProcessAllowed() {
			EngineStorage.animationsEnabled = !EngineStorage.animationsEnabled;
		}
	}

	public class MsgBuildCity : MessageToEngine {
		public MapUnit unit;
		public string name;

		public MsgBuildCity(MapUnit unit, string name) {
			this.unit = unit;
			this.name = name;
		}

		protected override async void ProcessAllowed() {
			try {
				if (unit == null || unit.owner != Sender || string.IsNullOrWhiteSpace(name)) return;

				City? city = await unit.BuildCity(name);
				if (city != null) {
					new MsgCityCreated(city).send();
				}
			} catch (Exception e) {
				EngineStorage.ReportUnhandledException(e, GetType().Name);
			}
		}
	}

	public class MsgDeclareWar : MessageToEngine {
		public Player opponent;

		public MsgDeclareWar(Player opponent) {
			this.opponent = opponent;
		}

		protected override void ProcessAllowed() {
			if (opponent == null || opponent == Sender) return;

			Sender.DeclareWarOn(opponent, EngineStorage.gameData.turn);
		}
	}

	// The sender proposes a deal. An AI opponent decides at once; a human
	// opponent is asked, and answers with MsgRespondToDeal.
	public class MsgProposeDeal : MessageToEngine {
		public Player opponent;
		public TradeOffer senderGives;
		public TradeOffer senderWants;

		// A hotseat opponent agrees at the proposer's screen, so the deal
		// needn't wait for them.
		public bool opponentAgreed;

		public MsgProposeDeal(Player opponent, TradeOffer senderGives, TradeOffer senderWants) {
			this.opponent = opponent;
			this.senderGives = senderGives;
			this.senderWants = senderWants;
		}

		protected override void ProcessAllowed() {
			GameData gD = EngineStorage.gameData;
			Player proposer = Sender;
			if (opponent == null || opponent == proposer || senderGives == null || senderWants == null) return;

			if (opponent.isHuman && opponentAgreed) {
				opponent.ExecuteDeal(gD, proposer, senderGives, senderWants);
				new MsgDealResult(proposer, opponent, true).send();
				return;
			}
			if (opponent.isHuman) {
				EngineStorage.pendingDeal = this;
				new MsgShowDealProposal(proposer, opponent, senderGives, senderWants) { recipient = opponent }.send();
				return;
			}

			bool accepted = opponent.WouldAcceptDealFrom(gD, proposer, senderGives, senderWants);
			if (accepted) {
				opponent.ExecuteDeal(gD, proposer, senderGives, senderWants);
			}
			new MsgDealResult(proposer, opponent, accepted).send();
		}

		internal Player Proposer => Sender;

		public override void DistrustRemoteSender() {
			opponentAgreed = false;
		}
	}

	// A human answers a deal another human proposed with MsgProposeDeal.
	public class MsgRespondToDeal : MessageToEngine {
		public bool accept;

		public MsgRespondToDeal(bool accept) {
			this.accept = accept;
		}

		// The opponent answers whether or not it is their turn.
		protected override bool IsAllowed() {
			return Sender != null && EngineStorage.pendingDeal?.opponent == Sender;
		}

		protected override void ProcessAllowed() {
			MsgProposeDeal deal = EngineStorage.pendingDeal;
			EngineStorage.pendingDeal = null;

			if (accept) {
				Sender.ExecuteDeal(EngineStorage.gameData, deal.Proposer, deal.senderGives, deal.senderWants);
			}
			new MsgDealResult(deal.Proposer, Sender, accept).send();
		}
	}

	// A human answers an AI's demand to leave its territory: withdraw their
	// units, or refuse and go to war.
	public class MsgRespondToTerritoryDemand : MessageToEngine {
		public bool withdraw;

		public MsgRespondToTerritoryDemand(bool withdraw) {
			this.withdraw = withdraw;
		}

		protected override void ProcessAllowed() {
			EngineStorage.territoryDemandAnswer = withdraw;
		}
	}

	// A human votes in the United Nations election; a null candidate
	// abstains.
	public class MsgCastUnitedNationsVote : MessageToEngine {
		public Player candidate;

		public MsgCastUnitedNationsVote(Player candidate) {
			this.candidate = candidate;
		}

		protected override void ProcessAllowed() {
			UnitedNations.CastHumanVote(EngineStorage.gameData, Sender, candidate);
		}
	}

	// The sender sends a diplomatic or espionage mission against another
	// civ, or one of its cities.
	public class MsgPerformEspionage : MessageToEngine {
		public EspionageMission mission;
		public Player target;
		public City city;

		public MsgPerformEspionage(EspionageMission mission, Player target, City city) {
			this.mission = mission;
			this.target = target;
			this.city = city;
		}

		protected override void ProcessAllowed() {
			Espionage.MissionResult result = Espionage.Perform(EngineStorage.gameData, Sender, mission, target, city);
			new MsgEspionageResult(Sender, mission, result.performed, result.success, result.message).send();
		}
	}

	public class MsgDiplomacyCompleted : MessageToEngine {
		protected override void ProcessAllowed() { }
	}
}
