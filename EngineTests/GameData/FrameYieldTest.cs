using System;
using System.Threading.Tasks;
using C7Engine;
using Xunit;

namespace EngineTests.GameData;

// Long engine work (the AIs' turns) hands the rest of the frame back once it
// has run for EngineStorage.frameBudget, and goes on at the next
// ProcessNextMessageToEngine, before any other message starts.
public class FrameYieldTest : IDisposable {
	private class Quiet : MessageToEngine {
		public override bool IsLocal => true;
		public int handled;

		protected override void ProcessAllowed() {
			++handled;
		}
	}

	public FrameYieldTest() {
		Dispose();
	}

	public void Dispose() {
		EngineStorage.frameBudget = TimeSpan.Zero;
		EngineStorage.ResetForNewGame();
	}

	// Work that waits for the next frame at each of its steps.
	private static async Task Steps(int count, Action onStep) {
		for (int i = 0; i < count; ++i) {
			await EngineStorage.YieldIfFrameBudgetSpent();
			onStep();
		}
	}

	[Fact]
	public void WithoutABudgetWorkRunsInOneGo() {
		int steps = 0;
		Task work = Steps(3, () => ++steps);
		Assert.True(work.IsCompleted);
		Assert.Equal(3, steps);
		Assert.False(EngineStorage.IsWaitingForNextFrame);
	}

	[Fact]
	public void WorkGoesOnAtTheNextFrameBeforeAnyMessage() {
		// Any time at all spends the budget, so each step waits a frame.
		EngineStorage.frameBudget = TimeSpan.FromTicks(1);
		int steps = 0;
		Task work = Steps(3, () => ++steps);
		Assert.Equal(0, steps);
		Assert.True(EngineStorage.IsWaitingForNextFrame);

		Quiet quiet = new();
		quiet.send();
		for (int frame = 1; frame <= 2; ++frame) {
			EngineStorage.ProcessNextMessageToEngine();
			Assert.Equal(frame, steps);
			// The message waits until the work is done.
			Assert.Equal(0, quiet.handled);
		}

		// The last step finishes the work, and then the message goes ahead.
		EngineStorage.ProcessNextMessageToEngine();
		Assert.True(work.IsCompleted);
		Assert.Equal(3, steps);
		Assert.Equal(1, quiet.handled);
	}

	[Fact]
	public void ANewGameDropsWaitingWork() {
		EngineStorage.frameBudget = TimeSpan.FromTicks(1);
		int steps = 0;
		Task work = Steps(1, () => ++steps);
		Assert.True(EngineStorage.IsWaitingForNextFrame);

		EngineStorage.ResetForNewGame();
		EngineStorage.ProcessNextMessageToEngine();
		Assert.False(EngineStorage.IsWaitingForNextFrame);
		Assert.Equal(0, steps);
		Assert.False(work.IsCompleted);
	}
}
