# Run once in an ADMIN PowerShell. Opens only the tunnel port and lets only the tunnel reach the relay.
New-NetFirewallRule -DisplayName "WireGuard civ in" -Direction Inbound -Protocol UDP -LocalPort 51820 -Action Allow
New-NetFirewallRule -DisplayName "Relay 8080 from tunnel only" -Direction Inbound -Protocol TCP -LocalPort 8080 -RemoteAddress 10.8.0.0/24 -Action Allow
