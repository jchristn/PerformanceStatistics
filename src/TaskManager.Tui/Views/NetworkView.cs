using System;
using System.Collections.Generic;
using System.Linq;
using PerformanceStatistics.Monitoring;
using TUIKit.Layout;
using TUIKit.Modals;
using TUIKit.Widgets;

namespace TaskManager.Tui
{
    /// <summary>
    /// Network tab: per-interface throughput and addresses, a chart for the selected interface, and active TCP
    /// connections. Enter on an interface or connection opens details; Enter on the chart maximizes it.
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
        private readonly ChartPanel _Chart;

        #endregion

        #region Constructors-and-Factories

        public NetworkView(TaskManagerState state, Action<Modal> show)
        {
            _State = state;
            _Interfaces.KeySelector = n => n.Name;
            _Interfaces
                .Column(new DataColumn<NetworkInterfaceSnapshot>("Interface", n => n.Name, true) { Width = 12 })
                .Column(new DataColumn<NetworkInterfaceSnapshot>("Addresses", n => Charts.AddressSummary(n.Addresses), true) { Width = 30 })
                .Column(new DataColumn<NetworkInterfaceSnapshot>("Rx", n => MetricFormatter.Format(n.ReceivedBytesPerSecond, MetricUnitEnum.BytesPerSecond), true) { Alignment = CellAlignment.Right, Width = 12, SortKey = n => n.ReceivedBytesPerSecond })
                .Column(new DataColumn<NetworkInterfaceSnapshot>("Tx", n => MetricFormatter.Format(n.SentBytesPerSecond, MetricUnitEnum.BytesPerSecond), true) { Alignment = CellAlignment.Right, Width = 12, SortKey = n => n.SentBytesPerSecond })
                .Column(new DataColumn<NetworkInterfaceSnapshot>("Rx pkts", n => MetricFormatter.Format(n.ReceivedPacketsPerSecond, MetricUnitEnum.PerSecond), true) { Alignment = CellAlignment.Right, Width = 9 })
                .Column(new DataColumn<NetworkInterfaceSnapshot>("Tx pkts", n => MetricFormatter.Format(n.SentPacketsPerSecond, MetricUnitEnum.PerSecond), true) { Alignment = CellAlignment.Right, Width = 9 })
                .Column(new DataColumn<NetworkInterfaceSnapshot>("Speed", n => n.SpeedBitsPerSecond.HasValue ? MetricFormatter.Number(n.SpeedBitsPerSecond.Value / 1e6) + " Mb/s" : MetricFormatter.NotAvailable) { Alignment = CellAlignment.Right, Width = 11 })
                .Column(new DataColumn<NetworkInterfaceSnapshot>("Type", n => n.InterfaceType, true) { Width = 14 })
                .Column(new DataColumn<NetworkInterfaceSnapshot>("Description", n => n.Description));
            _Interfaces.StretchLastColumn = true;
            _Interfaces.SelectionChanged += (s, e) => { if (_Interfaces.SelectedRow != null) _State.SelectedInterface = _Interfaces.SelectedRow.Name; UpdateChart(); };
            _Interfaces.RowActivated += index =>
            {
                NetworkInterfaceSnapshot row = _Interfaces.SelectedRow;
                if (row == null) return;
                string name = row.Name;
                show(new DetailsModal("Interface " + name, () => Details.Interface(_State, name)));
            };

            _Tcp.Column(new DataColumn<TcpConnectionSnapshot>("Local", c => c.Local, true) { Width = 30 })
                .Column(new DataColumn<TcpConnectionSnapshot>("Remote", c => c.Remote, true) { Width = 30 })
                .Column(new DataColumn<TcpConnectionSnapshot>("State", c => c.State, true));
            _Tcp.StretchLastColumn = true;
            _Tcp.KeySelector = c => c.Local + ">" + c.Remote;
            _Tcp.RowActivated += index =>
            {
                TcpConnectionSnapshot row = _Tcp.SelectedRow;
                if (row != null) show(new DetailsModal("TCP " + row.Local + " → " + row.Remote, () => Details.Connection(_State, row)));
            };

            _Chart = Charts.Panel("Interface throughput", MetricUnitEnum.BytesPerSecond, show);

            Root = new FramedStack(SplitOrientation.Vertical)
                .Add(_Interfaces, StackSize.Weighted(2, 4), "Interfaces (up, non-loopback) - Enter for details")
                .Add(_Chart, StackSize.Weighted(2, 5), "Selected interface (bytes/s)")
                .Add(_Tcp, StackSize.Weighted(3, 4), "TCP connections - Enter for details");
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
            string name = _State.SelectedInterface;
            if (name == null || _State.Latest == null) return;
            _Chart.Update(_State.Chart(name + " throughput", MetricUnitEnum.BytesPerSecond, null,
                ("Received", Charts.Read, _State.History.GetInterfaceSeries(name, MetricIds.NetworkRx)),
                ("Sent", Charts.Write, _State.History.GetInterfaceSeries(name, MetricIds.NetworkTx))));
        }

        #endregion
    }
}
