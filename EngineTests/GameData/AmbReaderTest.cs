using System;
using System.IO;
using System.Linq;
using ConvertCiv3Media;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.GameData;

public class AmbReaderTest {

	[SkippableFact]
	public void WorkerRunAmbTest() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		string path = Path.Combine(Civ3Location.GetCiv3Path(), "Art", "Units", "Worker", "WorkerRun.amb");

		Sfx sfx1 = new Amb(path).soundEffects.First();
		Sfx sfx2 = new Amb(path).soundEffects.Skip(1).First();

		Assert.Equal("WorkRunFoot1.wav", sfx1.wavName);
		Assert.Equal("WorkRunFoot2.wav", sfx2.wavName);
	}

	// Tracks are matched to prgm chunks by their program number, and prgm chunks to kmap chunks by name, not by position
	[SkippableFact]
	public void PrivateerAttackPlaysTheRightSamples() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		string path = Path.Combine(Civ3Location.GetCiv3Path(), "Art", "Units", "Privateer", "PrivateerAttack.amb");
		Skip.IfNot(File.Exists(path), "No PrivateerAttack.amb");
		Sfx[] sfx = new Amb(path).soundEffects.ToArray();

		Assert.Equal(7, sfx.Length);
		// The "Creak 3" track (program 5) is the last to play, and doesn't vary its speed and volume
		Sfx creak3 = sfx.Last();
		Assert.Equal("PrivateerAttackCreak3.wav", creak3.wavName);
		Assert.False(creak3.speedRandom);
		Assert.False(creak3.volumeRandom);
		Assert.Equal(new[] { "PrivateerAttackCannon1.wav", "PrivateerAttackCannon2.wav" }, sfx.Skip(4).Take(2).Select(s => s.wavName));
		Assert.True(sfx[4].delayStart < sfx[5].delayStart);
	}

	[SkippableFact]
	public void AllCiv3AmbFilesLoad() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		int files = 0;
		foreach (string path in Directory.EnumerateFiles(Civ3Location.GetCiv3Path(), "*.amb", SearchOption.AllDirectories)) {
			Amb amb = new(path);
			Assert.All(amb.soundEffects, s => Assert.EndsWith(".wav", s.wavName, StringComparison.OrdinalIgnoreCase));
			Assert.All(amb.soundEffects, s => Assert.True(s.delayStart >= 0 && s.delayStart < 60, path));
			files++;
		}
		Assert.True(files > 0);
	}

	[SkippableFact]
	public void AmbFilesWithMoreTracksThanSoundsLoad() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		// HopliteAttackA has a track without a prgm chunk, and HeavyCruiserRun a kmap chunk without items
		string units = Path.Combine(Civ3Location.GetCiv3Path(), "Art", "Units");
		string hoplite = Path.Combine(units, "Hoplite", "HopliteAttackA.amb");
		if (File.Exists(hoplite)) {
			Assert.Equal(6, new Amb(hoplite).soundEffects.Count);
		}
		string cruiser = Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "Art", "Units", "Cruiser", "HeavyCruiserRun.amb");
		if (File.Exists(cruiser)) {
			Assert.Equal(new[] { "HeavyCruiserRunEngine.wav", "HeavyCruiserRunSplash.wav" }, new Amb(cruiser).soundEffects.Select(s => s.wavName).OrderBy(s => s));
		}
	}

	private static string TempAmb(byte[] bytes) {
		string path = Path.Combine(Path.GetTempPath(), "c7-fix-parsers-" + Guid.NewGuid() + ".amb");
		File.WriteAllBytes(path, bytes);
		return path;
	}

	[Fact]
	public void MalformedAmbFilesFailCleanly() {
		byte[] prgm = new byte[44];
		"prgm"u8.CopyTo(prgm);
		foreach (int size in new[] { -8, -1, 0, 27, 1000 }) {
			BitConverter.GetBytes(size).CopyTo(prgm, 4);
			string path = TempAmb(prgm);
			try {
				// a size of -8 used to loop forever
				Assert.Throws<InvalidDataException>(() => new AmbData(path));
			} finally {
				File.Delete(path);
			}
		}
		// A few trailing bytes are ignored, rather than read past the end
		string trailing = TempAmb(new byte[] { 1, 2, 3 });
		try {
			Assert.Null(new AmbData(trailing).midiData);
			Assert.Throws<InvalidDataException>(() => new Amb(trailing));
		} finally {
			File.Delete(trailing);
		}
	}
}
