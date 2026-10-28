# Shuts down the running map-making VMs (civ3gen-<n>).
$vbox = "C:\Program Files\Oracle\VirtualBox\VBoxManage.exe"
foreach ($line in & $vbox list runningvms) {
	if ($line -match '^"(civ3gen-\d+)"') {
		& $vbox controlvm $Matches[1] acpipowerbutton
	}
}
