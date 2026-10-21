using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using C7GameData;
using Xunit;

namespace EngineTests.GameData;

// Conquests' Campaign Record: the nine campaigns and the best win at each,
// kept in a file, and Civ3's own CampaignRecord.hof read.
public class CampaignRecordTest : IDisposable {
	private readonly string directory = Path.Combine(Path.GetTempPath(), $"c7-camprec-{Guid.NewGuid():N}");
	private string FilePath => Path.Combine(directory, CampaignRecord.FileName);
	private static readonly DateTime Day = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

	private static readonly string[] Names = ["1 Mesopotamia", "2 Rise of Rome", "7 Sengoku"];

	public void Dispose() {
		if (Directory.Exists(directory)) {
			Directory.Delete(directory, true);
		}
	}

	[Theory]
	[InlineData(0, 600)]
	[InlineData(1, 800)]
	[InlineData(2, 1000)]
	[InlineData(3, 1200)]
	// 1.4 and 1.8 as floats are a hair under, and Civ3 rounds down.
	[InlineData(4, 1399)]
	[InlineData(5, 1600)]
	[InlineData(6, 1799)]
	[InlineData(7, 2000)]
	[InlineData(8, 600)]
	public void ScoreIsScaledForTheDifficulty(int level, int adjusted) {
		Assert.Equal(adjusted, CampaignRecord.AdjustedScore(1000, level));
	}

	[Fact]
	public void AdjustedScoreRoundsDown() {
		Assert.Equal(0, CampaignRecord.AdjustedScore(1, 0));
		Assert.Equal(2, CampaignRecord.AdjustedScore(3, 1));
	}

	[Fact]
	public void AnUnwonCampaignIsNotYetVictorious() {
		CampaignRecordEntry entry = new() { Campaign = "1 Mesopotamia" };
		Assert.Equal(CampaignRecord.NotYetVictorious, entry.Score);
		Assert.False(entry.Victorious);
		// A score that rounds to nothing isn't a win either.
		Assert.False((entry with { Score = 1, DifficultyLevel = 0 }).Victorious);
		Assert.True((entry with { Score = 2, DifficultyLevel = 0 }).Victorious);
	}

	[Fact]
	public void CampaignNamesComeFromTheLabelsSection() {
		string labels = "#OTHER\nSomething\n\n#CAMPAIGN_RECORD\r\nCAMPAIGNS\r\nHall of Fame\r\nExit\r\nCampaign\r\n"
			+ "Civilization\r\nDifficulty\r\nScore\r\nNot Yet Victorious\r\nNext\r\nPlay the Next Campaign\r\n"
			+ "1 Mesopotamia\r\n2 Rise of Rome\r\n\r\n\r\n#HALL_OF_FAME_WIN\r\nHALL OF FAME\r\n";
		Assert.Equal(["1 Mesopotamia", "2 Rise of Rome"], CampaignRecord.CampaignNames(labels));
		Assert.Empty(CampaignRecord.CampaignNames("#OTHER\nx\n"));
	}

	[Fact]
	public void AScenarioIsACampaignIfItsNameStartsWithOne() {
		Assert.True(CampaignRecord.IsCampaign("7 Sengoku - Sword of the Shogun", Names));
		Assert.True(CampaignRecord.IsCampaign("1 Mesopotamia", Names));
		Assert.False(CampaignRecord.IsCampaign("1 MP Mesopotamia", Names));
		Assert.False(CampaignRecord.IsCampaign("conquests", Names));
		// Civ3 compares with strncmp, so case counts.
		Assert.False(CampaignRecord.IsCampaign("1 mesopotamia", Names));
	}

	[Fact]
	public void CampaignsAreAddedOnceInTheOrderGiven() {
		List<CampaignRecordEntry> entries = [];
		Assert.True(CampaignRecord.AddCampaigns(entries,
			["1 Mesopotamia", "2 Rise of Rome", "7 Sengoku - Sword of the Shogun", "conquests", "Quick Civ"], Names));
		Assert.Equal(["1 Mesopotamia", "2 Rise of Rome", "7 Sengoku - Sword of the Shogun"], entries.Select(e => e.Campaign));
		Assert.False(CampaignRecord.AddCampaigns(entries, ["2 Rise of Rome"], Names));
		Assert.Equal(3, entries.Count);
	}

