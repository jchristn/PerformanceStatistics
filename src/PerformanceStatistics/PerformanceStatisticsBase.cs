using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;

namespace PerformanceStatistics
{
    /// <summary>
    /// Shared implementation of <see cref="IPerformanceStatistics"/>: monitored-process tracking, TCP
    /// connections, formatting, and disposal. Platform classes supply only their system and process counters.
    /// </summary>
    public abstract class PerformanceStatisticsBase : IPerformanceStatistics
    {
        #region Public-Members

        /// <summary>
        /// System-wide counters.
        /// </summary>
        public ISystemCounters System
        {
            get { return _System; }
        }

        /// <summary>
        /// Names of processes to monitor (without extension, e.g. "dotnet"). Setting null clears the list.
        /// </summary>
        public List<string> MonitoredProcessNames
        {
            get
            {
                return _MonitoredProcessNames;
            }
            set
            {
                _MonitoredProcessNames = value ?? new List<string>();
            }
        }

        /// <summary>
        /// Counters for every running process whose name is in <see cref="MonitoredProcessNames"/>, keyed by
        /// that name; a name with no running process maps to an empty list.
        /// <para>Each access re-scans running processes. A process that is still running keeps the same
        /// <see cref="IProcessCounters"/> instance across accesses, refreshed with current values, so CPU
        /// utilization measures the interval between accesses. Counters for processes that have exited, or
        /// whose names were removed, are disposed. Read this property once per sampling pass and reuse the result.</para>
        /// </summary>
        public Dictionary<string, List<IProcessCounters>> MonitoredProcesses
        {
            get
            {
                lock (_Lock)
                {
                    Dictionary<string, List<IProcessCounters>> ret = new Dictionary<string, List<IProcessCounters>>();
                    if (_Disposed) return ret;

                    HashSet<int> seen = new HashSet<int>();

                    foreach (string name in SnapshotNames())
                    {
                        if (ret.ContainsKey(name)) continue;

                        List<IProcessCounters> counters = new List<IProcessCounters>();
                        ret.Add(name, counters);
                        if (String.IsNullOrWhiteSpace(name)) continue;

                        foreach (Process process in GetProcessesByName(name))
                        {
                            int pid;
                            ProcessCountersBase tracked = Track(process, out pid);
                            if (tracked == null) continue;
                            seen.Add(pid);
                            counters.Add(tracked);
                        }
                    }

                    foreach (int stalePid in _Tracked.Keys.Where(k => !seen.Contains(k)).ToList())
                    {
                        _Tracked[stalePid].Dispose();
                        _Tracked.Remove(stalePid);
                    }

                    return ret;
                }
            }
        }

        /// <summary>
        /// Active TCP connections. Each access re-reads the operating system's connection table, so store the
        /// result in a local before reading its length and indexing it.
        /// </summary>
        public TcpConnectionInformation[] ActiveTcpConnections
        {
            get
            {
                try
                {
                    TcpConnectionInformation[] ret = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
                    return ret ?? new TcpConnectionInformation[0];
                }
                catch
                {
                    return new TcpConnectionInformation[0];
                }
            }
        }

        #endregion

        #region Private-Members

