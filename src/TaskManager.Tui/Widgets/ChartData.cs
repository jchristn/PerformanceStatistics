using System;
using System.Collections.Generic;
using PerformanceStatistics.Monitoring;
using TUIKit;

namespace TaskManager.Tui
{
    /// <summary>
    /// One series in a chart.
    /// </summary>
    public sealed class ChartSeriesData
    {
        #region Public-Members

        /// <summary>
        /// Legend name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Line color.
        /// </summary>
        public Color Color { get; set; } = Color.FromPalette(6);

        /// <summary>
        /// Values, oldest first, aligned with <see cref="ChartData.Times"/>. Unavailable points are 0.
        /// </summary>
        public double[] Values { get; set; } = new double[0];

        #endregion
    }

    /// <summary>
    /// Everything needed to draw a chart, compact or maximized with axes.
    /// </summary>
    public sealed class ChartData
    {
        #region Public-Members

        /// <summary>
        /// Title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Unit of every series.
        /// </summary>
        public MetricUnitEnum Unit { get; set; } = MetricUnitEnum.Count;

        /// <summary>
        /// Fixed top of the Y axis (e.g. 100 for percentages, total memory), or null to fit the data.
        /// </summary>
        public double? FixedMaximum { get; set; } = null;

        /// <summary>
        /// Sample times (UTC), oldest first.
        /// </summary>
        public DateTime[] Times { get; set; } = new DateTime[0];

        /// <summary>
        /// Series.
        /// </summary>
        public List<ChartSeriesData> Series { get; set; } = new List<ChartSeriesData>();

        /// <summary>
        /// An empty chart.
        /// </summary>
        public static ChartData Empty(string title, MetricUnitEnum unit)
        {
            return new ChartData { Title = title, Unit = unit };
        }

        #endregion
    }
}
