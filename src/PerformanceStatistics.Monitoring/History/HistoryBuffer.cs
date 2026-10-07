using System;
using System.Collections.Generic;
using System.Linq;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// In-memory history for one host: a ring of system snapshots plus one ring per process and per network
    /// interface. It feeds charts in the TUI and the server's live view without any storage. Exited processes
    /// stay queryable until their samples age out of the window. Thread-safe.
    /// </summary>
    public sealed class HistoryBuffer
    {
        #region Public-Members

        /// <summary>
        /// Points kept per system and interface series.
        /// </summary>
        public int Capacity { get; private set; }

        /// <summary>
        /// Points kept per process series.
        /// </summary>
        public int ProcessCapacity { get; private set; }

        /// <summary>
        /// Number of system points held.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_Lock) return _System.Count;
            }
        }

        #endregion

        #region Private-Members

        private readonly object _Lock = new object();
        private readonly RingBuffer<SystemPoint> _System;
        private readonly Dictionary<string, ProcessHistory> _Processes = new Dictionary<string, ProcessHistory>(StringComparer.Ordinal);
        private readonly Dictionary<string, RingBuffer<InterfacePoint>> _Interfaces = new Dictionary<string, RingBuffer<InterfacePoint>>(StringComparer.Ordinal);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="capacity">Points per system and interface series (default 900: 15 minutes at 1s).</param>
        /// <param name="processCapacity">Points per process series (default 300: 5 minutes at 1s). Process samples
        /// are compact (about 50 bytes), so 1,000 processes cost about 15 MB at the default.</param>
        public HistoryBuffer(int capacity = 900, int processCapacity = 300)
        {
            if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (processCapacity < 2) throw new ArgumentOutOfRangeException(nameof(processCapacity));
            Capacity = capacity;
            ProcessCapacity = processCapacity;
            _System = new RingBuffer<SystemPoint>(capacity);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Record a snapshot.
        /// </summary>
        /// <param name="snapshot">Snapshot.</param>
        public void Add(HostSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            DateTime ts = snapshot.TimestampUtc;

            lock (_Lock)
            {
                _System.Add(new SystemPoint { TimestampUtc = ts, System = snapshot.System });

                foreach (NetworkInterfaceSnapshot ni in snapshot.Network)
                {
                    RingBuffer<InterfacePoint> ring;
                    if (!_Interfaces.TryGetValue(ni.Name, out ring))
                    {
                        ring = new RingBuffer<InterfacePoint>(Capacity);
                        _Interfaces[ni.Name] = ring;
                    }

                    ring.Add(new InterfacePoint { TimestampUtc = ts, Interface = ni });
                }

                foreach (ProcessSnapshot p in snapshot.Processes)
                {
                    ProcessHistory history;
                    string key = p.Key;
                    if (!_Processes.TryGetValue(key, out history))
                    {
                        history = new ProcessHistory(ProcessCapacity);
                        _Processes[key] = history;
                    }

                    history.Latest = p;
                    history.LastSeenUtc = ts;
                    history.Points.Add(ProcessSample.From(ts, p));
                }

                // Forget processes whose last sample has left the window.
                DateTime oldest = _System.ToList()[0].TimestampUtc;
                foreach (string stale in _Processes.Where(e => e.Value.LastSeenUtc < oldest).Select(e => e.Key).ToList()) _Processes.Remove(stale);
                foreach (string stale in _Interfaces.Where(e => e.Value.Last().TimestampUtc < oldest).Select(e => e.Key).ToList()) _Interfaces.Remove(stale);
            }
        }

        /// <summary>
        /// A system series by metric ID (see <see cref="MetricCatalog"/>), oldest first.
        /// </summary>
        /// <param name="metricId">Metric ID, e.g. "system.cpu" or "system.cpu.core.3".</param>
        /// <returns>Points.</returns>
        public List<SeriesPoint> GetSystemSeries(string metricId)
        {
            lock (_Lock)
            {
                return _System.ToList().Select(p => new SeriesPoint(p.TimestampUtc, MetricCatalog.SystemValue(metricId, p.System))).ToList();
            }
        }

        /// <summary>
        /// A process series by process key and metric ID, oldest first. Empty if the process is unknown.
        /// </summary>
        /// <param name="processKey">Process key (<see cref="ProcessSnapshot.Key"/>).</param>
        /// <param name="metricId">Metric ID, e.g. "process.cpu".</param>
        /// <returns>Points.</returns>
        public List<SeriesPoint> GetProcessSeries(string processKey, string metricId)
        {
            lock (_Lock)
            {
                ProcessHistory history;
                if (!_Processes.TryGetValue(processKey, out history)) return new List<SeriesPoint>();
                return history.Points.ToList().Select(p => new SeriesPoint(p.TimestampUtc, p.Get(metricId))).ToList();
            }
        }

        /// <summary>
        /// A network interface series ("network.rx", "network.tx", "network.rx.packets", "network.tx.packets"), oldest first.
        /// </summary>
        /// <param name="interfaceName">Interface name.</param>
        /// <param name="metricId">Metric ID.</param>
        /// <returns>Points.</returns>
        public List<SeriesPoint> GetInterfaceSeries(string interfaceName, string metricId)
        {
            lock (_Lock)
            {
                RingBuffer<InterfacePoint> ring;
                if (!_Interfaces.TryGetValue(interfaceName, out ring)) return new List<SeriesPoint>();
                return ring.ToList().Select(p => new SeriesPoint(p.TimestampUtc, MetricCatalog.InterfaceValue(metricId, p.Interface))).ToList();
            }
        }

        /// <summary>
        /// The latest snapshot of every process still in the window, including exited ones.
        /// </summary>
        /// <returns>Processes with their last-seen time.</returns>
        public List<KeyValuePair<ProcessSnapshot, DateTime>> GetKnownProcesses()
        {
            lock (_Lock)
            {
                return _Processes.Values.Select(h => new KeyValuePair<ProcessSnapshot, DateTime>(h.Latest, h.LastSeenUtc)).ToList();
            }
        }

        /// <summary>
        /// Names of interfaces with history.
        /// </summary>
        /// <returns>Names.</returns>
        public List<string> GetInterfaceNames()
        {
            lock (_Lock)
            {
                return _Interfaces.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            }
        }

        #endregion

        #region Private-Classes

        private struct SystemPoint
        {
            public DateTime TimestampUtc;
            public SystemSnapshot System;
        }


        private struct InterfacePoint
        {
            public DateTime TimestampUtc;
            public NetworkInterfaceSnapshot Interface;
        }

        private sealed class ProcessHistory
        {
            public readonly RingBuffer<ProcessSample> Points;
            public ProcessSnapshot Latest;
            public DateTime LastSeenUtc;

            public ProcessHistory(int capacity)
            {
                Points = new RingBuffer<ProcessSample>(capacity);
            }
        }

        #endregion
    }
}
