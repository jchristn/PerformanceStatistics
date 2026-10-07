using System;
using System.Collections.Generic;
using System.Linq;
using PerformanceStatistics.Monitoring;

namespace TaskManager.Agent
{
    /// <summary>
    /// Agent settings, bound from the "TASKMGR" configuration section (environment variables TASKMGR__*).
    /// </summary>
    public class AgentSettings
    {
        #region Public-Members

        /// <summary>
        /// Server URL, e.g. http://monitor.local:8080. Required.
        /// </summary>
        public string Server { get; set; } = null;

        /// <summary>
        /// Agent key configured on the server. Required.
        /// </summary>
        public string AgentKey { get; set; } = null;

        /// <summary>
        /// Host identifier; defaults to the lower-cased machine name.
        /// </summary>
        public string HostId { get; set; } = null;

        /// <summary>
        /// Sampling interval in seconds. Default 1.
        /// </summary>
        public double IntervalSeconds { get; set; } = 1;

        /// <summary>
        /// Process actions remote admins may request, comma-separated: Kill, KillTree, SetPriority, Suspend, Resume.
        /// Default none.
        /// </summary>
        public string AllowedActions { get; set; } = null;

        /// <summary>
        /// If set, only processes owned by these users (comma-separated) may be targeted.
        /// </summary>
        public string AllowedProcessUsers { get; set; } = null;

        /// <summary>
        /// Snapshots kept while the server is unreachable. Default 600 (10 minutes at 1s).
        /// </summary>
        public int BufferSize { get; set; } = 600;

        #endregion

        #region Public-Methods

        /// <summary>
        /// The action policy these settings describe.
        /// </summary>
        /// <returns>Policy.</returns>
        public ProcessActionPolicy ToPolicy()
        {
            ProcessActionPolicy policy = new ProcessActionPolicy();
            foreach (string name in Split(AllowedActions))
            {
                ProcessActionKindEnum kind;
                if (!Enum.TryParse(name, true, out kind)) throw new InvalidOperationException("Unknown action in TASKMGR__ALLOWEDACTIONS: " + name);
                policy.AllowedActions.Add(kind);
            }

            foreach (string user in Split(AllowedProcessUsers)) policy.AllowedProcessUsers.Add(user);
            return policy;
        }

        #endregion

        #region Private-Methods

        private static IEnumerable<string> Split(string value)
        {
            return (value ?? string.Empty).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0);
        }

        #endregion
    }
}
