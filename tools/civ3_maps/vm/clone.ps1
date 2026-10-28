# Makes and starts the map-making VMs: linked clones of the template's base
# snapshot, each writing its maps to the corpus folder vm_civ3gen-<n>.
#
#   powershell -File tools\civ3_maps\vm\clone.ps1 -Count 6 -Maps 2000
#
# -Maps is how many maps of each type a VM makes (generate.py --count).
# stop.ps1 shuts them down; run this again to start them again.
param(
	[int]$Count = 6,
	[int]$Maps = 2000,
	[string]$Template = "civ3gen-template",
	[string]$Base = "E:\VMs"
)
$ErrorActionPreference = "Stop"
$vbox = "C:\Program Files\Oracle\VirtualBox\VBoxManage.exe"
$vms = & $vbox list vms
for ($i = 1; $i -le $Count; $i++) {
	$name = "civ3gen-$i"
	if (-not ($vms -match "`"$name`"")) {
		& $vbox clonevm $Template --snapshot base --options link --name $name --basefolder $Base --register
	}
	& $vbox guestproperty set $name /civ3/name $name
	& $vbox guestproperty set $name /civ3/count $Maps
	& $vbox startvm $name --type headless
}
