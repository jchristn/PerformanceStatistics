using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;

namespace PerformanceStatistics.Mac
{
    /// <summary>
    /// macOS performance statistics.
    /// </summary>
    public class MacPerformanceStatistics : IPerformanceStatistics
    {
        private bool _disposed = false;
        private readonly object _lock = new object();
        private Dictionary<string, List<IProcessCounters>> _cachedMonitoredProcesses = null;

        #region Public-Members

        /// <summary>
        /// Statistics for the system.
        /// </summary>
        public ISystemCounters System { get; } = new MacSystemCounters();

        /// <summary>
        /// Monitored process names.
        /// </summary>
        public List<string> MonitoredProcessNames
        {
            get
            {
                return _monitoredProcessNames;
            }
            set
            {
                if (value == null) _monitoredProcessNames = new List<string>();
                else _monitoredProcessNames = value;
            }
        }

        /// <summary>
        /// Statistics for monitored processes.
        /// Refreshes process data on each access; previous process handles are disposed.
        /// </summary>
        public Dictionary<string, List<IProcessCounters>> MonitoredProcesses
        {
            get
            {
                lock (_lock)
                {
                    DisposeMonitoredProcesses();

                    _cachedMonitoredProcesses = new Dictionary<string, List<IProcessCounters>>();

                    if (_monitoredProcessNames != null && _monitoredProcessNames.Count > 0)
                    {
                        foreach (string processName in _monitoredProcessNames)
                        {
                            Process[] processes = null;
                            try
                            {
                                processes = Process.GetProcessesByName(processName);
                            }
                            catch
                            {
                                processes = new Process[0];
                            }

                            if (processes == null || processes.Length == 0)
                            {
                                _cachedMonitoredProcesses.Add(processName, new List<IProcessCounters>());
                            }
                            else
                            {
                                List<IProcessCounters> counters = new List<IProcessCounters>();

                                foreach (Process process in processes)
                                {
                                    counters.Add(new MacProcessCounters(process));
                                }

                                _cachedMonitoredProcesses.Add(processName, counters);
                            }
                        }
                    }

                    return _cachedMonitoredProcesses;
                }
            }
        }

        /// <summary>
        /// Active TCP connections.
        /// </summary>
        public TcpConnectionInformation[] ActiveTcpConnections
        {
            get
            {
                try
                {
                    TcpConnectionInformation[] ret = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
                    if (ret == null) return new TcpConnectionInformation[0];
                    return ret;
                }
                catch
                {
                    return new TcpConnectionInformation[0];
                }
            }
        }

        #endregion

        #region Private-Members

        private List<string> _monitoredProcessNames = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="monitoredProcesses">Monitored process names.</param>
        public MacPerformanceStatistics(List<string> monitoredProcesses = null)
        {
            if (monitoredProcesses != null) _monitoredProcessNames = monitoredProcesses;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Produce a human-readable object.
        /// </summary>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("--------------------------------------------------" + Environment.NewLine);
            sb.Append("System Counters                 : " + Environment.NewLine);
            sb.Append(((MacSystemCounters)System).ToString());
            // Snapshot volatile collections once: MonitoredProcesses re-queries (and disposes/rebuilds)
            // on every access, and ActiveTcpConnections re-reads the live TCP table on every access,
            // so reading .Length and indexing separately can race and throw IndexOutOfRangeException.
            Dictionary<string, List<IProcessCounters>> monitoredProcesses = MonitoredProcesses;
            sb.Append("Monitored Processes             : " + monitoredProcesses.Count + Environment.NewLine);

            if (monitoredProcesses.Count > 0)
            {
                foreach (KeyValuePair<string, List<IProcessCounters>> entry in monitoredProcesses)
                {
                    sb.Append("  " + entry.Key + Environment.NewLine);

                    if (entry.Value != null && entry.Value.Count > 0)
                    {
                        foreach (IProcessCounters stats in entry.Value)
                        {
                            sb.Append(stats.ToString() + "---" + Environment.NewLine);
                        }
                    }
                    else
                    {
                        sb.Append("  (no data)" + Environment.NewLine);
                    }
                }
            }

            TcpConnectionInformation[] activeTcpConnections = ActiveTcpConnections;
            sb.Append("Active TCP Connections          : " + activeTcpConnections.Length + Environment.NewLine);

            if (activeTcpConnections.Length > 0)
            {
                for (int i = 0; i < activeTcpConnections.Length; i++)
                {
                    sb.Append(
                        "  | " +
                        activeTcpConnections[i].LocalEndPoint.ToString() +
                        " to " +
                        activeTcpConnections[i].RemoteEndPoint.ToString() +
                        ": " +
                        activeTcpConnections[i].State.ToString() +
                        Environment.NewLine);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Retrieve active TCP connections by port (source, destination, or both).
        /// </summary>
        /// <param name="sourcePort">Source port.</param>
        /// <param name="destPort">Destination port.</param>
        /// <returns>Array of TCP connections.</returns>
        public TcpConnectionInformation[] GetActiveTcpConnections(int? sourcePort = null, int? destPort = null)
        {
            if (sourcePort != null && destPort != null)
            {
                return ActiveTcpConnections.Where(c =>
                    c.LocalEndPoint.Port == sourcePort.Value
                    && c.RemoteEndPoint.Port == destPort.Value).ToArray();
            }
            else if (sourcePort != null && destPort == null)
            {
                return ActiveTcpConnections.Where(c =>
                    c.LocalEndPoint.Port == sourcePort.Value).ToArray();
            }
            else if (sourcePort == null && destPort != null)
            {
                return ActiveTcpConnections.Where(c =>
                    c.RemoteEndPoint.Port == destPort.Value).ToArray();
            }

            return ActiveTcpConnections;
        }

        #endregion

        #region Private-Methods

        private void DisposeMonitoredProcesses()
        {
            if (_cachedMonitoredProcesses != null)
            {
                foreach (var kvp in _cachedMonitoredProcesses)
                {
                    if (kvp.Value != null)
                    {
                        foreach (var counter in kvp.Value)
                        {
                            counter?.Dispose();
                        }
                    }
                }
                _cachedMonitoredProcesses = null;
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">True if disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                lock (_lock)
                {
                    DisposeMonitoredProcesses();
                }
            }

            _disposed = true;
        }

        #endregion
    }
}
