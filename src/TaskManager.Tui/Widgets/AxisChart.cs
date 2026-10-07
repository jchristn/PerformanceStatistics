using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PerformanceStatistics.Monitoring;
using TUIKit;
using TUIKit.Unicode;
using TUIKit.Widgets;

namespace TaskManager.Tui
{
    /// <summary>
    /// Draws a <see cref="ChartData"/> either compact (just the lines) or with a legend, labeled Y axis, time X axis,
    /// and axis titles. The plot itself is a TUIKit <see cref="LineChart"/> scaled to the same range as the labels.
    /// </summary>
    internal static class AxisChart
    {
        #region Private-Members

        private static readonly CellStyle _Axis = CellStyle.Default.WithForeground(Color.FromPalette(8));
        private static readonly CellStyle _Label = CellStyle.Default.WithForeground(Color.FromPalette(7));
        private static readonly CellStyle _Title = CellStyle.Default.WithAttribute(CellAttributes.Bold, true);

        #endregion

        #region Public-Methods

        /// <summary>
        /// The Y-axis title for a unit.
        /// </summary>
        public static string UnitTitle(MetricUnitEnum unit)
        {
            switch (unit)
            {
                case MetricUnitEnum.Percent: return "Percent of capacity (%)";
                case MetricUnitEnum.Bytes: return "Bytes";
                case MetricUnitEnum.BytesPerSecond: return "Bytes per second";
                case MetricUnitEnum.PerSecond: return "Per second";
                case MetricUnitEnum.Seconds: return "Seconds";
                default: return "Count";
            }
        }

        /// <summary>
        /// A rounded axis maximum that fits the data: 100 for percentages, otherwise 1, 2, or 5 times a power of ten
        /// (or of 1024 for byte units).
        /// </summary>
        public static double NiceMaximum(ChartData data)
        {
            if (data.FixedMaximum.HasValue && data.FixedMaximum.Value > 0) return data.FixedMaximum.Value;
            double max = 0;
            foreach (ChartSeriesData s in data.Series)
                foreach (double v in s.Values)
                    if (!Double.IsNaN(v) && v > max) max = v;

            if (data.Unit == MetricUnitEnum.Percent) return max <= 100 ? 100 : Math.Ceiling(max / 10) * 10;
            if (max <= 0) return 1;

            bool bytes = data.Unit == MetricUnitEnum.Bytes || data.Unit == MetricUnitEnum.BytesPerSecond;
            double scale = 1;
            if (bytes)
            {
                while (max / scale >= 1024) scale *= 1024;
            }

            double scaled = max / scale;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(scaled)));
            foreach (double step in new double[] { 1, 2, 5, 10 })
            {
                if (step * magnitude >= scaled) return step * magnitude * scale;
            }

