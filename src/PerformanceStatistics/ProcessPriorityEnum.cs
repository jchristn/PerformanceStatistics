namespace PerformanceStatistics
{
    /// <summary>
    /// Process priority on one scale for every operating system. On Windows these are priority classes;
    /// on Linux and macOS they map to nice values (Idle 19, BelowNormal 10, Normal 0, AboveNormal -5, High -10, RealTime -20).
    /// </summary>
    public enum ProcessPriorityEnum
    {
        /// <summary>
        /// Lowest priority.
        /// </summary>
        Idle,
        /// <summary>
        /// Below normal.
        /// </summary>
        BelowNormal,
        /// <summary>
        /// Normal.
        /// </summary>
        Normal,
        /// <summary>
        /// Above normal.
        /// </summary>
        AboveNormal,
        /// <summary>
        /// High.
        /// </summary>
        High,
        /// <summary>
        /// Real-time (Windows) or highest nice (Unix). Usually requires elevation.
        /// </summary>
        RealTime
    }
}
