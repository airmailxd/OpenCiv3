using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine;

// What a player slot on the Multiplayer Setup screen holds, named as Civ3
// names them (Conquests' labels.txt): Open, a seat for a person (joining
// over the network, or sharing this computer in hotseat); Closed, no civ at
// all; Computer, a civ the AI plays.
public enum SlotKind {
	Open,
	Closed,
	Computer,
}

// The Multiplayer Setup screen's game types (Civ3's "Game Type:"): people
// sharing this computer, or joining over the local network, or over the
// internet as well (through the online relay; see OnlineRelay).
public enum MultiplayerGameType {
	Hotseat,
	Lan,
	Internet,
}

// Civ3's game modes (its "Game Mode:" list), each a preset of the special
// rules; Custom is any other mix of them.
public enum MultiplayerGameMode {
	Standard,
	Elimination,
	Regicide,
	MassRegicide,
	VictoryPointScoring,
	Custom,
}

// A rival's slot: what it holds, the civilization chosen for it (null or
// Random for a random one), and in hotseat the name of whoever plays it
// (null for the default, "Player N").
public record struct PlayerSlot(SlotKind kind, string civilization = null, string name = null);

// The players a Multiplayer Setup makes: the host's civilization, the
// computer's civs, the other people sharing this computer, and the seats
// left for people joining over the network; or why the game can't start.
public class MultiplayerPlan {
	public Civilization playerCivilization;
	public List<SelectedOpponent> opponents = [];
	public List<HotseatPlayer> hotseatPlayers = [];
	public int guestSeats;
	public string problem;
}

// Turns the Multiplayer Setup screen's slots into the players of a game
// (see GameSetup), as Civ3's own screen does.
public static class MultiplayerSlots {
	// Civ3's default game name is the host's name and this (labels.txt).
	public const string GameNameSuffix = "'s_Game";

	public static string DefaultGameName(string playerName) => playerName + GameNameSuffix;

	// The plan for the host playing hostCivilization (null or Random for a
	// random one nobody else has) with the given rivals' slots. In hotseat
	// an Open slot is a person at this computer; otherwise it's a seat for
	// someone joining, who chooses their civilization in the lobby.
	public static MultiplayerPlan Plan(MultiplayerGameType type, string hostCivilization, IReadOnlyList<PlayerSlot> slots,
		List<Civilization> civilizations, Random random) {
		MultiplayerPlan plan = new();
		List<Civilization> playable = civilizations.Where(c => !c.isBarbarian).ToList();
		Civilization Chosen(string name) => name == null || name == NewGameChoices.Random ? null : playable.Find(c => c.name == name);

		// A civilization chosen twice is random the second time.
		Civilization host = Chosen(hostCivilization);
		HashSet<string> taken = host == null ? [] : [host.name];
		List<Civilization> slotCivs = slots.Select(slot => {
			Civilization civ = slot.kind == SlotKind.Closed ? null : Chosen(slot.civilization);
			bool networkSeat = slot.kind == SlotKind.Open && type != MultiplayerGameType.Hotseat;
			return civ != null && !networkSeat && taken.Add(civ.name) ? civ : null;
		}).ToList();
		plan.playerCivilization = host ?? NewGameChoices.RandomCivilization(civilizations, taken, random);

		for (int i = 0; i < slots.Count; ++i) {
			PlayerSlot slot = slots[i];
			switch (slot.kind) {
				case SlotKind.Computer:
					plan.opponents.Add(slotCivs[i] == null
						? new SelectedOpponent { isRandom = true }
						: new SelectedOpponent { Name = slotCivs[i].name });
					break;
				case SlotKind.Open when type == MultiplayerGameType.Hotseat:
					plan.hotseatPlayers.Add(new HotseatPlayer {
						civilization = slotCivs[i],
						name = NewGameChoices.HotseatName(slot.name, plan.hotseatPlayers.Count + 2),
					});
					break;
				case SlotKind.Open:
					++plan.guestSeats;
					break;
			}
		}

		int humans = plan.hotseatPlayers.Count + plan.guestSeats;
		if (humans + plan.opponents.Count == 0) {
			plan.problem = "Every other slot is Closed. Set a slot to Open for another player, or to Computer.";
		} else if (humans == 0) {
			plan.problem = type == MultiplayerGameType.Hotseat
				? "Set a slot to Open for each other player at this computer."
				: "Set a slot to Open for each player who will join.";
		}
		return plan;
	}

