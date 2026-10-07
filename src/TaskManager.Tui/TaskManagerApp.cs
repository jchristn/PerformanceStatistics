using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PerformanceStatistics;
using PerformanceStatistics.Monitoring;
using TUIKit;
using TUIKit.Content;
using TUIKit.Hosting;
using TUIKit.Layout;
using TUIKit.Modals;
using TUIKit.Widgets;

namespace TaskManager.Tui
{
    /// <summary>
    /// Builds the task manager screens on a <see cref="TuiApplication"/> and wires a snapshot source to them.
    /// Separate from <see cref="Program"/> so tests can drive it with a headless backend and a fake source.
    /// </summary>
    public sealed class TaskManagerApp
    {
        #region Public-Members

        /// <summary>
        /// State shown on screen.
        /// </summary>
        public TaskManagerState State { get; private set; }

        /// <summary>
        /// Tabs.
        /// </summary>
        public TabView Tabs { get; private set; }

        /// <summary>
        /// Process table.
        /// </summary>
        public DataTable<ProcessSnapshot> ProcessTable
        {
            get { return _Processes.Table; }
        }

        /// <summary>
        /// Tab names in order.
        /// </summary>
        public static readonly string[] TabNames = new string[] { "Overview", "Processes", "Network", "Disks", "Accounting" };

        #endregion

        #region Private-Members

        private static readonly double[] _Intervals = new double[] { 0.5, 1, 2, 5 };
        private static readonly int[] _Windows = new int[] { 60, 300, 900 };

