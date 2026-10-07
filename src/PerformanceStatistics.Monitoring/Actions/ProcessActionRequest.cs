using System;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// A request to act on a process.
    /// </summary>
    public class ProcessActionRequest
    {
        #region Public-Members

        /// <summary>
        /// Request ID.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Target host.
        /// </summary>
        public string HostId { get; set; } = string.Empty;

        /// <summary>
        /// Target process ID.
        /// </summary>
        public int Pid { get; set; } = 0;

        /// <summary>
        /// Expected start time of the target; when set, the action is refused if the PID now belongs to a different
        /// process (PID reuse).
        /// </summary>
        public DateTime? StartTimeUtc { get; set; } = null;

        /// <summary>
        /// Action.
        /// </summary>
        public ProcessActionKindEnum Action { get; set; } = ProcessActionKindEnum.Kill;

        /// <summary>
        /// New priority for <see cref="ProcessActionKindEnum.SetPriority"/>.
        /// </summary>
        public ProcessPriorityEnum? Priority { get; set; } = null;

        /// <summary>
        /// Who asked.
        /// </summary>
        public string RequestedBy { get; set; } = string.Empty;

        /// <summary>
        /// When it was requested (UTC).
        /// </summary>
        public DateTime RequestedUtc { get; set; } = DateTime.UtcNow;

        #endregion
    }
}
