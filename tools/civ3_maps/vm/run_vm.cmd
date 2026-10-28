@echo off
rem Makes maps in a map-making VM (see README.md), from the logon of its
rem user: generate.py with Quick Start, into the corpus folder vm_<name>,
rem where <name> is the clone's guest property /civ3/name. Started again if
rem it stops.
set CIV3_LAUNCH=exe
set CIV3_EXE=C:\Civ3\Conquests\Civ3Conquests.exe
set CIV3_AUTOSAVES=C:\Civ3\Conquests\Saves\Auto
set VBOXCONTROL="C:\Program Files\Oracle\VirtualBox Guest Additions\VBoxControl.exe"
set VMNAME=
for /f "tokens=2" %%a in ('%VBOXCONTROL% -nologo guestproperty get /civ3/name ^| findstr /b Value:') do set VMNAME=%%a
if "%VMNAME%"=="" set VMNAME=%COMPUTERNAME%
set COUNT=
for /f "tokens=2" %%a in ('%VBOXCONTROL% -nologo guestproperty get /civ3/count ^| findstr /b Value:') do set COUNT=%%a
if "%COUNT%"=="" set COUNT=2000
set OUT=\\VBOXSVR\corpus\vm_%VMNAME%
if not exist %OUT% mkdir %OUT%
rem The window settles after logon before the script takes over.
timeout /t 20 /nobreak >nul
:loop
C:\Python311\python.exe -u \\VBOXSVR\tools\generate.py --out=%OUT% --count=%COUNT% --block=50 >> %OUT%\run.log 2>&1
timeout /t 10 /nobreak >nul
goto loop
