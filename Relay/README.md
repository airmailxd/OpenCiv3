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
- A host can list its game publicly (**List publicly** in its lobby), and
  players find it under **Browse Online Games**. The relay keeps the list
  in memory: each relay has its own. A listing goes when the host unlists
  it or leaves, or after 90 seconds without the host listing it again
  (hosts do every 30 seconds). `GET /games` returns the list as JSON, and
  takes `?version=11` (the game's protocol version), `?notFull=true`,
  `?noPassword=true` and `?limit=50`. A listing says whether the game has a
  password, never the password itself.
- A host can ban a guest. The relay then turns that guest's address away
  from the host's room, and gives the host a key standing for the address
  (not the address itself), which the host gives back to have the ban again
  after the relay restarts or when it resumes the game.

The full protocol is described in [`Protocol/RelayProtocol.cs`](Protocol/RelayProtocol.cs),
which the game compiles too.

The relay logs who connects to which room and how many bytes they sent,
never what; of a game listed publicly, only its code. Traffic is encrypted between each game and Caddy (TLS), but
not end to end: whoever runs the relay could read the game's bytes.

## Running it locally

You need the .NET 8 SDK (a newer SDK works too).

```sh
dotnet run --project Relay -- --urls http://localhost:5080
```

Then, in each game, type `ws://localhost:5080` in **Settings → Online →
Relay server**, or put it in the game's `C7.ini` (next to the game, or in
the per-user OpenCiv3 folder):

```ini
[online]
relayUrl=ws://localhost:5080
```

`http://localhost:5080/health` says how many rooms and guests there are,
and how many games are listed; `http://localhost:5080/games` lists them.

The tests run a relay in-process: `dotnet test Relay.Tests`.

## Running your own relay

