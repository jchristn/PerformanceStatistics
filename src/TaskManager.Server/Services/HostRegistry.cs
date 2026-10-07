using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using PerformanceStatistics.Monitoring;

namespace TaskManager.Server
{
    /// <summary>
    /// Live state per host: facts, allowed actions, latest snapshot, stream subscribers, and the action queue.
    /// </summary>
    public sealed class HostRegistry
    {
        #region Private-Members

        private readonly ServerSettings _Settings;
        private readonly ConcurrentDictionary<string, HostEntry> _Hosts = new ConcurrentDictionary<string, HostEntry>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Constructors-and-Factories

        public HostRegistry(ServerSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register or update a host from an agent hello.
        /// </summary>
        public void Hello(AgentHello hello)
        {
            HostEntry entry = Get(hello.Info.HostId, true);
            lock (entry)
            {
                entry.Info = hello.Info;
                entry.AllowedActions = hello.AllowedActions ?? new List<ProcessActionKindEnum>();
            }
        }

        /// <summary>
        /// Seed a host known from storage (no live data yet).
        /// </summary>
        public void Seed(HostRecord record)
        {
            HostEntry entry = Get(record.Info.HostId, true);
            lock (entry)
            {
                if (entry.Info == null || String.IsNullOrEmpty(entry.Info.HostName)) entry.Info = record.Info;
                if (!entry.LastSeenUtc.HasValue) entry.LastSeenUtc = record.LastSeenUtc;
            }
        }

        /// <summary>
        /// Record a snapshot and fan it out to stream subscribers.
        /// </summary>
        public void Publish(HostSnapshot snapshot)
        {
            HostEntry entry = Get(snapshot.HostId, true);
            List<Channel<HostSnapshot>> subscribers;
            lock (entry)
            {
                if (entry.Latest == null || snapshot.TimestampUtc >= entry.Latest.TimestampUtc)
                {
                    entry.Latest = snapshot;
                    entry.LastSeenUtc = snapshot.TimestampUtc;
                }

                subscribers = entry.Subscribers.ToList();
            }

            foreach (Channel<HostSnapshot> s in subscribers) s.Writer.TryWrite(snapshot);
        }

        /// <summary>
        /// Latest snapshot of a host, or null.
        /// </summary>
        public HostSnapshot Latest(string hostId)
        {
            HostEntry entry = Get(hostId, false);
            if (entry == null) return null;
            lock (entry) return entry.Latest;
        }

        /// <summary>
        /// True if the host exists.
        /// </summary>
        public bool Exists(string hostId)
        {
            return hostId != null && _Hosts.ContainsKey(hostId);
        }

        /// <summary>
        /// Every host with its status.
        /// </summary>
        public List<HostStatus> Statuses()
        {
            DateTime now = DateTime.UtcNow;
            List<HostStatus> ret = new List<HostStatus>();
            foreach (HostEntry entry in _Hosts.Values)
            {
                lock (entry)
                {
                    ret.Add(new HostStatus
                    {
                        Info = entry.Info ?? new HostInfo { HostId = entry.HostId, HostName = entry.HostId },
                        LastSeenUtc = entry.LastSeenUtc,
                        Online = entry.LastSeenUtc.HasValue && (now - entry.LastSeenUtc.Value).TotalSeconds <= _Settings.OnlineSeconds,
                        AllowedActions = entry.AllowedActions.ToList(),
                        CpuPercent = entry.Latest?.System.CpuPercent,
                        MemoryUsedBytes = entry.Latest?.System.MemoryUsedBytes
                    });
                }
            }

            return ret.OrderBy(h => h.Info.HostId, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Allowed actions for a host.
        /// </summary>
        public List<ProcessActionKindEnum> AllowedActions(string hostId)
        {
            HostEntry entry = Get(hostId, false);
            if (entry == null) return new List<ProcessActionKindEnum>();
            lock (entry) return entry.AllowedActions.ToList();
        }

        /// <summary>
        /// Subscribe to a host's snapshots.
        /// </summary>
        public Channel<HostSnapshot> Subscribe(string hostId)
        {
            Channel<HostSnapshot> channel = Channel.CreateBounded<HostSnapshot>(new BoundedChannelOptions(4) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
            HostEntry entry = Get(hostId, true);
            lock (entry) entry.Subscribers.Add(channel);
            return channel;
        }

        /// <summary>
        /// Remove a subscription.
        /// </summary>
        public void Unsubscribe(string hostId, Channel<HostSnapshot> channel)
        {
            HostEntry entry = Get(hostId, false);
            if (entry == null) return;
            lock (entry) entry.Subscribers.Remove(channel);
            channel.Writer.TryComplete();
        }

        /// <summary>
        /// Queue an action for the host's agent.
        /// </summary>
        public ProcessActionResponse Enqueue(ProcessActionRequest request)
        {
            HostEntry entry = Get(request.HostId, true);
            ProcessActionResponse pending = new ProcessActionResponse { RequestId = request.Id, HostId = request.HostId, Pid = request.Pid, Action = request.Action, Result = "Pending", Message = "Waiting for the agent." };
            lock (entry)
            {
                entry.Pending.Enqueue(request);
                entry.Responses[request.Id] = pending;
                Trim(entry);
            }

            return pending;
        }

        /// <summary>
        /// Take queued actions for a host (called by its agent).
        /// </summary>
        public List<ProcessActionRequest> TakePending(string hostId)
        {
            HostEntry entry = Get(hostId, false);
            List<ProcessActionRequest> ret = new List<ProcessActionRequest>();
            if (entry == null) return ret;
            lock (entry)
            {
                while (entry.Pending.Count > 0)
                {
                    ProcessActionRequest r = entry.Pending.Dequeue();
                    // Requests older than two minutes are stale: the screen the user acted on is long gone.
                    if (DateTime.UtcNow - r.RequestedUtc > TimeSpan.FromMinutes(2))
                    {
                        entry.Responses[r.Id] = new ProcessActionResponse { RequestId = r.Id, HostId = hostId, Pid = r.Pid, Action = r.Action, Result = "Stale", Message = "The agent did not pick up the request in time.", CompletedUtc = DateTime.UtcNow };
                        continue;
                    }

                    ret.Add(r);
                }
            }

            return ret;
        }

        /// <summary>
        /// Record action results from an agent.
        /// </summary>
        public void Complete(string hostId, IEnumerable<ProcessActionResponse> responses)
        {
            HostEntry entry = Get(hostId, true);
            lock (entry)
            {
                foreach (ProcessActionResponse r in responses) entry.Responses[r.RequestId] = r;
            }
        }

        /// <summary>
        /// An action's status, or null.
        /// </summary>
        public ProcessActionResponse Response(string hostId, string requestId)
        {
            HostEntry entry = Get(hostId, false);
            if (entry == null) return null;
            lock (entry)
            {
                ProcessActionResponse r;
                return entry.Responses.TryGetValue(requestId, out r) ? r : null;
            }
        }

        #endregion

        #region Private-Methods

        private HostEntry Get(string hostId, bool create)
        {
            if (String.IsNullOrWhiteSpace(hostId)) return null;
            HostEntry entry;
            if (_Hosts.TryGetValue(hostId, out entry)) return entry;
            if (!create) return null;
            return _Hosts.GetOrAdd(hostId, id => new HostEntry { HostId = id });
        }

        private static void Trim(HostEntry entry)
        {
            if (entry.Responses.Count <= 500) return;
            foreach (string key in entry.Responses.Values.Where(r => r.CompletedUtc.HasValue).OrderBy(r => r.CompletedUtc).Take(entry.Responses.Count - 500).Select(r => r.RequestId).ToList())
                entry.Responses.Remove(key);
        }

        #endregion

        #region Private-Classes

        private sealed class HostEntry
        {
            public string HostId;
            public HostInfo Info;
            public List<ProcessActionKindEnum> AllowedActions = new List<ProcessActionKindEnum>();
            public HostSnapshot Latest;
            public DateTime? LastSeenUtc;
            public readonly List<Channel<HostSnapshot>> Subscribers = new List<Channel<HostSnapshot>>();
            public readonly Queue<ProcessActionRequest> Pending = new Queue<ProcessActionRequest>();
            public readonly Dictionary<string, ProcessActionResponse> Responses = new Dictionary<string, ProcessActionResponse>(StringComparer.Ordinal);
        }

        #endregion
    }
}
