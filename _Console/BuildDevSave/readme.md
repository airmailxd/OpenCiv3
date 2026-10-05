# BuildDevSave
Reads a .SAV file, converts it to C7's save format, then writes it to `C7/Text/c7-static-map-save.json` in the
repository (found by looking up from the working directory or the program's folder), or to the given output path.

## Build
`dotnet build -m`

## Usage
`dotnet run <path to .SAV file> [output path]`
