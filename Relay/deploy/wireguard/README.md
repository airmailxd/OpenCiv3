# Relay over WireGuard (Windows host)

Templates only: no real keys live in this folder.
- `server-civ.conf.template`: server tunnel for WireGuard for Windows.
- `friend.conf.template`: what each friend pastes into their own app.
- `firewall.ps1`: two Windows Firewall rules (admin PowerShell).
- `start-relay.ps1`: starts the relay capped (Below Normal, one core, 256 MB).

Friends type `ws://10.8.0.1:8080` in Settings → Online. Only UDP 51820 is forwarded on the router.
