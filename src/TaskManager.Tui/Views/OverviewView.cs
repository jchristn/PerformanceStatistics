using System;
using System.Globalization;
using System.Text;
using PerformanceStatistics.Monitoring;
using TUIKit;
using TUIKit.Content;
using TUIKit.Layout;
using TUIKit.Modals;
using TUIKit.Widgets;

namespace TaskManager.Tui
{
    /// <summary>
    /// Overview tab: CPU (total and per core), memory, disk I/O, and network over time. Tab moves between panels;
    /// Enter maximizes a chart or opens system details from the text panels.
    /// </summary>
    internal sealed class OverviewView
    {
        #region Public-Members

        public IWidget Root { get; private set; }

        #endregion

        #region Private-Members

        private readonly TaskManagerState _State;
        private readonly ChartPanel _Cpu;
        private readonly FocusPane _Cores = new FocusPane("cores", "system details");
        private readonly ChartPanel _Memory;
        private readonly FocusPane _MemoryText = new FocusPane("memory-text", "system details");
        private readonly ChartPanel _Disk;
        private readonly ChartPanel _Network;

        #endregion

        #region Constructors-and-Factories

        public OverviewView(TaskManagerState state, Action<Modal> show)
        {
            _State = state;
            _Cpu = Charts.Panel("CPU utilization", MetricUnitEnum.Percent, show);
            _Memory = Charts.Panel("Memory and swap in use", MetricUnitEnum.Bytes, show);
            _Disk = Charts.Panel("Disk I/O", MetricUnitEnum.BytesPerSecond, show);
            _Network = Charts.Panel("Network", MetricUnitEnum.BytesPerSecond, show);
            Action details = () => show(new DetailsModal("System", () => Details.System(_State)));
            _Cores.Activated += details;
            _MemoryText.Activated += details;

            FramedStack top = new FramedStack(SplitOrientation.Horizontal)
                .Add(_Cpu, StackSize.Weighted(3), "CPU utilization (%)")
                .Add(_Cores, StackSize.Weighted(2), "Per core");
            FramedStack middle = new FramedStack(SplitOrientation.Horizontal)
                .Add(_Memory, StackSize.Weighted(3), "Memory and swap in use")
                .Add(_MemoryText, StackSize.Weighted(2), "Memory and load");
            FramedStack bottom = new FramedStack(SplitOrientation.Horizontal)
                .Add(_Disk, StackSize.Weighted(1), "Disk I/O (bytes/s)")
                .Add(_Network, StackSize.Weighted(1), "Network (bytes/s)");

            Root = new FramedStack(SplitOrientation.Vertical)
                .Add(top, StackSize.Weighted(1, 6))
                .Add(middle, StackSize.Weighted(1, 6))
                .Add(bottom, StackSize.Weighted(1, 6));
        }

        #endregion

        #region Public-Methods

