using System;
using System.Collections.Generic;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// Everything sampled from one host at one instant. This is the unit the TUI renders, the agent sends,
    /// the server stores, and the web app receives.
    /// </summary>
    public class HostSnapshot
    {
        #region Public-Members

        /// <summary>
        /// Host identifier.
        /// </summary>
        public string HostId { get; set; } = string.Empty;

        /// <summary>
        /// Sample time (UTC).
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Seconds since the previous snapshot (the averaging interval for rates).
        /// </summary>
        public double IntervalSeconds { get; set; } = 0;

        /// <summary>
        /// System metrics.
        /// </summary>
        public SystemSnapshot System { get; set; } = new SystemSnapshot();

        /// <summary>
        /// Processes, ordered by PID. Empty when process collection is disabled.
        /// </summary>
        public List<ProcessSnapshot> Processes { get; set; } = new List<ProcessSnapshot>();

        /// <summary>
        /// Network interfaces.
        /// </summary>
        public List<NetworkInterfaceSnapshot> Network { get; set; } = new List<NetworkInterfaceSnapshot>();

        /// <summary>
        /// Volumes.
        /// </summary>
        public List<VolumeSnapshot> Volumes { get; set; } = new List<VolumeSnapshot>();

        /// <summary>
        /// Active TCP connections (refreshed every few snapshots to bound cost).
        /// </summary>
        public List<TcpConnectionSnapshot> TcpConnections { get; set; } = new List<TcpConnectionSnapshot>();

        #endregion
    }
}
