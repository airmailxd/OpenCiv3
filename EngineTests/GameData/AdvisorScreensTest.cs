using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using C7Engine;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// The advisors menu (the advisors button in the upper left) lists every
// advisor and report screen with its key, so they can all be reached with
// the mouse as well (on a Mac the function keys belong to the system unless
// Fn is held).
public class AdvisorScreensTest {
	// Godot's keycode for F1; F2 to F11 follow it.
	private const long KeyF1 = 4194332;

	[Fact]
	public void TheMenuListsEveryAdvisorAndReportScreenOnce() {
		string[] actions = typeof(C7Action).GetFields()
			.Where(f => f.IsLiteral && f.Name.StartsWith("Show") && (f.Name.EndsWith("Advisor") || f.Name.EndsWith("View")))
			.Select(f => (string)f.GetRawConstantValue())
			.ToArray();
		Assert.Equal(11, actions.Length);
		Assert.Equal(actions.OrderBy(a => a), C7Action.AdvisorScreens.Select(s => s.action).OrderBy(a => a));
		Assert.Equal(6, C7Action.AdvisorScreens.Count(s => s.isAdvisor));
		Assert.All(C7Action.AdvisorScreens.Take(6), s => Assert.True(s.isAdvisor));
	}

	[Fact]
	public void TheMenuShowsTheKeyEachScreenIsBoundTo() {
		string project = File.ReadAllText(PathUtils.getBasePath("../C7/project.godot"));
		for (int i = 0; i < C7Action.AdvisorScreens.Length; i++) {
			string action = C7Action.AdvisorScreens[i].action;
			Match binding = Regex.Match(project, "^" + action + @"=\{[^}]*?""physical_keycode"":(\d+)", RegexOptions.Multiline);
			Assert.True(binding.Success, $"{action} has no key in project.godot");
			Assert.Equal(KeyF1 + i, long.Parse(binding.Groups[1].Value));
			Assert.EndsWith($"(F{i + 1})", C7Action.AdvisorScreenLabel(i));
		}
		Assert.Equal("Trade Advisor (F2)", C7Action.AdvisorScreenLabel(1));
	}
}
