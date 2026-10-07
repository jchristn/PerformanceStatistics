using System;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// Retention and per-process recording policy for a history store.
    /// </summary>
    public class HistoryStoreSettings
    {
        #region Public-Members

        /// <summary>
        /// How long raw (per-sample) points are kept. Default 1 hour.
        /// </summary>
        public TimeSpan RawRetention { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// How long 1-minute aggregates of system and network series are kept. Default 30 days.
        /// </summary>
        public TimeSpan MinuteRetention { get; set; } = TimeSpan.FromDays(30);

        /// <summary>
        /// How long 1-hour aggregates of system and network series are kept. Default 365 days.
        /// </summary>
        public TimeSpan HourRetention { get; set; } = TimeSpan.FromDays(365);

        /// <summary>
        /// How long 1-minute aggregates of process series are kept. Default 24 hours.
        /// </summary>
        public TimeSpan ProcessMinuteRetention { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// How long 1-hour aggregates of process series (and process metadata) are kept. Default 30 days.
        /// </summary>
        public TimeSpan ProcessHourRetention { get; set; } = TimeSpan.FromDays(30);

        /// <summary>
        /// Processes recorded every sample: the top N by each of CPU, memory, disk I/O, and handles. Default 25.
        /// </summary>
        public int TopProcessesPerMetric { get; set; } = 25;

        /// <summary>
        /// Interval at which every other process is recorded. Default 60 seconds.
        /// </summary>
        public TimeSpan BackgroundProcessInterval { get; set; } = TimeSpan.FromSeconds(60);

        #endregion
    }
}
