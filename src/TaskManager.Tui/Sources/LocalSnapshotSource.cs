using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using PerformanceStatistics.Monitoring;

namespace TaskManager.Tui
{
    /// <summary>
    /// Samples the local machine in-process. Optionally records every snapshot to a SQLite history file.
    /// </summary>
    public sealed class LocalSnapshotSource : ISnapshotSource
    {
        #region Public-Members

        /// <inheritdoc />
        public event EventHandler<HostSnapshot> SnapshotReceived;

        /// <inheritdoc />
        public HostInfo HostInfo
        {
            get { return _Sampler.HostInfo; }
        }

        /// <inheritdoc />
        public string Description
        {
            get { return _Store != null ? "local, recording" : "local"; }
        }

        /// <inheritdoc />
        public TimeSpan Interval
        {
            get { return _Sampler.Interval; }
            set { _Sampler.Interval = value; }
        }

        /// <inheritdoc />
        public bool Paused
        {
            get { return _Sampler.Paused; }
            set { _Sampler.Paused = value; }
        }

        /// <inheritdoc />
        public bool ActionsEnabled
        {
            get { return true; }
        }

        #endregion

        #region Private-Members

        private readonly MetricsSampler _Sampler;
        private readonly ProcessActionExecutor _Executor = new ProcessActionExecutor(ProcessActionPolicy.AllowAll());
        private readonly SqliteHistoryStore _Store;
        private readonly BlockingCollection<HostSnapshot> _RecordQueue = new BlockingCollection<HostSnapshot>(256);
        private readonly CancellationTokenSource _Cts = new CancellationTokenSource();
        private Task _Recorder = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="interval">Sampling interval.</param>
        /// <param name="recordPath">Optional SQLite file to record history into.</param>
        public LocalSnapshotSource(TimeSpan interval, string recordPath = null)
        {
            _Sampler = new MetricsSampler();
            _Sampler.Interval = interval;
            _Sampler.SnapshotTaken += OnSnapshot;
            if (!String.IsNullOrWhiteSpace(recordPath)) _Store = new SqliteHistoryStore(recordPath);
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public void Start()
        {
            if (_Store != null)
            {
                _Store.InitializeAsync().GetAwaiter().GetResult();
                _Store.UpsertHostAsync(_Sampler.HostInfo).GetAwaiter().GetResult();
                _Recorder = Task.Run(RecordLoopAsync);
            }

            _Sampler.Start();
        }

        /// <inheritdoc />
        public Task<ProcessActionResponse> ExecuteAsync(ProcessActionRequest request)
        {
            HostSnapshot latest = _Sampler.Latest;
            return Task.Run(() => _Executor.Execute(request, latest != null ? latest.Processes : null));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _Sampler.SnapshotTaken -= OnSnapshot;
            _Sampler.Dispose();
            _Cts.Cancel();
            _RecordQueue.CompleteAdding();
            try { _Recorder?.Wait(TimeSpan.FromSeconds(5)); } catch { }
            _Store?.Dispose();
            _Cts.Dispose();
        }

        #endregion

        #region Private-Methods

        private void OnSnapshot(object sender, HostSnapshot snapshot)
        {
            if (_Store != null) _RecordQueue.TryAdd(snapshot);
            SnapshotReceived?.Invoke(this, snapshot);
        }

        private async Task RecordLoopAsync()
        {
            DateTime nextCompaction = DateTime.UtcNow.AddMinutes(1);
            try
            {
                foreach (HostSnapshot snapshot in _RecordQueue.GetConsumingEnumerable(_Cts.Token))
                {
                    try
                    {
                        await _Store.WriteAsync(snapshot, _Cts.Token).ConfigureAwait(false);
                        if (DateTime.UtcNow >= nextCompaction)
                        {
                            await _Store.CompactAsync(DateTime.UtcNow, _Cts.Token).ConfigureAwait(false);
                            nextCompaction = DateTime.UtcNow.AddMinutes(1);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch
                    {
                        // Recording is best-effort; the live view keeps working.
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        #endregion
    }
}
