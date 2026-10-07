using System;
using System.Collections.Generic;
using System.Linq;
using PerformanceStatistics.Monitoring;
using TUIKit.Layout;
using TUIKit.Widgets;

namespace TaskManager.Tui
{
    /// <summary>
    /// Network tab: per-interface throughput, a chart for the selected interface, and active TCP connections.
    /// </summary>
    internal sealed class NetworkView
    {
        #region Public-Members

        public IWidget Root { get; private set; }

        #endregion

        #region Private-Members

        private readonly TaskManagerState _State;
        private readonly DataTable<NetworkInterfaceSnapshot> _Interfaces = new DataTable<NetworkInterfaceSnapshot>();
        private readonly DataTable<TcpConnectionSnapshot> _Tcp = new DataTable<TcpConnectionSnapshot>();
        private readonly LineChart _Chart;
        private readonly ChartSeries _Tx;

        #endregion

        #region Constructors-and-Factories

        public NetworkView(TaskManagerState state)
        {
            _State = state;
            _Interfaces.KeySelector = n => n.Name;
            _Interfaces
                .Column(new DataColumn<NetworkInterfaceSnapshot>("Interface", n => n.Name, true) { Width = 14 })
                .Column(new DataColumn<NetworkInterfaceSnapshot>("Rx", n => MetricFormatter.Format(n.ReceivedBytesPerSecond, MetricUnitEnum.BytesPerSecond), true) { Alignment = CellAlignment.Right, Width = 12, SortKey = n => n.ReceivedBytesPerSecond })
                .Column(new DataColumn<NetworkInterfaceSnapshot>("Tx", n => MetricFormatter.Format(n.SentBytesPerSecond, MetricUnitEnum.BytesPerSecond), true) { Alignment = CellAlignment.Right, Width = 12, SortKey = n => n.SentBytesPerSecond })
                .Column(new DataColumn<NetworkInterfaceSnapshot>("Rx pkts", n => MetricFormatter.Format(n.ReceivedPacketsPerSecond, MetricUnitEnum.PerSecond), true) { Alignment = CellAlignment.Right, Width = 10 })
                .Column(new DataColumn<NetworkInterfaceSnapshot>("Tx pkts", n => MetricFormatter.Format(n.SentPacketsPerSecond, MetricUnitEnum.PerSecond), true) { Alignment = CellAlignment.Right, Width = 10 })
                .Column(new DataColumn<NetworkInterfaceSnapshot>("Speed", n => n.SpeedBitsPerSecond.HasValue ? MetricFormatter.Number(n.SpeedBitsPerSecond.Value / 1e6) + " Mb/s" : MetricFormatter.NotAvailable) { Alignment = CellAlignment.Right, Width = 12 })
                .Column(new DataColumn<NetworkInterfaceSnapshot>("Description", n => n.Description));
            _Interfaces.StretchLastColumn = true;
            _Interfaces.SelectionChanged += (s, e) => { if (_Interfaces.SelectedRow != null) _State.SelectedInterface = _Interfaces.SelectedRow.Name; UpdateChart(); };

            _Tcp.Column(new DataColumn<TcpConnectionSnapshot>("Local", c => c.Local, true) { Width = 28 })
                .Column(new DataColumn<TcpConnectionSnapshot>("Remote", c => c.Remote, true) { Width = 28 })
                .Column(new DataColumn<TcpConnectionSnapshot>("State", c => c.State, true));
            _Tcp.StretchLastColumn = true;
            _Tcp.KeySelector = c => c.Local + ">" + c.Remote;

            _Chart = Charts.CreatePair("Received", Charts.Read, null, "Sent", Charts.Write, out _Tx);

            Root = new FramedStack(SplitOrientation.Vertical)
                .Add(_Interfaces, StackSize.Weighted(2, 4), "Interfaces (up, non-loopback)")
                .Add(_Chart, StackSize.Weighted(2, 5), "Selected interface (bytes/s)")
                .Add(_Tcp, StackSize.Weighted(3, 4), "TCP connections");
        }

        #endregion

        #region Public-Methods

        public void Update()
        {
            if (_State.Latest == null) return;
            _Interfaces.Bind(_State.Latest.Network);
            if (_State.SelectedInterface == null)
            {
                NetworkInterfaceSnapshot busiest = _State.Latest.Network.OrderByDescending(n => n.ReceivedBytesPerSecond + n.SentBytesPerSecond).FirstOrDefault();
                if (busiest != null) _State.SelectedInterface = busiest.Name;
            }

            _Tcp.Bind(_State.Latest.TcpConnections);
            UpdateChart();
        }

        #endregion

        #region Private-Methods

        private void UpdateChart()
        {
            if (_State.SelectedInterface == null || _State.Latest == null) return;
            double interval = _State.Latest.IntervalSeconds > 0 ? _State.Latest.IntervalSeconds : 1;
            _Chart.SetValues(Charts.AtLeastTwo(_State.Window(_State.History.GetInterfaceSeries(_State.SelectedInterface, MetricIds.NetworkRx), interval)));
            _Tx.SetValues(Charts.AtLeastTwo(_State.Window(_State.History.GetInterfaceSeries(_State.SelectedInterface, MetricIds.NetworkTx), interval)));
        }

        #endregion
    }
}
