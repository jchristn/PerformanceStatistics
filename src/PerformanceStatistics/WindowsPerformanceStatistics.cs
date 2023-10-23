using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace PerformanceStatistics
{
    /// <summary>
    /// Windows performance statistics.
    /// </summary>
    public class WindowsPerformanceStatistics : IPerformanceStatistics
    {
        #region Public-Members

        /// <summary>
        /// OS platform.
        /// </summary>
        public new OSPlatform Platform { get; } = OSPlatform.Windows;

        /// <summary>
        /// Statistics for the system.
        /// </summary>
        public new ISystemCounters System { get; } = new WindowsSystemCounters();

        /// <summary>
        /// Monitored process names.
        /// </summary>
        public List<string> MonitoredProcessNames
        { 
            get
            {
                return _MonitoredProcessNames;
            }
            set
            {
                if (value == null) _MonitoredProcessNames = new List<string>();
                else _MonitoredProcessNames = value;
            }
        }

        /// <summary>
        /// Statistics for monitored processes.
        /// </summary>
        public new Dictionary<string, List<IProcessCounters>> MonitoredProcesses
        {
            get
            {
                Dictionary<string, List<IProcessCounters>> ret = new Dictionary<string, List<IProcessCounters>>();

                if (_MonitoredProcessNames != null && _MonitoredProcessNames.Count > 0)
                {
                    foreach (string processName in _MonitoredProcessNames)
                    {
                        Process[] processes = Process.GetProcessesByName(processName);

                        if (processes == null || processes.Length == 0)
                        {
                            ret.Add(processName, new List<IProcessCounters>());
                        }
                        else
                        {
                            List<IProcessCounters> counters = new List<IProcessCounters>();

                            foreach (Process process in processes)
                            {
                                counters.Add(new WindowsProcessCounters(process));
                            }

                            ret.Add(processName, counters);
                        }
                    }
                }

                return ret;
            }
        }

        /// <summary>
        /// Active TCP connections.
        /// </summary>
        public new TcpConnectionInformation[] ActiveTcpConnections
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                TcpConnectionInformation[] ret = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
                if (ret == null) return new TcpConnectionInformation[0];
                return ret;
            }
        }

        #endregion

        #region Private-Members

        private List<string> _MonitoredProcessNames = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="monitoredProcesses">Monitored process names.</param>
        public WindowsPerformanceStatistics(List<string> monitoredProcesses = null)
        {
            if (monitoredProcesses != null) _MonitoredProcessNames = monitoredProcesses;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Produce a human-readable object.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("--------------------------------------------------" + Environment.NewLine);
            sb.Append("Operating System                : " + Platform.ToString() + Environment.NewLine);
            sb.Append("System Counters                 : " + Environment.NewLine);
            sb.Append(System.ToString());
            sb.Append("Monitored Processes             : " + MonitoredProcesses.Count + Environment.NewLine);

            if (MonitoredProcesses.Count > 0)
            {
                foreach (KeyValuePair<string, List<IProcessCounters>> entry in MonitoredProcesses)
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

            sb.Append("Active TCP Connections          : " + ActiveTcpConnections.Length + Environment.NewLine);

            if (ActiveTcpConnections.Length > 0)
            {
                for (int i = 0; i < ActiveTcpConnections.Length; i++)
                {
                    sb.Append(
                        "  | " +
                        ActiveTcpConnections[i].LocalEndPoint.ToString() +
                        " to " +
                        ActiveTcpConnections[i].RemoteEndPoint.ToString() +
                        ": " +
                        ActiveTcpConnections[i].State.ToString() +
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
        public override TcpConnectionInformation[] GetActiveTcpConnections(int? sourcePort = null, int? destPort = null)
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
            else if (sourcePort == null && destPort == null)
            {
                // do nothing
            }

            return ActiveTcpConnections;
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}