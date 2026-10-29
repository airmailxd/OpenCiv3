<#
Makes one ready-to-import WireGuard file for a friend, so they never handle keys.
Run in a normal PowerShell on the host PC:
  .\add-friend.ps1 -Name Sam
Output (outside the repo): %LOCALAPPDATA%\C3Relay\friends\<Name>.conf  (+ <Name>.peer.txt)
The .conf holds the friend's private key: send it only to that friend, privately.
Needs the server's public key: pass -ServerPublicKey, or run once as admin so it can read it.
#>
param(
  [Parameter(Mandatory)][string]$Name,
  [string]$ServerPublicKey,
  [string]$Endpoint = 'airmailxd-relay.duckdns.org:51820',
  [string]$Tunnel = 'civ'
)
$ErrorActionPreference = 'Stop'
if ($Name -notmatch '^[A-Za-z0-9_-]+$') { throw 'Name: letters, digits, - and _ only.' }
$wg = 'C:\Program Files\WireGuard\wg.exe'
if (-not (Test-Path $wg)) { throw 'WireGuard for Windows is not installed.' }
$dir = Join-Path $env:LOCALAPPDATA 'C3Relay\friends'
New-Item -ItemType Directory -Force $dir | Out-Null
if (-not $ServerPublicKey) {
  $ServerPublicKey = (& $wg show $Tunnel public-key 2>$null)
  if (-not $ServerPublicKey) { throw "Could not read the server key. Run as admin, or pass -ServerPublicKey." }
}
# next free address: one above the highest one already issued (10.8.0.2 first)
$used = Get-ChildItem $dir -Filter *.conf -ErrorAction SilentlyContinue | ForEach-Object {
  if ((Get-Content $_.FullName -Raw) -match 'Address = 10\.8\.0\.(\d+)/32') { [int]$Matches[1] } }
$n = if ($used) { ($used | Measure-Object -Maximum).Maximum + 1 } else { 2 }
if ($n -gt 254) { throw 'Out of addresses.' }
if (Test-Path (Join-Path $dir "$Name.conf")) { throw "$Name.conf already exists. Pick another name or delete it." }
$priv = (& $wg genkey)
$pub  = ($priv | & $wg pubkey)
$psk  = (& $wg genpsk)
@"
[Interface]
Address = 10.8.0.$n/32
PrivateKey = $priv

[Peer]
PublicKey = $ServerPublicKey
PresharedKey = $psk
Endpoint = $Endpoint
AllowedIPs = 10.8.0.1/32
PersistentKeepalive = 25
"@ | Set-Content -Encoding ascii (Join-Path $dir "$Name.conf")
@"
[Peer]
# $Name
PublicKey = $pub
PresharedKey = $psk
AllowedIPs = 10.8.0.$n/32
"@ | Set-Content -Encoding ascii (Join-Path $dir "$Name.peer.txt")
Write-Host "Friend file : $dir\$Name.conf   (send this to $Name, privately)"
Write-Host "Server peer : $dir\$Name.peer.txt"
Write-Host ""
Write-Host "Now paste the .peer.txt text into the civ tunnel (WireGuard app > civ > Edit > Save)."
