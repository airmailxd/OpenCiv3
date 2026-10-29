# Running long jobs without slowing the owner's PC

The owner's ruling (ZUR-1): training and AI trials may run at any time, as long as they never take over the mouse and keyboard. They are headless, so the only cost is CPU and memory. `tools/advanced-ai-training/run-throttled.ps1` caps both: the whole process tree gets the first 12 of 20 cores, idle priority and 8 GB (a Windows job object; no Docker needed). Change with `-Cores` and `-MemGB`.

Start, or resume after a pause (the same line):

```powershell
powershell -ExecutionPolicy Bypass -File tools\advanced-ai-training\run-throttled.ps1 -Run "python tools\advanced-ai-training\train.py --out <dir> --resume --parallel 6"
```

- Use `--parallel 6` or less: each parallel trial is its own process and the 8 GB is shared.
- **Pause:** stop the command (Ctrl+C, or end the process tree). Finished trials are kept; a trial cut off mid-way restarts.
- **Resume:** run the same line again.
- A single trial works the same way: set the `C7_TRIAL_*` variables, then `-Run "\"C:\Program Files\dotnet\dotnet.exe\" test EngineTests --no-build --filter \"FullyQualifiedName~AiTrialTest\""`. From WSL, list the variables in `WSLENV` so they cross over.
- Checked 2026-10-29: a 2-game, 60-turn paired trial (seed 1000, 4 civs, Regent) gave identical results on the host and throttled (the games are deterministic); only the timing line differed.
