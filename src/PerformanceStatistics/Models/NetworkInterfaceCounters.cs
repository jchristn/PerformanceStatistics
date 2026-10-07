using System.Collections.Generic;

namespace PerformanceStatistics
{
    /// <summary>
    /// Throughput of one network interface, averaged over the interval since the previous read.
    /// Loopback and interfaces that are not up are excluded.
    /// </summary>
    public class NetworkInterfaceCounters
    {
        #region Public-Members

        /// <summary>
        /// Interface name, e.g. "en0", "eth0", or "Ethernet".
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Interface description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Unicast addresses in CIDR form, IPv4 first (e.g. "192.168.1.10/24", "fe80::1%en0/64").
        /// </summary>
        public List<string> Addresses { get; set; } = new List<string>();

        /// <summary>
        /// MAC address as colon-separated hex, or empty if the interface has none.
        /// </summary>
        public string MacAddress { get; set; } = string.Empty;

        /// <summary>
        /// Interface type, e.g. "Ethernet", "Wireless80211", or "Tunnel".
        /// </summary>
        public string InterfaceType { get; set; } = string.Empty;

        /// <summary>
        /// Link speed in bits per second, or null if unknown.
        /// </summary>
        public long? SpeedBitsPerSecond { get; set; } = null;

        /// <summary>
        /// Bytes received per second.
        /// </summary>
        public double ReceivedBytesPerSecond { get; set; } = 0;

        /// <summary>
        /// Bytes sent per second.
        /// </summary>
        public double SentBytesPerSecond { get; set; } = 0;

        /// <summary>
        /// Packets received per second, or null if the platform does not report packets.
        /// </summary>
        public double? ReceivedPacketsPerSecond { get; set; } = null;

        /// <summary>
        /// Packets sent per second, or null if the platform does not report packets.
        /// </summary>
        public double? SentPacketsPerSecond { get; set; } = null;

        #endregion
    }
}
