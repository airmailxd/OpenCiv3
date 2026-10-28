# Run once inside the template VM (setup_template.ps1 runs it): installs
# Python and Pillow, copies Civ3, keeps the screen awake, and makes
# run_vm.cmd start at every logon.
$ErrorActionPreference = "Stop"
$setup = "\\VBOXSVR\setup"

# The shared folders appear a while after logon (and not at all until the
# Guest Additions' reboot), so wait for them.
for ($i = 0; -not (Test-Path "$setup\civ3") -and $i -lt 60; $i++) {
	Start-Sleep 5
}
if (-not (Test-Path "$setup\civ3")) {
	throw "the shared folder $setup isn't there"
}

# Python for all users, with Pillow from the setup folder (no internet).
Start-Process -Wait "$setup\python-3.11.9-amd64.exe" -ArgumentList "/quiet", "InstallAllUsers=1", "PrependPath=1", "Include_test=0", "TargetDir=C:\Python311"
& C:\Python311\python.exe -m pip install --no-index --find-links "$setup\wheels" pillow

# A copy of Civ3 of its own: Civ3 writes its autosaves into its folder.
robocopy "$setup\civ3" C:\Civ3 /E /NFL /NDL /NJH /NJS /NP | Out-Null

# Nothing may cover or blank the screen the script reads.
powercfg /change monitor-timeout-ac 0
powercfg /change standby-timeout-ac 0
powercfg /change hibernate-timeout-ac 0
New-Item -Force "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Personalization" | Out-Null
Set-ItemProperty "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Personalization" NoLockScreen 1
Set-ItemProperty "HKCU:\Control Panel\Desktop" ScreenSaveActive 0
# No background work stealing the CPU or disk from Civ3: these VMs only
# make maps, offline from anything but the host's shared folders.
Set-MpPreference -DisableRealtimeMonitoring $true -DisableScanningNetworkFiles $true -ErrorAction SilentlyContinue
Add-MpPreference -ExclusionPath C:\Civ3, "\\VBOXSVR" -ErrorAction SilentlyContinue
foreach ($service in "wuauserv", "UsoSvc", "WSearch", "SysMain", "DiagTrack") {
	Stop-Service $service -Force -ErrorAction SilentlyContinue
	Set-Service $service -StartupType Disabled -ErrorAction SilentlyContinue
}
New-Item -Force "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU" | Out-Null
Set-ItemProperty "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU" NoAutoUpdate 1

# No notifications popping up over Civ3.
New-Item -Force "HKCU:\SOFTWARE\Policies\Microsoft\Windows\Explorer" | Out-Null
Set-ItemProperty "HKCU:\SOFTWARE\Policies\Microsoft\Windows\Explorer" DisableNotificationCenter 1

# run_vm.cmd at every logon, in the user's session (it needs the desktop).
# It is run from the host's folder, so changes to it reach every clone.
$startup = [Environment]::GetFolderPath("Startup")
Set-Content -Encoding ascii (Join-Path $startup "civ3gen.cmd") "@call \\VBOXSVR\tools\vm\run_vm.cmd"
"guest setup done"
