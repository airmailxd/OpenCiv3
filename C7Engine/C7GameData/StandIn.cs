namespace C7GameData;

// Who plays a server game's human player while they're away, as the game's
// owner chose (see LanHost.ChooseStandIn), until they're back: nobody (the
// game waits for them), the computer, or the owner at their own machine,
// in turns as in a hotseat game. While the computer plays them, they aren't
// human (Player.isHuman), but their save says they are, so that they keep
// their seat.
public enum StandIn {
	None,
	Computer,
	Hotseat,
}
