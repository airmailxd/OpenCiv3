# Gives a Windows executable exported by Godot the game's icon.
#
#   godot --headless --path Launcher -s ../.github/workflows/scripts/set_exe_icon.gd -- <exe> <ico>
#
# Godot 4.4 can only set the icon through rcedit, which on Linux runs under
# Wine, and rcedit adds the icon beside Godot's own named one rather than
# replacing it, so Windows still shows Godot's. Instead, each of the
# executable's icon images is overwritten in place by the .ico's image of
# the same size, as a PNG, which must be no bigger than the image it
# replaces. Nothing in the executable moves, so a game embedded at its end
# (the launcher's) still loads. Exits with 1, saying why, if it can't.
extends SceneTree

const RT_ICON := 3
const RT_GROUP_ICON := 14


func _init() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 2:
		printerr("Usage: godot --headless -s set_exe_icon.gd -- <exe> <ico>")
		quit(2)
		return
	var problem := set_icon(args[0], args[1])
	if problem != "":
		printerr("Couldn't set the icon of %s: %s" % [args[0], problem])
		quit(1)
		return
	print("Set the icon of %s from %s" % [args[0], args[1]])
	quit(0)


# The .ico's images by width (256 for 0).
func read_ico(path: String) -> Dictionary:
	var ico := FileAccess.get_file_as_bytes(path)
	var images := {}
	if ico.size() < 6 or ico.decode_u16(2) != 1:
		return images
	for i in ico.decode_u16(4):
		var entry := 6 + 16 * i
		var width := ico[entry] if ico[entry] != 0 else 256
		var size := ico.decode_u32(entry + 8)
		var offset := ico.decode_u32(entry + 12)
		images[width] = ico.slice(offset, offset + size)
	return images


func set_icon(exe_path: String, ico_path: String) -> String:
	var images := read_ico(ico_path)
	if images.is_empty():
		return "%s has no icons" % ico_path
	var exe := FileAccess.get_file_as_bytes(exe_path)
	if exe.size() < 0x40 or exe.decode_u16(0) != 0x5A4D:
		return "not a Windows executable"

	var pe := exe.decode_u32(0x3C)
	var section_count := exe.decode_u16(pe + 6)
	var optional_size := exe.decode_u16(pe + 20)
	var pe32_plus := exe.decode_u16(pe + 24) == 0x20B
	var data_directories := pe + 24 + (112 if pe32_plus else 96)
	var resources_rva := exe.decode_u32(data_directories + 2 * 8)
	var sections := []
	for i in section_count:
		var header := pe + 24 + optional_size + 40 * i
		sections.append([exe.decode_u32(header + 12), max(exe.decode_u32(header + 8), exe.decode_u32(header + 16)), exe.decode_u32(header + 20)])

	var resources := file_offset(sections, resources_rva)
	if resources < 0:
		return "no resources"
	var types := directory(exe, resources)
	if not types.has(RT_GROUP_ICON) or not types.has(RT_ICON):
		return "no icon"
	var groups := directory(exe, resources + (types[RT_GROUP_ICON] & 0x7FFFFFFF))
	var icons := directory(exe, resources + (types[RT_ICON] & 0x7FFFFFFF))

	# Every icon group; Godot's executables have one.
	for group_target in groups.values():
		var group := file_offset(sections, exe.decode_u32(data_entry(exe, resources, group_target)))
		for i in exe.decode_u16(group + 4):
			var entry := group + 6 + 14 * i
			var width := exe[entry] if exe[entry] != 0 else 256
			var id := exe.decode_u16(entry + 12)
			if not images.has(width):
				return "%s has no %dx%d image" % [ico_path, width, width]
			if not icons.has(id):
				return "icon image %d is missing" % id
			var image: PackedByteArray = images[width]
			var image_entry := data_entry(exe, resources, icons[id])
			var room := exe.decode_u32(image_entry + 4)
			if image.size() > room:
				return "the %dx%d image is %d bytes, more than the %d there's room for" % [width, width, image.size(), room]
			var at := file_offset(sections, exe.decode_u32(image_entry))
			for b in room:
				exe[at + b] = image[b] if b < image.size() else 0
			exe.encode_u32(image_entry + 4, image.size())
			# The group's entry: width and height (0 for 256), no palette,
			# one plane, 32 bits, and the image's size.
			exe[entry] = width % 256
			exe[entry + 1] = width % 256
			exe[entry + 2] = 0
			exe[entry + 3] = 0
			exe.encode_u16(entry + 4, 1)
			exe.encode_u16(entry + 6, 32)
			exe.encode_u32(entry + 8, image.size())

	var file := FileAccess.open(exe_path, FileAccess.WRITE)
	if file == null:
		return "can't write it (%s)" % error_string(FileAccess.get_open_error())
	file.store_buffer(exe)
	file.close()
	return ""


# Where in the file the address is, or -1 if it's in no section.
func file_offset(sections: Array, rva: int) -> int:
	for section in sections:
		if rva >= section[0] and rva < section[0] + section[1]:
			return rva - section[0] + section[2]
	return -1


# A resource directory's entries, by name or ID.
func directory(exe: PackedByteArray, at: int) -> Dictionary:
	var entries := {}
	var count := exe.decode_u16(at + 12) + exe.decode_u16(at + 14)
	for i in count:
		entries[exe.decode_u32(at + 16 + 8 * i)] = exe.decode_u32(at + 20 + 8 * i)
	return entries


# The data entry an entry leads to, through its first language.
func data_entry(exe: PackedByteArray, resources: int, target: int) -> int:
	while target & 0x80000000:
		target = directory(exe, resources + (target & 0x7FFFFFFF)).values()[0]
	return resources + target
