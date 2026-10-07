using System;
using PerformanceStatistics.Monitoring;
using TUIKit;
using TUIKit.Content;
using TUIKit.Layout;
using TUIKit.Widgets;

namespace TaskManager.Tui
{
    /// <summary>
    /// Disks tab: volumes, disk bytes and operations over time, and queue lengths.
    /// </summary>
    internal sealed class DisksView
    {
        #region Public-Members

        public IWidget Root { get; private set; }

        #endregion

        #region Private-Members

        private readonly TaskManagerState _State;
        private readonly DataTable<VolumeSnapshot> _Volumes = new DataTable<VolumeSnapshot>();
        private readonly LineChart _Bytes;
        private readonly ChartSeries _BytesWrite;
        private readonly LineChart _Ops;
        private readonly ChartSeries _OpsWrite;
        private readonly Pane _Queues = new Pane("queues");

        #endregion

        #region Constructors-and-Factories

        public DisksView(TaskManagerState state)
        {
            _State = state;
            _Volumes.KeySelector = v => v.Name;
            _Volumes
                .Column(new DataColumn<VolumeSnapshot>("Volume", v => v.Name, true) { Width = 28 })
                .Column(new DataColumn<VolumeSnapshot>("Format", v => v.Format, true) { Width = 8 })
                .Column(new DataColumn<VolumeSnapshot>("Size", v => MetricFormatter.Bytes(v.TotalBytes), true) { Alignment = CellAlignment.Right, Width = 11, SortKey = v => v.TotalBytes })
                .Column(new DataColumn<VolumeSnapshot>("Used", v => MetricFormatter.Bytes(v.UsedBytes), true) { Alignment = CellAlignment.Right, Width = 11, SortKey = v => v.UsedBytes })
                .Column(new DataColumn<VolumeSnapshot>("Free", v => MetricFormatter.Bytes(v.FreeBytes), true) { Alignment = CellAlignment.Right, Width = 11, SortKey = v => v.FreeBytes })
                .Column(new DataColumn<VolumeSnapshot>("Used %", v => v.TotalBytes > 0 ? MetricFormatter.Format(100.0 * v.UsedBytes / v.TotalBytes, MetricUnitEnum.Percent) : MetricFormatter.NotAvailable) { Alignment = CellAlignment.Right });
            _Volumes.StretchLastColumn = true;

            _Bytes = Charts.CreatePair("Read", Charts.Read, null, "Write", Charts.Write, out _BytesWrite);
            _Ops = Charts.CreatePair("Read ops", Charts.Read, null, "Write ops", Charts.Write, out _OpsWrite);

            Root = new FramedStack(SplitOrientation.Vertical)
                .Add(_Volumes, StackSize.Weighted(2, 4), "Volumes")
                .Add(new FramedStack(SplitOrientation.Horizontal)
                    .Add(_Bytes, StackSize.Weighted(1), "Disk bytes/s")
                    .Add(_Ops, StackSize.Weighted(1), "Disk operations/s"), StackSize.Weighted(3, 6))
                .Add(_Queues, StackSize.Fixed(2), "Queues");
        }

        #endregion

        #region Public-Methods

        public void Update()
        {
            HostSnapshot s = _State.Latest;
            if (s == null) return;
            double interval = s.IntervalSeconds > 0 ? s.IntervalSeconds : 1;
            HistoryBuffer h = _State.History;
            _Volumes.Bind(s.Volumes);
            _Bytes.SetValues(Charts.AtLeastTwo(_State.Window(h.GetSystemSeries(MetricIds.SystemDiskReadBytes), interval)));
            _BytesWrite.SetValues(Charts.AtLeastTwo(_State.Window(h.GetSystemSeries(MetricIds.SystemDiskWriteBytes), interval)));
            _Ops.SetValues(Charts.AtLeastTwo(_State.Window(h.GetSystemSeries(MetricIds.SystemDiskReadOps), interval)));
            _OpsWrite.SetValues(Charts.AtLeastTwo(_State.Window(h.GetSystemSeries(MetricIds.SystemDiskWriteOps), interval)));

            _Queues.Clear();
            string reason = _State.Unavailable(MetricIds.SystemDiskReadQueue);
            if (!s.System.DiskReadQueue.HasValue && reason != null)
                _Queues.WriteMarkup("[dim]Read/write queue: n/a (" + Markup.Escape(reason) + ")[/]");
            else
                _Queues.WriteMarkup("Read queue " + Charts.Fmt(s.System.DiskReadQueue, MetricUnitEnum.Count) + "   Write queue " + Charts.Fmt(s.System.DiskWriteQueue, MetricUnitEnum.Count)
                    + (reason != null ? "   [dim]" + Markup.Escape(reason) + "[/]" : string.Empty));
        }

        #endregion
    }
}
