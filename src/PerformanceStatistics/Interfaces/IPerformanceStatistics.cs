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
        /// Re-scans process names on each access. A process that is still running keeps the same counter instance
        /// (refreshed with current values), so CPU is measured between accesses; counters for exited processes or
        /// removed names are disposed.
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

        /// <summary>
        /// Static facts about the host: name, OS, architecture, CPU model, logical processors, memory, boot time.
        /// </summary>
        SystemInfo SystemInfo { get; }

        /// <summary>
        /// Counters for every running process the caller can see.
        /// Like <see cref="MonitoredProcesses"/>, a process that is still running keeps the same counter instance
        /// across calls (refreshed with current values), so rates are measured between calls; counters for exited
        /// processes are disposed. Call once per sampling pass.
        /// </summary>
        /// <returns>Process counters, ordered by process ID.</returns>
        IReadOnlyList<IProcessCounters> GetAllProcesses();
    }
}
