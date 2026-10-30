# Run the Advanced AI training ladder yourself. It runs from its own git
# worktree (so builds in C:\Dev\C3 don't disturb it), throttled by
# run-throttled.ps1, with no paid Claude calls.
#
#   Start a new ladder (a new dated folder under C:\Dev\ladder-runs):
#     powershell -ExecutionPolicy Bypass -File tools\advanced-ai-training\run-training.ps1
#   Resume the newest ladder (or name one with -Out):
#     powershell -ExecutionPolicy Bypass -File tools\advanced-ai-training\run-training.ps1 -Resume
#   Lighter or heavier on the PC:
#     ... -Parallel 4 -Cores 8 -MemGB 6
#   Extra train.py options, e.g. fewer games per trial:
#     ... -TrainArgs "--games 8 --rounds 3"
#
# Pause: Ctrl+C. Resume: the same line with -Resume. A trial cut off
# mid-way restarts; finished trials are kept. Progress is in <Out>\log.md;
# the page <Out>\dashboard.html is rebuilt when the run stops.
param(
	[string]$Out = "",
	[switch]$Resume,
	[int]$Parallel = 6,
	[int]$Cores = 12,
	[int]$MemGB = 8,
	[string]$TrainArgs = "",
	[string]$Runs = "C:\Dev\ladder-runs",
	[string]$Tree = "C:\Dev\C3-training",
	[string]$Ref = "Development"
)
$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

# Pick the output folder.
if ($Out -eq "") {
	if ($Resume) {
		$last = Get-ChildItem $Runs -Directory -ErrorAction SilentlyContinue |
			Where-Object { Test-Path (Join-Path $_.FullName "state.json") } |
			Sort-Object { (Get-Item (Join-Path $_.FullName "state.json")).LastWriteTime } -Descending |
			Select-Object -First 1
		if (-not $last) { throw "No ladder with a state.json in $Runs to resume. Leave out -Resume to start one." }
		$Out = $last.FullName
	} else {
		$Out = Join-Path $Runs ("ladder-" + (Get-Date -Format "yyyy-MM-dd-HHmm"))
	}
}
$Out = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Out)
if ($Resume -and -not (Test-Path (Join-Path $Out "state.json"))) { throw "$Out has no state.json to resume." }
if (-not $Resume -and (Test-Path (Join-Path $Out "state.json"))) { throw "$Out already has a ladder. Add -Resume, or pick another -Out." }
New-Item -ItemType Directory -Force $Out | Out-Null

# The worktree: create it, or move it to the latest $Ref. Never touches
# the main checkout's uncommitted work.
if (-not (Test-Path (Join-Path $Tree ".git"))) {
	git -C $repo worktree add --detach $Tree $Ref
	if ($LASTEXITCODE -ne 0) { throw "git worktree add failed" }
} else {
	if (git -C $Tree status --porcelain) { throw "$Tree has local changes; commit or discard them first." }
	git -C $Tree checkout --quiet --detach $Ref
	if ($LASTEXITCODE -ne 0) { throw "git checkout in $Tree failed" }
}
$commit = git -C $Tree log -1 --format="%h %s"

# No paid calls: the trials only call Claude when this is set.
Remove-Item Env:C7_TRIAL_MODEL -ErrorAction SilentlyContinue

$cmd = "python tools\advanced-ai-training\train.py --out `"$Out`" --parallel $Parallel"
if ($Resume) { $cmd += " --resume" }
if ($TrainArgs -ne "") { $cmd += " $TrainArgs" }

Write-Host "Code:     $commit ($Tree)"
Write-Host "Output:   $Out"
Write-Host "Progress: Get-Content `"$Out\log.md`" -Wait"
Write-Host "Pause with Ctrl+C; resume with -Resume."
Write-Host ""

Push-Location $Tree
try {
	powershell -ExecutionPolicy Bypass -File (Join-Path $Tree "tools\advanced-ai-training\run-throttled.ps1") -Cores $Cores -MemGB $MemGB -Run $cmd
	$code = $LASTEXITCODE
} finally {
	if (Test-Path (Join-Path $Out "state.json")) {
		python tools\advanced-ai-training\dashboard.py --out $Out
		Write-Host "Dashboard: $Out\dashboard.html"
	}
	Pop-Location
}
exit $code
