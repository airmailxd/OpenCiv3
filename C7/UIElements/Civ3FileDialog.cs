using C7Engine;
using C7GameData;
using Godot;
using Serilog;

[GlobalClass]
public partial class Civ3FileDialog : FileDialog {
	// An object for passing information (like save file paths) between scenes.
	GlobalSingleton Global;
	private ILogger log;

	// If true, go to scenario setup after loading, for scenarios.
	public bool GoToScenarioSetupAfterLoading = false;

	// Where the game saves, and looks first for saves to load. The Civ 3
	// folder is often read-only, or missing in standalone mode.
	public static string SavesDirectory => System.IO.Path.Combine(C7Settings.WritableDirectory, "Saves");

	private string Civ3SavesDirectory => Util.Civ3Root + "/Conquests/Saves";

	private const string ShowSavesAction = "show_saves";
	private const string ShowCiv3SavesAction = "show_civ3_saves";

	// The buttons that switch between our saves and Civ 3's, when loading.
	private Button savesButton, civ3SavesButton;

	public override void _Ready() {
		base._Ready();
		log = LogManager.ForContext<Civ3FileDialog>();

		FileMode = FileDialog.FileModeEnum.OpenFile;
		Access = AccessEnum.Filesystem;

		Global = GetNode<GlobalSingleton>("/root/GlobalSingleton");
		FileSelected += OnFileSelected;

		savesButton = AddButton("Our Saves", false, ShowSavesAction);
		civ3SavesButton = AddButton("Civ3 Saves", false, ShowCiv3SavesAction);
		savesButton.Visible = civ3SavesButton.Visible = false;
		CustomAction += OnCustomAction;
	}

	private void OnCustomAction(StringName action) {
		if (action == ShowSavesAction) {
			CurrentDir = SavesDirectory;
		} else if (action == ShowCiv3SavesAction) {
			CurrentDir = Civ3SavesDirectory;
		}
	}

	private static void EnsureSavesDirectory() {
		try {
			System.IO.Directory.CreateDirectory(SavesDirectory);
		} catch (System.Exception e) {
			LogManager.ForContext<Civ3FileDialog>().Warning(e, "Couldn't create the saves directory {Directory}", SavesDirectory);
		}
	}

	// Opens on our saves, with a button to browse Civ 3's for its .SAV files.
	public void SetDirectoryForLoadingSaves() {
		EnsureSavesDirectory();
		CurrentDir = SavesDirectory;
		FileMode = FileDialog.FileModeEnum.OpenFile;
		ShowSavesButtons(true);
	}

	public void SetDirectoryForLoading(string RelPath) {
		CurrentDir = Util.Civ3Root + "/" + RelPath;
		FileMode = FileDialog.FileModeEnum.OpenFile;
		ShowSavesButtons(false);
	}

	public void SetDirectoryForSaving() {
		EnsureSavesDirectory();
		CurrentDir = SavesDirectory;
		FileMode = FileDialog.FileModeEnum.SaveFile;
		ShowSavesButtons(false);
	}

	private void ShowSavesButtons(bool show) {
		if (savesButton == null) {
			return;
		}
		savesButton.Visible = show;
		civ3SavesButton.Visible = show && System.IO.Directory.Exists(Civ3SavesDirectory);
	}

	private void OnFileSelected(string path) {
		if (FileMode == FileDialog.FileModeEnum.OpenFile) {
			log.Information($"loading {path}");
			Global.LoadGamePath = path;
			if (GoToScenarioSetupAfterLoading) {
				GetTree().ChangeSceneToFile("res://UIElements/NewGame/scenario_setup.tscn");
			} else {
				LanSession.StartGame(GetTree());
			}
		} else {
			if (!path.EndsWith(".json")) {
				path = path + ".json";
			}

			log.Information($"Saving game to {path}");
			try {
				EngineStorage.ReadGameData((GameData gameData) => {
					C7GameData.Save.SaveGame.FromGameData(gameData).Save(path);
				});
			} catch (System.Exception e) {
				log.Error(e, "Couldn't save the game to {Path}", path);
				Util.ShowErrorDialog(GetParent() ?? this, "Couldn't save the game", $"The game couldn't be saved to\n{path}\n\n{e.Message}");
			}
		}
	}
}
