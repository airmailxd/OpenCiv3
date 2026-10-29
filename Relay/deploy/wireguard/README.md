# Relay over WireGuard (Windows host)

Templates only: no real keys live in this folder.
- `server-civ.conf.template`: server tunnel for WireGuard for Windows.
- `friend.conf.template`: what each friend pastes into their own app.
- `firewall.ps1`: two Windows Firewall rules (admin PowerShell).
- `start-relay.ps1`: starts the relay capped (Below Normal, one core, 256 MB).

Friends type `ws://10.8.0.1:8080` in Settings → Online. Only UDP 51820 is forwarded on the router.

## DuckDNS updater (keeps `airmailxd-relay.duckdns.org` on your home IP)
- `duckdns-install.ps1`: run once in a normal PowerShell (no admin needed). It asks for the DuckDNS token with hidden input, stores it encrypted for your Windows user in `%LOCALAPPDATA%\C3Relay\`, and schedules `duckdns-update.ps1` every 5 minutes.
- `duckdns-update.ps1`: detects the public IPv4, calls the DuckDNS HTTPS API, writes `OK`/`FAIL` lines (never the token) to `%LOCALAPPDATA%\C3Relay\duckdns.log`.
- Check: `Get-Content $env:LOCALAPPDATA\C3Relay\duckdns.log -Tail 3`. Remove: `Unregister-ScheduledTask 'C3 DuckDNS update' -Confirm:$false`.
- Friend configs use `Endpoint = airmailxd-relay.duckdns.org:51820`.

## Adding a friend (no keys for them to handle)
1. Run `.\add-friend.ps1 -Name Sam` on the host. It creates `%LOCALAPPDATA%\C3Relay\friends\Sam.conf` (their whole config, outside the repo) and `Sam.peer.txt`.
2. Paste `Sam.peer.txt` into the `civ` tunnel (WireGuard app > civ > Edit > Save) and keep the relay running.
3. Send Sam only `Sam.conf` (privately) and `friend-guide.md`. Sam imports the file and clicks Activate.
4. Invite codes shared as `CODE@ws://10.8.0.1:8080` work without any Settings step.

Trade-off: the host generates the friend's key, so the host sees it. The key only opens the relay, nothing else. If a `.conf` leaks, delete that peer and run the script again under a new name.
