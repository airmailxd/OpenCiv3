# Run a command so it can't slow the owner's PC: idle priority, only the
# first N cores, and a memory cap for the whole process tree (a Windows job
# object, so no Docker is needed). Headless tests never touch the mouse or
# keyboard.
#
#   powershell -File tools\advanced-ai-training\run-throttled.ps1 [-Cores 12] [-MemGB 8] -Run "<command line>"
#   powershell -File tools\advanced-ai-training\run-throttled.ps1 -Run "python tools\advanced-ai-training\train.py --out D:\runs\ladder --resume --parallel 6"
#
# Pause = stop the command (Ctrl+C or end the process tree); resume = run
# the same line again with --resume. A trial that was cut off restarts.
param(
	[int]$Cores = 12,
	[int]$MemGB = 8,
	[Parameter(Mandatory = $true)][string]$Run
)
$ErrorActionPreference = "Stop"
$total = [Environment]::ProcessorCount
if ($Cores -gt $total) { $Cores = $total }

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class JobLimit {
	[StructLayout(LayoutKind.Sequential)] struct BASIC {
		public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
		public uint LimitFlags; public UIntPtr MinWS, MaxWS;
		public uint ActiveProcessLimit; public UIntPtr Affinity;
		public uint PriorityClass, SchedulingClass;
	}
	[StructLayout(LayoutKind.Sequential)] struct IOC { public ulong a, b, c, d, e, f; }
	[StructLayout(LayoutKind.Sequential)] struct EXT {
		public BASIC Basic; public IOC Io;
		public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcess, PeakJob;
	}
	[DllImport("kernel32.dll")] static extern IntPtr CreateJobObject(IntPtr a, string n);
	[DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr j, int c, ref EXT i, int n);
	[DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr j, IntPtr p);
	public static void Apply(IntPtr process, ulong affinity, ulong memBytes) {
		IntPtr job = CreateJobObject(IntPtr.Zero, null);
		var e = new EXT();
		// affinity (0x10) | priority class (0x20) | job memory (0x200)
		e.Basic.LimitFlags = 0x10 | 0x20 | 0x200;
		e.Basic.Affinity = new UIntPtr(affinity);
		e.Basic.PriorityClass = 0x40; // IDLE_PRIORITY_CLASS
		e.JobMemoryLimit = new UIntPtr(memBytes);
		if (!SetInformationJobObject(job, 9, ref e, Marshal.SizeOf(typeof(EXT)))) throw new Exception("SetInformationJobObject " + Marshal.GetLastWin32Error());
		if (!AssignProcessToJobObject(job, process)) throw new Exception("AssignProcessToJobObject " + Marshal.GetLastWin32Error());
	}
}
"@

$mask = [uint64]([math]::Pow(2, $Cores) - 1)
[JobLimit]::Apply([System.Diagnostics.Process]::GetCurrentProcess().Handle, $mask, [uint64]$MemGB * 1GB)
Write-Host "throttled: $Cores of $total cores, idle priority, $MemGB GB cap"
cmd.exe /c $Run
exit $LASTEXITCODE
