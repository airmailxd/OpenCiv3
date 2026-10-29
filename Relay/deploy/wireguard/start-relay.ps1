# Starts the relay capped so it cannot slow games: Below Normal priority, one CPU core, 256 MB heap.
# Usage: .\start-relay.ps1 [-Url http://10.8.0.1:8080] [-Core 1]
# The secret is read from the RELAY_KEY_SECRET environment variable, or generated for this run.
param(
	[string]$Url = "http://10.8.0.1:8080",
	[int]$Core = 1
)
$relayDir = Resolve-Path (Join-Path $PSScriptRoot "..\..")
if (-not $env:RELAY_KEY_SECRET) {
	$env:RELAY_KEY_SECRET = -join ((1..32) | ForEach-Object { '{0:x2}' -f (Get-Random -Maximum 256) })
	Write-Host "No RELAY_KEY_SECRET set; using a random one for this run (invite keys reset on restart)."
}
$env:Relay__KeySecret = $env:RELAY_KEY_SECRET
$env:DOTNET_GCHeapHardLimit = "0x10000000"
$env:DOTNET_gcServer = "0"
$p = Start-Process dotnet -ArgumentList @("run","-c","Release","--urls",$Url) -WorkingDirectory $relayDir -PassThru -NoNewWindow
$p.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::BelowNormal
$p.ProcessorAffinity = [IntPtr](1 -shl $Core)
Write-Host "Relay started (pid $($p.Id)) on $Url, Below Normal, core $Core. Ctrl+C to stop."
$p.WaitForExit()
