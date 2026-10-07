namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// A process action.
    /// </summary>
    public enum ProcessActionKindEnum
    {
        /// <summary>Terminate the process.</summary>
        Kill,
        /// <summary>Terminate the process and its descendants.</summary>
        KillTree,
        /// <summary>Change priority.</summary>
        SetPriority,
        /// <summary>Suspend (stop).</summary>
        Suspend,
        /// <summary>Resume.</summary>
        Resume
    }
}
