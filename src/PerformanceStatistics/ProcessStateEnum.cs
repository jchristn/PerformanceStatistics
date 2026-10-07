namespace PerformanceStatistics
{
    /// <summary>
    /// Scheduling state of a process, normalized across operating systems.
    /// </summary>
    public enum ProcessStateEnum
    {
        /// <summary>
        /// State could not be determined (exited, or not permitted).
        /// </summary>
        Unknown,
        /// <summary>
        /// Running or runnable.
        /// </summary>
        Running,
        /// <summary>
        /// Sleeping (interruptible wait) or idle.
        /// </summary>
        Sleeping,
        /// <summary>
        /// Uninterruptible wait, usually disk I/O (Linux "D").
        /// </summary>
        Waiting,
        /// <summary>
        /// Stopped or suspended (SIGSTOP on Unix; all threads suspended on Windows).
        /// </summary>
        Stopped,
        /// <summary>
        /// Exited but not yet reaped by its parent.
        /// </summary>
        Zombie
    }
}
