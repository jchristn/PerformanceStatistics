using System;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// One point in a time series. A null value means the metric was unavailable at that time.
    /// </summary>
    public class SeriesPoint
    {
        #region Public-Members

        /// <summary>
        /// Time (UTC).
        /// </summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// Value (average over the bucket for downsampled series).
        /// </summary>
        public double? Value { get; set; }

        /// <summary>
        /// Maximum over the bucket for downsampled series; equal to <see cref="Value"/> for raw points.
        /// </summary>
        public double? Max { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SeriesPoint()
        {
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="timestampUtc">Time.</param>
        /// <param name="value">Value.</param>
        /// <param name="max">Maximum; null uses <paramref name="value"/>.</param>
        public SeriesPoint(DateTime timestampUtc, double? value, double? max = null)
        {
            TimestampUtc = timestampUtc;
            Value = value;
            Max = max ?? value;
        }

        #endregion
    }
}
