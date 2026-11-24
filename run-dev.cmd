@echo off
rem Build and run a dev version of OpenCiv3.
rem   run-dev            build, then open the game window
rem   run-dev console    same, but show log output in this terminal
rem   run-dev nobuild    skip the build step
rem Set GODOT_DIR to use a different Godot .NET install.

setlocal
if not defined GODOT_DIR set "GODOT_DIR=C:\Dev\Godot_v4.4-stable_mono_win64"
set "GODOT_EXE=%GODOT_DIR%\Godot_v4.4-stable_mono_win64.exe"
if /i "%~1"=="console" set "GODOT_EXE=%GODOT_DIR%\Godot_v4.4-stable_mono_win64_console.exe"

if not exist "%GODOT_EXE%" (
  echo Godot not found at "%GODOT_EXE%". Set GODOT_DIR to your Godot .NET 4.4 folder.
  exit /b 1
)

cd /d "%~dp0C7"

if /i not "%~1"=="nobuild" (
  echo Building...
  dotnet build C7.csproj -nologo -v quiet
  if errorlevel 1 (
    echo Build failed.
    exit /b 1
  )
)

echo Starting OpenCiv3...
if /i "%~1"=="console" (
  "%GODOT_EXE%" --path .
) else (
  start "" "%GODOT_EXE%" --path .
)
