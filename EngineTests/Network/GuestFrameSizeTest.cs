using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Network;
using C7GameData;
using C7GameData.Save;
using C7Relay;
using EngineTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace EngineTests.Network;

// The online relay takes only small messages from guests, since anyone can
// connect as one; every frame a guest's game sends, even the largest it
// could, has to fit with plenty to spare.
public class GuestFrameSizeTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly ITestOutputHelper output;

	public GuestFrameSizeTest(SaveGameFixture fixture, ITestOutputHelper output) {
		this.fixture = fixture;
		this.output = output;
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		EngineStorage.ResetNetworking();
	}

	public void Dispose() {
		EngineStorage.ResetNetworking();
	}

	// A frame as the guest's connection writes it: its header and payload.
	private int Frame(string name, byte[] payload) {
		int bytes = 5 + payload.Length;
		output.WriteLine($"{name}: {bytes:N0} bytes");
		return bytes;
	}

	[Fact]
	public async Task EveryGuestFrameFitsTheRelaysGuestLimit() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		await CreateGame.createGame(save, (_) => fixture.behaviors);
		C7GameData.GameData gameData = EngineStorage.gameData;
		Player[] humans = gameData.players.Where(p => p.isHuman).ToArray();
		MapUnit unit = humans[1].units.First();

		string longName = new('W', 40);
		List<int> frames = [
			Frame("Hello", NetSerialization.SerializeData(new HelloInfo(LanProtocol.Version, longName, new string('F', 32)))),
			Frame("ClaimSeat", NetSerialization.SerializeData(new ClaimSeatInfo(humans[1].id, longName))),
			Frame("ChooseCivilization", NetSerialization.SerializeData(new ChooseCivilizationInfo(longName, humans[1].id))),
			// Far longer than any path a unit is given: one through every
			// tile on the map.
			Frame("SetUnitPath, every tile", NetSerialization.Serialize(new MsgSetUnitPath(unit.id,
				new TilePath(gameData.map.tiles.Last(), new Queue<Tile>(gameData.map.tiles))) { playerID = humans[1].id })),
			// Every tech, both ways, and gold.
			Frame("ProposeDeal, every tech", NetSerialization.Serialize(new MsgProposeDeal(humans[0],
				new TradeOffer { gold = int.MaxValue, techs = [.. gameData.techs] },
				new TradeOffer { gold = int.MaxValue, techs = [.. gameData.techs] }) { playerID = humans[1].id })),
		];
		output.WriteLine($"{gameData.map.tiles.Count:N0} tiles, {gameData.techs.Count} techs");
		Assert.All(frames, bytes => Assert.True(bytes * 2 < RelayProtocol.DefaultMaxGuestMessageBytes,
			$"A guest frame of {bytes:N0} bytes is too close to the relay's limit"));
	}
}
