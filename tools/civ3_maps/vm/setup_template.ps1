# Once build_template.ps1's Windows install has finished and the user is
# logged on: runs guest_setup.ps1 in the template, shuts it down and takes
# the snapshot clone.ps1 clones from.
param([string]$Name = "civ3gen-template")
$vbox = "C:\Program Files\Oracle\VirtualBox\VBoxManage.exe"

# Guest Additions report a logged-on user when the guest is ready (the
# install reboots a few times on the way, and the property can't be read
# while the VM is restarting).
while (-not ((& $vbox guestproperty get $Name "/VirtualBox/GuestInfo/OS/LoggedInUsers" 2>$null) -match "Value: [1-9]")) {
	Start-Sleep 10
}
# The desktop settles before anything runs in it.
Start-Sleep 60
# The script is copied in rather than run from \\VBOXSVR, which a process
# started this way may not see yet; it waits for the shared folders itself.
& $vbox guestcontrol $Name copyto --username civ3 --password civ3 (Join-Path $PSScriptRoot "guest_setup.ps1") "C:\Windows\Temp\guest_setup.ps1"
$out = & $vbox guestcontrol $Name run --username civ3 --password civ3 --wait-stdout --wait-stderr `
	--exe "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe" -- `
	powershell.exe -ExecutionPolicy Bypass -File "C:\Windows\Temp\guest_setup.ps1" 2>&1
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
