// dm-probe: OpenCiv's direct messages client (C7Engine.Network.DirectMessages, the same object the game's chat line
// uses) signed in to a running game server, for CHAT.md's end-to-end check across the launcher, Roma and OpenCiv.
//
//   dotnet run --project tools/dm-probe -- --server http://127.0.0.1:5094 --user otto --password ...
//
// stdin, one command a line:  w <name> <text> | r <text> | ignore <name> | unignore <name> | quit
// stdout, one JSON object a line: {"ev":"ready"}, then each event the client queues for the chat line
// ({"ev":"Message"|"Unread"|"Notice","text":<the line as shown>,"from","to","body","fromMe"}).
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using C7Engine.Network;

static string Arg(string[] args, string name) {
	int i = Array.IndexOf(args, "--" + name);
	return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

string server = Arg(args, "server") ?? "http://127.0.0.1:5094";
string user = Arg(args, "user");
string password = Arg(args, "password");
object gate = new();
void Say(object o) {
	lock (gate) {
		Console.WriteLine(JsonSerializer.Serialize(o));
		Console.Out.Flush();
	}
}

GameServerClient client = new(server);
await client.LogInAsync(user, password);
using DirectMessages dms = DirectMessages.For(client);
dms.Start();
for (int i = 0; i < 100 && !dms.Connected; i++) {
	await Task.Delay(100);
}
Say(new { ev = dms.Connected ? "ready" : "not-connected", user = dms.Username });

using CancellationTokenSource stop = new();
Task pump = Task.Run(async () => {
	while (!stop.IsCancellationRequested) {
		while (dms.TryTake(out DirectMessageEvent e)) {
			Say(new { ev = e.Kind.ToString(), text = e.Text, from = e.Message?.From, to = e.Message?.To, body = e.Message?.Text, fromMe = e.FromMe });
		}
		await Task.Delay(50);
	}
});

string line;
while ((line = Console.ReadLine()) != null) {
	string[] p = line.Split(' ', 3);
	switch (p[0]) {
		case "w":
			await dms.SendAsync(p.Length > 1 ? p[1] : null, p.Length > 2 ? p[2] : null);
			break;
		case "r":
			await dms.ReplyAsync(line.Length > 2 ? line[2..] : null);
			break;
		case "ignore":
		case "unignore":
			await dms.IgnoreAsync(p.Length > 1 ? p[1] : null, p[0] == "ignore");
			break;
		case "connected":
			Say(new { ev = "connected", value = dms.Connected, lastFrom = dms.LastFrom });
			break;
		case "quit":
			stop.Cancel();
			break;
	}
	if (stop.IsCancellationRequested) {
		break;
	}
}
await Task.Delay(300);
stop.Cancel();
await pump;
