using System;
using PerformanceStatistics.Monitoring;
using TUIKit;
using TUIKit.Content;
using TUIKit.Layout;
using TUIKit.Modals;
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
        private readonly ChartPanel _Bytes;
        private readonly ChartPanel _Ops;
        private readonly Pane _Queues = new Pane("queues");

        #endregion

        #region Constructors-and-Factories

        public DisksView(TaskManagerState state, Action<Modal> show)
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

            _Volumes.RowActivated += index =>
            {
                VolumeSnapshot row = _Volumes.SelectedRow;
                if (row == null) return;
                string name = row.Name;
                show(new DetailsModal("Volume " + name, () =>
                {
                    VolumeSnapshot v = _State.Latest?.Volumes.Find(x => x.Name == name);
                    return v != null ? Details.Volume(v) : new DetailsContent().Row("Volume", name + " is no longer mounted.");
                }));
            };

            _Bytes = Charts.Panel("Disk bytes", MetricUnitEnum.BytesPerSecond, show);
            _Ops = Charts.Panel("Disk operations", MetricUnitEnum.PerSecond, show);

            Root = new FramedStack(SplitOrientation.Vertical)
                .Add(_Volumes, StackSize.Weighted(2, 4), "Volumes - Enter for details")
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
            HistoryBuffer h = _State.History;
            _Volumes.Bind(s.Volumes);
            _Bytes.Update(_State.Chart("Disk bytes", MetricUnitEnum.BytesPerSecond, null,
                ("Read", Charts.Read, h.GetSystemSeries(MetricIds.SystemDiskReadBytes)),
                ("Write", Charts.Write, h.GetSystemSeries(MetricIds.SystemDiskWriteBytes))));
            _Ops.Update(_State.Chart("Disk operations", MetricUnitEnum.PerSecond, null,
                ("Read ops", Charts.Read, h.GetSystemSeries(MetricIds.SystemDiskReadOps)),
                ("Write ops", Charts.Write, h.GetSystemSeries(MetricIds.SystemDiskWriteOps))));

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
