using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PerformanceStatistics.Monitoring;
using TaskManager.Tui;
using Touchstone.Core;
using TUIKit.Hosting;
using TUIKit.Terminal;
using TUIKit.Testing;

namespace Test.Tui
{
    /// <summary>
    /// Headless tests of the terminal task manager screens.
    /// </summary>
    public static class TuiSuite
    {
        private const string Id = "Tui";

        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case("RendersHeaderAndOverview", "Header and Overview render host facts, CPU, per-core bars, and memory", h =>
                {
                    h.Emit();
                    string screen = h.Screen();
                    Must(screen.Contains("fakehost"), "Host name in header", screen);
                    Must(screen.Contains("Fake CPU"), "CPU model in header", screen);
                    Must(screen.Contains("CPU utilization"), "CPU chart", screen);
                    Must(screen.Contains("Per core"), "Per-core panel", screen);
                    Must(screen.Contains("#3"), "Core rows", screen);
                    Must(screen.Contains("Load"), "Load line", screen);
                }),

                Case("ProcessesSortedByCpu", "Processes tab lists processes sorted by CPU, highest first", h =>
                {
                    h.Emit();
                    h.Key("2");
                    string screen = h.Screen();
                    Must(screen.Contains("Processes"), "Processes frame", screen);
                    int hot = screen.IndexOf("hotproc", StringComparison.Ordinal);
                    int idle = screen.IndexOf("idleproc", StringComparison.Ordinal);
                    Must(hot > 0 && idle > 0 && hot < idle, "Highest CPU first", screen);
                    Must(h.App.ProcessTable.SelectedRow != null, "A row is selected", screen);
                }),

                Case("FilterNarrowsRows", "Typing in the filter narrows the process list", h =>
                {
                    h.Emit();
                    h.Key("/");
                    foreach (char c in "idle") h.Key(c.ToString());
                    h.Emit();
                    Must(h.App.ProcessTable.Rows.Count == 1 && h.App.ProcessTable.Rows[0].Name == "idleproc", "Only the match remains", h.Screen());
                }),

                Case("DetailsShowMetadataAndReasons", "The detail panel shows path and command line, and n/a with a reason", h =>
                {
                    h.Source.HostInfo.Platform = "Mac";
                    h.Emit();
                    h.Key("2");
                    h.Key("n");
                    string screen = h.Screen();
                    Must(screen.Contains("/usr/bin/"), "Executable path shown", screen);
                    h.App.ProcessTable.Select(h.App.ProcessTable.Rows.ToList().FindIndex(p => p.Name == "rootproc"));
                    h.Pump();
                    screen = h.Screen();
                    Must(screen.Contains("n/a (Hidden for other users"), "Reason for an unavailable value", screen);
                }),

                Case("KillAsksThenActs", "k asks for confirmation and then sends a Kill request for the selected process", h =>
                {
                    h.Emit();
                    h.Key("2");
                    ProcessSnapshot selected = h.App.ProcessTable.SelectedRow;
                    h.Key("k");
                    Must(h.Screen().Contains("Kill " + selected.Name), "Confirmation dialog", h.Screen());
                    h.Key("enter");
                    for (int i = 0; i < 20 && h.Source.Requests.Count == 0; i++) { Thread.Sleep(25); h.Pump(); }
                    Must(h.Source.Requests.Count == 1, "One request", h.Screen());
                    Must(h.Source.Requests[0].Pid == selected.Pid && h.Source.Requests[0].Action == ProcessActionKindEnum.Kill, "Kill for the selected PID", h.Screen());
                    Must(h.Source.Requests[0].StartTimeUtc == selected.StartTimeUtc, "Start time guards against PID reuse", h.Screen());
                }),

                Case("KillCancelled", "Cancelling the confirmation sends nothing", h =>
                {
                    h.Emit();
                    h.Key("2");
                    h.Key("k");
                    h.Key("escape");
                    Thread.Sleep(100);
                    h.Pump();
                    Must(h.Source.Requests.Count == 0, "No request after cancel", h.Screen());
                }),

