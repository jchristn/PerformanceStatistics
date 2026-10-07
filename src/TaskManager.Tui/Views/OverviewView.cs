using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PerformanceStatistics.Monitoring;
using TUIKit;
using TUIKit.Content;
using TUIKit.Layout;
using TUIKit.Widgets;

namespace TaskManager.Tui
{
    /// <summary>
    /// Overview tab: CPU (total and per core), memory, disk I/O, and network over time.
    /// </summary>
    internal sealed class OverviewView
    {
        #region Public-Members

        public IWidget Root { get; private set; }

        #endregion

        #region Private-Members

        private readonly LineChart _Cpu;
        private readonly Pane _Cores = new Pane("cores");
        private readonly LineChart _Memory;
        private readonly ChartSeries _Swap;
        private readonly Pane _MemoryText = new Pane("memory-text");
        private readonly LineChart _Disk;
        private readonly ChartSeries _DiskWrite;
        private readonly LineChart _Network;
        private readonly ChartSeries _NetworkTx;

        #endregion

        #region Constructors-and-Factories

        public OverviewView()
        {
            _Cpu = Charts.Create("CPU %", Charts.Cpu, 100);
            _Memory = Charts.CreatePair("Used", Charts.Memory, null, "Swap", Charts.Swap, out _Swap);
            _Disk = Charts.CreatePair("Read", Charts.Read, null, "Write", Charts.Write, out _DiskWrite);
            _Network = Charts.CreatePair("Received", Charts.Read, null, "Sent", Charts.Write, out _NetworkTx);

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

        public void Update(TaskManagerState state)
        {
            HostSnapshot s = state.Latest;
            if (s == null) return;
            double interval = s.IntervalSeconds > 0 ? s.IntervalSeconds : 1;
            HistoryBuffer h = state.History;

            _Cpu.SetValues(Charts.AtLeastTwo(state.Window(h.GetSystemSeries(MetricIds.SystemCpu), interval)));
            _Memory.SetValues(Charts.AtLeastTwo(state.Window(h.GetSystemSeries(MetricIds.SystemMemoryUsed), interval)));
            _Swap.SetValues(Charts.AtLeastTwo(state.Window(h.GetSystemSeries(MetricIds.SystemSwapUsed), interval)));
            _Memory.Maximum = s.System.MemoryTotalBytes;
            _Disk.SetValues(Charts.AtLeastTwo(state.Window(h.GetSystemSeries(MetricIds.SystemDiskReadBytes), interval)));
            _DiskWrite.SetValues(Charts.AtLeastTwo(state.Window(h.GetSystemSeries(MetricIds.SystemDiskWriteBytes), interval)));
            _Network.SetValues(Charts.AtLeastTwo(state.Window(h.GetSystemSeries(MetricIds.SystemNetworkRx), interval)));
            _NetworkTx.SetValues(Charts.AtLeastTwo(state.Window(h.GetSystemSeries(MetricIds.SystemNetworkTx), interval)));

            _Cores.Clear();
            double[] cores = s.System.PerCoreCpuPercent ?? new double[0];
            _Cores.WriteMarkup("[bold]Total[/] " + Charts.Bar(s.System.CpuPercent, 14));

            // Lay cores out in columns so many-core machines fit without scrolling.
            int columns = cores.Length > 48 ? 4 : cores.Length > 24 ? 3 : cores.Length > 12 ? 2 : 1;
            int barWidth = columns >= 3 ? 5 : columns == 2 ? 8 : 14;
            int rows = (cores.Length + columns - 1) / columns;
            for (int r = 0; r < rows; r++)
            {
                System.Text.StringBuilder line = new System.Text.StringBuilder();
                for (int c = 0; c < columns; c++)
                {
                    int i = c * rows + r;
                    if (i >= cores.Length) break;
                    if (c > 0) line.Append("  ");
                    line.Append("[dim]").Append(("#" + i.ToString(CultureInfo.InvariantCulture)).PadRight(4)).Append("[/]").Append(Charts.Bar(cores[i], barWidth));
                }

                _Cores.WriteMarkup(line.ToString());
            }

            _Cores.ScrollUp(10000);

            SystemSnapshot sys = s.System;
            double? memPct = sys.MemoryTotalBytes > 0 && sys.MemoryUsedBytes.HasValue ? 100.0 * sys.MemoryUsedBytes.Value / sys.MemoryTotalBytes.Value : (double?)null;
            double? swapPct = sys.SwapTotalBytes > 0 && sys.SwapUsedBytes.HasValue ? 100.0 * sys.SwapUsedBytes.Value / sys.SwapTotalBytes.Value : (double?)null;
            _MemoryText.Clear();
            _MemoryText.WriteMarkup("[bold]Memory[/]    " + Charts.Bar(memPct, 14));
            _MemoryText.WriteMarkup("  used " + Charts.Fmt(sys.MemoryUsedBytes, MetricUnitEnum.Bytes) + " of " + Charts.Fmt(sys.MemoryTotalBytes, MetricUnitEnum.Bytes) + ", available " + Charts.Fmt(sys.MemoryAvailableBytes, MetricUnitEnum.Bytes));
            _MemoryText.WriteMarkup("[bold]Swap[/]      " + Charts.Bar(swapPct, 14));
            _MemoryText.WriteMarkup("  used " + Charts.Fmt(sys.SwapUsedBytes, MetricUnitEnum.Bytes) + " of " + Charts.Fmt(sys.SwapTotalBytes, MetricUnitEnum.Bytes));
            string load = sys.LoadAverage1.HasValue
                ? Charts.Fmt(sys.LoadAverage1, MetricUnitEnum.Count) + " / " + Charts.Fmt(sys.LoadAverage5, MetricUnitEnum.Count) + " / " + Charts.Fmt(sys.LoadAverage15, MetricUnitEnum.Count)
                : "[dim]n/a (" + Markup.Escape(state.Unavailable(MetricIds.SystemLoad1) ?? "unavailable") + ")[/]";
            _MemoryText.WriteMarkup("[bold]Load[/]      " + load);
            _MemoryText.WriteMarkup("[bold]Processes[/] " + Charts.Fmt(sys.ProcessCount, MetricUnitEnum.Count) + "   [bold]Threads[/] " + Charts.Fmt(sys.ThreadCount, MetricUnitEnum.Count) + "   [bold]Handles[/] " + Charts.Fmt(sys.HandleCount, MetricUnitEnum.Count));
            _MemoryText.WriteMarkup("[bold]Disk[/]      rd " + Charts.Fmt(sys.DiskReadBytesPerSecond, MetricUnitEnum.BytesPerSecond) + "  wr " + Charts.Fmt(sys.DiskWriteBytesPerSecond, MetricUnitEnum.BytesPerSecond));
            _MemoryText.WriteMarkup("[bold]Network[/]   rx " + Charts.Fmt(sys.NetworkReceivedBytesPerSecond, MetricUnitEnum.BytesPerSecond) + "  tx " + Charts.Fmt(sys.NetworkSentBytesPerSecond, MetricUnitEnum.BytesPerSecond));
            _MemoryText.ScrollUp(10000);
        }

        #endregion
    }
}
