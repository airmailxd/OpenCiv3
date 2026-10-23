extends Node
## Finds the latest release, works out which of its packages the game folder
## needs, and downloads and unpacks them.
##
## Releases publish the game and its art as separate packages, described by
## manifest.json (owner's decision, 2026-10-12). Each package has a content
## hash that changes only when its files do, and the game folder keeps a
## record (installed.json) of the hashes it has. Only packages whose hash
## differs are downloaded, so most updates skip the art, which is most of the
## download.

## Reports what's happening: a message, and how far along it is (both 0
## when there's nothing to count).
signal progress(message: String, done: int, total: int)

const RELEASE_URL := "https://api.github.com/repos/airmailxd/OpenCiv3/releases/latest"
const RECORD_FILE := "installed.json"
## Downloads land next to the game, so there's room for them where the game
## goes, and are named so no package can claim them.
const DOWNLOAD_PREFIX := ".openciv-download-"
const USER_AGENT := "OpenCiv3-Launcher"
## How long to work between screen updates while hashing or unpacking.
const FRAME_BUDGET_MSEC := 30
## The intro movie isn't part of the releases: it's fetched on its own, from
## the owner's Google Drive, into the game folder's Intro folder, which no
## package owns, so updates leave it be. The game plays it from there.
const INTRO_URL := "https://drive.usercontent.google.com/download?id=1cqQ1iOvQRF18r5BBcwl_JWe81R42JCV-&export=download&confirm=t"
const INTRO_PATH := "Intro/OpenCiv3_Intro.ogv"

## Where to ask for the latest release; a test can point it elsewhere.
var release_url := RELEASE_URL
## Where to fetch the intro movie from; a test can point it elsewhere.
var intro_url := INTRO_URL


## The platform's name as it appears in package names.
static func platform() -> String:
	match OS.get_name():
		"Windows":
			return "windows"
		"macOS":
			return "mac"
		_:
			return "linux"


## The game program in a game folder: what Play starts.
static func game_executable(dir: String) -> String:
	match platform():
		"windows":
			return dir.path_join("OpenCiv3.exe")
		"mac":
			return dir.path_join("OpenCiv3.app")
		_:
			return dir.path_join("OpenCiv3.x86_64")


static func game_installed(dir: String) -> bool:
	var path := game_executable(dir)
	return FileAccess.file_exists(path) or DirAccess.dir_exists_absolute(path)


## The packages this platform uses, by the name the game folder's record
## knows them by, with their name in the manifest.
static func package_names() -> Dictionary:
	return {"game": "game-" + platform(), "art": "art"}


static func load_record(dir: String) -> Dictionary:
	var text := FileAccess.get_file_as_string(dir.path_join(RECORD_FILE))
	var record = JSON.parse_string(text) if text != "" else null
	if not record is Dictionary:
		record = {}
	if not record.get("packages") is Dictionary:
		record["packages"] = {}
	return record


static func save_record(dir: String, record: Dictionary) -> Error:
	var file := FileAccess.open(dir.path_join(RECORD_FILE), FileAccess.WRITE)
	if file == null:
		return FileAccess.get_open_error()
	file.store_string(JSON.stringify(record, "\t") + "\n")
	return OK


## Whether the game folder should have the intro movie fetched: it doesn't
## have it yet, and it isn't turned off in the game's settings. On Windows,
## the game also looks in %APPDATA%\OpenCiv3, so a copy there counts.
static func intro_wanted(dir: String) -> bool:
	var folders := [dir]
	if platform() == "windows" and OS.get_environment("APPDATA") != "":
		folders.append(OS.get_environment("APPDATA").path_join("OpenCiv3"))
	for folder in folders:
		if FileAccess.file_exists(folder.path_join(INTRO_PATH)):
			return false
		# The game's "Play the intro movie on launch" setting, saved as
		# playOnLaunch in the [intro] section of C7.ini.
		var settings := FileAccess.get_file_as_string(folder.path_join("C7.ini"))
		var turned_off := RegEx.create_from_string("(?m)^\\s*playOnLaunch\\s*=\\s*false\\s*$")
		if turned_off.search(settings) != null:
			return false
	return true


