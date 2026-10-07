namespace PerformanceStatistics
{
    /// <summary>
    /// Outcome of a process action.
    /// </summary>
    public enum ProcessActionResultEnum
    {
        /// <summary>
        /// The action was applied.
        /// </summary>
        Success,
        /// <summary>
        /// No process with that ID exists.
        /// </summary>
        NotFound,
        /// <summary>
        /// The caller lacks permission (another user's or a protected process; run elevated).
        /// </summary>
        AccessDenied,
        /// <summary>
        /// The action is not supported on this operating system.
        /// </summary>
        NotSupported,
        /// <summary>
        /// The action failed for another reason.
        /// </summary>
        Failed
    }
}
