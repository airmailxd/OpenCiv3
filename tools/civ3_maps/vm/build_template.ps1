# Builds the template VM the map-making VMs are cloned from (see README.md):
# Windows 10 installed unattended, with Guest Additions, UAC off and the
# user logged on automatically, and the shared folders the guests use.
#
#   powershell -File tools\civ3_maps\vm\build_template.ps1
#
# Then, once Windows is up: setup_template.ps1, then clone.ps1.
param(
	[string]$Iso = "E:\VMs\iso\win10_ent_eval.iso",
	[string]$Base = "E:\VMs",
	[string]$Name = "civ3gen-template",
	[string]$Setup = "E:\VMs\share",
	[string]$Tools = "C:\Dev\C3\tools\civ3_maps",
	[string]$Corpus = "C:\Dev\civ3_map_corpus"
)
$ErrorActionPreference = "Stop"
$vbox = "C:\Program Files\Oracle\VirtualBox\VBoxManage.exe"

& $vbox createvm --name $Name --ostype Windows10_64 --basefolder $Base --register
& $vbox modifyvm $Name --memory 3072 --cpus 2 --vram 128 --graphicscontroller vboxsvga `
	--nic1 nat --audio-driver null --clipboard-mode disabled --usb-ohci off --mouse usbtablet
$disk = Join-Path $Base "$Name\$Name.vdi"
& $vbox createmedium disk --filename $disk --size 40960
& $vbox storagectl $Name --name SATA --add sata --controller IntelAhci --portcount 2
& $vbox storageattach $Name --storagectl SATA --port 0 --device 0 --type hdd --medium $disk

# The guests read the setup files and the scripts, and write their maps to
# the corpus, as \\VBOXSVR\<name>.
& $vbox sharedfolder add $Name --name setup --hostpath $Setup --readonly
& $vbox sharedfolder add $Name --name tools --hostpath $Tools --readonly
& $vbox sharedfolder add $Name --name corpus --hostpath $Corpus

# At the end of the install: UAC off (so the setup can install for all
# users) and an automatic logon, so a clone starts making maps on boot.
$winlogon = '"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"'
$post = 'cmd /c reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System" /v EnableLUA /t REG_DWORD /d 0 /f' +
	" & reg add $winlogon /v AutoAdminLogon /t REG_SZ /d 1 /f" +
	" & reg add $winlogon /v DefaultUserName /t REG_SZ /d civ3 /f" +
	" & reg add $winlogon /v DefaultPassword /t REG_SZ /d civ3 /f"
# Windows PowerShell passes quotes inside an argument on unescaped.
$postArg = '--post-install-command=' + $post.Replace('"', '\"')

# VirtualBox's answer file, without its product key: evaluation media stop
# at "Windows cannot find the Microsoft Software License Terms" on an empty
# one.
$template = Get-Content -Raw "C:\Program Files\Oracle\VirtualBox\UnattendedTemplates\win_nt6_unattended.xml"
$template = $template -replace '(?s)\s*<ProductKey>.*?</ProductKey>', ''
$answers = Join-Path $Base "$Name\win10_eval_unattended.xml"
Set-Content -Encoding utf8 $answers $template

& $vbox unattended install $Name --iso=$Iso --user=civ3 --password=civ3 --full-user-name=civ3 `
	--install-additions --locale=en_US --country=US --time-zone=UTC --hostname=civ3gen.local `
	--script-template=$answers $postArg --start-vm=headless
