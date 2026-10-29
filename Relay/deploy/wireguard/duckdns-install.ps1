# One-time setup for the DuckDNS updater. Run in a normal (non-admin) PowerShell as the user who plays.
# Safe to run again: keeps the saved token unless -NewToken is given.
# 1. Asks for the DuckDNS token (hidden input) and stores it encrypted for this Windows user, outside the repo.
# 2. Registers a Task Scheduler job that runs duckdns-update.ps1 every 5 minutes.
# 3. Runs it once and reports the result.
param([string]$Domain = 'airmailxd-relay', [switch]$NewToken)   # -NewToken: ask for the token again
$ErrorActionPreference = 'Stop'
$stateDir = Join-Path $env:LOCALAPPDATA 'C3Relay'
New-Item -ItemType Directory -Force -Path $stateDir | Out-Null
$script = Join-Path $PSScriptRoot 'duckdns-update.ps1'

$tokenFile = Join-Path $stateDir 'duckdns.token.xml'
if ($NewToken -or -not (Test-Path $tokenFile)) {
    $secure = Read-Host -AsSecureString 'Paste your DuckDNS token (input is hidden)'
    $secure | Export-Clixml $tokenFile   # DPAPI: only this user on this PC can read it
}

# conhost --headless starts PowerShell with no console window at all; -WindowStyle Hidden alone still flashes one.
$action = New-ScheduledTaskAction -Execute 'conhost.exe' `
    -Argument "--headless powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$script`" -Domain $Domain"
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 5)
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Minutes 2) -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName 'C3 DuckDNS update' -Action $action -Trigger $trigger -Settings $settings `
    -Description 'Keeps the OpenCiv3 relay hostname pointing at this PC' -Force | Out-Null

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $script -Domain $Domain
Write-Host "Log: $(Join-Path $stateDir 'duckdns.log')"