                Case("OtherTabsRender", "Network, Disks, and Accounting tabs render their data", h =>
                {
                    h.Emit();
                    h.Emit();
                    h.Key("3");
                    string network = h.Screen();
                    Must(network.Contains("eth0") && network.Contains("Established"), "Network tab", network);
                    h.Key("4");
                    string disks = h.Screen();
                    Must(disks.Contains("ext4") && disks.Contains("Volumes"), "Disks tab", disks);
                    h.Key("5");
                    string accounting = h.Screen();
                    Must(accounting.Contains("hotproc") && accounting.Contains("CPU time"), "Accounting tab", accounting);
                }),

                Case("ChartMaximizesWithAxes", "Tab into a chart, Enter maximizes it with X and Y axis labels, Esc restores", h =>
                {
                    for (int i = 0; i < 5; i++) h.Emit();
                    for (int i = 0; i < 8 && !h.Screen().Contains("Enter: maximize"); i++) h.Key("tab");
                    Must(h.Screen().Contains("Enter: maximize"), "A chart takes focus", h.Screen());
                    h.Key("enter");
                    string screen = h.Screen();
                    Must(screen.Contains("Y: ") && screen.Contains("X: Time"), "Axis titles", screen);
                    string tick = new DateTime(2026, 10, 7, 12, 0, 3, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm:0");
                    Must(screen.Contains(tick), "Time tick labels in local time", screen);
                    h.Key("escape");
                    Must(!h.Screen().Contains("X: Time"), "Esc restores", h.Screen());
                }),

                Case("ProcessDetailsModal", "Enter on a process opens a details modal with path, command line, and charts", h =>
                {
                    h.Emit();
                    h.Emit();
                    h.Key("2");
                    h.Key("n");
                    h.Key("enter");
                    string screen = h.Screen();
                    ProcessSnapshot selected = h.App.ProcessTable.SelectedRow;
                    Must(screen.Contains(selected.Name + " (PID " + selected.Pid + ")"), "Modal title", screen);
                    Must(screen.Contains("Command line") && screen.Contains("--serve"), "Command line", screen);
                    Must(screen.Contains("X: Time"), "Charts with axes", screen);
                    h.Key("escape");
                    Must(!h.Screen().Contains("(PID " + selected.Pid + ")"), "Esc closes", h.Screen());
                }),

                Case("InterfaceAddressesAndDetails", "Network tab shows interface addresses; Enter opens interface details", h =>
                {
                    h.Emit();
                    h.Emit();
                    h.Key("3");
                    string screen = h.Screen();
                    Must(screen.Contains("10.0.0.1/24") && screen.Contains("+2"), "Address summary", screen);
                    h.Key("enter");
                    screen = h.Screen();
                    Must(screen.Contains("Interface eth0"), "Details modal", screen);
                    Must(screen.Contains("192.168.1.5/24") && screen.Contains("fe80::1/64"), "Every address", screen);
                    Must(screen.Contains("02:00:00:aa:bb:cc"), "MAC address", screen);
                }),

                Case("HelpOpens", "? opens help", h =>
                {
                    h.Emit();
                    h.Key("?");
                    Must(h.Screen().Contains("taskmgr help"), "Help modal", h.Screen());
                }),

                Case("ElevationHintWhenNotElevated", "A non-elevated collector shows the admin-privileges hint in the header and help", h =>
                {
                    h.Source.HostInfo.Elevated = false;
                    h.Emit();
                    Must(h.Screen().Contains("admin privileges may be required (run with sudo)"), "Header hint", h.Screen());
                    h.Key("?");
                    Must(h.Screen().Contains("sudo ./run-tui.sh"), "Help explains how to elevate", h.Screen());
                }),

                Case("NoElevationHintWhenElevated", "An elevated collector shows no admin-privileges hint", h =>
                {
                    h.Emit();
                    Must(!h.Screen().Contains("admin privileges may be required"), "No hint when elevated", h.Screen());
                }),

                Case("PauseShowsInHeader", "space pauses and the header says so", h =>
                {
                    h.Emit();
                    h.Key("space");
                    Must(h.Source.Paused && h.Screen().Contains("PAUSED"), "Paused", h.Screen());
                }),
            };

