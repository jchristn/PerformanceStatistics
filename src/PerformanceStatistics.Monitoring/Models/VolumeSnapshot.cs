namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// One volume at one instant.
    /// </summary>
    public class VolumeSnapshot
    {
        #region Public-Members

        /// <summary>
        /// Mount point or drive.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Filesystem format.
        /// </summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>
        /// Capacity in bytes.
        /// </summary>
        public long TotalBytes { get; set; } = 0;

        /// <summary>
        /// Available bytes.
        /// </summary>
        public long FreeBytes { get; set; } = 0;

        /// <summary>
        /// Used bytes.
        /// </summary>
        public long UsedBytes { get; set; } = 0;

        #endregion
    }
}
