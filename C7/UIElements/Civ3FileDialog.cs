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

	// Where the game saves, and looks for saves to load.
	public static string SavesDirectory => System.IO.Path.Combine(C7Settings.WritableDirectory, "Saves");

	public override void _Ready() {
		base._Ready();
		log = LogManager.ForContext<Civ3FileDialog>();

		FileMode = FileDialog.FileModeEnum.OpenFile;
		Access = AccessEnum.Filesystem;

		Global = GetNode<GlobalSingleton>("/root/GlobalSingleton");
		FileSelected += OnFileSelected;
	}

	private static void EnsureSavesDirectory() {
		try {
			System.IO.Directory.CreateDirectory(SavesDirectory);
		} catch (System.Exception e) {
			LogManager.ForContext<Civ3FileDialog>().Warning(e, "Couldn't create the saves directory {Directory}", SavesDirectory);
		}
	}

	// Opens on our saves. Only the game's own saves, so a recorded game's
	// play log (.jsonl) beside them isn't offered.
	public void SetDirectoryForLoadingSaves() {
		EnsureSavesDirectory();
		CurrentDir = SavesDirectory;
		FileMode = FileDialog.FileModeEnum.OpenFile;
		Filters = new[] { "*.json ; Saved Games" };
	}

	// Opens on the saves folder, or on a recorded game's own folder (see
	// C7Engine.PlayLog.PlayLogFolders) with the save's name filled in.
	public void SetDirectoryForSaving(string folder = null, string fileName = null) {
		EnsureSavesDirectory();
		CurrentDir = SavesDirectory;
		if (!string.IsNullOrEmpty(folder)) {
			try {
				System.IO.Directory.CreateDirectory(folder);
				CurrentDir = folder;
			} catch (System.Exception e) {
				log.Warning(e, "Couldn't make the game's folder {Directory}", folder);
			}
		}
		FileMode = FileDialog.FileModeEnum.SaveFile;
		Filters = new[] { "*.json ; Saved Games" };
		if (!string.IsNullOrEmpty(fileName)) {
			CurrentFile = fileName + ".json";
		}
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
			// The game is taken now, and written on a worker thread.
			Node parent = GetParent() ?? this;
			try {
				ReportSaveFailure(SaveManager.SaveInBackground(EngineStorage.gameData, path), path, parent);
			} catch (System.Exception e) {
				ShowSaveFailure(e, path, parent);
			}
		}
	}

	private async void ReportSaveFailure(System.Threading.Tasks.Task saving, string path, Node parent) {
		try {
			await saving;
		} catch (System.Exception e) {
			ShowSaveFailure(e, path, parent);
		}
	}

	private void ShowSaveFailure(System.Exception e, string path, Node parent) {
		log.Error(e, "Couldn't save the game to {Path}", path);
		if (IsInstanceValid(parent) && parent.IsInsideTree()) {
			Util.ShowErrorDialog(parent, "Couldn't save the game", $"The game couldn't be saved to\n{path}\n\n{e.Message}");
		}
	}
}