	[Fact]
	public void AWinIsRecordedIfItIsWorthAtLeastAsMuch() {
		List<CampaignRecordEntry> entries = [new() { Campaign = "1 Mesopotamia" }, new() { Campaign = "2 Rise of Rome" }];

		Assert.True(CampaignRecord.RecordVictory(entries, "2 Rise of Rome", 500, 2, "Regent", "Romans", "Military", Day));
		CampaignRecordEntry won = entries[1];
		Assert.Equal(500, won.Score);
		Assert.Equal(500, won.AdjustedScore);
		Assert.Equal("Romans", won.Civilization);
		Assert.Equal("Regent", won.Difficulty);
		Assert.Equal("Military", won.VictoryType);
		Assert.Equal(Day, won.WonAt);
		Assert.False(entries[0].Victorious);

		// 600 at Chieftain counts as 360, less than 500.
		Assert.False(CampaignRecord.RecordVictory(entries, "2 Rise of Rome", 600, 0, "Chieftain", "Carthaginians", "Domination", Day));
		Assert.Equal("Romans", entries[1].Civilization);

		// 250 at Deity counts as 449, still less; 300 counts as 539.
		Assert.False(CampaignRecord.RecordVictory(entries, "2 Rise of Rome", 250, 6, "Deity", "Gauls", "", Day));
		Assert.True(CampaignRecord.RecordVictory(entries, "2 Rise of Rome", 300, 6, "Deity", "Gauls", "", Day));
		Assert.Equal(539, entries[1].AdjustedScore);

		// An equal score replaces the one there, as in Civ3.
		Assert.True(CampaignRecord.RecordVictory(entries, "2 Rise of Rome", 539, 2, "Regent", "Iberians", "", Day));
		Assert.Equal("Iberians", entries[1].Civilization);
	}

	[Fact]
	public void AGameThatIsNotACampaignIsNotRecorded() {
		List<CampaignRecordEntry> entries = [new() { Campaign = "1 Mesopotamia" }];
		Assert.False(CampaignRecord.RecordVictory(entries, "Quick Civ", 900, 3, "Monarch", "Romans", "", Day));
		Assert.False(entries[0].Victorious);
	}

	[Fact]
	public void TheNextCampaignIsTheFirstUnwonThatCanBePlayed() {
		List<CampaignRecordEntry> entries = [
			new() { Campaign = "1 Mesopotamia", Score = 400, DifficultyLevel = 2 },
			new() { Campaign = "2 Rise of Rome" },
			new() { Campaign = "3 Fall of Rome" },
		];
		Assert.Equal("2 Rise of Rome", CampaignRecord.NextCampaign(entries, _ => true).Campaign);
		Assert.Equal("3 Fall of Rome", CampaignRecord.NextCampaign(entries, c => c != "2 Rise of Rome").Campaign);
		Assert.Null(CampaignRecord.NextCampaign(entries, _ => false));
		Assert.False(CampaignRecord.AllVictorious(entries));
		Assert.True(CampaignRecord.AllVictorious(entries.Take(1)));
	}

	[Fact]
	public void TheRecordIsSavedAndLoaded() {
		Assert.Null(CampaignRecord.Load(FilePath));
		List<CampaignRecordEntry> entries = [
			new() { Campaign = "1 Mesopotamia", Score = 400, DifficultyLevel = 3, Difficulty = "Monarch", Civilization = "Sumerians", WonAt = Day },
			new() { Campaign = "2 Rise of Rome" },
		];
		Assert.True(CampaignRecord.Save(FilePath, entries));
		Assert.Equal(entries, CampaignRecord.Load(FilePath));
	}

	[Fact]
	public void AnUnreadableFileLoadsAsNone() {
		Directory.CreateDirectory(directory);
		File.WriteAllText(FilePath, "not json");
		Assert.Null(CampaignRecord.Load(FilePath));
	}

	[Fact]
	public void Civ3sRecordIsRead() {
		byte[] hof = Civ3Record([("1 Mesopotamia", -1, 0, "", ""), ("2 Rise of Rome", 812, 4, "Romans", "Emperor")], padding: 100);
		List<CampaignRecordEntry> entries = CampaignRecord.ReadCiv3(hof);
		Assert.Equal(2, entries.Count);
		Assert.Equal("1 Mesopotamia", entries[0].Campaign);
		Assert.False(entries[0].Victorious);
		Assert.Equal("2 Rise of Rome", entries[1].Campaign);
		Assert.Equal(812, entries[1].Score);
		Assert.Equal(4, entries[1].DifficultyLevel);
		Assert.Equal("Romans", entries[1].Civilization);
		Assert.Equal("Emperor", entries[1].Difficulty);
		Assert.Equal(1136, entries[1].AdjustedScore);
	}

