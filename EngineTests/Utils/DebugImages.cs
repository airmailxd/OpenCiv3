using System;
using System.IO;
using SixLabors.ImageSharp;

namespace EngineTests.Utils;

// Pictures some tests draw to help understand what they generate.
//
// They're written only when C7_DEBUG_IMAGE_DIR names a directory to write
// them to; otherwise they're drawn and encoded but thrown away, so test runs
// don't litter the working directory and concurrent runs don't collide.
public static class DebugImages {
	public static void Save(Image image, string fileName) {
		string directory = Environment.GetEnvironmentVariable("C7_DEBUG_IMAGE_DIR");
		if (string.IsNullOrWhiteSpace(directory)) {
			image.SaveAsPng(Stream.Null);
			return;
		}

		Directory.CreateDirectory(directory);
		string path = Path.Combine(directory, fileName);
		image.SaveAsPng(path);
		Console.WriteLine($"Debug image saved to: {path}");
	}
}