## Downloads the intro movie into the game folder. Returns "" or what went
## wrong.
func download_intro(dir: String) -> String:
	var path := dir.path_join(INTRO_PATH)
	if DirAccess.make_dir_recursive_absolute(path.get_base_dir()) != OK:
		return "Can't make the folder %s." % path.get_base_dir()
	var download := path.get_base_dir().path_join(DOWNLOAD_PREFIX + "intro")
	var error := await _download(intro_url, download, 0, "intro")
	# Only Ogg Theora movies play in the game.
	if error == "":
		var file := FileAccess.open(download, FileAccess.READ)
		var magic := file.get_buffer(4).get_string_from_ascii() if file != null else ""
		file = null
		if magic != "OggS":
			error = "What arrived isn't a movie the game can play."
	if error == "" and DirAccess.rename_absolute(download, path) != OK:
		error = "Couldn't save it in %s." % path.get_base_dir()
	DirAccess.remove_absolute(download)
	return error


## Asks GitHub for the latest release and reads its manifest. Returns
## {"version": ..., "manifest": ..., "urls": {file name: url}} or, when that
## isn't possible, {"error": message}, plus "version" if the release was
## found but has no manifest.
func fetch_latest() -> Dictionary:
	var response := await _fetch(release_url, PackedStringArray(["Accept: application/vnd.github+json"]))
	if response.has("error"):
		return response
	var release = JSON.parse_string(response.body.get_string_from_utf8())
	if not release is Dictionary:
		return {"error": "GitHub sent an answer the launcher couldn't read."}
	var version := str(release.get("tag_name", "")).trim_prefix("v")
	var urls := {}
	for asset in release.get("assets", []):
		if asset is Dictionary:
			urls[str(asset.get("name"))] = str(asset.get("browser_download_url"))
	if not urls.has("manifest.json"):
		# Releases made before the launcher existed have no manifest.
		return {"version": version, "error": "Version %s can't be installed by the launcher. Download it from the website instead." % version}
	response = await _fetch(urls["manifest.json"], PackedStringArray())
	if response.has("error"):
		return response
	var manifest = JSON.parse_string(response.body.get_string_from_utf8())
	if not manifest is Dictionary or not manifest.get("packages") is Dictionary:
		return {"version": version, "error": "The release's list of files couldn't be read."}
	return {"version": str(manifest.get("version", version)), "manifest": manifest, "urls": urls}


## Downloads a small file into memory: {"body": bytes} or {"error": message}.
func _fetch(url: String, headers: PackedStringArray) -> Dictionary:
	var request := HTTPRequest.new()
	request.timeout = 30
	add_child(request)
	headers.append("User-Agent: " + USER_AGENT)
	var err := request.request(url, headers)
	if err != OK:
		request.queue_free()
		return {"error": "Couldn't check for updates (error %d)." % err}
	var response: Array = await request.request_completed
	request.queue_free()
	var result: int = response[0]
	var code: int = response[1]
	if result != HTTPRequest.RESULT_SUCCESS:
		return {"error": "Couldn't reach GitHub to check for updates. Check your internet connection."}
	if code == 403 or code == 429:
		# GitHub lets each internet connection ask 60 times an hour without
		# signing in.
		return {"error": "GitHub has had too many requests from this internet connection. Try again in an hour."}
	if code == 404:
		return {"error": "No version has been released yet."}
	if code != 200:
		return {"error": "GitHub answered with error %d. Try again later." % code}
	return {"body": response[3]}


