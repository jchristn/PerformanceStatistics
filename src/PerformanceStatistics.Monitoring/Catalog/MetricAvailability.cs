namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// Availability of a metric on one platform.
    /// </summary>
    public class MetricAvailability
    {
        #region Public-Members

        /// <summary>
        /// Level.
        /// </summary>
        public AvailabilityEnum Level { get; set; } = AvailabilityEnum.Full;

        /// <summary>
        /// Explanation shown as the "n/a" reason or tooltip; null when fully available.
        /// </summary>
        public string Note { get; set; } = null;

        #endregion
    }
}