        private readonly TuiApplication _App;
        private readonly ISnapshotSource _Source;
        private readonly Pane _Header;
        private readonly OverviewView _Overview;
        private readonly ProcessesView _Processes;
        private readonly NetworkView _Network;
        private readonly DisksView _Disks;
        private readonly AccountingView _Accounting;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Build the screens.
        /// </summary>
        /// <param name="app">Application.</param>
        /// <param name="source">Snapshot source.</param>
        public TaskManagerApp(TuiApplication app, ISnapshotSource source)
        {
            _App = app ?? throw new ArgumentNullException(nameof(app));
            _Source = source ?? throw new ArgumentNullException(nameof(source));
            State = new TaskManagerState(source.HostInfo);

            _App.HighlightFocusedRegion = true;
            _App.ApplyThemeToWidgets = true;

            _Header = _App.AddPane("header", region => region.TopAnchored(0, 2).FillWidth().WithPadding(0));

            _Overview = new OverviewView();
            _Processes = new ProcessesView(State);
            _Network = new NetworkView(State);
            _Disks = new DisksView(State);
            _Accounting = new AccountingView(State);

            // Forward keys so arrows, typing, and Tab reach the active tab's content (the table, the filter).
            Tabs = new TabView { ForwardKeys = true };
            Tabs.Add(TabNames[0], _Overview.Root);
            Tabs.Add(TabNames[1], _Processes.Root);
            Tabs.Add(TabNames[2], _Network.Root);
            Tabs.Add(TabNames[3], _Disks.Root);
            Tabs.Add(TabNames[4], _Accounting.Root);
            Tabs.ActiveTabChanged += (s, e) =>
            {
                if (Tabs.ActiveIndex == 1) FocusProcessTable();
                RefreshActive();
            };
            _App.AddWidget("main", Tabs, region => region.FillWidth().FillHeight(2, 1).WithPadding(0));

            StatusBar footer = _App.AddWidget("footer", new StatusBar { RightText = "taskmgr" }, region => region.BottomAnchored(0, 1).FillWidth().WithPadding(0));
            _App.BindKeyHints(footer)
                .AddAppHint("?", "Help")
                .AddAppHint("1", "Tabs 1-5")
                .AddAppHint("/", "Filter")
                .AddAppHint("k", "Kill")
                .AddAppHint("p", "Priority")
                .AddAppHint("space", "Pause")
                .AddAppHint("ctrl+q", "Quit");

            BindKeys();
            _Source.SnapshotReceived += OnSnapshot;
            RenderHeader();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Apply a snapshot and refresh the screens. Must run on the UI loop.
        /// </summary>
        /// <param name="snapshot">Snapshot.</param>
        public void Apply(HostSnapshot snapshot)
        {
            State.Apply(snapshot);
            RenderHeader();
            RefreshActive();
        }

        #endregion

        #region Private-Methods

        private void OnSnapshot(object sender, HostSnapshot snapshot)
        {
            // Widgets are not thread-safe: hand the snapshot to the UI loop.
            _App.Post(() => Apply(snapshot));
        }

        private void RefreshActive()
        {
            switch (Tabs.ActiveIndex)
            {
                case 0: _Overview.Update(State); break;
                case 1: _Processes.Update(); break;
                case 2: _Network.Update(); break;
                case 3: _Disks.Update(); break;
                case 4: _Accounting.Update(); break;
            }

            _App.RequestRender();
        }

        private void RenderHeader()
        {
            HostInfo h = State.Host;
            HostSnapshot s = State.Latest;
            _Header.Clear();
            StringBuilder line1 = new StringBuilder();
            line1.Append("[bold]").Append(Markup.Escape(h.HostName)).Append("[/]  ");
            line1.Append(Markup.Escape(h.OperatingSystem)).Append("  ").Append(Markup.Escape(h.Architecture));
            if (!String.IsNullOrEmpty(h.CpuModel)) line1.Append("  ").Append(Markup.Escape(h.CpuModel));
            line1.Append("  ").Append(h.LogicalProcessors.ToString(CultureInfo.InvariantCulture)).Append(" cores  ");
            line1.Append(Charts.Fmt(h.TotalMemoryBytes, MetricUnitEnum.Bytes)).Append(" RAM");
            if (s != null && s.System.UptimeSeconds.HasValue) line1.Append("  up ").Append(Charts.Fmt(s.System.UptimeSeconds, MetricUnitEnum.Seconds));
            _Header.WriteMarkup(line1.ToString());

            StringBuilder line2 = new StringBuilder();
            if (s != null)
            {
                line2.Append("CPU ").Append(Charts.Fmt(s.System.CpuPercent, MetricUnitEnum.Percent));
                line2.Append("  Mem ").Append(Charts.Fmt(s.System.MemoryUsedBytes, MetricUnitEnum.Bytes)).Append("/").Append(Charts.Fmt(s.System.MemoryTotalBytes, MetricUnitEnum.Bytes));
                line2.Append("  Procs ").Append(Charts.Fmt(s.System.ProcessCount, MetricUnitEnum.Count)).Append("  ");
            }
            else
            {
                line2.Append("[dim]Waiting for the first sample...[/]  ");
            }

            line2.Append("[dim]").Append(Markup.Escape(_Source.Description));
            line2.Append(", every ").Append(MetricFormatter.Number(_Source.Interval.TotalSeconds)).Append("s");
            line2.Append(", window ").Append(MetricFormatter.Duration(State.WindowSeconds)).Append("[/]");
            if (_Source.Paused) line2.Append("  [yellow]PAUSED[/]");
            if (!h.Elevated) line2.Append("  [dim]not elevated: some processes show n/a[/]");
            _Header.WriteMarkup(line2.ToString());
        }

        private void BindKeys()
        {
            _App.Bind("ctrl+q", () => _App.Quit());
            _App.Bind("q", () => _App.Quit());
            _App.Bind("f1", () => ShowHelp());
            _App.Bind("?", () => ShowHelp());
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                _App.Bind((i + 1).ToString(CultureInfo.InvariantCulture), () => Tabs.Activate(index));
            }

            _App.Bind("/", () =>
            {
                Tabs.Activate(1);
                _App.Focus("main");
                _Processes.Stack.Focus(_Processes.FilterField);
            });
            _App.Bind("escape", () => { if (Tabs.ActiveIndex == 1) FocusProcessTable(); });
            _App.Bind("space", () => { _Source.Paused = !_Source.Paused; RenderHeader(); _App.RequestRender(); });
            _App.Bind("=", () => StepInterval(-1));
            _App.Bind("-", () => StepInterval(1));
            _App.Bind("w", () =>
            {
                int next = (Array.IndexOf(_Windows, State.WindowSeconds) + 1) % _Windows.Length;
                State.WindowSeconds = _Windows[next];
                RenderHeader();
                RefreshActive();
            });
            _App.Bind("c", () => SortProcesses(MetricIds.ProcessCpu, false));
            _App.Bind("m", () => SortProcesses(MetricIds.ProcessWorkingSet, false));
            _App.Bind("d", () => SortProcesses(MetricIds.ProcessDiskRead, false));
            _App.Bind("n", () => SortProcesses(MetricIds.ProcessName, true));
            _App.Bind("g", () =>
            {
                State.GroupBy = State.GroupBy == AccountingGroupByEnum.Name ? AccountingGroupByEnum.User : AccountingGroupByEnum.Name;
                RefreshActive();
            });
            _App.Bind("r", () => { State.Accountant.Reset(); RefreshActive(); });
            _App.Bind("k", () => _ = ActOnSelectedAsync(ProcessActionKindEnum.Kill));
            _App.Bind("t", () => _ = ActOnSelectedAsync(ProcessActionKindEnum.KillTree));
            _App.Bind("p", () => _ = ActOnSelectedAsync(ProcessActionKindEnum.SetPriority));
            _App.Bind("s", () =>
            {
                ProcessSnapshot p = State.SelectedProcess();
                _ = ActOnSelectedAsync(p != null && p.State == "Stopped" ? ProcessActionKindEnum.Resume : ProcessActionKindEnum.Suspend);
            });
        }

