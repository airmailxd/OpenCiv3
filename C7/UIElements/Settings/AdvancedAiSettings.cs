using C7Engine;
using C7Engine.AI.Advanced;

// Settings for the Advanced AI's model planner (see
// C7Engine.AI.Advanced.ModelPlanner), under Advanced AI on the settings page,
// saved to C7.ini. With no key, Advanced AIs plan by rules alone.
public static class AdvancedAiSettings {
	public const string SettingsSection = "advancedAi";
	public const string ApiKeyKey = "apiKey";
	public const string ModelKey = "model";

	// An Anthropic API key. Kept in C7.ini in plain text, so the
	// ANTHROPIC_API_KEY environment variable is the safer place; it is used
	// when this is empty.
	public static string ApiKey {
		get => C7Settings.GetSettingsValueOrDefault(SettingsSection, ApiKeyKey, "");
		set {
			C7Settings.SetValue(SettingsSection, ApiKeyKey, value?.Trim() ?? "");
			C7Settings.SaveSettings();
		}
	}

	public static string Model {
		get => C7Settings.GetSettingsValueOrDefault(SettingsSection, ModelKey, ModelPlanner.DefaultModel);
		set {
			C7Settings.SetValue(SettingsSection, ModelKey, string.IsNullOrWhiteSpace(value) ? ModelPlanner.DefaultModel : value.Trim());
			C7Settings.SaveSettings();
		}
	}

	// Hands the settings to the engine; only a host's engine (or a single
	// machine's) plays the AI.
	public static void Apply() {
		ModelPlanner.apiKey = !string.IsNullOrWhiteSpace(ApiKey) ? ApiKey : System.Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
		ModelPlanner.model = Model;
		ModelPlanner.waitForAnswers = false;
		// What the reviews learn, kept from game to game beside C7.ini.
		PolicyPlaybook.path = System.IO.Path.Combine(C7Settings.WritableDirectory, "AdvancedAiPlaybook.jsonl");
	}
}
