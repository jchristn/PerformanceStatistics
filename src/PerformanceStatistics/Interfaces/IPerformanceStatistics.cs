using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace PerformanceStatistics
{
    /// <summary>
    /// Interface for platform performance statistics.
    /// </summary>
    public interface IPerformanceStatistics : IDisposable
    {
        /// <summary>
        /// System-level performance counters.
        /// </summary>
        ISystemCounters System { get; }

        /// <summary>
        /// List of process names to monitor.
        /// </summary>
        List<string> MonitoredProcessNames { get; set; }

        /// <summary>
        /// Performance data for monitored processes.
        /// Key: process name, Value: list of process counters for all instances with that name.
        /// Refreshes process data on each access; previous process handles are disposed.
        /// </summary>
        Dictionary<string, List<IProcessCounters>> MonitoredProcesses { get; }

        /// <summary>
        /// Active TCP connections.
        /// </summary>
        TcpConnectionInformation[] ActiveTcpConnections { get; }

        /// <summary>
        /// Retrieve active TCP connections filtered by source and/or destination port.
        /// </summary>
        /// <param name="sourcePort">Source port filter (optional).</param>
        /// <param name="destPort">Destination port filter (optional).</param>
        /// <returns>Array of TCP connections matching the filter criteria.</returns>
        TcpConnectionInformation[] GetActiveTcpConnections(int? sourcePort = null, int? destPort = null);
    }
}
