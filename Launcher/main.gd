extends Control
## The launcher's window: shows the installed and latest versions, installs
## or updates the game, and starts it.
##
## A launcher that downloads only what changed is how players install and
## update the game (owner's decision, 2026-10-12). It never stands in the
## way of playing: with no internet, or GitHub refusing to answer, Play
## still works if the game is installed.

const Installer := preload("res://installer.gd")
const SETTINGS_FILE := "user://launcher.cfg"

var installer: Installer
var game_dir := ""
## The latest release, from Installer.fetch_latest(), once known.
var latest := {}
## What the game folder needs from it, from Installer.plan().
var needed := []
var busy := false

var installed_label: Label
var latest_label: Label
var folder_label: Label
var status_label: Label
var progress_bar: ProgressBar
var progress_label: Label
var update_button: Button
var play_button: Button
var folder_button: Button
var folder_dialog: FileDialog


func _ready() -> void:
	installer = Installer.new()
	add_child(installer)
	installer.progress.connect(_on_progress)
	var release_url := _argument("--release-url")
	if release_url != "":
		installer.release_url = release_url
	_build()
	game_dir = _argument("--game-dir")
	if game_dir == "":
		game_dir = _saved_game_dir()
	if game_dir == "":
		game_dir = _default_game_dir()
	_check()


## Reads --name=value from the command line (for testing against another
## release or folder).
static func _argument(name: String) -> String:
	for arg in OS.get_cmdline_args() + OS.get_cmdline_user_args():
		if arg.begins_with(name + "="):
			return arg.substr(name.length() + 1)
	return ""


func _saved_game_dir() -> String:
	var settings := ConfigFile.new()
	if settings.load(SETTINGS_FILE) != OK:
		return ""
	return str(settings.get_value("launcher", "game_dir", ""))


func _save_game_dir() -> void:
	var settings := ConfigFile.new()
	settings.load(SETTINGS_FILE)
	settings.set_value("launcher", "game_dir", game_dir)
	settings.save(SETTINGS_FILE)


## The folder the launcher sits in (on a Mac, the one holding the launcher
## app). A Mac may run a freshly downloaded app from a hidden, read-only copy
## ("App Translocation"); then the game goes in the home folder instead.
static func _default_game_dir() -> String:
	var exe := OS.get_executable_path()
	var dir := exe.get_base_dir()
	var app := exe.find(".app/Contents/MacOS")
	if app >= 0:
		dir = exe.substr(0, app + 4).get_base_dir()
	if dir.contains("/AppTranslocation/"):
		dir = OS.get_environment("HOME").path_join("OpenCiv3")
	return dir


func _build() -> void:
	var background := ColorRect.new()
	background.color = Color("1b1f24")
	background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(background)

	var margin := MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for side in ["left", "right", "top", "bottom"]:
		margin.add_theme_constant_override("margin_" + side, 24)
	add_child(margin)
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 10)
	margin.add_child(column)

	var title := Label.new()
	title.text = "OpenCiv3"
	title.add_theme_font_size_override("font_size", 30)
	column.add_child(title)

	var grid := GridContainer.new()
	grid.columns = 2
	grid.add_theme_constant_override("h_separation", 16)
	column.add_child(grid)
	installed_label = _row(grid, "Installed version")
	latest_label = _row(grid, "Latest version")

	var folder_row := HBoxContainer.new()
	column.add_child(folder_row)
	folder_label = Label.new()
	folder_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	folder_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	folder_label.modulate = Color(1, 1, 1, 0.7)
	# Long paths are cut short; hovering shows all of it.
	folder_label.mouse_filter = Control.MOUSE_FILTER_PASS
	folder_row.add_child(folder_label)
	folder_button = Button.new()
	folder_button.text = "Choose folder..."
	folder_button.pressed.connect(_on_choose_folder)
	folder_row.add_child(folder_button)

	status_label = Label.new()
	status_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	status_label.size_flags_vertical = Control.SIZE_EXPAND_FILL
	column.add_child(status_label)

	progress_bar = ProgressBar.new()
	progress_bar.show_percentage = false
	progress_bar.custom_minimum_size.y = 18
	column.add_child(progress_bar)
	progress_label = Label.new()
	progress_label.modulate = Color(1, 1, 1, 0.7)
	column.add_child(progress_label)
	_on_progress("", 0, 0)

	var buttons := HBoxContainer.new()
	buttons.alignment = BoxContainer.ALIGNMENT_END
	buttons.add_theme_constant_override("separation", 12)
	column.add_child(buttons)
	update_button = Button.new()
	update_button.custom_minimum_size = Vector2(170, 40)
	update_button.pressed.connect(_on_update)
	buttons.add_child(update_button)
	play_button = Button.new()
	play_button.text = "Play"
	play_button.custom_minimum_size = Vector2(130, 40)
	play_button.pressed.connect(_on_play)
	buttons.add_child(play_button)

	folder_dialog = FileDialog.new()
	folder_dialog.file_mode = FileDialog.FILE_MODE_OPEN_DIR
	folder_dialog.access = FileDialog.ACCESS_FILESYSTEM
	folder_dialog.use_native_dialog = true
	folder_dialog.title = "Choose the folder for the game"
	folder_dialog.dir_selected.connect(_on_folder_selected)
	add_child(folder_dialog)


