using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PerformanceStatistics.Monitoring;
using TUIKit;
using TUIKit.Layout;
using TUIKit.Modals;
using TUIKit.Widgets;

namespace TaskManager.Tui
{
    /// <summary>
    /// Processes tab: a filterable, sortable table of every process, and a detail panel with metadata and history
    /// charts for the selected process.
    /// </summary>
    internal sealed class ProcessesView
    {
        #region Public-Members

        public IWidget Root { get; private set; }

        public DataTable<ProcessSnapshot> Table { get; private set; }

        public TextField FilterField { get; private set; }

        public FramedStack Stack { get; private set; }

        /// <summary>
        /// Column index of CPU, for sort shortcuts.
        /// </summary>
        public Dictionary<string, int> ColumnIndex { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

        #endregion

        #region Private-Members

        private static readonly string[] _Columns = new string[]
        {
            MetricIds.ProcessPid, MetricIds.ProcessName, MetricIds.ProcessUser, MetricIds.ProcessCpu, MetricIds.ProcessWorkingSet,
            MetricIds.ProcessDiskRead, MetricIds.ProcessDiskWrite, MetricIds.ProcessHandles, MetricIds.ProcessThreads,
            MetricIds.ProcessState, MetricIds.ProcessPath
        };

        private readonly TaskManagerState _State;
        private readonly DefinitionList _Details = new DefinitionList();
        private readonly ChartPanel _CpuChart;
        private readonly ChartPanel _MemoryChart;
        private readonly ChartPanel _DiskChart;

        #endregion

        #region Constructors-and-Factories

        public ProcessesView(TaskManagerState state, Action<Modal> show)
        {
            _State = state;
            FilterField = new TextField { Placeholder = "Filter by PID, name, user, path, or command line" };
            FilterField.ValueChanged += (s, e) => { _State.Filter = FilterField.Value; Rebind(); };

            Table = new DataTable<ProcessSnapshot>();
            Table.KeySelector = p => p.Key;
            Table.EmptyText = _State.Host.Elevated ? "No processes match." : "No processes match. Some processes may be hidden: admin privileges may be required.";
            Table.StretchLastColumn = true;
            Table.RowStyle = p => p.State == "Stopped" || p.State == "Zombie" ? (CellStyle?)CellStyle.Default.WithAttribute(CellAttributes.Dim, true) : null;
            for (int i = 0; i < _Columns.Length; i++)
            {
                MetricDefinition m = MetricCatalog.Get(_Columns[i]);
                string id = m.Id;
                DataColumn<ProcessSnapshot> column = new DataColumn<ProcessSnapshot>(m.ShortName, p => MetricCatalog.ProcessDisplay(id, p), true);
                if (m.Unit == MetricUnitEnum.Text)
                {
                    if (id == MetricIds.ProcessPid)
                    {
                        column.SortKey = p => p.Pid;
                        column.Alignment = CellAlignment.Right;
                        column.Width = 7;
                    }
                    else
                    {
                        column.SortKey = p => MetricCatalog.ProcessDisplay(id, p);
                        if (id == MetricIds.ProcessName) column.Width = 22;
                        if (id == MetricIds.ProcessUser) column.Width = 14;
                        if (id == MetricIds.ProcessState) column.Width = 9;
                    }
                }
                else
                {
                    column.SortKey = p => MetricCatalog.ProcessValue(id, p) ?? -1;
                    column.Alignment = CellAlignment.Right;
                    column.Width = id == MetricIds.ProcessCpu ? 7 : id == MetricIds.ProcessHandles || id == MetricIds.ProcessThreads ? 8 : 11;
                }

                Table.Column(column);
                ColumnIndex[id] = i;
            }

            Table.SelectionChanged += (s, e) =>
            {
                ProcessSnapshot row = Table.SelectedRow;
                if (row != null) _State.SelectedProcessKey = row.Key;
                UpdateDetails();
            };

            // Enter on a row opens live details for that process.
            Table.RowActivated += index =>
            {
                ProcessSnapshot row = Table.SelectedRow;
                if (row == null) return;
                string key = row.Key;
                show(new DetailsModal(row.Name + " (PID " + row.Pid.ToString(CultureInfo.InvariantCulture) + ")", () => Details.Process(_State, key)));
            };

            _CpuChart = Charts.Panel("Process CPU %", MetricUnitEnum.Percent, show);
            _MemoryChart = Charts.Panel("Process memory (resident)", MetricUnitEnum.Bytes, show);
            _DiskChart = Charts.Panel("Process disk I/O", MetricUnitEnum.BytesPerSecond, show);

            FramedStack charts = new FramedStack(SplitOrientation.Vertical)
                .Add(_CpuChart, StackSize.Weighted(1, 3), "CPU %")
                .Add(_MemoryChart, StackSize.Weighted(1, 3), "Memory (resident)")
                .Add(_DiskChart, StackSize.Weighted(1, 3), "Disk I/O (bytes/s)");
            FramedStack detail = new FramedStack(SplitOrientation.Horizontal)
                .Add(_Details, StackSize.Weighted(3), "Selected process")
                .Add(charts, StackSize.Weighted(2));

            Stack = new FramedStack(SplitOrientation.Vertical)
                .Add(FilterField, StackSize.Fixed(1), "Filter ( / )")
                .Add(Table, StackSize.Weighted(3, 5), "Processes")
                .Add(detail, StackSize.Weighted(2, 9));
            Root = Stack;

            Table.SortByColumn(ColumnIndex[MetricIds.ProcessCpu], false);
        }

        #endregion

        #region Public-Methods

        public void Update()
        {
            Rebind();
            UpdateDetails();
        }

        public void SortBy(string metricId, bool ascending)
        {
            int index;
            if (ColumnIndex.TryGetValue(metricId, out index)) Table.SortByColumn(index, ascending);
        }

        #endregion

        #region Private-Methods

        private void Rebind()
        {
            Table.Bind(_State.FilteredProcesses());
            if (_State.SelectedProcessKey == null && Table.SelectedRow != null) _State.SelectedProcessKey = Table.SelectedRow.Key;
        }

        private void UpdateDetails()
        {
            ProcessSnapshot p = _State.SelectedProcess();
            _Details.Clear();
            if (p == null)
            {
                _Details.Set("Selection", "Select a process with the arrow keys.");
                return;
            }

            foreach (string id in new[] { MetricIds.ProcessName, MetricIds.ProcessPid, MetricIds.ProcessParentPid, MetricIds.ProcessUser, MetricIds.ProcessState, MetricIds.ProcessPriority, MetricIds.ProcessStart })
                _Details.Set(MetricCatalog.Get(id).Name, MetricCatalog.ProcessDisplay(id, p));

            _Details.Set("Executable", Display(p.ExecutablePath, MetricIds.ProcessPath));
            _Details.Set("Command line", Display(p.CommandLine, MetricIds.ProcessCommandLine));
            _Details.AddSection("Usage");
            foreach (string id in new[] { MetricIds.ProcessCpu, MetricIds.ProcessCpuTime, MetricIds.ProcessWorkingSet, MetricIds.ProcessPrivate, MetricIds.ProcessVirtual, MetricIds.ProcessDiskRead, MetricIds.ProcessDiskWrite, MetricIds.ProcessDiskReadTotal, MetricIds.ProcessDiskWriteTotal, MetricIds.ProcessHandles, MetricIds.ProcessThreads })
            {
                string text = MetricCatalog.ProcessDisplay(id, p);
                if (text == MetricFormatter.NotAvailable)
                {
                    string reason = _State.Unavailable(id);
                    if (reason != null) text += " (" + reason + ")";
                    else if (!_State.Host.Elevated) text += " (admin privileges may be required)";
                }

                _Details.Set(MetricCatalog.Get(id).Name, text);
            }

            HistoryBuffer h = _State.History;
            string title = " " + p.Name + " (" + p.Pid.ToString(CultureInfo.InvariantCulture) + ")";
            _CpuChart.Update(_State.Chart("CPU %" + title, MetricUnitEnum.Percent, null, ("CPU", Charts.Cpu, h.GetProcessSeries(p.Key, MetricIds.ProcessCpu))));
            _MemoryChart.Update(_State.Chart("Memory (resident)" + title, MetricUnitEnum.Bytes, null, ("Memory", Charts.Memory, h.GetProcessSeries(p.Key, MetricIds.ProcessWorkingSet))));
            _DiskChart.Update(_State.Chart("Disk I/O" + title, MetricUnitEnum.BytesPerSecond, null,
                ("Read", Charts.Read, h.GetProcessSeries(p.Key, MetricIds.ProcessDiskRead)),
                ("Write", Charts.Write, h.GetProcessSeries(p.Key, MetricIds.ProcessDiskWrite))));
        }

        private string Display(string value, string metricId)
        {
            if (!String.IsNullOrEmpty(value)) return value;
            string reason = _State.Unavailable(metricId);
            if (reason == null && !_State.Host.Elevated) reason = "admin privileges may be required";
            return MetricFormatter.NotAvailable + (reason != null ? " (" + reason + ")" : string.Empty);
        }

        #endregion
    }
}
