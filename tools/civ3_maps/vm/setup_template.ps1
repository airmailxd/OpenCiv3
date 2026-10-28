# Once build_template.ps1's Windows install has finished and the user is
# logged on: runs guest_setup.ps1 in the template, shuts it down and takes
# the snapshot clone.ps1 clones from.
#
# guest_setup.ps1 runs at the user's logon, from the Startup folder: the
# shared folders (\\VBOXSVR\...) are only there in the user's own session,
# not in processes guestcontrol starts.
param([string]$Name = "civ3gen-template")
$vbox = "C:\Program Files\Oracle\VirtualBox\VBoxManage.exe"
$auth = "--username", "civ3", "--password", "civ3"
$startup = "C:\Users\civ3\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup"
$log = "C:\Windows\Temp\guest_setup.log"

function Wait-LoggedOn {
	# Guest Additions report a logged-on user when the guest is ready (the
	# property can't be read while the VM is restarting).
	while (-not ((& $vbox guestproperty get $Name "/VirtualBox/GuestInfo/OS/LoggedInUsers" 2>$null) -match "Value: [1-9]")) {
		Start-Sleep 10
	}
}

function Guest-Run([string]$command) {
	& $vbox guestcontrol $Name run @auth --wait-stdout --wait-stderr --exe "C:\Windows\System32\cmd.exe" -- cmd.exe /c $command 2>&1
}

Wait-LoggedOn
# The desktop settles before anything runs in it.
Start-Sleep 60

# A one-time logon script that runs guest_setup.ps1, logs it, and removes
# itself; then a restart to log on again.
$once = Join-Path $env:TEMP "civ3gen_setup.cmd"
Set-Content -Encoding ascii $once @"
@echo off
powershell.exe -ExecutionPolicy Bypass -File C:\Windows\Temp\guest_setup.ps1 > $log 2>&1
del "%~f0"
"@
& $vbox guestcontrol $Name copyto @auth (Join-Path $PSScriptRoot "guest_setup.ps1") "C:\Windows\Temp\guest_setup.ps1"
& $vbox guestcontrol $Name copyto @auth $once "$startup\civ3gen_setup.cmd"
Guest-Run "del $log 2>nul & shutdown /r /t 5" | Out-Null

# Wait for the restart and the setup (Python, and copying Civ3 take a few
# minutes).
Start-Sleep 60
Wait-LoggedOn
$out = ""
for ($i = 0; $i -lt 90 -and -not ($out -match "guest setup done|Exception|Error"); $i++) {
	Start-Sleep 10
	$out = (Guest-Run "type $log 2>nul") -join "`n"
}
$out
# Only a template that was set up is worth snapshotting.
if (-not ($out -match "guest setup done")) {
	throw "guest_setup.ps1 failed in the template; not taking the snapshot"
}

& $vbox controlvm $Name acpipowerbutton
while ((& $vbox showvminfo $Name --machinereadable) -match 'VMState="running"') {
	Start-Sleep 5
}
# A base from an earlier run is replaced (delete its clones first: they
# depend on it).
if ((& $vbox snapshot $Name list --machinereadable 2>$null) -match 'SnapshotName="base"') {
	& $vbox snapshot $Name delete base
}
& $vbox snapshot $Name take base