static func _row(grid: GridContainer, caption: String) -> Label:
	var name_label := Label.new()
	name_label.text = caption + ":"
	name_label.modulate = Color(1, 1, 1, 0.7)
	grid.add_child(name_label)
	var value := Label.new()
	grid.add_child(value)
	return value


## Shows what's installed, then asks GitHub what's new.
func _check() -> void:
	_set_busy(true)
	_show_installed()
	status_label.text = "Checking for a new version..."
	if latest.is_empty() or latest.has("error"):
		latest = await installer.fetch_latest()
	latest_label.text = str(latest.get("version", "")) if latest.get("version", "") != "" else "unknown"
	needed = []
	if latest.has("error"):
		status_label.text = latest.error
		if Installer.game_installed(game_dir):
			status_label.text += " You can still play the version you have."
	else:
		needed = await installer.plan(game_dir, latest.manifest)
		_show_installed()
		status_label.text = _plan_text()
	_on_progress("", 0, 0)
	_set_busy(false)


func _show_installed() -> void:
	folder_label.text = "Game folder: " + game_dir
	folder_label.tooltip_text = game_dir
	var record := Installer.load_record(game_dir)
	if record.has("version") and Installer.game_installed(game_dir):
		installed_label.text = str(record.version)
	elif Installer.game_installed(game_dir):
		installed_label.text = "unknown (installed without the launcher)"
	else:
		installed_label.text = "not installed"


func _plan_text() -> String:
	if needed.is_empty():
		return "You have the latest version."
	var size := 0
	for local in needed:
		size += int(latest.manifest.packages[Installer.package_names()[local]].size)
	var what := "the game and its art" if needed.size() == 2 else ("the game" if needed[0] == "game" else "the game's art")
	var verb := "install" if not Installer.game_installed(game_dir) else "update"
	return "Ready to %s: %s, %s to download." % [verb, what, _megabytes(size)]


func _set_busy(value: bool) -> void:
	busy = value
	var installed := Installer.game_installed(game_dir)
	play_button.disabled = busy or not installed
	folder_button.disabled = busy
	if latest.has("error"):
		update_button.text = "Check again"
		update_button.disabled = busy
	elif needed.is_empty():
		update_button.text = "Up to date"
		update_button.disabled = true
	else:
		update_button.text = "Update" if installed else "Install"
		update_button.disabled = busy


func _on_update() -> void:
	if latest.has("error"):
		_check()
		return
	_set_busy(true)
	# The game first, then the art, so a first install that's cut short
	# leaves the smaller part done.
	for local in ["game", "art"]:
		if local not in needed:
			continue
		var error: String = await installer.install(game_dir, local, latest)
		if error != "":
			status_label.text = error
			_on_progress("", 0, 0)
			_show_installed()
			_set_busy(false)
			return
		needed.erase(local)
	_show_installed()
	status_label.text = "Version %s is installed. Have fun!" % latest.version
	_on_progress("", 0, 0)
	_set_busy(false)


func _on_play() -> void:
	var game := Installer.game_executable(game_dir)
	var pid: int
	if Installer.platform() == "mac":
		pid = OS.create_process("/usr/bin/open", PackedStringArray([game]))
	else:
		pid = OS.create_process(game, PackedStringArray())
	if pid <= 0:
		status_label.text = "Couldn't start the game. Try updating it, or choose its folder again."
		return
	get_tree().quit()


func _on_choose_folder() -> void:
	folder_dialog.current_dir = game_dir
	folder_dialog.popup_centered_ratio(0.8)


func _on_folder_selected(dir: String) -> void:
	game_dir = dir
	_save_game_dir()
	_check()


func _on_progress(message: String, done: int, total: int) -> void:
	if message != "":
		status_label.text = message
	progress_bar.visible = total > 0
	progress_bar.max_value = max(total, 1)
	progress_bar.value = done
	if total <= 0:
		progress_label.text = ""
	elif total > 1 << 20:
		progress_label.text = "%s of %s" % [_megabytes(done), _megabytes(total)]
	else:
		progress_label.text = "%d of %d" % [done, total]


static func _megabytes(bytes: int) -> String:
	return "%.1f MB" % (bytes / 1048576.0)
