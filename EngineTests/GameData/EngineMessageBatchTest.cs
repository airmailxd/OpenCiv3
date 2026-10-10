using System;
using System.Threading.Tasks;
using C7Engine;
using Xunit;

namespace EngineTests.GameData;

// ProcessMessagesToEngine handles a run of messages in one call, but stops
// where the UI has something to show, or a message is still running, as
// handling one message a frame would have.
public class EngineMessageBatchTest : IDisposable {
	private class Quiet : MessageToEngine {
		public override bool IsLocal => true;
		public int handled;

		protected override void ProcessAllowed() {
			++handled;
		}
	}

	private class Shown : MessageToEngine {
		public override bool IsLocal => true;

		protected override void ProcessAllowed() {
			EngineStorage.SendToLocalUI(new MessageToUI());
		}
	}

	private class Waiting : MessageToEngine {
		public override bool IsLocal => true;
		public readonly TaskCompletionSource done = new();

		protected override Task ProcessAllowedAsync() => done.Task;
	}

	public EngineMessageBatchTest() {
		Dispose();
	}

	public void Dispose() {
		EngineStorage.pendingMessages.Clear();
		EngineStorage.messagesToUI.Clear();
	}

	[Fact]
	public void HandlesARunOfMessagesUntilTheUIHasSomethingToShow() {
		Quiet quiet = new();
		quiet.send();
		quiet.send();
		new Shown().send();
		quiet.send();
		EngineStorage.ProcessMessagesToEngine(TimeSpan.FromSeconds(10));
		Assert.Equal(2, quiet.handled);
		Assert.Single(EngineStorage.messagesToUI);
		Assert.True(EngineStorage.HasPendingMessagesToEngine());

		EngineStorage.messagesToUI.Clear();
		EngineStorage.ProcessMessagesToEngine(TimeSpan.FromSeconds(10));
		Assert.Equal(3, quiet.handled);
		Assert.False(EngineStorage.HasPendingMessagesToEngine());
	}

	[Fact]
	public void StopsAtAMessageStillRunning() {
		Quiet quiet = new();
		Waiting waiting = new();
		waiting.send();
		quiet.send();
		EngineStorage.ProcessMessagesToEngine(TimeSpan.FromSeconds(10));
		Assert.Equal(0, quiet.handled);

		waiting.done.SetResult();
		EngineStorage.ProcessMessagesToEngine(TimeSpan.FromSeconds(10));
		Assert.Equal(1, quiet.handled);
	}
}
