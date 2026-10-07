using System;
using System.Collections.Generic;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// What a collector allows remote callers to do. Everything is denied by default; the local TUI uses
    /// <see cref="AllowAll"/> because the user at the keyboard already owns the machine.
    /// </summary>
    public class ProcessActionPolicy
    {
        #region Public-Members

        /// <summary>
        /// Allowed actions.
        /// </summary>
        public HashSet<ProcessActionKindEnum> AllowedActions { get; set; } = new HashSet<ProcessActionKindEnum>();

        /// <summary>
        /// If non-empty, only processes owned by these users may be targeted.
        /// </summary>
        public HashSet<string> AllowedProcessUsers { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Process names that may never be targeted (case-insensitive).
        /// </summary>
        public HashSet<string> ProtectedProcessNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System", "Idle", "csrss", "wininit", "winlogon", "smss", "services", "lsass", "kernel_task", "launchd", "init", "systemd"
        };

        #endregion

        #region Public-Methods

        /// <summary>
        /// A policy that allows every action except on protected system processes.
        /// </summary>
        /// <returns>Policy.</returns>
        public static ProcessActionPolicy AllowAll()
        {
            ProcessActionPolicy ret = new ProcessActionPolicy();
            foreach (ProcessActionKindEnum kind in Enum.GetValues(typeof(ProcessActionKindEnum))) ret.AllowedActions.Add(kind);
            return ret;
        }

        /// <summary>
        /// Evaluate a request against a target process.
        /// </summary>
        /// <param name="request">Request.</param>
        /// <param name="target">Current target process, or null if unknown.</param>
        /// <returns>Null if allowed, otherwise the reason it is denied.</returns>
        public string Evaluate(ProcessActionRequest request, ProcessSnapshot target)
        {
            if (request == null) return "No request.";
            if (!AllowedActions.Contains(request.Action)) return "Action " + request.Action + " is not enabled on this host.";
            if (request.Action == ProcessActionKindEnum.SetPriority && !request.Priority.HasValue) return "SetPriority requires a priority.";
            if (request.Pid <= 4) return "System processes cannot be targeted.";
            if (target == null) return null;
            if (ProtectedProcessNames.Contains(target.Name)) return "Process " + target.Name + " is protected.";
            if (AllowedProcessUsers.Count > 0 && !AllowedProcessUsers.Contains(target.User ?? string.Empty)) return "Processes owned by " + target.User + " may not be targeted.";
            return null;
        }

        #endregion
    }
}
