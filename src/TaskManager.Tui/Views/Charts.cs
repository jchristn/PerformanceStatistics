using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PerformanceStatistics.Monitoring;
using TUIKit;
using TUIKit.Widgets;

namespace TaskManager.Tui
{
    /// <summary>
    /// Chart and text-bar helpers shared by the views.
    /// </summary>
    internal static class Charts
    {
        #region Public-Members

        public static readonly Color Cpu = Color.FromPalette(6);
        public static readonly Color Memory = Color.FromPalette(5);
        public static readonly Color Read = Color.FromPalette(2);
        public static readonly Color Write = Color.FromPalette(3);
        public static readonly Color Swap = Color.FromPalette(1);

        #endregion

        #region Public-Methods

        /// <summary>
        /// A chart with an optional second series.
        /// </summary>
        public static LineChart CreatePair(string primaryName, Color color, double? maximum, string secondName, Color secondColor, out ChartSeries second)
        {
            LineChart chart = new LineChart(new double[] { 0, 0 });
            chart.Color = color;
            chart.PrimaryName = primaryName;
            chart.Minimum = 0;
            chart.Maximum = maximum;
            chart.ShowLegend = secondName != null;
            second = secondName != null ? chart.AddSeries(secondName, new double[] { 0, 0 }, secondColor) : null;
            return chart;
        }

        /// <summary>
        /// A single-series chart.
        /// </summary>
        public static LineChart Create(string primaryName, Color color, double? maximum)
        {
            ChartSeries ignored;
            return CreatePair(primaryName, color, maximum, null, color, out ignored);
        }

        /// <summary>
        /// Ensure a chart always has two points (charts need a line).
        /// </summary>
        public static List<double> AtLeastTwo(List<double> values)
        {
            if (values.Count >= 2) return values;
            List<double> ret = new List<double>(values);
            while (ret.Count < 2) ret.Insert(0, 0);
            return ret;
        }

        /// <summary>
        /// A text bar like "███████░░░ 70.0%" using markup colors by load.
        /// </summary>
        public static string Bar(double? percent, int width)
        {
            if (!percent.HasValue) return "[dim]" + new string('·', width) + " n/a[/]";
            double p = Math.Max(0, Math.Min(100, percent.Value));
            int filled = (int)Math.Round(p / 100.0 * width);
            string color = p >= 85 ? "red" : p >= 60 ? "yellow" : "green";
            StringBuilder sb = new StringBuilder();
            sb.Append("[" + color + "]").Append(new string('█', filled)).Append("[/]");
            sb.Append("[dim]").Append(new string('░', width - filled)).Append("[/]");
            sb.Append(" ").Append(p.ToString("0", CultureInfo.InvariantCulture).PadLeft(3)).Append('%');
            return sb.ToString();
        }

        /// <summary>
        /// Format a value with a unit, escaped for markup.
        /// </summary>
        public static string Fmt(double? value, MetricUnitEnum unit)
        {
            return Markup.Escape(MetricFormatter.Format(value, unit));
        }

        public static string Fmt(long? value, MetricUnitEnum unit)
        {
            return Fmt(value.HasValue ? (double?)value.Value : null, unit);
        }

        public static string Fmt(int? value, MetricUnitEnum unit)
        {
            return Fmt(value.HasValue ? (double?)value.Value : null, unit);
        }

        #endregion
    }
}
