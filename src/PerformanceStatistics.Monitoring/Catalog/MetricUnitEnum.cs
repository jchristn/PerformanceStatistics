namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// Unit of a metric value.
    /// </summary>
    public enum MetricUnitEnum
    {
        /// <summary>Percentage, 0-100.</summary>
        Percent,
        /// <summary>Bytes.</summary>
        Bytes,
        /// <summary>Bytes per second.</summary>
        BytesPerSecond,
        /// <summary>Events or operations per second.</summary>
        PerSecond,
        /// <summary>Dimensionless count or ratio.</summary>
        Count,
        /// <summary>Seconds (durations).</summary>
        Seconds,
        /// <summary>Timestamp (UTC).</summary>
        Timestamp,
        /// <summary>Text.</summary>
        Text
    }
}