            return 10 * magnitude * scale;
        }

        /// <summary>
        /// Draw just the lines (and a legend for multi-series charts).
        /// </summary>
        public static void RenderCompact(ISurface surface, ChartData data, LineChart chart)
        {
            Apply(chart, data, data.FixedMaximum, data.Series.Count > 1);
            chart.Render(surface);
        }

        /// <summary>
        /// Draw the chart with a legend, Y tick labels and title, X time labels and title.
        /// </summary>
        public static void RenderWithAxes(ISurface surface, ChartData data)
        {
            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width < 20 || height < 7)
            {
                RenderCompact(surface, data, new LineChart(new double[] { 0, 0 }));
                return;
            }

            double max = NiceMaximum(data);

            // Row 0: legend with the latest and peak value of each series.
            int x = 0;
            foreach (ChartSeriesData s in data.Series)
            {
                double latest = s.Values.Length > 0 ? s.Values[s.Values.Length - 1] : 0;
                double peak = s.Values.Length > 0 ? s.Values.Max() : 0;
                string text = s.Name + " " + MetricFormatter.Format(latest, data.Unit) + " (peak " + MetricFormatter.Format(peak, data.Unit) + ")  ";
                surface.DrawText(x, 0, "■ ", CellStyle.Default.WithForeground(s.Color));
                x += 2;
                x += surface.DrawText(x, 0, TextFit.Truncate(text, Math.Max(0, width - x)), _Label);
                if (x >= width) break;
            }

            // Row 1: Y-axis title.
            surface.DrawText(0, 1, TextFit.Truncate("Y: " + UnitTitle(data.Unit), width), _Title);

            // Layout: plot rows 2..height-4, baseline at height-3, X labels at height-2, X title at height-1.
            int top = 2;
            int baseline = height - 3;
            int plotHeight = baseline - top;
            double[] ticks = new double[] { 1, 0.75, 0.5, 0.25, 0 };
            List<string> labels = ticks.Select(f => MetricFormatter.Format(max * f, data.Unit)).ToList();
            int labelWidth = labels.Max(l => TextFit.Width(l)) + 1;
            int axisX = labelWidth;
            int plotX = axisX + 1;
            int plotWidth = width - plotX;
            if (plotWidth < 4 || plotHeight < 2)
            {
                RenderCompact(surface, data, new LineChart(new double[] { 0, 0 }));
                return;
            }

            for (int row = top; row < baseline; row++) surface.DrawText(axisX, row, "│", _Axis);
            for (int i = 0; i < ticks.Length; i++)
            {
                int row = top + (int)Math.Round((1 - ticks[i]) * (plotHeight - 1));
                if (ticks[i] == 0) row = baseline - 1;
                surface.DrawText(0, row, TextFit.PadLeft(labels[i], labelWidth - 1), _Label);
                surface.DrawText(axisX, row, "┤", _Axis);
            }

            surface.DrawText(axisX, baseline, "└", _Axis);
            surface.DrawText(plotX, baseline, new string('─', plotWidth), _Axis);

            // The plot.
            LineChart chart = new LineChart(new double[] { 0, 0 });
            Apply(chart, data, max, false);
            chart.Render(new SurfaceView(surface, new Rect(plotX, top, plotWidth, plotHeight)));

            // X axis: clock times at evenly spaced positions, skipping labels that would overlap.
            if (data.Times.Length >= 2)
            {
                DateTime first = data.Times[0];
                DateTime last = data.Times[data.Times.Length - 1];
                int lastEnd = -1;
                for (int i = 0; i <= 4; i++)
                {
                    double f = i / 4.0;
                    DateTime t = first + TimeSpan.FromTicks((long)((last - first).Ticks * f));
                    string label = t.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                    int column = plotX + (int)Math.Round(f * (plotWidth - 1));
                    int start = Math.Max(plotX, Math.Min(width - label.Length, column - label.Length / 2));
                    if (start <= lastEnd) continue;
                    surface.DrawText(column, baseline, "┬", _Axis);
                    surface.DrawText(start, baseline + 1, label, _Label);
                    lastEnd = start + label.Length;
                }

                string span = MetricFormatter.Duration((last - first).TotalSeconds);
                string title = "X: Time (local), last " + span;
                surface.DrawText(Math.Max(plotX, plotX + (plotWidth - title.Length) / 2), baseline + 2, TextFit.Truncate(title, plotWidth), _Title);
            }
            else
            {
                surface.DrawText(plotX, baseline + 1, "Waiting for samples...", _Label);
                surface.DrawText(plotX, baseline + 2, "X: Time (local)", _Title);
            }
        }

        #endregion

        #region Private-Methods

        private static void Apply(LineChart chart, ChartData data, double? maximum, bool legend)
        {
            chart.ClearSeries();
            chart.Minimum = 0;
            chart.Maximum = maximum;
            chart.ShowLegend = legend;
            if (data.Series.Count == 0)
            {
                chart.SetValues(new double[] { 0, 0 });
                return;
            }

            chart.PrimaryName = data.Series[0].Name;
            chart.Color = data.Series[0].Color;
            chart.SetValues(AtLeastTwo(data.Series[0].Values));
            for (int i = 1; i < data.Series.Count; i++)
                chart.AddSeries(data.Series[i].Name, AtLeastTwo(data.Series[i].Values), data.Series[i].Color);
        }

        private static double[] AtLeastTwo(double[] values)
        {
            if (values.Length >= 2) return values;
            return values.Length == 1 ? new double[] { values[0], values[0] } : new double[] { 0, 0 };
        }

        #endregion
    }
}
