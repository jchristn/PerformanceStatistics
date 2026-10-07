namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// What a metric describes.
    /// </summary>
    public enum MetricScopeEnum
    {
        /// <summary>The whole host.</summary>
        System,
        /// <summary>One process.</summary>
        Process,
        /// <summary>One network interface.</summary>
        Network
    }
}
