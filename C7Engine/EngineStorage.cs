namespace C7Engine {
	using System;
	using C7GameData;
	using System.Collections.Generic;
	using System.Threading.Tasks;

	/**
	 * This class stores references to data that the engine needs between calls from the player.
	 * Most obviously this includes a reference to the C7GameData, but it might eventually
	 * also include things like keeping track of which networked players are up to date.
	 *
	 * Note that we should NOT store pointers to pieces of the game data here; that will
	 * all be handled within C7GameData.  We just need a pointer to the main, top level
	 * so we don't forget the state of the game after we create it.
	 **/
	public static class EngineStorage {
		public static GameData gameData { get; set; }

		// The player whose perspective this machine's UI shows.
		public static ID uiControllerID;

		// The human player whose turn it is. In a hotseat game the UI follows
		// them from player to player; in a LAN game each machine's UI stays
		// with its own player while the turn moves between machines.
		public static ID activePlayerID;
		public static bool uiFollowsActivePlayer = true;

		// The human player an AI is waiting on to answer a trade offer, if any.
		// They may answer it even though it isn't their turn.
		public static ID diplomacyPlayerID;

		// The AI waiting on diplomacyPlayerID's answer.
		internal static ID diplomacyAIPlayerID;

		// Whether a human player is there to be asked something. A LAN host
		// answers for its guests' seats, which may be empty; otherwise every
		// player is at this machine.
		public static Func<ID, bool> playerReachable;

		// A deal one human proposed to another, waiting for their answer.
		internal static MsgProposeDeal pendingDeal;

		// How the human an AI told to leave its territory answered: true to
		// withdraw, false to refuse, null if they haven't answered.
		internal static bool? territoryDemandAnswer;

		// The sender of the message being processed, so the messages it causes
		// go back to them. It flows with the handling of the message across
		// its awaits, so a handler that waits (for an animation, say) still
		// answers its own sender, and messages handled meanwhile answer theirs.
		internal static ID processingSenderID {
			get => processingSender.Value;
			set => processingSender.Value = value;
		}
		private static readonly System.Threading.AsyncLocal<ID> processingSender = new();

		// A LAN client sends messages to the host instead of its own engine, and
		// a LAN host delivers messages to the machine of the player they are
		// for. When null, messages stay on this machine.
		public static Action<MessageToEngine> remoteEngine;
		public static Action<MessageToUI> uiMessageRouter;

		// Counts the messages the engine has finished processing (or turned
		// down), so a LAN host can tell when the game has changed.
		public static long processedMessageCount { get; private set; }

		internal static bool animationsEnabled = false;

		// Set while a LAN guest predicts one of its own orders (see
		// MovePrediction): the host tells its UI what came of the order, so
		// nothing is sent to the UI meanwhile but animations.
		internal static bool predicting = false;

		internal static readonly Queue<MessageToEngine> pendingMessages = new();
		internal static readonly Queue<MessageToUI> messagesToUI = new();
		internal static readonly Queue<AnimationMessage> animationMessages = new();

		internal static readonly Dictionary<Guid, TaskCompletionSource<bool>> pendingAnimations = new();
		static readonly Dictionary<Type, List<TaskCompletionSource<MessageToEngine>>> pendingEngineWaiters = new();

		// A message whose handling has started but not yet finished, such as
		// a unit's move waiting for its animation, or the end of a turn
		// waiting for the AIs to play.
		private sealed class RunningMessage {
			public MessageToEngine msg;
			public Task task;
			// Whether the next message waits for this one to finish. Answers
			// to an AI handled while it waits don't hold anything up.
			public bool holdsQueue;
		}
		private static readonly List<RunningMessage> runningMessages = new();

		// Handles the next message to the engine, if it may start now.
		//
		// The messages are handled one at a time, in order: the next doesn't
		// start until the one before has finished, even if that one waits
		// for animations or for the AIs to play. Some messages don't have to
		// wait: answers to an AI that is waiting on them (it couldn't go on
		// without them), settings of this machine, and messages the sender
		// may not send now, which are turned down at once as they always were.
		public static void ProcessNextMessageToEngine() {
			FinishCompletedMessages();
			if (pendingMessages.Count == 0) {
				return;
			}

			bool busy = runningMessages.Exists(r => r.holdsQueue);
			MessageToEngine msg = busy ? TakeFirstPending(m => m.MayRunWhileBusy()) : pendingMessages.Dequeue();
			if (msg == null) {
				return;
			}

			Task task;
			processingSenderID = msg.playerID;
			try {
				task = msg.Start();
			} catch (Exception e) {
				ReportUnhandledException(e, msg.GetType().Name);
				task = null;
			} finally {
				processingSenderID = null;
			}

			if (task == null) {
				// Turned down, or failed.
				++processedMessageCount;
				return;
			}
			runningMessages.Add(new RunningMessage { msg = msg, task = task, holdsQueue = !busy });
			FinishCompletedMessages();
		}

		// Removes and returns the first waiting message that matches, keeping
		// the others in order.
		private static MessageToEngine TakeFirstPending(Func<MessageToEngine, bool> match) {
			MessageToEngine found = null;
			int count = pendingMessages.Count;
			for (int i = 0; i < count; ++i) {
				MessageToEngine m = pendingMessages.Dequeue();
				if (found == null && match(m)) {
					found = m;
				} else {
					pendingMessages.Enqueue(m);
				}
			}
			return found;
		}

		// Counts the messages that have finished, reports any that failed and
		// wakes whoever waits for them.
		private static void FinishCompletedMessages() {
			for (int i = 0; i < runningMessages.Count;) {
				RunningMessage running = runningMessages[i];
				if (!running.task.IsCompleted) {
					++i;
					continue;
				}
				runningMessages.RemoveAt(i);
				++processedMessageCount;
				if (running.task.IsFaulted) {
					ReportUnhandledException(running.task.Exception.GetBaseException(), running.msg.GetType().Name);
				} else if (!running.task.IsCanceled) {
					NotifyEngineWaiters(running.msg);
				}
			}
		}

		private static void NotifyEngineWaiters(MessageToEngine msg) {
			if (pendingEngineWaiters.Remove(msg.GetType(), out var waiters)) {
				foreach (TaskCompletionSource<MessageToEngine> tcs in waiters) {
					tcs.TrySetResult(msg);
				}
			}
		}

		internal static void SendToEngine(MessageToEngine msg) {
			if (remoteEngine != null && !msg.IsLocal) {
				remoteEngine(msg);
			} else {
				pendingMessages.Enqueue(msg);
			}
		}

		internal static void SendToUI(MessageToUI msg) {
			if (predicting) {
				return;
			}
			// Without a LAN host to pass it on, only a game being watched
			// rather than played has a spectator to show it to.
			if (msg.IsForSpectatorsOnly && uiMessageRouter == null && gameData?.observerMode != true) {
				return;
			}
			if (uiMessageRouter != null) {
				uiMessageRouter(msg);
			} else {
				messagesToUI.Enqueue(msg);
			}
		}

		// Delivers a message to this machine's UI, bypassing any LAN routing.
		public static void SendToLocalUI(MessageToUI msg) {
			messagesToUI.Enqueue(msg);
		}

		// Queues a message from a LAN client for this machine's engine.
		public static void ReceiveFromRemote(MessageToEngine msg) {
			pendingMessages.Enqueue(msg);
		}

		public static bool HasPendingMessagesToEngine() {
			return pendingMessages.Count > 0;
		}

		// Returns the engine to how a fresh single-machine game starts, for
		// when a LAN game ends.
		public static void ResetNetworking() {
			remoteEngine = null;
			uiMessageRouter = null;
			uiFollowsActivePlayer = true;
			playerReachable = null;
			diplomacyPlayerID = null;
			diplomacyAIPlayerID = null;
			pendingDeal = null;
			territoryDemandAnswer = null;
		}

		// Drops the work left over from a previous game, so it can't block
		// or leak into a newly created or loaded one. The previous game's turn
		// loop may be waiting on an animation or a message; its waiters are
		// dropped without being completed, so that loop never resumes.
		internal static void ResetForNewGame() {
			pendingMessages.Clear();
			messagesToUI.Clear();
			animationMessages.Clear();
			pendingAnimations.Clear();
			pendingEngineWaiters.Clear();
			runningMessages.Clear();
			processingSenderID = null;
			diplomacyPlayerID = null;
			diplomacyAIPlayerID = null;
			pendingDeal = null;
			territoryDemandAnswer = null;
			TurnHandling.ResetForNewGame();
			UnitInteractions.ResetForNewGame();
			// The tile change log would otherwise keep the previous game alive.
			TileChangeJournal.Reset();
		}

		// The last exception that escaped an engine handler nobody awaits
		// (such as a message's ProcessAllowed), so that a test host can tell
		// that one happened. Such exceptions are logged and don't stop the
		// engine.
		public static Exception LastUnhandledEngineException { get; private set; }

		// Raised with each such exception, and the name of what threw it.
		public static event Action<Exception, string> UnhandledEngineException;

		private static readonly Serilog.ILogger log = Serilog.Log.ForContext(typeof(EngineStorage));

		internal static void ReportUnhandledException(Exception e, string source) {
			log.Error(e, "Unhandled exception in {Source}", source);
			LastUnhandledEngineException = e;
			UnhandledEngineException?.Invoke(e, source);
		}

		// Reports the exception, if any, of a task that nothing awaits.
		internal static void ObserveTask(Task task, string source) {
			if (task.IsCompleted) {
				if (task.IsFaulted) {
					ReportUnhandledException(task.Exception.GetBaseException(), source);
				}
				return;
			}
			task.ContinueWith(t => ReportUnhandledException(t.Exception.GetBaseException(), source),
				System.Threading.CancellationToken.None,
				TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
				TaskScheduler.Default);
		}

		// Forgets LastUnhandledEngineException, for tests.
		public static void ClearUnhandledEngineException() {
			LastUnhandledEngineException = null;
		}

		public static bool HasPendingAnimations() {
			return pendingAnimations.Count > 0;
		}

		public static bool TryDequeueNextMessageToUI(out MessageToUI message) {
			if (messagesToUI.Count > 0) {
				message = messagesToUI.Dequeue();
				return true;
			}
			message = null;
			return false;
		}

		public static bool TryDequeueNextAnimationMessage(out AnimationMessage message) {
			if (animationMessages.Count > 0) {
				message = animationMessages.Dequeue();
				return true;
			}
			message = null;
			return false;
		}

		internal static Task WaitForAnimationFinished(Guid animationId) {
			var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			pendingAnimations[animationId] = tcs;
			return tcs.Task;
		}

		// Waits for the engine to finish processing the next message of the
		// given type. Several may wait for the same type; they all get it.
		public static async Task<T> WaitForMessageToEngine<T>() where T : MessageToEngine {
			var tcs = new TaskCompletionSource<MessageToEngine>(TaskCreationOptions.RunContinuationsAsynchronously);
			if (!pendingEngineWaiters.TryGetValue(typeof(T), out var waiters)) {
				waiters = new();
				pendingEngineWaiters[typeof(T)] = waiters;
			}
			waiters.Add(tcs);

			return (T)await tcs.Task;
		}

		public static bool IsPlayerReachable(ID player) {
			return playerReachable?.Invoke(player) ?? true;
		}

		// Waits for the given player to close the diplomacy screen an AI
		// opened for them. Anyone else closing theirs is no answer.
		internal static async Task WaitForDiplomacyCompleted(ID player) {
			MsgDiplomacyCompleted completed;
			do {
				completed = await WaitForMessageToEngine<MsgDiplomacyCompleted>();
			} while (completed.playerID != player);
		}

		public static void ReadGameData(Action<GameData> accessor) {
			accessor(gameData);
		}

		public static void InitializeGameDataForTests(GameData gD) {
			gameData = gD;
			// A turn loop a previous test left waiting won't resume.
			TurnHandling.ResetForNewGame();
		}
	}
}
