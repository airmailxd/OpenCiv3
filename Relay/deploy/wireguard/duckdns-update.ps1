# Updates a DuckDNS hostname with this PC's current public IPv4.
# Run by Task Scheduler every 5 minutes (see duckdns-install.ps1). Can also be run by hand.
# The token is read from a Windows-encrypted file outside the repo; it is never logged or printed.
param(
    [string]$Domain = 'airmailxd-relay',   # DuckDNS subdomain, without .duckdns.org
    [string]$StateDir = (Join-Path $env:LOCALAPPDATA 'C3Relay')
)
$ErrorActionPreference = 'Stop'
$tokenFile = Join-Path $StateDir 'duckdns.token.xml'
$logFile = Join-Path $StateDir 'duckdns.log'
New-Item -ItemType Directory -Force -Path $StateDir | Out-Null

function Write-Result([string]$msg, [int]$code) {
    $line = '{0:u} {1}' -f (Get-Date), $msg
    Add-Content -Path $logFile -Value $line
    if ((Test-Path $logFile) -and (Get-Item $logFile).Length -gt 100KB) {
        Get-Content $logFile -Tail 200 | Set-Content $logFile
    }
    Write-Host $msg
    exit $code
}

if (-not (Test-Path $tokenFile)) { Write-Result 'FAIL: no token file. Run duckdns-install.ps1 first.' 2 }

try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $ip = $null
    foreach ($src in 'https://api.ipify.org', 'https://ipv4.icanhazip.com') {
        try {
            $c = (Invoke-RestMethod -Uri $src -TimeoutSec 15).ToString().Trim()
            if ($c -match '^\d{1,3}(\.\d{1,3}){3}$') { $ip = $c; break }
        } catch { }
    }
    if (-not $ip) { Write-Result 'FAIL: could not detect public IPv4.' 3 }

    $secure = Import-Clixml $tokenFile
    $token = [Net.NetworkCredential]::new('', $secure).Password
    try {
        $resp = (Invoke-RestMethod -Uri "https://www.duckdns.org/update?domains=$Domain&token=$token&ip=$ip" -TimeoutSec 20).ToString().Trim()
    } catch {
        # Do not print $_ : the message can contain the request URL with the token.
        Write-Result "FAIL: DuckDNS request error (HTTP/network). IP was $ip." 4
    } finally { $token = $null }

    if ($resp -like 'OK*') { Write-Result "OK: $Domain.duckdns.org -> $ip" 0 }
    else { Write-Result "FAIL: DuckDNS answered '$resp' (wrong token or name?). IP was $ip." 5 }
} catch {
    Write-Result 'FAIL: unexpected error.' 1
}
