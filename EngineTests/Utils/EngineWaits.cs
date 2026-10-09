using System;
using System.Diagnostics;
using System.Threading;
using C7Engine;

namespace EngineTests.Utils;

// Bounded waits for the engine.
//
// The engine handles many messages in async void methods, so an exception
// thrown while handling one doesn't reach the test: the engine just stops
// sending messages. Waiting for a message that will never come must fail the
// test with a clear message rather than hang the whole test run.
public static class EngineWaits {
	// Generous: a single wait covers at most one game turn of AI play.
	public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

	// How long the engine may sit with nothing to do before we give up on it.
	public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromSeconds(30);

	// Processes engine messages until the engine hands the UI to a player,
	// passing every other message to the UI to onOtherMessage.
	public static MsgStartTurn WaitForStartTurnMessage(Action<MessageToUI> onOtherMessage = null,
		TimeSpan? timeout = null, TimeSpan? idleTimeout = null) {
		return WaitForMessageToUI<MsgStartTurn>(onOtherMessage, timeout, idleTimeout);
	}

	public static T WaitForMessageToUI<T>(Action<MessageToUI> onOtherMessage = null,
		TimeSpan? timeout = null, TimeSpan? idleTimeout = null) where T : MessageToUI {
		TimeSpan limit = timeout ?? DefaultTimeout;
		TimeSpan idleLimit = idleTimeout ?? DefaultIdleTimeout;
		EngineFailures failures = EngineFailures.Watch();
		Stopwatch waiting = Stopwatch.StartNew();
		Stopwatch idle = Stopwatch.StartNew();
		long lastProcessed = EngineStorage.processedMessageCount;

		while (true) {
			EngineStorage.ProcessNextMessageToEngine();
			bool progressed = EngineStorage.processedMessageCount != lastProcessed;
			lastProcessed = EngineStorage.processedMessageCount;

			while (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg)) {
				progressed = true;
				if (msg is T wanted) {
					return wanted;
				}
				onOtherMessage?.Invoke(msg);
			}

			failures.ThrowIfAny($"waiting for {typeof(T).Name}");
			if (progressed) {
				idle.Restart();
			} else {
				if (idle.Elapsed > idleLimit) {
					throw new TimeoutException($"The engine went idle for {idleLimit.TotalSeconds:0}s without sending {typeof(T).Name}; " +
											   "an engine message handler probably failed. Pending engine messages: " +
											   EngineStorage.HasPendingMessagesToEngine());
				}
				// Nothing to do: maybe the engine is waiting on another thread.
				Thread.Sleep(1);
			}
			if (waiting.Elapsed > limit) {
				throw new TimeoutException($"The engine didn't send {typeof(T).Name} within {limit.TotalSeconds:0}s");
			}
		}
	}

}

// Finds exceptions the engine recorded from its async void message handlers,
// which it keeps in EngineStorage.LastUnhandledEngineException.
public sealed class EngineFailures {
	private readonly Exception before;

	private EngineFailures(Exception before) {
		this.before = before;
	}

	// Starts watching for exceptions recorded from now on.
	public static EngineFailures Watch() {
		return new EngineFailures(EngineStorage.LastUnhandledEngineException);
	}

	public void ThrowIfAny(string doing) {
		if (EngineStorage.LastUnhandledEngineException is Exception e && !ReferenceEquals(e, before)) {
			throw new Exception($"The engine failed while {doing}: {e.Message}", e);
		}
	}
}
