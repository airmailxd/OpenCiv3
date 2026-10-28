# Makes and starts the map-making VMs: linked clones of the template's base
# snapshot, each writing its maps to the corpus folder vm_civ3gen-<n>.
#
#   powershell -File tools\civ3_maps\vm\clone.ps1 -Count 6 -Maps 3
#   powershell -File ...\clone.ps1 -Count 2 -Tasks "--sizes=Tiny,Small;--sizes=Huge"
#
# Each VM makes its share of every world setting Civ3 offers (-Tasks, one
# string of generate.py arguments per VM; by default each size of world,
# with Huge split by climate) with every landform, water, climate,
# temperature and age in it, -Maps maps of each. stop.ps1 shuts them down;
# run this again to start them again.
param(
	[int]$Count = 6,
	[int]$Maps = 3,
	[string[]]$Tasks = @(
		"--sizes=Tiny",
		"--sizes=Small",
		"--sizes=Standard",
		"--sizes=Large",
		"--sizes=Huge --climates=Ari,Nrm",
		"--sizes=Huge --climates=Wet"
	),
	[string]$Template = "civ3gen-template",
	[string]$Base = "E:\VMs"
)
$ErrorActionPreference = "Stop"
$vbox = "C:\Program Files\Oracle\VirtualBox\VBoxManage.exe"
# powershell -File passes -Tasks "a","b" as one string, so tasks may also
# come as one, separated by ';': -Tasks "--sizes=Tiny;--sizes=Huge".
if ($Tasks.Count -eq 1 -and $Tasks[0] -match ';') {
	$Tasks = $Tasks[0] -split ';' | ForEach-Object { $_.Trim() }
}
# Every landform, water, climate, temperature and age, unless a task says.
$all = "--landforms=Pan,Con,Arc --water=80,70,60 --climates=Ari,Nrm,Wet --temperatures=Wrm,Tmp,Col --ages=3B,4B,5B"
$vms = & $vbox list vms
for ($i = 1; $i -le $Count; $i++) {
	$name = "civ3gen-$i"
	if (-not ($vms -match "`"$name`"")) {
		& $vbox clonevm $Template --snapshot base --options link --name $name --basefolder $Base --register
	}
	# The task's own arguments come last, so they win over $all's.
	$task = $Tasks[($i - 1) % $Tasks.Count]
	& $vbox guestproperty set $name /civ3/name $name
	& $vbox guestproperty set $name /civ3/count $Maps
	& $vbox guestproperty set $name /civ3/args "--block=$Maps $all $task"
	& $vbox startvm $name --type headless
}
# Civ3 runs full screen at 1024x768 in the VMs, which the script reads
# (its positions are shares of the screen); keep the desktop the same size.
Start-Sleep 90
for ($i = 1; $i -le $Count; $i++) {
	& $vbox controlvm "civ3gen-$i" setvideomodehint 1024 768 32
}