        private readonly object _Lock = new object();
        private readonly SystemCountersBase _System;
        private readonly Dictionary<int, ProcessCountersBase> _Tracked = new Dictionary<int, ProcessCountersBase>();
        private List<string> _MonitoredProcessNames = new List<string>();
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="system">System counters; owned and disposed by this instance.</param>
        /// <param name="monitoredProcesses">Initial process names to monitor; may be null.</param>
        internal PerformanceStatisticsBase(SystemCountersBase system, List<string> monitoredProcesses)
        {
            _System = system ?? throw new ArgumentNullException(nameof(system));
            if (monitoredProcesses != null) _MonitoredProcessNames = monitoredProcesses;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Active TCP connections filtered by local (source) and/or remote (destination) port.
        /// </summary>
        /// <param name="sourcePort">Local port, or null for any.</param>
        /// <param name="destPort">Remote port, or null for any.</param>
        /// <returns>Matching connections.</returns>
        public TcpConnectionInformation[] GetActiveTcpConnections(int? sourcePort = null, int? destPort = null)
        {
            TcpConnectionInformation[] all = ActiveTcpConnections;
            if (sourcePort == null && destPort == null) return all;

            return all.Where(c =>
                (sourcePort == null || c.LocalEndPoint.Port == sourcePort.Value)
                && (destPort == null || c.RemoteEndPoint.Port == destPort.Value)).ToArray();
        }

        /// <summary>
        /// Human-readable summary of system counters, monitored processes, and TCP connections.
        /// </summary>
        /// <returns>String.</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("--------------------------------------------------" + Environment.NewLine);
            sb.Append("System Counters                 : " + Environment.NewLine);
            sb.Append(_System.ToString());

            Dictionary<string, List<IProcessCounters>> monitoredProcesses = MonitoredProcesses;
            sb.Append("Monitored Processes             : " + monitoredProcesses.Count + Environment.NewLine);
            foreach (KeyValuePair<string, List<IProcessCounters>> entry in monitoredProcesses)
            {
                sb.Append("  " + entry.Key + Environment.NewLine);
                if (entry.Value.Count < 1)
                {
                    sb.Append("  (no data)" + Environment.NewLine);
                    continue;
                }

                foreach (IProcessCounters counters in entry.Value)
                {
                    sb.Append(counters.ToString() + "---" + Environment.NewLine);
                }
            }

            TcpConnectionInformation[] connections = ActiveTcpConnections;
            sb.Append("Active TCP Connections          : " + connections.Length + Environment.NewLine);
            foreach (TcpConnectionInformation connection in connections)
            {
                sb.Append(
                    "  | " + connection.LocalEndPoint +
                    " to " + connection.RemoteEndPoint +
                    ": " + connection.State +
                    Environment.NewLine);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Dispose all tracked process counters and the system counters.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Internal-and-Protected-Methods

        /// <summary>
        /// Create platform-specific counters for a process.
        /// </summary>
        /// <param name="process">Process; ownership transfers to the returned counters.</param>
        /// <returns>Counters.</returns>
        internal abstract ProcessCountersBase CreateProcessCounters(Process process);

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            lock (_Lock)
            {
                if (_Disposed) return;

                if (disposing)
                {
                    foreach (ProcessCountersBase counters in _Tracked.Values) counters.Dispose();
                    _Tracked.Clear();
                    _System.Dispose();
                }

                _Disposed = true;
            }
        }

        #endregion

        #region Private-Methods

        private List<string> SnapshotNames()
        {
            List<string> names = _MonitoredProcessNames;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    return new List<string>(names);
                }
                catch (InvalidOperationException)
                {
                    // The caller modified the list during the copy; try again.
                }
                catch (ArgumentException)
                {
                    // Same as above on some runtimes.
                }
            }

            return new List<string>();
        }

        private static Process[] GetProcessesByName(string name)
        {
            try
            {
                return Process.GetProcessesByName(name) ?? new Process[0];
            }
            catch
            {
                return new Process[0];
            }
        }

        private ProcessCountersBase Track(Process process, out int pid)
        {
            pid = -1;
            try
            {
                pid = process.Id;
            }
            catch
            {
                process.Dispose();
                return null;
            }

            ProcessCountersBase existing;
            if (_Tracked.TryGetValue(pid, out existing))
            {
                if (!existing.HasExited())
                {
                    process.Dispose();
                    existing.Refresh();
                    return existing;
                }

                // The PID was reused by a new process.
                existing.Dispose();
                _Tracked.Remove(pid);
            }

            ProcessCountersBase created = CreateProcessCounters(process);
            _Tracked[pid] = created;
            return created;
        }

        #endregion
    }
}
