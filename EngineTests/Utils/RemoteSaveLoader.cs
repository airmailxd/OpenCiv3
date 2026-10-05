using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using C7Engine;
using C7GameData.Save;
using QueryCiv3;
using Xunit;

namespace EngineTests.Utils;

// A test file kept online, pinned to the exact bytes the tests expect.
public sealed record RemoteFile(string Name, string Uri, string Sha256, long Size);

// The online test saves.
public static class RemoteSaves {
	public static readonly RemoteFile Conquests16PlayersSav = new("Conquests 16 Players.SAV",
		"https://www.dropbox.com/scl/fi/gmxbx1mtrammzfc6vly1g/Conquests-16-Players.SAV?rlkey=2z1es5aetqva4ymv59qduq1at&st=d0udmb3w&dl=1",
		"4ed54e54c0f6e5c00098e88dcd0b248024999d739dd41d9d0bd9cae2c8cc747d", 104411);
	public static readonly RemoteFile Conquests16PlayersJson = new("Conquests 16 Players.json",
		"https://www.dropbox.com/scl/fi/g1qxuvc6xptg1l6hx9s21/Conquests-16-Players.json?rlkey=bkq158od7469pibhtw44g04if&st=tqax1064&dl=1",
		"1e1fa4fa8e73643f778989b775264f8cc9e465c300ef24f6cf6604b5d1e37d6f", 4688368);
	public static readonly RemoteFile MultiTurnDealA = new("MultiTurnDeal_Save_A.SAV",
		"https://www.dropbox.com/scl/fi/pb3k02ufgi0q7okwwykvs/MultiTurnDeal_Save_A.SAV?rlkey=y44s3c8czlp01evm3h35sgm1u&st=e6o8e2h0&dl=1",
		"650bdc7cfa6fc89bda66f300eb7d5baab4fb6998f479ff98a765a6bf00a85e08", 150220);
	public static readonly RemoteFile MultiTurnDealB = new("MultiTurnDeal_Save_B.SAV",
		"https://www.dropbox.com/scl/fi/aqyjc5ld5qyg5qozq99un/MultiTurnDeal_Save_B.SAV?rlkey=f8y4rf6ufhws5xr65wkohec04&st=mmhybnr3&dl=1",
		"aa7cb2b4c177eb98e9543f7cc1a3857050db4249dd3b1e3eeac8623a5ac146c5", 181467);
	public static readonly RemoteFile MultiTurnDealC = new("MultiTurnDeal_Save_C.SAV",
		"https://www.dropbox.com/scl/fi/chv75f5ezrxzhvclne2sk/MultiTurnDeal_Save_C.SAV?rlkey=dwa8wgzcx03pgjoysqnrauvfc&st=g40d2zta&dl=1",
		"2cb099289872084ae93f5b88d1f2bd8e0669c0e355a13da0eb61f3ba9bc3d0b8", 182123);
	public static readonly RemoteFile MultiTurnDealD = new("MultiTurnDeal_Save_D.SAV",
		"https://www.dropbox.com/scl/fi/1zpkjmvgobctndfwdml5z/MultiTurnDeal_Save_D.SAV?rlkey=d2v15p7a05s0nslnj9nz66wwr&st=iaajcftt&dl=1",
		"ab44a339e637a1df021f27d596b847759859444cb8742dc86ab8a315fdd0020f", 181892);
	public static readonly RemoteFile MultiTurnDealE = new("MultiTurnDeal_Save_E.SAV",
		"https://www.dropbox.com/scl/fi/8uutphldi1wzn59qd8h29/MultiTurnDeal_Save_E.SAV?rlkey=q0tay21soe6g0aefqpmshbeq7&st=y3anm45t&dl=1",
		"dfdc28a59e5803af5977c70c9337bbf52e714019da17725db3f40143bd07f4e3", 181026);
	public static readonly RemoteFile MultiTurnDealF = new("MultiTurnDeal_Save_F.SAV",
		"https://www.dropbox.com/scl/fi/l8bm8dhacd85cwyxn3nn7/MultiTurnDeal_Save_F.SAV?rlkey=mr7itejsucesk14h859jwjffu&st=fuo8n4d9&dl=1",
		"6e4df14067a10b6fadc225db55c29bc5333442f6b6d792abdbc1781c925fcaa2", 189045);
	public static readonly RemoteFile MiddleAgesAbbasids = new("Middle Ages Scenario Abbasids, 843 AD.SAV",
		"https://www.dropbox.com/scl/fi/nz7wp7whu326i7em8jle9/Middle-Ages-Scenario-Abbasids-843-AD.SAV?rlkey=oz65m286jbchytd3yuu5ksr6f&st=b3dsheus&dl=1",
		"4ad9dc387a74e15721412ea2b7268356e5623d05b7ab3fc4f2f015bf760713a6", 144006);
	public static readonly RemoteFile SampleGotmSave = new("12345.SAV",
		"https://drive.usercontent.google.com/download?id=1QlIavkLtPZEIv1kHK9sO0fY2yp3o2si7&confirm=y",
		"c6cc885728010fa3c7fb22746a587474011422a2bcf7d5e4deb5e250bb3ce500", 42932);
}

