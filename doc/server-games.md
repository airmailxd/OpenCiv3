# Server games

Games that live on a server rather than on one player's machine, so friends
can play them over days or weeks, together when they're all online and in
turn when they aren't. Eventually all online play may go this way; LAN games
and relay games (Host Online) stay as they are.

## What players see

- Each player has an account on the server: a username and a password.
  Anyone can sign up from the game. There's no email, so a forgotten password
  is reset by whoever runs the server.
- **My Games** lists the server games the player is in, in two lists:
  **Your Turn** and **Another Player's Turn**. Each shows the game's title
  (the host chose it), the player's current era as an icon, and whose turn it
  is.
- When it's their turn a player can **Play**: connect and take their turn.
- When it isn't, they can **View**: connect and look at their cities, units,
  production and advisors, but not act.
- **Open Games** lists the server games with seats still free. A player picks
  one, gives its password if it has one, and claims a civilization; that
  seat is theirs from then on.
- A game starts once every seat is claimed. It waits for each player as long
  as it takes: there's no turn timer and no notification in this version.

## Turns: together or in turn

At the start of each turn the server looks at who's connected:

- Everyone connected: the humans move at once (simultaneous turns).
- Anyone missing: the humans move one at a time, in turn order, as in a
  hotseat game. Whoever is next plays when they connect; the others can View.

A turn keeps the way it began. If a player drops partway through a
simultaneous turn, the others can still finish theirs; the dropped player's
turn stays open until they come back, and the next turn is played in turn.
The computer players move on the server once the humans have finished.

Between those, when nobody is connected, the game is at rest: "whose turn it
is" is the first human in turn order who hasn't finished this turn.

## In the game

The game server itself is kept in a separate, private repository, which
builds with this one's engine. What's here is the game's side:

- **The protocol**: `C7Engine/Network/GameServerProtocol.cs`, the HTTP API
  the game and the server speak, which the server takes from here.
- **The client**: `C7Engine/Network/GameServerClient.cs`, for signing up and
  logging in, My Games, Open Games, uploading a game, claiming a seat, and
  joining a game (`JoinGameAsync`), which connects through the relay with a
  token for the player's own seat.
- **The server's host**: the server plays each game with this engine, as a
  dedicated `LanHost` (`LanHost.Dedicated`): one with no player of its own,
  that lets in only the seats' tokens. It refuses a player's commands when
  it isn't their turn (`LanHost.MayActNow`), so a player who joins to View
  plays on when their turn comes. As each turn begins
  (`EngineStorage.newTurnBeginning`) it has the humans move at once if
  they're all connected, and in turn otherwise
  (`LanHost.ChooseTurnsFromWhoIsConnected`).
- **The screens**: `C7/UIElements/ServerGames/` (Server Games on the main
  menu: log in, My Games, Open Games, and joining a game) and
  `MultiplayerSetupScreen.ServerGame.cs` (setting up a new server game).
- **Saving**: the server saves a server game as it's played, so in one the
  game's Save Game says so rather than saving what the player's machine has,
  which is only what their civilization can see.

## Started by another program

A program the player has logged in to already (a hub with games in it) can
start this one straight into server games, with no second login, by these
arguments after `--`: `--server=<url>`, the game server for this run, in
place of the one in the settings, which isn't changed; `--server-game=<id>`,
to join that game at once (Play or View, as the server says) with no menu,
and quit when the player leaves it, or once they've seen why it couldn't be
joined, or give up joining, as the program waits for the game to end; and
`--server-games`, to open Server Games at once, closing to the main menu.
The session comes in the environment, as `C7_SERVER_USER` and
`C7_SERVER_TOKEN`, and is used for that run only: it isn't saved in C7.ini
nor written to the log, and neither is a login made in that run. Without
`--server=` the other two are ignored. See
`C7/UIElements/ServerGames/ServerHandoff.cs`.
