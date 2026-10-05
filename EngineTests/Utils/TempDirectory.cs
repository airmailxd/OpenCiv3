using System;
using System.IO;

namespace EngineTests.Utils;

// A directory of its own for a test's output files, so that concurrent test
// runs never write to the same file. It's deleted when disposed, unless
// C7_KEEP_TEST_OUTPUT is set (handy for looking at what a test wrote).
public sealed class TempDirectory : IDisposable {
	public string Path { get; }

	public TempDirectory(string purpose = "output") {
		Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "c7-engine-tests", purpose,
			$"{Environment.ProcessId}-{Guid.NewGuid():N}");
		Directory.CreateDirectory(Path);
	}

	public string File(string name) => System.IO.Path.Combine(Path, name);

	public void Dispose() {
		if (Environment.GetEnvironmentVariable("C7_KEEP_TEST_OUTPUT") != null) {
			Console.WriteLine($"Test output kept in {Path}");
			return;
		}
		try {
			Directory.Delete(Path, recursive: true);
		} catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
		}
	}
}