	[Fact]
	public void ABrokenCiv3RecordIsNotRead() {
		byte[] hof = Civ3Record([("1 Mesopotamia", -1, 0, "", "")], padding: 0);
		Assert.Throws<InvalidDataException>(() => CampaignRecord.ReadCiv3(hof.Take(hof.Length - 10).ToArray()));
		Directory.CreateDirectory(directory);
		string path = Path.Combine(directory, CampaignRecord.Civ3FileName);
		File.WriteAllBytes(path, Encoding.ASCII.GetBytes("nonsense"));
		Assert.Null(CampaignRecord.ReadCiv3(path));
	}

	// The record in the Civ3 install, if there is one, reads as Civ3 made it:
	// the nine campaigns.
	[Fact]
	public void TheInstalledCiv3RecordIsRead() {
		string path = Path.Combine(@"D:\Steam\steamapps\common\Sid Meier's Civilization III Complete\Conquests", CampaignRecord.Civ3FileName);
		if (!File.Exists(path)) {
			return;
		}
		List<CampaignRecordEntry> entries = CampaignRecord.ReadCiv3(path);
		Assert.NotNull(entries);
		Assert.Equal(9, entries.Count);
		Assert.Equal("1 Mesopotamia", entries[0].Campaign);
		Assert.Equal("7 Sengoku - Sword of the Shogun", entries[6].Campaign);
		Assert.Equal("9 WWII in the Pacific", entries[8].Campaign);
	}

	[Fact]
	public void ANewRecordStartsFromCiv3sAndAddsMissingCampaigns() {
		Directory.CreateDirectory(directory);
		string civ3Path = Path.Combine(directory, CampaignRecord.Civ3FileName);
		File.WriteAllBytes(civ3Path, Civ3Record([("1 Mesopotamia", 300, 2, "Sumerians", "Regent")], padding: 0));

		List<CampaignRecordEntry> entries = CampaignRecord.LoadOrCreate(FilePath, civ3Path,
			["1 Mesopotamia", "2 Rise of Rome"], Names);
		Assert.Equal(["1 Mesopotamia", "2 Rise of Rome"], entries.Select(e => e.Campaign));
		Assert.True(entries[0].Victorious);
		Assert.Equal(entries, CampaignRecord.Load(FilePath));

		// Once there's a record, Civ3's isn't read again.
		CampaignRecord.RecordVictory(entries, "2 Rise of Rome", 100, 2, "Regent", "Romans", "", Day);
		CampaignRecord.Save(FilePath, entries);
		File.Delete(civ3Path);
		Assert.True(CampaignRecord.LoadOrCreate(FilePath, civ3Path, [], Names)[1].Victorious);
	}

	[Fact]
	public void WithoutCampaignsTheRecordIsEmptyAndNotWritten() {
		Assert.Empty(CampaignRecord.LoadOrCreate(FilePath, null, [], []));
		Assert.False(File.Exists(FilePath));
	}

	// A CampaignRecord.hof laid out as Civ3 writes it.
	private static byte[] Civ3Record((string name, int score, int level, string civ, string difficulty)[] entries, int padding) {
		using MemoryStream stream = new();
		using BinaryWriter writer = new(stream);
		void Chunk(string tag, byte[] data) {
			writer.Write(Encoding.ASCII.GetBytes(tag));
			writer.Write(data.Length);
			writer.Write(data);
		}
		byte[] Str(string s, int length) {
			byte[] bytes = new byte[length];
			Encoding.ASCII.GetBytes(s).CopyTo(bytes, 0);
			return bytes;
		}
		Chunk("CMRC", BitConverter.GetBytes(0));
		Chunk("CMRC", BitConverter.GetBytes(entries.Length));
		foreach (var e in entries) {
			Chunk("GMRC", BitConverter.GetBytes(1));
			byte[] fields = new byte[288];
			Str(e.name, 260).CopyTo(fields, 0);
			int[] ints = [1, e.score, 0, 3, e.level, 0, 3];
			for (int i = 0; i < ints.Length; i++) {
				BitConverter.GetBytes(ints[i]).CopyTo(fields, 260 + i * 4);
			}
			Chunk("GMRC", fields);
			Chunk("GMRC", [.. Str(e.civ, 260), .. Str(e.difficulty, 260)]);
		}
		writer.Write(new byte[padding]);
		return stream.ToArray();
	}
}
