using System;
using System.Globalization;
using PerformanceStatistics.Monitoring;
using TUIKit;
using TUIKit.Content;
using TUIKit.Layout;
using TUIKit.Widgets;

namespace TaskManager.Tui
{
    /// <summary>
    /// Accounting tab: resources consumed since start, grouped by process name or user.
    /// </summary>
    internal sealed class AccountingView
    {
        #region Public-Members

        public IWidget Root { get; private set; }

        #endregion

        #region Private-Members

        private readonly TaskManagerState _State;
        private readonly Pane _Info = new Pane("accounting-info");
        private readonly DataTable<AccountingRow> _Table = new DataTable<AccountingRow>();

        #endregion

        #region Constructors-and-Factories

        public AccountingView(TaskManagerState state)
        {
            _State = state;
            _Table.KeySelector = r => r.Group;
            _Table
                .Column(new DataColumn<AccountingRow>("Group", r => r.Group, true) { Width = 28 })
                .Column(new DataColumn<AccountingRow>("CPU time", r => MetricFormatter.Duration(r.CpuSeconds), true) { Alignment = CellAlignment.Right, Width = 12, SortKey = r => r.CpuSeconds })
                .Column(new DataColumn<AccountingRow>("Avg CPU", r => MetricFormatter.Format(r.AverageCpuPercent, MetricUnitEnum.Percent), true) { Alignment = CellAlignment.Right, Width = 9, SortKey = r => r.AverageCpuPercent })
                .Column(new DataColumn<AccountingRow>("Peak memory", r => MetricFormatter.Bytes(r.PeakWorkingSetBytes), true) { Alignment = CellAlignment.Right, Width = 12, SortKey = r => r.PeakWorkingSetBytes })
                .Column(new DataColumn<AccountingRow>("Disk read", r => MetricFormatter.Bytes(r.DiskReadBytes), true) { Alignment = CellAlignment.Right, Width = 11, SortKey = r => r.DiskReadBytes })
                .Column(new DataColumn<AccountingRow>("Disk write", r => MetricFormatter.Bytes(r.DiskWriteBytes), true) { Alignment = CellAlignment.Right, Width = 11, SortKey = r => r.DiskWriteBytes })
                .Column(new DataColumn<AccountingRow>("Processes", r => r.ProcessCount.ToString(CultureInfo.InvariantCulture), true) { Alignment = CellAlignment.Right, SortKey = r => r.ProcessCount });
            _Table.StretchLastColumn = true;
            _Table.SortByColumn(1, false);

            Root = new FramedStack(SplitOrientation.Vertical)
                .Add(_Info, StackSize.Fixed(1), "Accounting")
                .Add(_Table, StackSize.Weighted(1), "Consumers");
        }

        #endregion

        #region Public-Methods

        public void Update()
        {
            _Info.Clear();
            _Info.WriteMarkup("Since " + _State.Accountant.SinceUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                + ", grouped by [bold]" + _State.GroupBy + "[/].  [yellow]g[/] toggles name/user, [yellow]r[/] resets.");
            _Table.Bind(_State.Accountant.Report(_State.GroupBy));
        }

        #endregion
    }
}
