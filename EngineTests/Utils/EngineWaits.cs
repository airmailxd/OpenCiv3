using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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

// Finds exceptions the engine recorded from its async void message handlers.
//
// The engine may keep the last such exception in a static field or property
// of type Exception; this looks for any such member in the engine assembly,
// so it keeps working whatever the member is called (and does nothing if
// there isn't one).
public sealed class EngineFailures {
	private static readonly Lazy<IReadOnlyList<Func<object>>> members = new(FindMembers);

	private readonly List<(Func<object> read, object before)> watched;

	private EngineFailures(List<(Func<object>, object)> watched) {
		this.watched = watched;
	}

	// Starts watching for exceptions recorded from now on.
	public static EngineFailures Watch() {
		return new EngineFailures(members.Value.Select(read => (read, read())).ToList());
	}

	public void ThrowIfAny(string doing) {
		foreach ((Func<object> read, object before) in watched) {
			if (read() is Exception e && !ReferenceEquals(e, before)) {
				throw new Exception($"The engine failed while {doing}: {e.Message}", e);
			}
		}
	}

	private static IReadOnlyList<Func<object>> FindMembers() {
		List<Func<object>> found = [];
		Type[] types;
		try {
			types = typeof(EngineStorage).Assembly.GetTypes();
		} catch (ReflectionTypeLoadException e) {
			types = e.Types.Where(t => t != null).ToArray();
		}
		const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
		foreach (Type type in types) {
			if (type.ContainsGenericParameters) {
				continue;
			}
			foreach (FieldInfo field in type.GetFields(flags)) {
				if (typeof(Exception).IsAssignableFrom(field.FieldType) && !field.IsLiteral) {
					found.Add(() => field.GetValue(null));
				}
			}
			foreach (PropertyInfo property in type.GetProperties(flags)) {
				if (typeof(Exception).IsAssignableFrom(property.PropertyType) && property.GetMethod != null
					&& property.GetIndexParameters().Length == 0) {
					found.Add(() => property.GetValue(null));
				}
			}
		}
		return found;
	}
}