## The packages the game folder needs from this manifest, by their record
## names. A package the record doesn't know, but whose files are all there
## already (from unpacking a full download), is checked against its content
## hash and recorded if it matches, so it isn't downloaded again.
func plan(dir: String, manifest: Dictionary) -> Array:
	var record := load_record(dir)
	var needed := []
	var adopted := false
	for local in package_names():
		var package = manifest.packages.get(package_names()[local])
		if not package is Dictionary:
			continue
		var installed = record.packages.get(local)
		if installed is Dictionary:
			if installed.get("hash") != package.hash:
				needed.append(local)
			continue
		var have := ""
		if _owns_present(dir, package.owns):
			have = await content_hash(dir, package.owns)
		if have == package.hash:
			record.packages[local] = _record_entry(package, manifest)
			adopted = true
		else:
			needed.append(local)
	if adopted:
		if record.packages.has("game") and not record.has("version"):
			record["version"] = str(manifest.version)
		save_record(dir, record)
	return needed


static func _record_entry(package: Dictionary, manifest: Dictionary) -> Dictionary:
	return {"hash": package.hash, "version": str(manifest.version), "file": package.file, "owns": package.owns}


static func _owns_present(dir: String, owns: Array) -> bool:
	for name in owns:
		var path := dir.path_join(name)
		if not FileAccess.file_exists(path) and not DirAccess.dir_exists_absolute(path):
			return false
	return not owns.is_empty()


## The content hash of files already in the game folder, made the same way
## as the release's (.github/workflows/scripts/release_manifest.py): sha256
## over sorted "<sha256 of the file>  <relative path>" lines.
func content_hash(dir: String, owns: Array) -> String:
	var paths: Array[String] = []
	for name in owns:
		_list_files(dir, name, paths)
	paths.sort()
	var listing := HashingContext.new()
	listing.start(HashingContext.HASH_SHA256)
	var started := Time.get_ticks_msec()
	for i in paths.size():
		var file := HashingContext.new()
		file.start(HashingContext.HASH_SHA256)
		file.update(FileAccess.get_file_as_bytes(dir.path_join(paths[i])))
		listing.update((file.finish().hex_encode() + "  " + paths[i] + "\n").to_utf8_buffer())
		if Time.get_ticks_msec() - started > FRAME_BUDGET_MSEC:
			progress.emit("Checking the files you already have...", i, paths.size())
			await get_tree().process_frame
			started = Time.get_ticks_msec()
	return listing.finish().hex_encode()


static func _list_files(dir: String, relative: String, into: Array[String]) -> void:
	var path := dir.path_join(relative)
	if FileAccess.file_exists(path):
		into.append(relative)
		return
	var access := DirAccess.open(path)
	if access == null:
		return
	access.include_hidden = true
	for file in access.get_files():
		into.append(relative + "/" + file)
	for sub in access.get_directories():
		_list_files(dir, relative + "/" + sub, into)


## Downloads one package and unpacks it into the game folder. Returns "" when
## it worked, or what went wrong.
func install(dir: String, local: String, latest: Dictionary) -> String:
	var manifest: Dictionary = latest.manifest
	var package = manifest.packages.get(package_names()[local])
	if not package is Dictionary:
		return "The release has no %s package for this computer." % local
	var url := str(latest.urls.get(package.file, ""))
	if url == "":
		return "The release is missing %s." % package.file
	for name in package.owns:
		if not _safe_top_level(str(name)):
			return "The release's package %s has an unexpected file, %s." % [package.file, name]
	if DirAccess.make_dir_recursive_absolute(dir) != OK:
		return "Can't make the folder %s. Choose another folder." % dir

	var download := dir.path_join(DOWNLOAD_PREFIX + str(package.file))
	var error := await _download(url, download, int(package.size), local)
	if error == "":
		error = await _check_download(download, package)
	if error == "":
		error = await _unpack(dir, download, local, package, manifest)
	DirAccess.remove_absolute(download)
	return error


