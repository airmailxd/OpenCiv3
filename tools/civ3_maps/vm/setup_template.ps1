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
& $vbox guestcontrol $Name run --username civ3 --password civ3 --wait-stdout --wait-stderr `
	--exe "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe" -- `
	powershell.exe -ExecutionPolicy Bypass -File "\\VBOXSVR\tools\vm\guest_setup.ps1"

& $vbox controlvm $Name acpipowerbutton
while ((& $vbox showvminfo $Name --machinereadable) -match 'VMState="running"') {
	Start-Sleep 5
}
& $vbox snapshot $Name take base
