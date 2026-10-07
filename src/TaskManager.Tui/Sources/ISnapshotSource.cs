using System;
using System.Threading.Tasks;
using PerformanceStatistics.Monitoring;

namespace TaskManager.Tui
{
    /// <summary>
    /// Where snapshots come from: the local machine (in-process) or a TaskManager server.
    /// </summary>
    public interface ISnapshotSource : IDisposable
    {
        /// <summary>
        /// Raised on a background thread for every snapshot.
        /// </summary>
        event EventHandler<HostSnapshot> SnapshotReceived;

        /// <summary>
        /// Host facts.
        /// </summary>
        HostInfo HostInfo { get; }

        /// <summary>
        /// Short description for the header, e.g. "local" or "server http://host:8080".
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Sampling interval (local sources only; remote sources follow the agent).
        /// </summary>
        TimeSpan Interval { get; set; }

        /// <summary>
        /// Pause snapshot delivery.
        /// </summary>
        bool Paused { get; set; }

        /// <summary>
        /// True if process actions are possible from here.
        /// </summary>
        bool ActionsEnabled { get; }

        /// <summary>
        /// Begin delivering snapshots.
        /// </summary>
        void Start();

        /// <summary>
        /// Apply a process action and wait for its outcome.
        /// </summary>
        /// <param name="request">Request.</param>
        /// <returns>Response.</returns>
        Task<ProcessActionResponse> ExecuteAsync(ProcessActionRequest request);
    }
}
