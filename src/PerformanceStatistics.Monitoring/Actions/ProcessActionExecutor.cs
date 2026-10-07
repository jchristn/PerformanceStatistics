using System;
using System.Collections.Generic;
using System.Linq;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// Applies <see cref="ProcessActionRequest"/>s on the local machine after checking the policy and guarding
    /// against PID reuse.
    /// </summary>
    public sealed class ProcessActionExecutor
    {
        #region Public-Members

        /// <summary>
        /// Policy.
        /// </summary>
        public ProcessActionPolicy Policy { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="policy">Policy.</param>
        public ProcessActionExecutor(ProcessActionPolicy policy)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Execute a request.
        /// </summary>
        /// <param name="request">Request.</param>
        /// <param name="processes">Current processes (for policy and PID-reuse checks); may be null.</param>
        /// <returns>Response.</returns>
        public ProcessActionResponse Execute(ProcessActionRequest request, IEnumerable<ProcessSnapshot> processes)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ProcessActionResponse response = new ProcessActionResponse
            {
                RequestId = request.Id,
                HostId = request.HostId,
                Pid = request.Pid,
                Action = request.Action
            };

            ProcessSnapshot target = processes?.FirstOrDefault(p => p.Pid == request.Pid);
            if (target != null && request.StartTimeUtc.HasValue && target.StartTimeUtc.HasValue
                && Math.Abs((target.StartTimeUtc.Value - request.StartTimeUtc.Value).TotalSeconds) > 1)
            {
                return Complete(response, "Stale", "PID " + request.Pid + " now belongs to a different process.");
            }

            string denied = Policy.Evaluate(request, target);
            if (denied != null) return Complete(response, "PolicyDenied", denied);

            ProcessActionResultEnum result;
            switch (request.Action)
            {
                case ProcessActionKindEnum.Kill: result = ProcessActions.Kill(request.Pid, false); break;
                case ProcessActionKindEnum.KillTree: result = ProcessActions.Kill(request.Pid, true); break;
                case ProcessActionKindEnum.SetPriority: result = ProcessActions.SetPriority(request.Pid, request.Priority.Value); break;
                case ProcessActionKindEnum.Suspend: result = ProcessActions.Suspend(request.Pid); break;
                case ProcessActionKindEnum.Resume: result = ProcessActions.Resume(request.Pid); break;
                default: result = ProcessActionResultEnum.NotSupported; break;
            }

            return Complete(response, result.ToString(), Describe(result));
        }

        #endregion

        #region Private-Methods

        private static ProcessActionResponse Complete(ProcessActionResponse response, string result, string message)
        {
            response.Result = result;
            response.Message = message;
            response.CompletedUtc = DateTime.UtcNow;
            return response;
        }

        private static string Describe(ProcessActionResultEnum result)
        {
            switch (result)
            {
                case ProcessActionResultEnum.Success: return "Done.";
                case ProcessActionResultEnum.NotFound: return "The process no longer exists.";
                case ProcessActionResultEnum.AccessDenied: return "Access denied. Run the collector elevated (Administrator or root) to act on other users' processes.";
                case ProcessActionResultEnum.NotSupported: return "Not supported on this platform.";
                default: return "The action failed.";
            }
        }

        #endregion
    }
}