	// Sets the special rules as the game mode has them. Custom leaves them
	// as they are.
	public static void ApplyGameMode(VictoryConditions conditions, MultiplayerGameMode mode) {
		if (mode == MultiplayerGameMode.Custom) {
			return;
		}
		conditions.CityElimination = mode == MultiplayerGameMode.Elimination;
		conditions.Regicide = mode == MultiplayerGameMode.Regicide;
		conditions.MassRegicide = mode == MultiplayerGameMode.MassRegicide;
		conditions.VictoryLocations = mode == MultiplayerGameMode.VictoryPointScoring;
	}

	// The game mode the special rules make: Standard with none of them, the
	// mode of the one that's on, or Custom for several.
	public static MultiplayerGameMode GameModeOf(VictoryConditions conditions) {
		List<MultiplayerGameMode> on = [];
		if (conditions.CityElimination) {
			on.Add(MultiplayerGameMode.Elimination);
		}
		if (conditions.Regicide) {
			on.Add(MultiplayerGameMode.Regicide);
		}
		if (conditions.MassRegicide) {
			on.Add(MultiplayerGameMode.MassRegicide);
		}
		if (conditions.VictoryLocations) {
			on.Add(MultiplayerGameMode.VictoryPointScoring);
		}
		return on.Count switch {
			0 => MultiplayerGameMode.Standard,
			1 => on[0],
			_ => MultiplayerGameMode.Custom,
		};
	}

	// The slots as last set up: Open for the LAN seats or hotseat players
	// (as the game type has them), Closed, or Computer, with the
	// civilization chosen for each.
	public static List<PlayerSlot> SlotsAsLastTime(NewGameChoices choices, MultiplayerGameType type, int count) {
		List<PlayerSlot> slots = [];
		for (int i = 0; i < count; ++i) {
			string civ = i < choices.Opponents.Count ? choices.Opponents[i] : NewGameChoices.Random;
			bool open = type == MultiplayerGameType.Hotseat ? choices.HotseatSlots.ContainsKey(i) : choices.LanSlots.Contains(i);
			SlotKind kind = open ? SlotKind.Open : choices.ClosedSlots.Contains(i) ? SlotKind.Closed : SlotKind.Computer;
			slots.Add(new PlayerSlot(kind, civ, choices.HotseatSlots.GetValueOrDefault(i)));
		}
		return slots;
	}

	// Remembers the slots, for the next setup and Quick Start.
	public static void RememberSlots(NewGameChoices choices, MultiplayerGameType type, IReadOnlyList<PlayerSlot> slots) {
		choices.Opponents = slots.Select(s => s.civilization ?? NewGameChoices.Random).ToList();
		choices.ClosedSlots = new(Enumerable.Range(0, slots.Count).Where(i => slots[i].kind == SlotKind.Closed));
		IEnumerable<int> open = Enumerable.Range(0, slots.Count).Where(i => slots[i].kind == SlotKind.Open);
		if (type == MultiplayerGameType.Hotseat) {
			choices.HotseatSlots = new(open.ToDictionary(i => i, i => slots[i].name ?? ""));
		} else {
			// As the player setup screen does when hosting: this game has no
			// hotseat players.
			choices.LanSlots = new(open);
			choices.HotseatSlots = [];
		}
		choices.MultiplayerGameType = type.ToString();
	}
}
