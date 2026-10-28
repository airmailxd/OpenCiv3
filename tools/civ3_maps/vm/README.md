# Map-making VMs

Several VirtualBox VMs each run Civ3 and `generate.py` (Quick Start) at once,
writing their maps to the corpus folder `vm_civ3gen-<n>`. Each VM has its own
copy of Civ3, since Civ3 writes its autosaves into its own folder, and its
own screen, mouse and keyboard, so the host stays free.

Setup, once (VirtualBox 7.2 on the host):

1. Put the Windows 10 Enterprise evaluation ISO in `E:\VMs\iso` and fill the
   setup folder `E:\VMs\share`: `civ3\` (the Civ3 install, without
   `Conquests\Saves`), `python-3.11.9-amd64.exe`, and `wheels\` (Pillow for
   Python 3.11, `pip download pillow --only-binary=:all: --platform win_amd64
   --python-version 3.11 -d wheels`).
2. `build_template.ps1`: creates the template VM and installs Windows,
   unattended, with Guest Additions, UAC off and an automatic logon.
3. `setup_template.ps1`: once Windows is up, runs `guest_setup.ps1` in it
   (Python, Pillow, Civ3 to `C:\Civ3`, no screen saver or lock screen,
   `run_vm.cmd` at logon), shuts it down and takes the `base` snapshot.

Running: `clone.ps1 -Count 6 -Maps 2000` makes the linked clones (once) and
starts them headless; `stop.ps1` shuts them down. Each VM's log is
`run.log` in its corpus folder. `run_vm.cmd` and the scripts are read from
the host, so changes to them reach the VMs at their next start.

Don't change `run_vm.cmd` while VMs are running it: cmd reads a batch file
as it goes, so an edit under it breaks the loop and the VM stops making
maps. Stop the VMs (or reset them after the change). generate.py and
civ3win.py can change any time; a VM picks them up when its Python next
starts.

Watching: `watch.ps1` shows every VM's screen at http://127.0.0.1:8770,
read-only. To use a VM yourself, put a file named `PAUSE` in its corpus
folder (the script stops between maps), open it with Show in VirtualBox
Manager, and delete `PAUSE` when done. The same works for a run on the
host.

The VMs share the host folders as `\\VBOXSVR\setup` (read-only),
`\\VBOXSVR\tools` (this folder's parent, read-only) and `\\VBOXSVR\corpus`.
