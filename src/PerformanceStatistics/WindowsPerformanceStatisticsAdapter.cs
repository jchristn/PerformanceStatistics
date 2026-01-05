using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

namespace PerformanceStatistics
{
    /// <summary>
    /// Adapter to make WindowsPerformanceStatistics implement IPerformanceStatistics.
    /// This allows the existing Windows implementation to be used through the common interface
    /// without modifying the original class.
    /// </summary>
    internal class WindowsPerformanceStatisticsAdapter : IPerformanceStatistics
    {
        private readonly WindowsPerformanceStatistics _inner;
        private readonly WindowsSystemCountersAdapter _systemAdapter;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="monitoredProcesses">Optional list of process names to monitor.</param>
        public WindowsPerformanceStatisticsAdapter(List<string> monitoredProcesses = null)
        {
            _inner = new WindowsPerformanceStatistics(monitoredProcesses);
            _systemAdapter = new WindowsSystemCountersAdapter(_inner.System);
        }

        /// <summary>
        /// System-level performance counters.
        /// </summary>
        public ISystemCounters System => _systemAdapter;

        /// <summary>
        /// List of process names to monitor.
        /// </summary>
        public List<string> MonitoredProcessNames
        {
            get => _inner.MonitoredProcessNames;
            set => _inner.MonitoredProcessNames = value;
        }

        /// <summary>
        /// Performance data for monitored processes.
        /// </summary>
        public Dictionary<string, List<IProcessCounters>> MonitoredProcesses
        {
            get
            {
                var result = new Dictionary<string, List<IProcessCounters>>();
                foreach (var kvp in _inner.MonitoredProcesses)
                {
                    result[kvp.Key] = kvp.Value
                        .Select(p => (IProcessCounters)new WindowsProcessCountersAdapter(p))
                        .ToList();
                }
                return result;
            }
        }

        /// <summary>
        /// Active TCP connections.
        /// </summary>
        public TcpConnectionInformation[] ActiveTcpConnections => _inner.ActiveTcpConnections;

        /// <summary>
        /// Retrieve active TCP connections filtered by source and/or destination port.
        /// </summary>
        public TcpConnectionInformation[] GetActiveTcpConnections(int? sourcePort = null, int? destPort = null)
            => _inner.GetActiveTcpConnections(sourcePort, destPort);

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            _inner.Dispose();
        }
    }
}
