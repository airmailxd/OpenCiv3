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
		// They may negotiate even though it isn't their turn.
		public static ID diplomacyPlayerID;

		// A deal one human proposed to another, waiting for their answer.
		internal static MsgProposeDeal pendingDeal;

		// The sender of the message being processed, so the messages it causes
		// go back to them.
		internal static ID processingSenderID;

		// A LAN client sends messages to the host instead of its own engine, and
		// a LAN host delivers messages to the machine of the player they are
		// for. When null, messages stay on this machine.
		public static Action<MessageToEngine> remoteEngine;
		public static Action<MessageToUI> uiMessageRouter;

		// Counts the messages the engine has processed, so a LAN host can tell
		// when the game has changed.
		public static long processedMessageCount { get; private set; }

		internal static bool animationsEnabled = false;

		internal static readonly Queue<MessageToEngine> pendingMessages = new();
		internal static readonly Queue<MessageToUI> messagesToUI = new();
		internal static readonly Queue<AnimationMessage> animationMessages = new();

		internal static readonly Dictionary<Guid, TaskCompletionSource<bool>> pendingAnimations = new();
		static readonly Dictionary<Type, TaskCompletionSource<MessageToEngine>> pendingEngineWaiters = new();

		public static void ProcessNextMessageToEngine() {
			if (pendingMessages.Count > 0) {
				var msg = pendingMessages.Dequeue();
				processingSenderID = msg.playerID;
				bool processed;
				try {
					processed = msg.process();
				} finally {
					processingSenderID = null;
					++processedMessageCount;
				}

				var type = msg.GetType();
				if (processed && pendingEngineWaiters.TryGetValue(type, out var tcs)) {
					tcs.TrySetResult(msg);
					pendingEngineWaiters.Remove(type);
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
			diplomacyPlayerID = null;
			pendingDeal = null;
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
			processingSenderID = null;
			diplomacyPlayerID = null;
			pendingDeal = null;
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

		public static Task<T> WaitForMessageToEngine<T>() where T : MessageToEngine {
			var tcs = new TaskCompletionSource<MessageToEngine>(TaskCreationOptions.RunContinuationsAsynchronously);
			pendingEngineWaiters[typeof(T)] = tcs;

			return tcs.Task.ContinueWith(t => (T)t.Result);
		}

		public static void ReadGameData(Action<GameData> accessor) {
			accessor(gameData);
		}

		public static void InitializeGameDataForTests(GameData gD) {
			gameData = gD;
		}
	}
}
