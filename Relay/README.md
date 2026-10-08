# OpenCiv3 relay

The relay lets players join each other's games over the internet with a
join code, like `KQ7-4MZ`, without port forwarding or a VPN app. The host
chooses **Host Online** in its lobby and reads out the code; the others
choose **Join LAN Game** and type it under **Join online**.

Hosting on the local network keeps working as before, with or without a
relay.

## How it works

The host's game and each guest's game connect to the relay over a
WebSocket (`wss://`). The relay pairs them up by join code and passes the
game's bytes between them without reading them. The host's game is still
the only one that runs the game; the relay just stands in for the network
connections that port forwarding would otherwise give it.

- A host connects to `/host` and is given a join code for its room and a
  secret key. With the key it can claim the same code again
  (`/host?code=...&key=...`): after a network blip, or when it resumes the
  game with **Resume Last LAN Game** after its game closed. A room waits 10
  minutes for its host to come back, and a host with its key can claim its
  code even after that, as long as nobody else has it.
- A guest connects to `/join/{code}`. When a guest's connection or the
  host's drops, guests reconnect by themselves, as on a LAN.
- Messages between the host and the relay say which guest they're from or
  for, and the host sends bytes due to several guests (like the game's
  updates) once, for the relay to copy to each.
- The relay pings every connection every 15 seconds, and drops one that has
  been silent for 45.
- Both sides say which version of the relay protocol and of the game they
  run, so an out-of-date game gets a clear message rather than a broken
  game.

The full protocol is described in [`Protocol/RelayProtocol.cs`](Protocol/RelayProtocol.cs),
which the game compiles too.

The relay logs who connects to which room and how many bytes they sent,
never what. Traffic is encrypted between each game and Caddy (TLS), but
not end to end: whoever runs the relay could read the game's bytes.

## Running it locally

You need the .NET 8 SDK (a newer SDK works too).

```sh
dotnet run --project Relay -- --urls http://localhost:5080
```

Then, in each game's `C7.ini` (next to the game, or in the per-user
OpenCiv3 folder):

```ini
[online]
relayUrl=ws://localhost:5080
```

`http://localhost:5080/health` says how many rooms and guests there are.

The tests run a relay in-process: `dotnet test Relay.Tests`.

## Deploying it on a small server

This sets the relay up on a Hetzner Cloud server, behind
[Caddy](https://caddyserver.com), which gets a free Let's Encrypt
certificate for your domain and renews it by itself. Any Linux server with
Docker works the same way. The smallest server is plenty: a game sends a
few kilobytes per update.

You need a domain (or a subdomain of one you have) whose DNS you can edit.
This guide uses `relay.example.org`; use yours instead.

### 1. Create the server

1. Sign up at [Hetzner Cloud](https://console.hetzner.cloud) and create a
   project.
2. **Add Server**: pick a location near your players, the **Ubuntu 24.04**
   image, and the smallest shared-vCPU type (such as CX22 or CAX11). Add
   your SSH public key (`~/.ssh/id_ed25519.pub`; make one with
   `ssh-keygen -t ed25519` if you don't have one).
3. Under **Firewalls**, create one that allows incoming TCP 22 (SSH),
   TCP 80 and TCP 443, and UDP 443, and apply it to the server.
4. Create the server and note its IPv4 address (and IPv6, if you like).

### 2. Point your domain at it

At your DNS provider, add a record for the relay's name:

| Type | Name    | Value                 |
|------|---------|-----------------------|
| A    | `relay` | the server's IPv4     |
| AAAA | `relay` | the server's IPv6 (optional) |

Wait until `dig +short relay.example.org` (or `nslookup relay.example.org`)
answers with the server's address. That can take a few minutes. Caddy
needs it to get the certificate.

### 3. Install Docker and the relay

```sh
ssh root@relay.example.org

# Docker, with its compose plugin
curl -fsSL https://get.docker.com | sh

# The relay's files: a clone of the repository (or copy the Relay folder
# over with scp -r Relay root@relay.example.org:/opt/relay)
git clone https://github.com/airmailxd/OpenCiv3.git /opt/openciv3
cd /opt/openciv3/Relay

# Its settings
cp .env.example .env
sed -i "s/^RELAY_DOMAIN=.*/RELAY_DOMAIN=relay.example.org/" .env
sed -i "s/^RELAY_KEY_SECRET=.*/RELAY_KEY_SECRET=$(openssl rand -hex 32)/" .env

docker compose up -d --build
```

Keep `RELAY_KEY_SECRET` the same from then on: it's what lets hosts claim
their codes again after the relay restarts.

### 4. Check it

```sh
curl https://relay.example.org/health
# {"status":"ok","version":1,"rooms":0,"hosts":0,"guests":0}

docker compose logs -f relay   # the relay's log
docker compose logs caddy      # if the certificate didn't come
```

### 5. Point the game at it

Each player can set it in their `C7.ini`:

```ini
[online]
relayUrl=wss://relay.example.org
```

Or, to make it everyone's default, change `OnlineRelay.DefaultUrl` in
[`C7Engine/Network/OnlineRelay.cs`](../C7Engine/Network/OnlineRelay.cs)
and ship a new build. Until a relay is set, **Host Online** and **Join
online** say that none is.

### Updating

```sh
cd /opt/openciv3 && git pull
cd Relay && docker compose up -d --build
```

Games in progress lose the relay for a moment and reconnect by themselves.

## Settings

Set these in `appsettings.json`, or as environment variables in
`docker-compose.yml` (like `Relay__MaxRooms: "500"`).

| Setting | Default | What it does |
|---------|---------|--------------|
| `Relay:MaxMessageBytes` | 16 MiB | Larger messages close the connection. |
| `Relay:MaxQueuedBytes` | 64 MiB | A connection with this much waiting to be sent to it is dropped. |
| `Relay:MaxRooms` | 1000 | Rooms at once, counting those waiting for their host. |
| `Relay:MaxGuestsPerRoom` | 16 | Guests in a room through the relay. |
| `Relay:RoomTtlSeconds` | 600 | How long a room waits for its host to come back. |
| `Relay:PingIntervalSeconds` | 15 | How often connections are pinged. |
| `Relay:IdleTimeoutSeconds` | 45 | Silence after which a connection is dropped. Keep it at least twice the ping interval. |
| `Relay:ConnectionsPerMinute` | 120 | Connections per minute from one address. |
| `Relay:RoomsPerHour` | 30 | Rooms made per hour from one address. |
| `Relay:FailedJoinsPerTenMinutes` | 20 | Wrong codes (or keys) from one address, after which it can't join for the rest of the ten minutes. |
| `Relay:KeySecret` | (random each start) | Makes the hosts' keys. Set it so hosts can claim their codes across restarts. |
| `Relay:TrustForwardedHeaders` | false | Take the client's address from Caddy's `X-Forwarded-For`. Only when the relay can't be reached except through the proxy. |
| `Kestrel:Limits:MaxConcurrentUpgradedConnections` | 5000 | WebSocket connections at once. |

Join codes are six characters from `ABCDEFGHJKMNPQRSTUVWXYZ23456789`
(no 0/O or 1/I/L), so there are almost 900 million of them; with the
limit on wrong codes, guessing one in use isn't practical.
