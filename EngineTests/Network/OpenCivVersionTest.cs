using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using C7Engine.Network;
using C7GameServer;
using Xunit;

namespace EngineTests.Network;

// OpenCiv's version (C7Engine/VERSION), built into the engine, and sent to
// game servers with each request.
public class OpenCivVersionTest {
	private static string VersionFile() {
		for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent) {
			string file = Path.Combine(dir.FullName, "C7Engine", "VERSION");
			if (File.Exists(file)) {
				return file;
			}
		}
		throw new FileNotFoundException("C7Engine/VERSION isn't above the tests' folder.");
	}

	[Fact]
	public void TheEngineKnowsItsVersionFromTheFile() {
		string inFile = File.ReadAllText(VersionFile()).Trim();
		Assert.Equal(inFile, OpenCivVersion.Current);
		Assert.NotNull(OpenCivVersion.Parse(OpenCivVersion.Current));
	}

	[Theory]
	[InlineData("0.16.1", "0.16.1")]
	[InlineData(" 1.2.3\n", "1.2.3")]
	[InlineData("0.16.1-dev", null)]
	[InlineData("dev", null)]
	[InlineData("0.16", null)]
	[InlineData("0.16.1.2", null)]
	[InlineData("0.-1.1", null)]
	[InlineData("", null)]
	[InlineData(null, null)]
	public void ParsesOnlyReleases(string? text, string? expected) {
		Assert.Equal(expected == null ? null : Version.Parse(expected), OpenCivVersion.Parse(text));
	}

	[Fact]
	public void ADevelopmentBuildSaysSo() {
		bool was = OpenCivVersion.Development;
		try {
			OpenCivVersion.Development = false;
			Assert.Equal(OpenCivVersion.Current, OpenCivVersion.Sent);
			OpenCivVersion.Development = true;
			Assert.Equal(OpenCivVersion.Current + "-dev", OpenCivVersion.Sent);
		} finally {
			OpenCivVersion.Development = was;
		}
	}

	// A server that answers one request with status and body, and gives
	// back the request's head.
	private static async Task<(string head, GameServerException refused)> AskRefusingServer(int status, string body) {
		TcpListener listener = new(IPAddress.Loopback, 0);
		listener.Start();
		try {
			int port = ((IPEndPoint)listener.LocalEndpoint).Port;
			Task<string> served = Task.Run(async () => {
				using TcpClient connection = await listener.AcceptTcpClientAsync();
				using NetworkStream stream = connection.GetStream();
				StringBuilder head = new();
				byte[] one = new byte[1];
				while (!head.ToString().EndsWith("\r\n\r\n") && await stream.ReadAsync(one) == 1) {
					head.Append((char)one[0]);
				}
				byte[] content = Encoding.UTF8.GetBytes(body);
				byte[] answer = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Refused\r\nContent-Type: application/json\r\n"
					+ $"Content-Length: {content.Length}\r\nConnection: close\r\n\r\n");
				await stream.WriteAsync(answer);
				await stream.WriteAsync(content);
				return head.ToString();
			});
			GameServerClient client = new($"http://127.0.0.1:{port}", new Session("alice", "token"));
			GameServerException refused = await Assert.ThrowsAsync<GameServerException>(() => client.MyGamesAsync());
			return (await served, refused);
		} finally {
			listener.Stop();
		}
	}

	[Fact]
	public async Task EachRequestSaysTheVersionAndARefusalForItIsShown() {
		const string Why = "This version of OpenCiv is out of date. Update OpenCiv in AirGames, then try again.";
		(string head, GameServerException refused) = await AskRefusingServer(426, $"{{\"error\":\"{Why}\"}}");
		Assert.Contains($"\r\n{OpenCivVersion.Header}: {OpenCivVersion.Sent}\r\n", head, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("?v=" + GameServerProtocol.Version, head);
		Assert.Equal(Why, refused.Message);
		Assert.Equal(HttpStatusCode.UpgradeRequired, refused.Status);
	}

	[Fact]
	public async Task ARefusalForTheVersionWithoutWordsSaysToUpdate() {
		(_, GameServerException refused) = await AskRefusingServer(426, "");
		Assert.Equal(GameServerClient.UpdateNeeded, refused.Message);
	}
}