Anyone can run a relay for their friends or community. You need a small
Linux server that's always on, and a name for it on the internet, so that
players' games can reach it at an address like `relay.example.org`. The
relay runs in Docker behind [Caddy](https://caddyserver.com), which gets a
free Let's Encrypt certificate for that name and renews it by itself.

Pick one option for each of the two choices below, then follow the common
steps. Any Linux server with Docker works the same way; these are just the
two this guide walks through.

**Where it runs**

| Option | Cost | Good to know |
|--------|------|--------------|
| [A. Oracle Cloud Always Free](#server-a-oracle-cloud-always-free) | Free | Arm server. Sign-up needs a credit card, free servers are sometimes "out of capacity", and an idle free server can be reclaimed unless you upgrade the account to Pay As You Go (still free within the limits). |
| [B. Hetzner Cloud](#server-b-hetzner-cloud) | A few euros a month | Simplest and most predictable. |

**Its name**

| Option | Cost | Good to know |
|--------|------|--------------|
| [A. DuckDNS](#name-a-duckdns) | Free | A name like `myrelay.duckdns.org`. Needs a (free) account. |
| [B. Your own domain](#name-b-your-own-domain) | What you pay for the domain | A name like `relay.example.org`. |
| [C. sslip.io](#name-c-sslipio-no-account) | Free | A name made from the server's address, like `203-0-113-7.sslip.io`. No account, but the name changes if the address does. |

The smallest server is plenty: a game sends a few kilobytes per update.
The relay's Docker image builds for both x86-64 (amd64) and Arm (arm64)
servers: its base images (`mcr.microsoft.com/dotnet/sdk:8.0`,
`mcr.microsoft.com/dotnet/aspnet:8.0` and `caddy:2`) are published for
both, and the relay has been built and started for `linux/arm64`.

Throughout, `relay.example.org` stands for your relay's name, and
`203.0.113.7` for your server's public IP address; use yours instead.

### Server A: Oracle Cloud Always Free

Oracle's Always Free tier includes Arm servers (the `VM.Standard.A1.Flex`
shape). As of 2026 the free allowance for an Always Free account is
**2 OCPUs and 12 GB of memory** in all, across all your Arm servers (it
was 4 OCPUs and 24 GB until June 2026), and **10 TB a month** of outgoing
traffic. One server with 1 OCPU and 6 GB is far more than the relay needs.

1. **Sign up** at [oracle.com/cloud/free](https://www.oracle.com/cloud/free/).
   Most people need a mobile phone number and a credit card; the card
   isn't charged unless you upgrade.
2. **Choose the home region carefully.** You pick it at sign-up, Always
   Free servers can only be made there, and it can't easily be changed.
   Pick one near your players.
3. **Create the server.** In the console, open **Compute → Instances →
   Create instance**:
   - **Image:** Canonical Ubuntu 24.04 (the console picks its Arm build
     for an Arm shape).
   - **Shape:** Ampere, `VM.Standard.A1.Flex`, 1 OCPU and 6 GB of memory
     (it says "Always Free-eligible").
   - **Networking:** a public subnet, with **Assign a public IPv4
     address** on.
   - **SSH keys:** add your public key (`~/.ssh/id_ed25519.pub`; make one
     with `ssh-keygen -t ed25519` if you don't have one).

   If it says **Out of capacity** (or "out of host capacity"), Oracle has
   no free Arm servers left in that place just now. Try another
   availability domain in the same form, or try again later (often hours
   later, or at a quieter time of day). Upgrading to Pay As You Go also
   helps.
4. Note the server's **public IP address** on its page. You log in as
   `ubuntu`, not `root`: `ssh ubuntu@203.0.113.7`.
5. **Open the ports in Oracle's network.** By default only SSH (port 22)
   gets in. Go to **Networking → Virtual cloud networks →** your network
   **→ Security → Security lists → Default Security List → Add Ingress
   Rules**, and add three rules, each with source CIDR `0.0.0.0/0`:
   - IP protocol **TCP**, destination port `80`
   - IP protocol **TCP**, destination port `443`
   - IP protocol **UDP**, destination port `443`

   If your server uses a network security group (NSG) instead, add the
   same rules there. Either one is enough: Oracle lets traffic in if any
   of them allows it.
6. **Open the same ports on the server itself.** Oracle's images have
   their own firewall that also lets in only SSH. On Ubuntu, Oracle says
   **not to use `ufw`** (it can remove rules the server needs to boot) and
   to edit `/etc/iptables/rules.v4` instead. This adds the three ports
   right after the SSH rule and applies them:

   ```sh
   sudo sed -i '/--dport 22 -j ACCEPT/a\
   -A INPUT -p tcp -m state --state NEW -m tcp --dport 80 -j ACCEPT\
   -A INPUT -p tcp -m state --state NEW -m tcp --dport 443 -j ACCEPT\
   -A INPUT -p udp -m state --state NEW -m udp --dport 443 -j ACCEPT' /etc/iptables/rules.v4
   sudo iptables-restore < /etc/iptables/rules.v4
   ```

   The file is read again at each boot, so this lasts. Do it **before
   installing Docker**: `iptables-restore` clears the rules Docker adds,
   so if you ever run it again later, follow it with
   `sudo systemctl restart docker`.

   On an Oracle Linux image, which uses firewalld, it's instead:

   ```sh
   sudo firewall-cmd --permanent --add-service=http --add-service=https --add-port=443/udp
   sudo firewall-cmd --reload
   ```
7. **Keep it from being reclaimed.** Oracle may stop an Always Free
   server it considers idle: one whose CPU (95th percentile), network and
   memory use all stay under 20% for 7 days. A relay is idle most of the
   time, so this will likely happen to it. Oracle's way to avoid it is to
   upgrade the account to **Pay As You Go** (in the console, **Billing →
   Upgrade and Manage Payment**). Always Free resources stay free after
   upgrading; set a budget alert (**Billing → Budgets**) so you'd hear of
   anything that isn't. Without upgrading, check on the server now and
   then and start it again if Oracle stopped it.

Then choose a name for it, below.

### Server B: Hetzner Cloud

1. Sign up at [Hetzner Cloud](https://console.hetzner.cloud) and create a
   project.
2. **Add Server**: pick a location near your players, the **Ubuntu 24.04**
   image, and the smallest shared-vCPU type (such as CX23, or the Arm
   CAX11; see [hetzner.com/cloud](https://www.hetzner.com/cloud/) for the
   current types and prices). Add your SSH public key
   (`~/.ssh/id_ed25519.pub`; make one with `ssh-keygen -t ed25519` if you
   don't have one).
3. Under **Firewalls → Create Firewall**, add inbound rules for TCP 22
   (SSH), TCP 80, TCP 443 and UDP 443, each from any IPv4 and IPv6, and
   apply the firewall to the server. Once a firewall is applied, anything
   it doesn't allow is dropped.
4. Create the server and note its IPv4 address (and IPv6, if you like).
   You log in as `root`: `ssh root@203.0.113.7`.

Hetzner's Ubuntu image has no firewall of its own switched on, so nothing
else needs opening.

### Name A: DuckDNS

[DuckDNS](https://www.duckdns.org) gives free names like
`myrelay.duckdns.org`.

1. Go to [duckdns.org](https://www.duckdns.org) and sign in (with
   GitHub, Google, Reddit or another account it offers).
2. Type the name you want under **sub domain** and click **add domain**.
3. In the **current ip** box next to it, type your server's public IPv4
   address and click **update ip**. (You can also give it an IPv6 address,
   but DuckDNS doesn't find one by itself.)
4. Note your **token** at the top of the page. Keep it private: it lets
   anyone change your names.
5. **Keep it up to date.** Server addresses rarely change, but if yours
   ever does, this updates DuckDNS within five minutes. On the server:

   ```sh
   mkdir -p ~/duckdns && cd ~/duckdns
   cat > duck.sh <<'EOF'
   echo url="https://www.duckdns.org/update?domains=myrelay&token=YOUR-TOKEN&ip=" | curl -k -o ~/duckdns/duck.log -K -
   EOF
   chmod 700 duck.sh
   ./duck.sh && cat duck.log    # says OK
   (crontab -l 2>/dev/null; echo '*/5 * * * * ~/duckdns/duck.sh >/dev/null 2>&1') | crontab -
   ```

   Use your name without `.duckdns.org` for `domains`, and your token.
   Leaving `ip=` empty lets DuckDNS use the address the request comes
   from, which is the server's.

Your relay's name is then `myrelay.duckdns.org`.

### Name B: your own domain

At your domain's DNS provider, add a record for the relay's name:

| Type | Name    | Value                 |
|------|---------|-----------------------|
| A    | `relay` | the server's IPv4     |
| AAAA | `relay` | the server's IPv6 (optional; only if the server has one) |

Your relay's name is then `relay.` followed by your domain.

### Name C: sslip.io (no account)

[sslip.io](https://sslip.io) answers any name with an IP address in it
with that address, with nothing to sign up for. For a server at
`203.0.113.7`, the name `203-0-113-7.sslip.io` already works. Let's
Encrypt gives certificates for these names.

The catch is that the name is the address: if the server's address
changes, so does the relay's name, and every player has to change theirs.
It's best for trying things out; DuckDNS is as free and keeps its name.

### Check the name

Whichever you chose, wait until the name answers with the server's
address. That can take a few minutes. Caddy needs it to get the
certificate.

```sh
nslookup relay.example.org     # or: dig +short relay.example.org
```

### Install Docker and the relay

On the server (on Oracle, after opening its firewall as above):

```sh
# Docker, with its compose plugin. The convenience script is the quickest
# way; Docker also documents installing from its apt repository:
# https://docs.docker.com/engine/install/ubuntu/
curl -fsSL https://get.docker.com | sudo sh

# The relay's files: a clone of the repository (or copy the Relay folder
# over with scp -r Relay you@203.0.113.7:/opt/openciv3/Relay)
sudo git clone https://github.com/airmailxd/OpenCiv3.git /opt/openciv3
cd /opt/openciv3/Relay

# Its settings
sudo cp .env.example .env
sudo sed -i "s/^RELAY_DOMAIN=.*/RELAY_DOMAIN=relay.example.org/" .env
sudo sed -i "s/^RELAY_KEY_SECRET=.*/RELAY_KEY_SECRET=$(openssl rand -hex 32)/" .env

sudo docker compose up -d --build
```

`RELAY_DOMAIN` is the name alone, without `https://` or `wss://`.

`RELAY_KEY_SECRET` is made **once** and kept forever: it's what lets
hosts claim their join codes and their bans again after the relay
restarts. Never generate a new one for a running relay. If it changes,
games hosted before can't get their codes back. Keep a copy somewhere
safe off the server (see [Backups](#backups)).

The first build takes a few minutes (longer on a small Arm server).

### Check it

```sh
curl https://relay.example.org/health
# {"status":"ok","version":1,"rooms":0,"hosts":0,"guests":0,"listed":0}
```

If that fails:

- `sudo docker compose logs caddy` says why the certificate didn't come.
  Usually the name doesn't point at the server yet, or ports 80 and 443
  aren't open (on Oracle, check **both** the security list and the
  server's own firewall).
- `sudo docker compose ps` should show both `relay` and `caddy` running.
- `sudo docker compose logs relay` shows the relay's own log. It won't
  start without `RELAY_KEY_SECRET`.

### Tell your players

Players can use your relay in either of two ways:

- **Set it once:** in the game's **Settings**, under **Online**, type the
  relay's name (like `relay.example.org`) in **Relay server** and click
  **Test**. Hosting and joining online then go through it, and **Browse
  Online Games** lists the games hosted on it.
- **Just share invites:** a host can type the relay's name in **Relay
  server** in its lobby before choosing **Host Online**. The join code it
  then shows (and **Copy Code** copies) includes the relay, like
  `KQ7-4MZ@relay.example.org`, so players who type it under **Join
  online** reach your relay without setting anything.

Either way, everyone needs the same version of the game.

### Updating

```sh
cd /opt/openciv3 && sudo git pull
cd Relay && sudo docker compose up -d --build
```

Games in progress lose the relay for a moment and reconnect by themselves.
Update when a new version of the game comes out: a relay that's too old
for a game says so rather than letting it play.

### Logs

```sh
cd /opt/openciv3/Relay
sudo docker compose logs -f relay    # the relay's log, as it happens
sudo docker compose logs caddy       # certificates and connections
```

The relay logs who connects to which room and how much they sent, never
what.

### Backups

The only thing to back up is `/opt/openciv3/Relay/.env`, for its
`RELAY_KEY_SECRET`. Copy it to your own computer once:

```sh
scp you@203.0.113.7:/opt/openciv3/Relay/.env relay.env
```

If you ever move the relay to a new server, put the same `.env` there
(and point the name at the new address). Caddy's certificates are kept in
a Docker volume and are fetched again by themselves if lost. Rooms and
the public game list are in memory only and need no backup.

### Making it everyone's default

`OnlineRelay.DefaultUrl` in
[`C7Engine/Network/OnlineRelay.cs`](../C7Engine/Network/OnlineRelay.cs) is
the relay used when a player hasn't chosen one. Changing it there and
shipping a new build makes yours everyone's default. Until a relay is
set, **Host Online** and **Join online** say that none is.

### Sources

The provider details above come from their own documentation (checked
October 2026):

- Oracle: [Always Free resources](https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm)
  (A1 allowance, 10 TB egress, idle reclamation, home region, out of
  capacity), [Free Tier overview](https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier.htm)
  (sign-up, home region, upgrading),
  [security lists](https://docs.oracle.com/en-us/iaas/Content/Network/Concepts/securitylists.htm),
  [known issues: Ubuntu and UFW](https://docs.oracle.com/en-us/iaas/Content/Compute/known-issues.htm),
  and, for the June 2026 change to the A1 allowance,
  [InfoQ](https://www.infoq.com/news/2026/07/oracle-cloud-free-tier-limits/).
- Hetzner: [creating a firewall](https://docs.hetzner.com/cloud/firewalls/getting-started/creating-a-firewall/),
  [cloud plans](https://www.hetzner.com/cloud/).
- DuckDNS: [update API](https://www.duckdns.org/spec.jsp),
  [Linux cron install](https://www.duckdns.org/install.jsp).
- sslip.io: [sslip.io](https://sslip.io).
- Docker: [installing on Ubuntu](https://docs.docker.com/engine/install/ubuntu/).

## Settings

Set these in `appsettings.json`, or as environment variables in
`docker-compose.yml` (like `Relay__MaxRooms: "500"`).

| Setting | Default | What it does |
|---------|---------|--------------|
| `Relay:MaxHostMessageBytes` | 8 MiB | Larger messages from a host close its connection. A whole game is about 160 KB. |
| `Relay:MaxGuestMessageBytes` | 256 KiB | Larger messages from a guest close its connection. A guest's game sends a few hundred bytes at a time. |
| `Relay:MaxReceiveBufferBytes` | 512 MiB | Memory, all told, for large messages as they arrive; a large message past it is turned away. |
| `Relay:MaxQueuedBytes` | 64 MiB | A connection with this much waiting to be sent to it is dropped. |
| `Relay:MaxRooms` | 1000 | Rooms at once, counting those waiting for their host. |
| `Relay:MaxConnectionsPerAddress` | 32 | Connections open at once from one address. |
| `Relay:MaxConnections` | 2000 | Connections open at once in all. |
| `Relay:MaxGuestsPerRoom` | 16 | Guests in a room through the relay. |
| `Relay:RoomTtlSeconds` | 600 | How long a room waits for its host to come back. |
| `Relay:PingIntervalSeconds` | 15 | How often connections are pinged. |
| `Relay:IdleTimeoutSeconds` | 45 | Silence after which a connection is dropped. Keep it at least twice the ping interval. |
| `Relay:ConnectionsPerMinute` | 120 | Connections per minute from one address. |
| `Relay:RoomsPerHour` | 30 | Rooms made per hour from one address. |
| `Relay:FailedJoinsPerTenMinutes` | 20 | Wrong codes (or keys) from one address, after which it can't join for the rest of the ten minutes. |
| `Relay:MaxListingsPerAddress` | 3 | Games one address may list publicly at once. |
| `Relay:ListingTtlSeconds` | 90 | How long a listing lasts without its host listing it again. Hosts do every 30 seconds. |
| `Relay:MaxPublicGames` | 200 | The most games `/games` returns at once. |
| `Relay:GameListRequestsPerMinute` | 30 | Requests to `/games` per minute from one address. The game's browser asks every 20 seconds while open. |
| `Relay:KeySecret` | (required in production; random each start otherwise) | Makes the hosts' keys and ban keys. The relay won't start in production without it, since hosts would lose their codes and bans whenever it restarts. |
| `Relay:TrustForwardedHeaders` | false | Take the client's address from Caddy's `X-Forwarded-For`. Only when the relay can't be reached except through the proxy. |
| `Kestrel:Limits:MaxConcurrentUpgradedConnections` | 5000 | WebSocket connections at once. |

Join codes are six characters from `ABCDEFGHJKMNPQRSTUVWXYZ23456789`
(no 0/O or 1/I/L), so there are almost 900 million of them; with the
limit on wrong codes, guessing one in use isn't practical.