func _download(url: String, path: String, size: int, local: String) -> String:
	DirAccess.remove_absolute(path)
	var request := HTTPRequest.new()
	request.download_file = path
	request.use_threads = true
	request.download_chunk_size = 1 << 18
	add_child(request)
	var state := {"done": false}
	request.request_completed.connect(func(result: int, code: int, _headers, _body):
		state.result = result
		state.code = code
		state.done = true
	)
	var err := request.request(url, PackedStringArray(["User-Agent: " + USER_AGENT]))
	if err != OK:
		request.queue_free()
		return "Couldn't start the download (error %d)." % err
	var label: String = {"game": "Downloading the game...", "art": "Downloading the art...", "intro": "Downloading the intro movie..."}[local]
	while not state.done:
		# Without a size given, the server's, once it's said.
		progress.emit(label, request.get_downloaded_bytes(), size if size > 0 else request.get_body_size())
		await get_tree().create_timer(0.1).timeout
	request.queue_free()
	if state.result == HTTPRequest.RESULT_DOWNLOAD_FILE_CANT_OPEN or state.result == HTTPRequest.RESULT_DOWNLOAD_FILE_WRITE_ERROR:
		return "Couldn't save the download in the game's folder. Is the disk full, or the folder read-only?"
	if state.result != HTTPRequest.RESULT_SUCCESS:
		return "The download stopped (error %d). Check your internet connection and try again." % state.result
	if state.code != 200:
		return "The download failed: GitHub answered with error %d." % state.code
	return ""


## Makes sure the download arrived whole before anything is replaced.
func _check_download(path: String, package: Dictionary) -> String:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null or file.get_length() != int(package.size):
		return "The download didn't arrive whole. Try again."
	if not package.has("sha256"):
		return ""
	var hashing := HashingContext.new()
	hashing.start(HashingContext.HASH_SHA256)
	var started := Time.get_ticks_msec()
	while file.get_position() < file.get_length():
		hashing.update(file.get_buffer(1 << 20))
		if Time.get_ticks_msec() - started > FRAME_BUDGET_MSEC:
			progress.emit("Checking the download...", file.get_position(), file.get_length())
			await get_tree().process_frame
			started = Time.get_ticks_msec()
	if hashing.finish().hex_encode() != package.sha256:
		return "The download was damaged on the way. Try again."
	return ""


func _unpack(dir: String, download: String, local: String, package: Dictionary, manifest: Dictionary) -> String:
	var zip := ZIPReader.new()
	if zip.open(download) != OK:
		return "Couldn't open the download."

	# Forget the package before touching its files, so if unpacking is cut
	# short, the next run installs it again instead of trusting half of it.
	var record := load_record(dir)
	var old_owns: Array = record.packages.get(local, {}).get("owns", [])
	record.packages.erase(local)
	if save_record(dir, record) != OK:
		zip.close()
		return "Can't write in %s. Choose another folder." % dir

	# Remove what the package had before, so files a new version dropped
	# don't linger. That's both the old and new top-level folders and files,
	# in case the new version renamed one.
	progress.emit("Removing the old files...", 0, 0)
	await get_tree().process_frame
	for name in old_owns + package.owns:
		if _safe_top_level(str(name)) and not _remove(dir.path_join(name)):
			zip.close()
			return "Couldn't remove the old %s. Is the game still open?" % name

	var links: Dictionary = package.get("links", {})
	var files := zip.get_files()
	var started := Time.get_ticks_msec()
	for i in files.size():
		var name := files[i]
		if name.ends_with("/") or links.has(name):
			continue
		if not _safe_path(name):
			zip.close()
			return "The download has an unexpected file, %s." % name
		var path := dir.path_join(name)
		DirAccess.make_dir_recursive_absolute(path.get_base_dir())
		var file := FileAccess.open(path, FileAccess.WRITE)
		if file == null:
			zip.close()
			return "Couldn't write %s. Is the disk full?" % name
		# Files are written exactly as they are in the zip, which keeps the
		# Mac app's signature intact.
		file.store_buffer(zip.read_file(name))
		file.close()
		if Time.get_ticks_msec() - started > FRAME_BUDGET_MSEC:
			progress.emit("Unpacking...", i, files.size())
			await get_tree().process_frame
			started = Time.get_ticks_msec()
	zip.close()

	if OS.get_name() != "Windows":
		var error := _restore_unix_details(dir, local, package, links)
		if error != "":
			return error

	record = load_record(dir)
	record.packages[local] = _record_entry(package, manifest)
	if local == "game":
		record["version"] = str(manifest.version)
	if save_record(dir, record) != OK:
		return "Can't write in %s. Choose another folder." % dir
	return ""


