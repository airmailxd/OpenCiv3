using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace C7Relay;

// Sets up the relay's web app: the WebSocket endpoints for hosts and guests,
// and /health.
public static class RelayServer {
	// The app, not yet started. configure, if given, adjusts the settings
	// after they're read, as tests do.
	public static WebApplication Build(string[] args, Action<RelayOptions> configure = null) {
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
		builder.Services.Configure<RelayOptions>(builder.Configuration.GetSection(RelayOptions.Section));
		if (configure != null) {
			builder.Services.PostConfigure(configure);
		}
		builder.Services.AddSingleton<RelayHub>();
		builder.Services.AddHostedService<Sweeper>();

		WebApplication app = builder.Build();
		RelayOptions options = app.Services.GetRequiredService<IOptions<RelayOptions>>().Value;
		if (options.TrustForwardedHeaders) {
			ForwardedHeadersOptions forwarded = new() { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
			// The proxy is wherever the relay's port is reachable from, which
			// should only be the proxy itself.
			forwarded.KnownNetworks.Clear();
			forwarded.KnownProxies.Clear();
			app.UseForwardedHeaders(forwarded);
		}
		app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = options.PingInterval });

		RelayHub hub = app.Services.GetRequiredService<RelayHub>();
		app.Map(RelayProtocol.HostPath, context => hub.Host(context));
		app.Map(RelayProtocol.JoinPath + "{code}", context => hub.Join(context, (string)context.Request.RouteValues["code"]));
		app.MapGet("/health", () => Results.Json(hub.Health()));
		app.MapGet("/", () => "OpenCiv3 relay. Connect to it from the game.");
		return app;
	}

	// Clears away rooms whose host didn't come back, now and then.
	private sealed class Sweeper(RelayHub hub, IOptions<RelayOptions> options) : BackgroundService {
		protected override async Task ExecuteAsync(CancellationToken stopping) {
			TimeSpan interval = TimeSpan.FromSeconds(options.Value.SweepIntervalSeconds);
			try {
				while (!stopping.IsCancellationRequested) {
					await Task.Delay(interval, stopping);
					hub.Sweep();
				}
			} catch (OperationCanceledException) {
			}
		}
	}
}
