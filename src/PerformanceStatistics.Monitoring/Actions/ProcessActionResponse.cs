using System;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// The outcome of a <see cref="ProcessActionRequest"/>.
    /// </summary>
    public class ProcessActionResponse
    {
        #region Public-Members

        /// <summary>
        /// Request ID.
        /// </summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>
        /// Host.
        /// </summary>
        public string HostId { get; set; } = string.Empty;

        /// <summary>
        /// Process ID.
        /// </summary>
        public int Pid { get; set; } = 0;

        /// <summary>
        /// Action.
        /// </summary>
        public ProcessActionKindEnum Action { get; set; } = ProcessActionKindEnum.Kill;

        /// <summary>
        /// Outcome: Success, NotFound, AccessDenied, NotSupported, Failed, PolicyDenied, Stale, or Pending.
        /// </summary>
        public string Result { get; set; } = "Pending";

        /// <summary>
        /// Explanation.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// When it completed (UTC), or null while pending.
        /// </summary>
        public DateTime? CompletedUtc { get; set; } = null;

        /// <summary>
        /// True when the action was applied.
        /// </summary>
        public bool Succeeded
        {
            get { return String.Equals(Result, ProcessActionResultEnum.Success.ToString(), StringComparison.Ordinal); }
        }

        #endregion
    }
}