            cases.Add(new TestCaseDescriptor(Id, "LocalSourceSmoke", "The real local source drives the app and lists this test process", async ct =>
            {
                using (LocalSnapshotSource source = new LocalSnapshotSource(TimeSpan.FromMilliseconds(500)))
                using (TuiApplication app = new TuiApplication(new HeadlessBackend(160, 50)))
                {
                    TaskManagerApp tm = new TaskManagerApp(app, source);
                    app.Start();
                    source.Start();
                    for (int i = 0; i < 40 && tm.State.SnapshotCount < 2; i++)
                    {
                        await System.Threading.Tasks.Task.Delay(100, ct).ConfigureAwait(false);
                        app.PumpInputOnce();
                    }

                    Must(tm.State.SnapshotCount >= 2, "Snapshots arrived", "");
                    int self = Environment.ProcessId;
                    Must(tm.State.Latest.Processes.Any(p => p.Pid == self), "This process is listed", "");
                    tm.Tabs.Activate(1);
                    app.PumpInputOnce();
                    app.RenderOnce();
                    Must(tm.ProcessTable.Rows.Count > 1, "Table bound", Snapshot.ToText(app.CaptureFrame()));

                    // TUI_DUMP=1 prints every tab with live data, for eyeballing layouts.
                    if (Environment.GetEnvironmentVariable("TUI_DUMP") == "1")
                    {
                        for (int tab = 0; tab < TaskManagerApp.TabNames.Length; tab++)
                        {
                            tm.Tabs.Activate(tab);
                            app.PumpInputOnce();
                            app.RenderOnce();
                            Console.WriteLine("===== " + TaskManagerApp.TabNames[tab]);
                            Console.WriteLine(Snapshot.ToText(app.CaptureFrame()));
                        }
                    }

                    app.Stop();
                }
            }));

            return new TestSuiteDescriptor(Id, "Terminal UI (headless)", cases);
        }

        private static TestCaseDescriptor Case(string caseId, string name, Action<Harness> body)
        {
            return new TestCaseDescriptor(Id, caseId, name, ct =>
            {
                using (Harness h = new Harness())
                {
                    body(h);
                }

                return System.Threading.Tasks.Task.CompletedTask;
            });
        }

        private static void Must(bool condition, string message, string screen)
        {
            if (!condition) throw new Exception(message + (String.IsNullOrEmpty(screen) ? string.Empty : Environment.NewLine + screen));
        }

        /// <summary>
        /// A headless app over a fake source.
        /// </summary>
        private sealed class Harness : IDisposable
        {
            public readonly FakeSnapshotSource Source = new FakeSnapshotSource();
            public readonly HeadlessBackend Backend = new HeadlessBackend(160, 50);
            public readonly TuiApplication TuiApp;
            public readonly TaskManagerApp App;
            private DateTime _Now = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

            public Harness()
            {
                TuiApp = new TuiApplication(Backend);
                App = new TaskManagerApp(TuiApp, Source);
                TuiApp.Start();
            }

            public void Emit()
            {
                _Now = _Now.AddSeconds(1);
                Source.Emit(FakeSnapshotSource.Snapshot(_Now,
                    FakeSnapshotSource.Process(100, "hotproc", 75, 500L << 20),
                    FakeSnapshotSource.Process(200, "idleproc", 0.5, 50L << 20),
                    FakeSnapshotSource.Process(300, "rootproc", null, 20L << 20, "root")));
                Pump();
            }

            public void Key(string chord)
            {
                Backend.FeedKey(chord);
                Pump();
            }

            public void Pump()
            {
                TuiApp.PumpInputOnce();
                TuiApp.PumpInputOnce();
                TuiApp.RenderOnce();
            }

            public string Screen()
            {
                TuiApp.RenderOnce();
                return Snapshot.ToText(TuiApp.CaptureFrame());
            }

            public void Dispose()
            {
                TuiApp.Stop();
                TuiApp.Dispose();
            }
        }
    }
}
