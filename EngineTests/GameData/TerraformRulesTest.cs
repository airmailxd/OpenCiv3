using System.Linq;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class TerraformRulesTest {
	// A terraform asking for a resource the rules haven't got leaves it out.
	// The bundled MDJ rules once asked for Coal for the railroad, and every
	// turn of a Rising Tide game under them failed in the worker AI, which
	// looked the missing resource up as a null (owner's report, 2026-10-28).
	[Fact]
	public void AResourceTheRulesHaventGotIsLeftOut() {
		SaveGame save = SaveGameFixture.LoadGameMode(new C7Engine.Lua.GameMode.Config("civ3")).GetSave();
		SaveTerraform railroad = save.TerraForms.First(t => t.Name == "Railroad");
		railroad.RequiredResources.Add("Unobtainium");

		C7GameData.GameData gameData = SaveGameFixture.HydrateSaveGame(save);

		Terraform loaded = gameData.Terraforms.First(t => t.Name == "Railroad");
		Assert.DoesNotContain(null, loaded.RequiredResources);
		Assert.Equal(railroad.RequiredResources.Count - 1, loaded.RequiredResources.Count);
	}
}
