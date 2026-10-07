namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// How fully a platform supports a metric.
    /// </summary>
    public enum AvailabilityEnum
    {
        /// <summary>Available with the documented meaning.</summary>
        Full,
        /// <summary>Available with a platform-specific meaning or a privilege requirement (see the note).</summary>
        Partial,
        /// <summary>Not available; the value is always null.</summary>
        Unavailable
    }
}
