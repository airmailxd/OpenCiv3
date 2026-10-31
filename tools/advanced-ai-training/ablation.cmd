@echo off
rem ZUR-119 ablation: 9 exam runs (defaults, each of 7 categories at 0, all at 50) on the ZUR-108 Regent seeds.
rem Run only when no other long run is going and the owner's hours allow. Usage: ablation.cmd <outdir>
rem Skips any run that already has done.flag, so it can be restarted.
setlocal
cd /d "%~dp0..\.."
set OUT=%~1
if "%OUT%"=="" set OUT=C:\Dev\ladder-runs\zur119
set CATS=Expansion Workers Research Military Diplomacy Infrastructure Wonders
call :one defaults ""
call :one control50 "WeightExpansion=50,WeightWorkers=50,WeightResearch=50,WeightMilitary=50,WeightDiplomacy=50,WeightInfrastructure=50,WeightWonders=50"
for %%C in (%CATS%) do call :one no%%C "Weight%%C=0"
exit /b 0
:one
if exist "%OUT%\%1\done.flag" exit /b 0
mkdir "%OUT%\%1" 2>nul
set C7_TRIAL_OUT=%OUT%\%1\results.txt
set C7_TRIAL_STATE=%OUT%\%1\state.log
set C7_TRIAL_JSON=%OUT%\%1\results.json
set C7_TRIAL_EXPERIMENTS=
set C7_TRIAL_ADVANCED=1
set C7_TRIAL_PAIRED=0
set C7_TRIAL_GAMES=100
set C7_TRIAL_DIFFICULTY=2
set C7_TRIAL_SEED=7500
set C7_TRIAL_TURNS=200
set C7_TRIAL_CIVS=8
set C7_ADVANCED_TUNING=%~2
set C7_TRIAL_MODEL=
"C:\Program Files\dotnet\dotnet.exe" test EngineTests --no-build --filter "FullyQualifiedName~AiTrialTest&FullyQualifiedName!~EngineTests.Network." > "%OUT%\%1\test.log" 2>&1
if not errorlevel 1 echo done > "%OUT%\%1\done.flag"
exit /b 0
