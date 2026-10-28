# Shows the map-making VMs' screens side by side, read-only: screenshots
# every running civ3gen VM every few seconds into E:\VMs\watch, with a page
# that reloads them (and each VM's map count and last log line).
#
#   powershell -File tools\civ3_maps\vm\watch.ps1
#
# then open http://127.0.0.1:8770 (the folder served on this PC only; a page
# opened from disk may not load the screenshots). Ctrl+C stops it.
param([string]$Out = "E:\VMs\watch", [int]$Seconds = 5, [string]$Corpus = "C:\Dev\civ3_map_corpus", [int]$Port = 8770)
$vbox = "C:\Program Files\Oracle\VirtualBox\VBoxManage.exe"
New-Item -ItemType Directory -Force $Out | Out-Null
$page = @"
<!doctype html><meta charset="utf-8"><title>Civ3 VMs</title>
<style>body{background:#111;color:#ddd;font:13px sans-serif;margin:8px}
#g{display:grid;grid-template-columns:repeat(auto-fill,minmax(420px,1fr));gap:8px}
img{width:100%;display:block;background:#000}</style>
<div id="g"></div>
<script>
async function tick(){
  let s=await (await fetch('status.json?'+Date.now())).json();
  let g=document.getElementById('g');
  g.innerHTML=s.map(v=>'<div><b>'+v.name+'</b> '+v.maps+' maps<br><small>'+v.log+'</small><img src="'+v.name+'.png?'+Date.now()+'"></div>').join('');
}
tick(); setInterval(tick, $($Seconds * 1000));
</script>
"@
Set-Content -Encoding utf8 (Join-Path $Out "index.html") $page
$server = Start-Process python -ArgumentList "-m", "http.server", $Port, "--bind", "127.0.0.1", "--directory", $Out -WindowStyle Hidden -PassThru
while ($true) {
	$status = @()
	foreach ($line in & $vbox list runningvms) {
		if ($line -match '^"(civ3gen-[^"]+)"') {
			$name = $Matches[1]
			& $vbox controlvm $name screenshotpng (Join-Path $Out "$name.png") 2>$null | Out-Null
			$folder = Join-Path $Corpus "vm_$name"
			$maps = @(Get-ChildItem $folder -Filter *.SAV -ErrorAction SilentlyContinue).Count
			$log = Get-Content (Join-Path $folder "run.log") -Tail 1 -ErrorAction SilentlyContinue
			$status += [pscustomobject]@{ name = $name; maps = $maps; log = "$log" }
		}
	}
	ConvertTo-Json @($status) | Set-Content -Encoding utf8 (Join-Path $Out "status.json")
	Start-Sleep $Seconds
}
Stop-Process $server
