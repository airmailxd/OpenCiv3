using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using C7GameData;
using Serilog;

namespace C7Engine.AI.Advanced {
	// What the Advanced AI's policy reviews have learned, kept from game to
	// game: each review's situation, the changes it made, how the civ fared
	// over the turns after, and the lesson the model drew. Every review
	// reads the latest entries, so later games build on what worked before
	// (like the notes files RuneBench's agents kept, and Vox Deorum's
	// archive of past episodes and their outcomes).
	//
	// One JSON object per line in a file on the machine that runs the AI
	// (the host, in a network game). With no path set, nothing is kept.
	public static class PolicyPlaybook {
		private static readonly ILogger log = Log.ForContext(typeof(PolicyPlaybook));

		public static string path;

		// How many entries a review reads.
		public const int EntriesRead = 20;

		public sealed class Entry {
			public string civ { get; set; }
			public string era { get; set; }
			public int fromTurn { get; set; }
			public int toTurn { get; set; }
			public string situation { get; set; }
			public Dictionary<string, double> changes { get; set; } = new();
			public string outcome { get; set; }
			// Better (positive) or worse (negative) standing after: places
			// gained in score rank, plus the change in share of cities.
			public double gain { get; set; }
			public string lesson { get; set; }
		}

		public static void Add(Entry entry) {
			if (string.IsNullOrWhiteSpace(path)) {
				return;
			}
			try {
				Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
				lock (typeof(PolicyPlaybook)) {
					File.AppendAllText(path, JsonSerializer.Serialize(entry) + "\n");
				}
			} catch (Exception e) {
				log.Warning(e, "Could not write the Advanced AI playbook at {Path}", path);
			}
		}

		// The latest entries, those from the same era first.
		public static List<Entry> Read(string era) {
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
				return [];
			}
			try {
				List<Entry> all;
				lock (typeof(PolicyPlaybook)) {
					all = File.ReadAllLines(path).Where(l => l.Trim().Length > 0)
						.Select(l => JsonSerializer.Deserialize<Entry>(l)).Where(e => e != null).ToList();
				}
				return all.AsEnumerable().Reverse().OrderByDescending(e => e.era == era).Take(EntriesRead).ToList();
			} catch (Exception e) {
				log.Warning(e, "Could not read the Advanced AI playbook at {Path}", path);
				return [];
			}
		}
	}
}
