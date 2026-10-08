using Xunit;

// The game's engine state is static, so tests that play a game can't run at
// the same time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
