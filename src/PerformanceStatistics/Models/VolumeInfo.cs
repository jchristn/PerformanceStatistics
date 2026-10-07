namespace PerformanceStatistics
{
    /// <summary>
    /// A mounted volume counted toward disk space totals.
    /// </summary>
    public class VolumeInfo
    {
        #region Public-Members

        /// <summary>
        /// Mount point or drive name, e.g. "/" or "C:\".
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Filesystem format, e.g. "apfs", "ext4", or "NTFS".
        /// </summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>
        /// Capacity in bytes.
        /// </summary>
        public long TotalBytes { get; set; } = 0;

        /// <summary>
        /// Bytes available to the current user.
        /// </summary>
        public long FreeBytes { get; set; } = 0;

        /// <summary>
        /// Used bytes (capacity minus available).
        /// </summary>
        public long UsedBytes
        {
            get { return TotalBytes - FreeBytes; }
        }

        #endregion
    }
}
