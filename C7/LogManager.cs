using System;
using Godot;
using Serilog;
using Serilog.Templates;

// Configures Serilog for the game. It must be the FIRST autoload in
// project.godot: Godot constructs every autoload before adding any of them to
// the tree, and the other autoloads' constructors (GlobalSingleton loads the
// game mode and its Lua) touch classes that cache
// `static ILogger log = Log.ForContext<T>()`. A logger cached before
// Log.Logger is set stays silent forever, so configuration happens in this
// node's constructor rather than in _Ready.
public partial class LogManager : Node {
	private static readonly object configureLock = new();
	private static bool configured;
	private static bool flushed;

	public LogManager() {
		Configure();
	}

	// Sets up Log.Logger once; safe to call more than once.
	public static void Configure() {
		lock (configureLock) {
			if (configured) {
				return;
			}
			configured = true;
		}

		// Format looks like:
		// timestamp [level] context: message
		//		Exception: exception
		// Example: 22:25:32.528 [DBG] MainMenu: enter MainMenu._Ready
		ExpressionTemplate consoleTemplate = new ExpressionTemplate(
			"{@t:HH:mm:ss.fff} [{@l:u3}]{#if SourceContext is not null} {SourceContext}:{#end} {@m:lj}{#if @x is not null}\tException: {@x}{#end}\n");

		// You can filter this several ways with the expression in Filter.ByIncludingOnly
		//   "SourceContext like 'C7Engine.AI.%'"	<-- filters on the source context, i.e. namespace + class name.  In this case, only shows messages from the C7Engine.AI namespace.
		//   "@m like '%citizen%'"					<-- filters on the message, in this case only returning messages containing the phrase 'citizen'
		//   "@l = 'Information'"					<-- filters on the level.
		// Filtering on the level can be used in conjunction with other filters, e.g.:
		//   "@l = 'Information' OR SourceContext like 'C7Engine.AI.%'"
		// Includes all logs of an 'Information' level regardless of namespace, and all logs of
		// the C7Engine.AI namespace regardless of log level.
		string filter = "(@l = 'Fatal' OR @l = 'Error' OR @l = 'Warning' OR @l = 'Information')";
		// suggested:
		// filter += " OR SourceContext like 'C7Engine.AI.%'"; // (insert the namespace you need to debug)

		Log.Logger = new LoggerConfiguration()
			// C7_LOG picks another file, e.g. for running two instances side by side.
			// At 50 MB the log rolls over to log_001.txt and so on, keeping the
			// newest few files, rather than stopping for good.
			.WriteTo.File(System.Environment.GetEnvironmentVariable("C7_LOG") ?? System.IO.Path.Combine(C7Engine.C7Settings.WritableDirectory, "log.txt"), buffered: true, flushToDiskInterval: TimeSpan.FromMilliseconds(2500), fileSizeLimitBytes: 52428800, //50 MB
						  rollOnFileSizeLimit: true, retainedFileCountLimit: 5,
						  outputTemplate: "[{Level:u3}] {Timestamp:HH:mm:ss} {SourceContext}: {Message:lj} {NewLine}{Exception}")
			.WriteTo.Console(consoleTemplate)
			.Filter.ByIncludingOnly(filter)
			.MinimumLevel.Debug()
			.CreateLogger();

		// The file sink is buffered, so flush whenever the process goes away,
		// however that happens: the window closing, GetTree().Quit() from a
		// menu, or a crash.
		AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();
		AppDomain.CurrentDomain.UnhandledException += (_, e) => {
			Log.ForContext<LogManager>().Fatal(e.ExceptionObject as Exception, "Unhandled exception");
			if (e.IsTerminating) {
				Flush();
			}
		};

		Log.ForContext<LogManager>().Debug("Hello!");
	}

	private static void Flush() {
		lock (configureLock) {
			if (flushed) {
				return;
			}
			flushed = true;
		}
		Log.ForContext<LogManager>().Debug("Goodbye!");
		Log.CloseAndFlush();
	}

	public override void _Notification(int what) {
		if (what == NotificationWMCloseRequest) {
			// Godot's default auto-accept-quit quits after this; the log is
			// flushed when this autoload leaves the tree on the way out.
			Log.ForContext<LogManager>().Debug("Window close requested");
		} else if (what == NotificationPredelete) {
			Flush();
		}
	}

	public override void _ExitTree() {
		// Autoloads leave the tree when the game quits, by any route.
		Flush();
	}

	public static ILogger ForContext<T>() {
		return Log.ForContext<T>();
	}
}
