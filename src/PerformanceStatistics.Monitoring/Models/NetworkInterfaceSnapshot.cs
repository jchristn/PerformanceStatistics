namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// One network interface at one instant.
    /// </summary>
    public class NetworkInterfaceSnapshot
    {
        #region Public-Members

        /// <summary>
        /// Interface name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Link speed in bits per second.
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
        /// Packets received per second.
        /// </summary>
        public double? ReceivedPacketsPerSecond { get; set; } = null;

        /// <summary>
        /// Packets sent per second.
        /// </summary>
        public double? SentPacketsPerSecond { get; set; } = null;

        #endregion
    }
}
