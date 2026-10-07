using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PerformanceStatistics.Monitoring;
using TUIKit;
using TUIKit.Modals;
using TUIKit.Widgets;

namespace TaskManager.Tui
{
    /// <summary>
    /// Colors, chart panels, and text-bar helpers shared by the views.
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
        /// A focusable chart panel that opens a maximized, live <see cref="ChartModal"/> on Enter.
        /// </summary>
        public static ChartPanel Panel(string title, MetricUnitEnum unit, Action<Modal> show)
        {
            ChartPanel panel = new ChartPanel(ChartData.Empty(title, unit));
            panel.Activated += p => show(new ChartModal(() => p.Data));
            return panel;
        }

        /// <summary>
        /// A text bar like "███████░░░ 70%" using markup colors by load.
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

        /// <summary>
        /// Addresses for a table cell: the first address plus a count of the rest.
        /// </summary>
        public static string AddressSummary(List<string> addresses)
        {
            if (addresses == null || addresses.Count == 0) return "—";

            // Lead with a routable address (addresses arrive IPv4 first); link-local IPv6 lose their zone suffix here
            // since the details modal lists every address in full.
            string first = addresses.FirstOrDefault(a => !a.StartsWith("fe80:", StringComparison.OrdinalIgnoreCase)) ?? addresses[0];
            int zone = first.IndexOf('%');
            if (zone > 0)
            {
                int slash = first.IndexOf('/', zone);
                first = first.Substring(0, zone) + (slash > 0 ? first.Substring(slash) : string.Empty);
            }

            return addresses.Count == 1 ? first : first + "  +" + (addresses.Count - 1).ToString(CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