// Downloads remote test files once into a cache shared by every test run on
// this machine, keyed by their content hash.
//
// A file is downloaded to a uniquely named temporary file, checked against
// its pinned hash, and only then renamed into place, so concurrent test runs
// never see a partly written file and never write to the same path. A cached
// file is used without touching the network. A test whose file can't be
// downloaded (offline, rate limited, server errors) is skipped, not failed.
public static class RemoteFileCache {
	private static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(100) };

	// Set C7_TEST_CACHE_DIR to keep the cache somewhere else.
	public static string CacheDirectory {
		get {
			string configured = Environment.GetEnvironmentVariable("C7_TEST_CACHE_DIR");
			return string.IsNullOrWhiteSpace(configured)
				? Path.Combine(Path.GetTempPath(), "c7-engine-tests", "remote-files")
				: configured;
		}
	}

	// The local path of the file, downloading it if it isn't cached yet.
	// Throws SkipException if it can't be downloaded.
	public static async Task<string> GetAsync(RemoteFile file) {
		if (file.Uri.Contains("dropbox") && file.Uri.EndsWith("dl=0")) {
			throw new ArgumentException($"Change the dl=0 to dl=1 at the end of the url of {file.Name} to be able to download the file, " +
										"otherwise we get Dropbox's web page instead of the file");
		}

		// The file keeps its name, whose extension says what kind of save it is.
		string directory = Path.Combine(CacheDirectory, file.Sha256);
		string path = Path.Combine(directory, file.Name);
		if (IsIntact(path, file)) {
			return path;
		}

		byte[] data;
		try {
			data = await http.GetByteArrayAsync(file.Uri);
		} catch (Exception e) when (e is HttpRequestException or TaskCanceledException) {
			throw new SkipException($"Couldn't download {file.Name}: {e.Message}");
		}

		string hash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
		if (hash != file.Sha256 || data.Length != file.Size) {
			if (LooksLikeAWebPage(data)) {
				// A rate limit or interstitial page served in place of the file.
				throw new SkipException($"Couldn't download {file.Name}: the server sent a web page instead");
			}
			Assert.Fail($"{file.Name} downloaded from {file.Uri} isn't the file the tests expect: " +
						$"{data.Length} bytes with SHA-256 {hash}, expected {file.Size} bytes with SHA-256 {file.Sha256}");
		}

		Directory.CreateDirectory(directory);
		string temporary = Path.Combine(directory, $".{Guid.NewGuid():N}.download");
		try {
			await File.WriteAllBytesAsync(temporary, data);
			try {
				File.Move(temporary, path, overwrite: true);
			} catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
				// Another test run put it in place first, and may be reading it.
				if (!IsIntact(path, file)) {
					throw;
				}
			}
		} finally {
			TryDelete(temporary);
		}
		return path;
	}

	private static bool IsIntact(string path, RemoteFile file) {
		try {
			using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			if (stream.Length != file.Size) {
				return false;
			}
			return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant() == file.Sha256;
		} catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException or IOException or UnauthorizedAccessException) {
			return false;
		}
	}

	private static bool LooksLikeAWebPage(byte[] data) {
		string start = Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, 512)).TrimStart();
		return start.StartsWith("<", StringComparison.Ordinal);
	}

	private static void TryDelete(string path) {
		try {
			File.Delete(path);
		} catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
		}
	}
}

public class RemoteSaveLoader {
	private static string defaultBicPath => Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "conquests.biq");
	private static string defaultPediaIconsPath => Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "Text", "PediaIcons.txt");

	// Downloads (or finds in the cache) a remote save and loads it. Loading
	// errors are returned rather than thrown, for the test to assert on;
	// download failures skip the test.
	protected static async Task<(SaveGame game, Exception ex, string savePath)> LoadGameAndData(RemoteFile file, string biqPath = "default", string pediaPath = "default") {
		string savePath = await RemoteFileCache.GetAsync(file);

		SaveGame game = null;
		Exception ex = Record.Exception(() => {
			game = SaveManager.LoadSave(savePath, biqPath == "default" ? defaultBicPath : biqPath,
				(relativeModePath) => { return pediaPath == "default" ? defaultPediaIconsPath : pediaPath; });
		});

		return (game, ex, savePath);
	}
}
