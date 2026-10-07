using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// One background sampling loop per host. It is the only reader of the library, publishes a
    /// <see cref="HostSnapshot"/> every <see cref="Interval"/>, and keeps the latest one. Thread-safe.
    /// </summary>
    public sealed class MetricsSampler : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Raised on the sampling thread after each snapshot. Handlers must be quick and must not throw;
        /// marshal to a UI thread before touching widgets.
        /// </summary>
        public event EventHandler<HostSnapshot> SnapshotTaken;

        /// <summary>
        /// Host facts.
        /// </summary>
        public HostInfo HostInfo
        {
            get { return _Builder.HostInfo; }
        }

        /// <summary>
        /// The most recent snapshot, or null before the first one.
        /// </summary>
        public HostSnapshot Latest
        {
            get { return Volatile.Read(ref _Latest); }
        }

        /// <summary>
        /// Sampling interval (minimum 250ms).
        /// </summary>
        public TimeSpan Interval
        {
            get
            {
                return _Interval;
            }
            set
            {
                _Interval = value < TimeSpan.FromMilliseconds(250) ? TimeSpan.FromMilliseconds(250) : value;
            }
        }

        /// <summary>
        /// When true, the loop keeps running but takes no snapshots.
        /// </summary>
        public bool Paused { get; set; } = false;

        /// <summary>
        /// Collect processes in each snapshot.
        /// </summary>
        public bool IncludeProcesses
        {
            get { return _Builder.IncludeProcesses; }
            set { _Builder.IncludeProcesses = value; }
        }

        /// <summary>
        /// True while the loop runs.
        /// </summary>
        public bool IsRunning
        {
            get { return _Loop != null && !_Loop.IsCompleted; }
        }

        #endregion

        #region Private-Members

        private readonly object _Lock = new object();
        private readonly IPerformanceStatistics _Stats;
        private readonly bool _OwnsStats;
        private readonly SnapshotBuilder _Builder;
        private HostSnapshot _Latest = null;
        private TimeSpan _Interval = TimeSpan.FromSeconds(1);
        private CancellationTokenSource _Cts = null;
        private Task _Loop = null;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate for the local machine.
        /// </summary>
        /// <param name="hostId">Host identifier; null uses the lower-cased machine name.</param>
        public MetricsSampler(string hostId = null)
            : this(PerformanceStatisticsFactory.Create(), true, hostId)
        {
        }

        /// <summary>
        /// Instantiate over an existing statistics source.
        /// </summary>
        /// <param name="stats">Statistics source.</param>
        /// <param name="ownsStats">True to dispose <paramref name="stats"/> with this sampler.</param>
        /// <param name="hostId">Host identifier; null uses the lower-cased machine name.</param>
        public MetricsSampler(IPerformanceStatistics stats, bool ownsStats, string hostId = null)
        {
            _Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _OwnsStats = ownsStats;
            _Builder = new SnapshotBuilder(stats, hostId);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Take one snapshot now, synchronously, and publish it.
        /// </summary>
        /// <returns>Snapshot.</returns>
        public HostSnapshot SampleNow()
        {
            HostSnapshot snapshot;
            lock (_Lock)
            {
                if (_Disposed) throw new ObjectDisposedException(nameof(MetricsSampler));
                snapshot = _Builder.Build();
                Volatile.Write(ref _Latest, snapshot);
            }

            EventHandler<HostSnapshot> handler = SnapshotTaken;
            if (handler != null)
            {
                foreach (Delegate d in handler.GetInvocationList())
                {
                    try { ((EventHandler<HostSnapshot>)d)(this, snapshot); }
                    catch { }
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Start the background loop. Idempotent.
        /// </summary>
        public void Start()
        {
            lock (_Lock)
            {
                if (_Disposed) throw new ObjectDisposedException(nameof(MetricsSampler));
                if (IsRunning) return;
                _Cts = new CancellationTokenSource();
                CancellationToken token = _Cts.Token;
                _Loop = Task.Run(() => RunAsync(token));
            }
        }

        /// <summary>
        /// Stop the background loop and wait for it to finish.
        /// </summary>
        public void Stop()
        {
            Task loop;
            lock (_Lock)
            {
                if (_Cts == null) return;
                _Cts.Cancel();
                loop = _Loop;
            }

            try { loop?.Wait(TimeSpan.FromSeconds(5)); }
            catch { }

            lock (_Lock)
            {
                _Cts?.Dispose();
                _Cts = null;
                _Loop = null;
            }
        }

        /// <summary>
        /// Stop and release resources.
        /// </summary>
        public void Dispose()
        {
            Stop();
            lock (_Lock)
            {
                if (_Disposed) return;
                _Disposed = true;
                if (_OwnsStats) _Stats.Dispose();
            }
        }

        #endregion

        #region Private-Methods

        private async Task RunAsync(CancellationToken token)
        {
            Stopwatch clock = Stopwatch.StartNew();
            TimeSpan next = TimeSpan.Zero;

            while (!token.IsCancellationRequested)
            {
                if (!Paused)
                {
                    try { SampleNow(); }
                    catch (ObjectDisposedException) { return; }
                    catch { }
                }

                // Fixed-rate schedule; if a sample overran, start the next one immediately without bursting.
                next += Interval;
                TimeSpan delay = next - clock.Elapsed;
                if (delay < TimeSpan.Zero)
                {
                    next = clock.Elapsed;
                    delay = TimeSpan.Zero;
                }

                try { await Task.Delay(delay, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }

        #endregion
    }
}
