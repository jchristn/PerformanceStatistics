using System;
using System.Diagnostics;
using System.Threading;

namespace PerformanceStatistics.Internal
{
    /// <summary>
    /// Turns a monotonically increasing raw sample (CPU ticks, cumulative disk operations, process CPU time)
    /// into a rate computed over the interval between two reads. Shared by every platform so that rate
    /// metrics behave identically on Windows, Linux, and macOS:
    /// <list type="bullet">
    /// <item>The first read waits until the minimum interval has elapsed since the baseline sample, so a
    /// one-shot caller still receives a real value instead of zero.</item>
    /// <item>Reads closer together than the minimum interval return the previously computed value instead of
    /// resetting the baseline, so reading related properties back-to-back is consistent.</item>
    /// <item>When a progress predicate is supplied and the raw counters have not advanced (macOS publishes
    /// host CPU ticks in bursts, sometimes nearly a second apart), the baseline is kept and the previous value
    /// is returned; the first read polls until the counters advance or <see cref="MaximumFirstReadWait"/> elapses.</item>
    /// <item>Timing uses <see cref="Stopwatch"/>, which is monotonic and unaffected by wall-clock changes.</item>
    /// </list>
    /// Thread-safe.
    /// </summary>
    /// <typeparam name="TSample">Raw sample type.</typeparam>
    /// <typeparam name="TResult">Computed rate type.</typeparam>
    internal sealed class DeltaSampler<TSample, TResult>
    {
        #region Public-Members

        /// <summary>
        /// Default minimum interval between two samples used to compute a rate.
        /// </summary>
        public static readonly TimeSpan DefaultMinimumInterval = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Longest time the first read waits for the raw counters to advance.
        /// </summary>
        public static readonly TimeSpan MaximumFirstReadWait = TimeSpan.FromMilliseconds(1500);

        /// <summary>
        /// Delegate that reads a raw sample.
        /// </summary>
        /// <param name="sample">The sample, when available.</param>
        /// <returns>True if a sample was read.</returns>
        public delegate bool TryRead(out TSample sample);

        #endregion

        #region Private-Members

        private readonly object _Lock = new object();
        private readonly TryRead _Read;
        private readonly Func<TSample, TSample, TimeSpan, TResult> _Compute;
        private readonly Func<TSample, TSample, bool> _HasProgressed;
        private readonly TimeSpan _MinimumInterval;
        private bool _HasBaseline = false;
        private TSample _Baseline;
        private long _BaselineTimestamp;
        private bool _HasResult = false;
        private TResult _LastResult;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="read">Reads a raw sample; must not throw.</param>
        /// <param name="compute">Computes the result from (previous, current, elapsed).</param>
        /// <param name="hasProgressed">Optional; returns false when (previous, current) show no counter movement, meaning the interval carries no information yet.</param>
        /// <param name="minimumInterval">Minimum interval between samples; null uses <see cref="DefaultMinimumInterval"/>.</param>
        public DeltaSampler(
            TryRead read,
            Func<TSample, TSample, TimeSpan, TResult> compute,
            Func<TSample, TSample, bool> hasProgressed = null,
            TimeSpan? minimumInterval = null)
        {
            _Read = read ?? throw new ArgumentNullException(nameof(read));
            _Compute = compute ?? throw new ArgumentNullException(nameof(compute));
            _HasProgressed = hasProgressed;
            _MinimumInterval = minimumInterval ?? DefaultMinimumInterval;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Take a baseline sample now, if one has not been taken, so that the first call to
        /// <see cref="TryGet"/> measures from this point without waiting.
        /// </summary>
        public void Prime()
        {
            lock (_Lock)
            {
                if (!_HasBaseline) TakeBaseline();
            }
        }

        /// <summary>
        /// Get the rate over the interval since the previous sample.
        /// </summary>
        /// <param name="result">The computed rate.</param>
        /// <returns>True if a value is available.</returns>
        public bool TryGet(out TResult result)
        {
            lock (_Lock)
            {
                if (!_HasBaseline && !TakeBaseline())
                {
                    result = default(TResult);
                    return false;
                }

                TimeSpan elapsed = Elapsed(_BaselineTimestamp);
                if (elapsed < _MinimumInterval)
                {
                    if (_HasResult)
                    {
                        result = _LastResult;
                        return true;
                    }

                    Thread.Sleep(_MinimumInterval - elapsed);
                }

                TSample current;
                if (!_Read(out current))
                {
                    result = default(TResult);
                    return false;
                }

                if (_HasProgressed != null && !_HasProgressed(_Baseline, current))
                {
                    if (_HasResult)
                    {
                        result = _LastResult;
                        return true;
                    }

                    Stopwatch waited = Stopwatch.StartNew();
                    while (!_HasProgressed(_Baseline, current) && waited.Elapsed < MaximumFirstReadWait)
                    {
                        Thread.Sleep(_MinimumInterval);
                        if (!_Read(out current))
                        {
                            result = default(TResult);
                            return false;
                        }
                    }
                }

                long now = Stopwatch.GetTimestamp();
                elapsed = TimeSpan.FromSeconds((now - _BaselineTimestamp) / (double)Stopwatch.Frequency);

                _LastResult = _Compute(_Baseline, current, elapsed);
                _HasResult = true;
                _Baseline = current;
                _BaselineTimestamp = now;

                result = _LastResult;
                return true;
            }
        }

        #endregion

        #region Private-Methods

        private bool TakeBaseline()
        {
            TSample sample;
            if (!_Read(out sample)) return false;
            _Baseline = sample;
            _BaselineTimestamp = Stopwatch.GetTimestamp();
            _HasBaseline = true;
            return true;
        }

        private static TimeSpan Elapsed(long since)
        {
            return TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - since) / (double)Stopwatch.Frequency);
        }

        #endregion
    }
}
