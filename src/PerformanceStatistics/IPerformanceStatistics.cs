using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace PerformanceStatistics
{
    /// <summary>
    /// Base class for performance statistics.
    /// </summary>
    public abstract class IPerformanceStatistics
    {
        #region Public-Members

        /// <summary>
        /// OS platform.
        /// </summary>
        public OSPlatform Platform { get; set; } = OSPlatform.Windows;

        /// <summary>
        /// Statistics for the system.
        /// </summary>
        public ISystemCounters System { get; set; }

        /// <summary>
        /// Statistics for monitored processes.
        /// </summary>
        public Dictionary<string, List<ISystemCounters>> MonitoredProcesses
        {
            get
            {
                return _MonitoredProcesses;
            }
            set
            {
                if (value == null) _MonitoredProcesses = new Dictionary<string, List<ISystemCounters>>();
                else _MonitoredProcesses = value;
            }
        }

        /// <summary>
        /// Active TCP connections.
        /// </summary>
        public TcpConnectionInformation[] ActiveTcpConnections { get; set; }

        #endregion

        #region Private-Members

        private Dictionary<string, List<ISystemCounters>> _MonitoredProcesses = new Dictionary<string, List<ISystemCounters>>();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Retrieve active TCP connections by port (source, destination, or both).
        /// </summary>
        /// <param name="sourcePort">Source port.</param>
        /// <param name="destPort">Destination port.</param>
        /// <returns>Array of TCP connections.</returns>
        public abstract TcpConnectionInformation[] GetActiveTcpConnections(int? sourcePort = null, int? destPort = null);
        
        #endregion

        #region Private-Methods

        #endregion
    }
}