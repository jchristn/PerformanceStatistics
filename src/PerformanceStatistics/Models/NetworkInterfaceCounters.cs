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