        public void Update()
        {
            HostSnapshot s = _State.Latest;
            if (s == null) return;
            HistoryBuffer h = _State.History;
            SystemSnapshot sys = s.System;

            _Cpu.Update(_State.Chart("CPU utilization", MetricUnitEnum.Percent, 100, ("CPU", Charts.Cpu, h.GetSystemSeries(MetricIds.SystemCpu))));
            _Memory.Update(_State.Chart("Memory and swap in use", MetricUnitEnum.Bytes, sys.MemoryTotalBytes,
                ("Used", Charts.Memory, h.GetSystemSeries(MetricIds.SystemMemoryUsed)),
                ("Swap", Charts.Swap, h.GetSystemSeries(MetricIds.SystemSwapUsed))));
            _Disk.Update(_State.Chart("Disk I/O", MetricUnitEnum.BytesPerSecond, null,
                ("Read", Charts.Read, h.GetSystemSeries(MetricIds.SystemDiskReadBytes)),
                ("Write", Charts.Write, h.GetSystemSeries(MetricIds.SystemDiskWriteBytes))));
            _Network.Update(_State.Chart("Network", MetricUnitEnum.BytesPerSecond, null,
                ("Received", Charts.Read, h.GetSystemSeries(MetricIds.SystemNetworkRx)),
                ("Sent", Charts.Write, h.GetSystemSeries(MetricIds.SystemNetworkTx))));

            Pane cores = _Cores.Pane;
            cores.Clear();
            double[] perCore = sys.PerCoreCpuPercent ?? new double[0];
            cores.WriteMarkup("[bold]Total[/] " + Charts.Bar(sys.CpuPercent, 14));

            // Lay cores out in columns so many-core machines fit without scrolling.
            int columns = perCore.Length > 48 ? 4 : perCore.Length > 24 ? 3 : perCore.Length > 12 ? 2 : 1;
            int barWidth = columns >= 3 ? 5 : columns == 2 ? 8 : 14;
            int rows = (perCore.Length + columns - 1) / columns;
            for (int r = 0; r < rows; r++)
            {
                StringBuilder line = new StringBuilder();
                for (int c = 0; c < columns; c++)
                {
                    int i = c * rows + r;
                    if (i >= perCore.Length) break;
                    if (c > 0) line.Append("  ");
                    line.Append("[dim]").Append(("#" + i.ToString(CultureInfo.InvariantCulture)).PadRight(4)).Append("[/]").Append(Charts.Bar(perCore[i], barWidth));
                }

                cores.WriteMarkup(line.ToString());
            }

            cores.ScrollUp(10000);

            double? memPct = sys.MemoryTotalBytes > 0 && sys.MemoryUsedBytes.HasValue ? 100.0 * sys.MemoryUsedBytes.Value / sys.MemoryTotalBytes.Value : (double?)null;
            double? swapPct = sys.SwapTotalBytes > 0 && sys.SwapUsedBytes.HasValue ? 100.0 * sys.SwapUsedBytes.Value / sys.SwapTotalBytes.Value : (double?)null;
            Pane text = _MemoryText.Pane;
            text.Clear();
            text.WriteMarkup("[bold]Memory[/]    " + Charts.Bar(memPct, 14));
            text.WriteMarkup("  used " + Charts.Fmt(sys.MemoryUsedBytes, MetricUnitEnum.Bytes) + " of " + Charts.Fmt(sys.MemoryTotalBytes, MetricUnitEnum.Bytes) + ", available " + Charts.Fmt(sys.MemoryAvailableBytes, MetricUnitEnum.Bytes));
            text.WriteMarkup("[bold]Swap[/]      " + Charts.Bar(swapPct, 14));
            text.WriteMarkup("  used " + Charts.Fmt(sys.SwapUsedBytes, MetricUnitEnum.Bytes) + " of " + Charts.Fmt(sys.SwapTotalBytes, MetricUnitEnum.Bytes));
            string load = sys.LoadAverage1.HasValue
                ? Charts.Fmt(sys.LoadAverage1, MetricUnitEnum.Count) + " / " + Charts.Fmt(sys.LoadAverage5, MetricUnitEnum.Count) + " / " + Charts.Fmt(sys.LoadAverage15, MetricUnitEnum.Count)
                : "[dim]n/a (" + Markup.Escape(_State.Unavailable(MetricIds.SystemLoad1) ?? "unavailable") + ")[/]";
            text.WriteMarkup("[bold]Load[/]      " + load);
            text.WriteMarkup("[bold]Processes[/] " + Charts.Fmt(sys.ProcessCount, MetricUnitEnum.Count) + "   [bold]Threads[/] " + Charts.Fmt(sys.ThreadCount, MetricUnitEnum.Count) + "   [bold]Handles[/] " + Charts.Fmt(sys.HandleCount, MetricUnitEnum.Count));
            text.WriteMarkup("[bold]Disk[/]      rd " + Charts.Fmt(sys.DiskReadBytesPerSecond, MetricUnitEnum.BytesPerSecond) + "  wr " + Charts.Fmt(sys.DiskWriteBytesPerSecond, MetricUnitEnum.BytesPerSecond));
            text.WriteMarkup("[bold]Network[/]   rx " + Charts.Fmt(sys.NetworkReceivedBytesPerSecond, MetricUnitEnum.BytesPerSecond) + "  tx " + Charts.Fmt(sys.NetworkSentBytesPerSecond, MetricUnitEnum.BytesPerSecond));
            text.ScrollUp(10000);
        }

        #endregion
    }
}
