#!/usr/bin/env python3
"""Writes manifest.json for a release, which the launcher reads to decide
what to download.

The game and its art are published as separate packages (owner's decision,
2026-10-12), so a new version only costs players the parts that changed.
Each package gets a content hash: sha256 over a sorted listing of the
package's files, one "<sha256 of the file>  <relative path>" line each (the
same lines `sha256sum` prints). It is computed from what's inside the zip,
not the zip itself, so the art's hash stays the same from release to release
until an art file actually changes, however the zip was put together.

Usage: release_manifest.py <version> <dist dir> <out file> name=file...
where name is "art" or "game-<platform>".
"""

import hashlib
import json
import os
import stat
import sys
import zipfile


def describe(path):
	listing = []
	executables = []
	links = {}
	top_level = set()
	with zipfile.ZipFile(path) as archive:
		for info in archive.infolist():
			name = info.filename
			if name.endswith("/"):
				continue
			# The launcher refuses anything that would land outside the game
			# folder, so catch it here too, before it's published.
			if name.startswith("/") or ".." in name.split("/") or "\\" in name:
				sys.exit(f"{path}: unsafe path in zip: {name}")
			top_level.add(name.split("/")[0])
			data = archive.read(info)
			listing.append((name, hashlib.sha256(data).hexdigest()))
			# The launcher extracts with Godot's ZIPReader, which can't see
			# Unix permissions or symbolic links, so they're listed here for
			# it to restore (the game's executable on Linux and Mac).
			mode = info.external_attr >> 16
			if stat.S_ISLNK(mode):
				links[name] = data.decode("utf-8")
			elif mode & 0o111:
				executables.append(name)
	listing.sort()
	content = "".join(f"{digest}  {name}\n" for name, digest in listing)
	return {
		"hash": hashlib.sha256(content.encode("utf-8")).hexdigest(),
		"files": len(listing),
		"owns": sorted(top_level),
		"executables": sorted(executables),
		"links": links,
	}


def file_sha256(path):
	digest = hashlib.sha256()
	with open(path, "rb") as f:
		for chunk in iter(lambda: f.read(1 << 20), b""):
			digest.update(chunk)
	return digest.hexdigest()


def main():
	if len(sys.argv) < 5:
		sys.exit(__doc__)
	version, dist, out = sys.argv[1:4]
	packages = {}
	for arg in sys.argv[4:]:
		name, file_name = arg.split("=", 1)
		path = os.path.join(dist, file_name)
		package = {
			"file": file_name,
			"size": os.path.getsize(path),
			# The zip's own sha256, so the launcher can tell a download
			# arrived whole before it replaces anything.
			"sha256": file_sha256(path),
		}
		package.update(describe(path))
		packages[name] = package
		print(f"{name}: {file_name}, {package['size']} bytes, {package['files']} files, owns {package['owns']}")
	manifest = {"format": 1, "version": version, "packages": packages}
	with open(out, "w", encoding="utf-8") as f:
		json.dump(manifest, f, indent=1)
		f.write("\n")


if __name__ == "__main__":
	main()