## Godot's ZIPReader can't see Unix permissions or symbolic links, so the
## manifest lists them and they're put back here. On a Mac the game app is
## then cleared of download flags and, if its signature doesn't check out,
## signed again the way the release does (composite/macResign).
func _restore_unix_details(dir: String, local: String, package: Dictionary, links: Dictionary) -> String:
	for name in links:
		if not _safe_path(name):
			continue
		var path := dir.path_join(name)
		DirAccess.make_dir_recursive_absolute(path.get_base_dir())
		OS.execute("ln", ["-sfn", str(links[name]), path])
	var executables := []
	for name in package.get("executables", []):
		if _safe_path(str(name)):
			executables.append(dir.path_join(name))
	if local == "game" and platform() == "linux":
		executables.append(game_executable(dir))
	if not executables.is_empty():
		OS.execute("chmod", PackedStringArray(["+x"] + executables))

	if local != "game" or platform() != "mac" or not DirAccess.dir_exists_absolute(game_executable(dir)):
		return ""
	var app := game_executable(dir)
	progress.emit("Getting the game ready for your Mac...", 0, 0)
	# The game's own program needs to stay runnable even if the zip lost its
	# permissions.
	OS.execute("/bin/sh", ["-c", "chmod +x \"$0\"/Contents/MacOS/*", app])
	OS.execute("xattr", ["-cr", app])
	if OS.execute("codesign", ["--verify", "--deep", "--strict", app]) == 0:
		return ""
	var entitlements := dir.path_join(DOWNLOAD_PREFIX + "entitlements.plist")
	OS.execute("/bin/sh", ["-c", "codesign -d --entitlements - --xml \"$0\" > \"$1\"", app, entitlements])
	var args := ["--force", "--deep", "--options", "runtime"]
	var ent_file := FileAccess.open(entitlements, FileAccess.READ)
	if ent_file != null and ent_file.get_length() > 0:
		args += ["--entitlements", entitlements]
	ent_file = null
	var code := OS.execute("codesign", PackedStringArray(args + ["-s", "-", app]))
	DirAccess.remove_absolute(entitlements)
	if code != 0:
		return "The game was installed, but your Mac couldn't approve it (codesign error %d)." % code
	return ""


## Top-level names a package may own: a plain name in the game folder, never
## the launcher itself, its record, or its downloads.
func _safe_top_level(name: String) -> bool:
	if not _safe_path(name) or name.contains("/"):
		return false
	if name == RECORD_FILE or name.begins_with(DOWNLOAD_PREFIX):
		return false
	return name != _launcher_name()


static func _safe_path(name: String) -> bool:
	if name == "" or name.begins_with("/") or name.contains("\\") or name.contains(":"):
		return false
	for part in name.split("/"):
		if part == ".." or part == ".":
			return false
	return true


## The launcher's own file (or app on a Mac), which a package must never
## replace.
static func _launcher_name() -> String:
	var exe := OS.get_executable_path()
	var app := exe.find(".app/Contents/MacOS")
	if app >= 0:
		return exe.substr(0, app + 4).get_file()
	return exe.get_file()


## Deletes a file or folder. On Linux and Mac rm doesn't follow symbolic
## links out of the folder; on Windows the folder is walked.
static func _remove(path: String) -> bool:
	if OS.get_name() != "Windows":
		return OS.execute("rm", ["-rf", path]) == 0
	if DirAccess.dir_exists_absolute(path):
		return _remove_tree(path)
	if FileAccess.file_exists(path):
		return DirAccess.remove_absolute(path) == OK
	return true


static func _remove_tree(path: String) -> bool:
	var access := DirAccess.open(path)
	if access == null:
		return false
	access.include_hidden = true
	for file in access.get_files():
		if access.remove(file) != OK:
			return false
	for sub in access.get_directories():
		if not _remove_tree(path.path_join(sub)):
			return false
	return DirAccess.remove_absolute(path) == OK