        private void FocusProcessTable()
        {
            _App.Focus("main");
            _Processes.Stack.Focus(_Processes.Table);
        }

        private void StepInterval(int direction)
        {
            int index = Array.IndexOf(_Intervals, _Source.Interval.TotalSeconds);
            if (index < 0) index = 1;
            index = Math.Max(0, Math.Min(_Intervals.Length - 1, index + direction));
            _Source.Interval = TimeSpan.FromSeconds(_Intervals[index]);
            RenderHeader();
            _App.RequestRender();
        }

        private void SortProcesses(string metricId, bool ascending)
        {
            Tabs.Activate(1);
            _Processes.SortBy(metricId, ascending);
            RefreshActive();
        }

        private async Task ActOnSelectedAsync(ProcessActionKindEnum action)
        {
            if (Tabs.ActiveIndex != 1) return;
            ProcessSnapshot target = State.SelectedProcess();
            if (target == null)
            {
                _App.Notify("Select a process first.", NotificationSeverity.Warning, 3000);
                return;
            }

            if (!_Source.ActionsEnabled)
            {
                _App.Notify("Process actions are not enabled for this connection.", NotificationSeverity.Warning, 4000);
                return;
            }

            string label = target.Name + " (" + target.Pid.ToString(CultureInfo.InvariantCulture) + ")";
            ProcessActionRequest request = new ProcessActionRequest { HostId = State.Host.HostId, Pid = target.Pid, StartTimeUtc = target.StartTimeUtc, Action = action, RequestedBy = Environment.UserName };

            if (action == ProcessActionKindEnum.SetPriority)
            {
                string[] names = Enum.GetNames(typeof(ProcessPriorityEnum));
                int choice = await _App.SelectAsync("Priority for " + label, names).ConfigureAwait(false);
                if (choice < 0 || choice >= names.Length) return;
                request.Priority = (ProcessPriorityEnum)choice;
            }
            else
            {
                string verb = action == ProcessActionKindEnum.KillTree ? "Kill " + label + " and all of its child processes?"
                    : action == ProcessActionKindEnum.Kill ? "Kill " + label + "?"
                    : action == ProcessActionKindEnum.Suspend ? "Suspend " + label + "?"
                    : "Resume " + label + "?";
                bool ok = await _App.ConfirmAsync(verb, action == ProcessActionKindEnum.Resume ? "Resume" : action == ProcessActionKindEnum.Suspend ? "Suspend" : "Kill", "Cancel").ConfigureAwait(false);
                if (!ok) return;
            }

            ProcessActionResponse response;
            try
            {
                response = await _Source.ExecuteAsync(request).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _App.Post(() => _App.Notify("Action failed: " + e.Message, NotificationSeverity.Error, 5000));
                return;
            }

            NotificationSeverity severity = response.Succeeded ? NotificationSeverity.Success : NotificationSeverity.Error;
            _App.Post(() => _App.Notify(action + " " + label + ": " + response.Result + ". " + response.Message, severity, 5000));
        }

        private void ShowHelp()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("1-5        Overview, Processes, Network, Disks, Accounting");
            sb.AppendLine("Tab        Move focus between panels");
            sb.AppendLine("/          Filter processes (PID, name, user, path, command line)");
            sb.AppendLine("c m d n    Sort processes by CPU, memory, disk, name (or click a header)");
            sb.AppendLine("k / t      Kill the selected process / its whole tree");
            sb.AppendLine("p          Set priority     s   Suspend or resume");
            sb.AppendLine("space      Pause sampling   = -  Faster / slower sampling");
            sb.AppendLine("w          Chart window: 1, 5, or 15 minutes");
            sb.AppendLine("g / r      Accounting: group by name or user / reset");
            sb.AppendLine("? or F1    This help        q or Ctrl+Q  Quit");
            sb.AppendLine();
            sb.AppendLine("Every metric means the same thing on Windows, Linux, and macOS.");
            sb.AppendLine("CPU % is of the whole machine (all cores). n/a means the platform");
            sb.AppendLine("does not provide the value, or the process belongs to another user");
            sb.Append("and taskmgr is not running elevated.");
            _ = _App.ShowAsync(new MessageModal("taskmgr help", sb.ToString(), new[] { "Close" }));
        }

        #endregion
    }
}
