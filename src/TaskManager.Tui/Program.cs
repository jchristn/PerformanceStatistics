using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using TUIKit.Hosting;
using TUIKit.Theming;

namespace TaskManager.Tui
{
    /// <summary>
    /// Entry point: taskmgr [--server URL [--host ID] [--user NAME --password PASS | --token TOKEN]] [--interval SECONDS] [--record FILE] [--theme dark|light|contrast]
    /// </summary>
    public static class Program
    {
        #region Public-Methods

        /// <summary>
        /// Run the task manager.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            Options options;
            try
            {
                options = Options.Parse(args);
            }
            catch (ArgumentException e)
            {
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine(Options.Usage);
                return 2;
            }

            if (options.Help)
            {
                Console.WriteLine(Options.Usage);
                return 0;
            }

            ISnapshotSource source;
            try
            {
                source = options.Server != null
                    ? await RemoteSnapshotSource.ConnectAsync(options.Server, options.Host, options.User, options.Password, options.Token).ConfigureAwait(false)
                    : new LocalSnapshotSource(options.Interval, options.Record);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Could not start: " + e.Message);
                return 1;
            }

            using (source)
            {
                await TuiApp.RunAsync(app =>
                {
                    app.Theme = options.Theme;
                    new TaskManagerApp(app, source);
                    source.Start();
                }, CancellationToken.None).ConfigureAwait(false);
            }

            return 0;
        }

        #endregion

        #region Private-Classes

        private sealed class Options
        {
            public const string Usage =
                "taskmgr - cross-platform task manager\n\n" +
                "  taskmgr                         Monitor this machine\n" +
                "  taskmgr --record FILE           ...and record history to a SQLite file\n" +
                "  taskmgr --server URL [--host ID] [--user NAME --password PASS | --token TOKEN]\n" +
                "                                  Monitor a host through a TaskManager server\n\n" +
                "Options: --interval SECONDS (default 1), --theme dark|light|contrast, --help\n" +
                "Environment: TASKMGR_SERVER, TASKMGR_TOKEN, TASKMGR_USER, TASKMGR_PASSWORD";

            public bool Help;
            public string Server = Environment.GetEnvironmentVariable("TASKMGR_SERVER");
            public string Host;
            public string User = Environment.GetEnvironmentVariable("TASKMGR_USER");
            public string Password = Environment.GetEnvironmentVariable("TASKMGR_PASSWORD");
            public string Token = Environment.GetEnvironmentVariable("TASKMGR_TOKEN");
            public string Record;
            public TimeSpan Interval = TimeSpan.FromSeconds(1);
            public Theme Theme = Theme.Dark;

            public static Options Parse(string[] args)
            {
                Options o = new Options();
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i];
                    switch (a)
                    {
                        case "-h":
                        case "--help": o.Help = true; break;
                        case "--server": o.Server = Next(args, ref i, a); break;
                        case "--host": o.Host = Next(args, ref i, a); break;
                        case "--user": o.User = Next(args, ref i, a); break;
                        case "--password": o.Password = Next(args, ref i, a); break;
                        case "--token": o.Token = Next(args, ref i, a); break;
                        case "--record": o.Record = Next(args, ref i, a); break;
                        case "--interval":
                            double seconds;
                            if (!Double.TryParse(Next(args, ref i, a), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) || seconds < 0.25)
                                throw new ArgumentException("--interval must be a number of seconds >= 0.25");
                            o.Interval = TimeSpan.FromSeconds(seconds);
                            break;
                        case "--theme":
                            string theme = Next(args, ref i, a);
                            o.Theme = theme == "light" ? Theme.Light : theme == "contrast" ? Theme.HighContrast : Theme.Dark;
                            break;
                        default: throw new ArgumentException("Unknown argument: " + a);
                    }
                }

                return o;
            }

            private static string Next(string[] args, ref int i, string name)
            {
                if (i + 1 >= args.Length) throw new ArgumentException(name + " needs a value");
                return args[++i];
            }
        }

        #endregion
    }
}
