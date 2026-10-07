using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskManager.Server
{
    /// <summary>
    /// Server settings, bound from the "TASKMGR" configuration section (environment variables TASKMGR__*).
    /// </summary>
    public class ServerSettings
    {
        #region Public-Members

        /// <summary>
        /// Admin user name (may request process actions). Default "admin".
        /// </summary>
        public string AdminUser { get; set; } = "admin";

        /// <summary>
        /// Admin password. Required.
        /// </summary>
        public string AdminPassword { get; set; } = null;

        /// <summary>
        /// Optional read-only user name. Default "viewer".
        /// </summary>
        public string ViewerUser { get; set; } = "viewer";

        /// <summary>
        /// Optional read-only user password; the viewer account is disabled when empty.
        /// </summary>
        public string ViewerPassword { get; set; } = null;

        /// <summary>
        /// Agent keys, comma-separated (TASKMGR__AGENTKEY or TASKMGR__AGENTKEYS).
        /// </summary>
        public string AgentKey { get; set; } = null;

        /// <summary>
        /// Additional agent keys, comma-separated.
        /// </summary>
        public string AgentKeys { get; set; } = null;

        /// <summary>
        /// SQLite database path. Default "taskmgr.db" in the working directory.
        /// </summary>
        public string Storage { get; set; } = "taskmgr.db";

        /// <summary>
        /// Session lifetime in hours. Default 12.
        /// </summary>
        public double SessionHours { get; set; } = 12;

        /// <summary>
        /// A host is online if a snapshot arrived within this many seconds. Default 15.
        /// </summary>
        public double OnlineSeconds { get; set; } = 15;

        /// <summary>
        /// Raw retention in minutes. Default 60.
        /// </summary>
        public double RawRetentionMinutes { get; set; } = 60;

        /// <summary>
        /// System minute-aggregate retention in days. Default 30.
        /// </summary>
        public double MinuteRetentionDays { get; set; } = 30;

        /// <summary>
        /// Process minute-aggregate retention in hours. Default 24.
        /// </summary>
        public double ProcessMinuteRetentionHours { get; set; } = 24;

        /// <summary>
        /// Allowed CORS origins, comma-separated (only needed when the web app is served from another origin).
        /// </summary>
        public string CorsOrigins { get; set; } = null;

        #endregion

        #region Public-Methods

        /// <summary>
        /// All configured agent keys.
        /// </summary>
        /// <returns>Keys.</returns>
        public HashSet<string> GetAgentKeys()
        {
            return new HashSet<string>(
                ((AgentKey ?? string.Empty) + "," + (AgentKeys ?? string.Empty))
                    .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim())
                    .Where(k => k.Length > 0),
                StringComparer.Ordinal);
        }

        /// <summary>
        /// History store settings.
        /// </summary>
        /// <returns>Settings.</returns>
        public PerformanceStatistics.Monitoring.HistoryStoreSettings ToHistorySettings()
        {
            return new PerformanceStatistics.Monitoring.HistoryStoreSettings
            {
                RawRetention = TimeSpan.FromMinutes(Math.Max(1, RawRetentionMinutes)),
                MinuteRetention = TimeSpan.FromDays(Math.Max(1, MinuteRetentionDays)),
                ProcessMinuteRetention = TimeSpan.FromHours(Math.Max(1, ProcessMinuteRetentionHours))
            };
        }

        #endregion
    }
}
