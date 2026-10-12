using System;
using C7GameData;
using Xunit;

namespace EngineTests.GameData;

public class UnitArtMetadataTest {
	private const string Sample = """
		{
		  "actions": {
		    "DEFAULT": "WorkerDefault",
		    "RUN": "workerRun",
		    "ROAD": "workerRoad",
		    "FORTIFY": ""
		  },
		  "flics": {
		    "WorkerDefault": {"size": [50, 51], "directions": 8, "frames": 15, "frameMs": 125, "offset": [93, 80], "canvas": [240, 240]},
		    "workerRoad": {"size": [64, 54], "directions": 8, "frames": 10, "frameMs": 100, "offset": [88, 78], "canvas": [240, 240]},
		    "workerRun": {"size": [48, 48], "directions": 8, "frames": 10, "frameMs": 66, "offset": [96, 77], "canvas": [240, 240]}
		  }
		}
		""";

	[Fact]
	public void ParsesTheFlicsOfEachAction() {
		UnitArtMetadata metadata = UnitArtMetadata.Parse(Sample);

		Assert.Equal(3, metadata.Actions.Count);
		Assert.Equal("workerRun", metadata.FlicNameOf("RUN"));
		// Actions and flic names are matched ignoring case, like INI keys and files.
		Assert.Equal("workerRoad", metadata.FlicNameOf("road"));
		UnitArtMetadata.Flic run = metadata.GetFlic("WORKERRUN");
		Assert.Equal(48, run.Width);
		Assert.Equal(48, run.Height);
		Assert.Equal(8, run.Directions);
		Assert.Equal(10, run.Frames);
		Assert.Equal(66, run.FrameMs);
		Assert.Equal(96, run.OffsetLeft);
		Assert.Equal(77, run.OffsetTop);
		Assert.Equal(240, run.CanvasWidth);
		Assert.Equal(240, run.CanvasHeight);
		Assert.Equal(660, run.DurationMs);
	}

	[Fact]
	public void AnActionWithoutAFlicOfItsOwnUsesTheDefaultOne() {
		UnitArtMetadata metadata = UnitArtMetadata.Parse(Sample);

		// An empty entry, as in an INI, is no animation.
		Assert.Null(metadata.FlicNameOf("FORTIFY"));
		Assert.Null(metadata.FlicNameOf("DEATH"));
		Assert.Equal("WorkerDefault", metadata.FlicNameOrDefault("DEATH"));
		Assert.Equal("WorkerDefault", metadata.FlicNameOrDefault("FORTIFY"));
		Assert.Equal("workerRoad", metadata.FlicNameOrDefault("ROAD"));
	}

	[Theory]
	[InlineData("""{"actions": {"RUN": "a"}, "flics": {"a": {"size": [1, 1], "directions": 8, "frames": 1, "frameMs": 1, "offset": [0, 0], "canvas": [240, 240]}}}""")]
	[InlineData("""{"actions": {"DEFAULT": "missing"}, "flics": {}}""")]
	[InlineData("""{"actions": {"DEFAULT": "a"}, "flics": {"a": {"size": [1], "directions": 8, "frames": 1, "frameMs": 1, "offset": [0, 0], "canvas": [240, 240]}}}""")]
	[InlineData("""{"actions": {"DEFAULT": "a"}, "flics": {"a": {"size": [1, 1], "directions": 8, "frames": 0, "frameMs": 1, "offset": [0, 0], "canvas": [240, 240]}}}""")]
	[InlineData("""{"actions": {"DEFAULT": "a"}}""")]
	[InlineData("not json")]
	public void RejectsInvalidMetadata(string json) {
		Assert.Throws<FormatException>(() => UnitArtMetadata.Parse(json));
	}

	[Theory]
	// A sheet with a column per frame.
	[InlineData(0, 10, 10, 0)]
	[InlineData(7, 10, 10, 7)]
	// A still: one column for every frame.
	[InlineData(0, 10, 1, 0)]
	[InlineData(9, 10, 1, 0)]
	// A move cycle of every other frame (ArtGen/units/flic.py move_columns):
	// column c is frame c*n/k, and stands for the frames up to the next column's.
	[InlineData(0, 10, 5, 0)]
	[InlineData(1, 10, 5, 0)]
	[InlineData(2, 10, 5, 1)]
	[InlineData(9, 10, 5, 4)]
	[InlineData(0, 15, 8, 0)]
	[InlineData(1, 15, 8, 0)]
	[InlineData(2, 15, 8, 1)]
	[InlineData(3, 15, 8, 1)]
	[InlineData(14, 15, 8, 7)]
	public void SheetColumnOfAFrame(int frame, int frames, int columns, int expected) {
		Assert.Equal(expected, UnitArtMetadata.SheetColumn(frame, frames, columns));
	}

	// Every column of a move cycle is shown from the frame it was rendered
	// from (ArtGen/units/flic.py move_columns: every other frame, i * n / k
	// rounded up), and stands for the frames up to the next column's.
	[Fact]
	public void MoveCycleColumnsStartAtTheirOwnFrame() {
		for (int frames = 1; frames <= 40; frames++) {
			int columns = (frames + 1) / 2;
			for (int column = 0; column < columns; column++) {
				int renderedFrame = (column * frames + columns - 1) / columns;
				Assert.Equal(Math.Min(2 * column, frames - 1), renderedFrame);
				Assert.Equal(column, UnitArtMetadata.SheetColumn(renderedFrame, frames, columns));
				if (renderedFrame > 0) {
					Assert.Equal(column - 1, UnitArtMetadata.SheetColumn(renderedFrame - 1, frames, columns));
				}
			}
		}
	}
}
