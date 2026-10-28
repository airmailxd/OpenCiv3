@echo off
rem Makes maps in a map-making VM (see README.md), from the logon of its
rem user: generate.py with Quick Start, into the corpus folder vm_<name>.
rem The clone's guest properties say what: /civ3/name its name, /civ3/count
rem the maps of each type, /civ3/args generate.py's other arguments (its
rem share of the settings, see clone.ps1). Started again if it stops.
set CIV3_LAUNCH=exe
set CIV3_EXE=C:\Civ3\Conquests\Civ3Conquests.exe
set CIV3_AUTOSAVES=C:\Civ3\Conquests\Saves\Auto
rem In for /f, "call" keeps the quotes around a path with spaces.
rem The Guest Additions put VBoxControl in System32.
set VBOXCONTROL=C:\Windows\System32\VBoxControl.exe
rem The shared folders as drives: \\VBOXSVR paths aren't always seen.
net use T: \\VBOXSVR\tools >nul 2>&1
net use Z: \\VBOXSVR\corpus >nul 2>&1
set VMNAME=
for /f "tokens=2" %%a in ('%VBOXCONTROL% -nologo guestproperty get /civ3/name ^| findstr /b Value:') do set VMNAME=%%a
if "%VMNAME%"=="" set VMNAME=%COMPUTERNAME%
set COUNT=
for /f "tokens=2" %%a in ('%VBOXCONTROL% -nologo guestproperty get /civ3/count ^| findstr /b Value:') do set COUNT=%%a
if "%COUNT%"=="" set COUNT=2000
set ARGS=
for /f "tokens=1,*" %%a in ('%VBOXCONTROL% -nologo guestproperty get /civ3/args ^| findstr /b Value:') do set ARGS=%%b
set OUT=Z:\vm_%VMNAME%
if not exist %OUT% mkdir %OUT%
rem Civ3 full screen: in the VM it runs at 1024x768, which the script reads
rem as well (its positions are shares of the screen). An earlier version
rem set FullScreen=0, which Civ3 ignores; put it back.
powershell -NoProfile -Command "(Get-Content C:\Civ3\Conquests\conquests.ini) -replace '^FullScreen=0','FullScreen=1' | Set-Content C:\Civ3\Conquests\conquests.ini"
rem The window settles after logon before the script takes over.
timeout /t 20 /nobreak >nul
:loop
C:\Python311\python.exe -u T:\generate.py --out=%OUT% --count=%COUNT% %ARGS% >> %OUT%\run.log 2>&1
timeout /t 10 /nobreak >nul
goto loop
