using System;
using System.Globalization;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// Formats metric values identically in every UI: binary byte units, per-second rates, percentages with one
    /// decimal, durations as d hh:mm:ss, and "n/a" for unavailable values. Invariant culture.
    /// </summary>
    public static class MetricFormatter
    {
        #region Public-Members

        /// <summary>
        /// Text shown for an unavailable value.
        /// </summary>
        public const string NotAvailable = "n/a";

        #endregion

        #region Private-Members

        private static readonly string[] _ByteUnits = new string[] { "B", "KiB", "MiB", "GiB", "TiB", "PiB" };

        #endregion

        #region Public-Methods

        /// <summary>
        /// Format a value for a unit.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="unit">Unit.</param>
        /// <returns>Text.</returns>
        public static string Format(double? value, MetricUnitEnum unit)
        {
            if (!value.HasValue || Double.IsNaN(value.Value)) return NotAvailable;
            double v = value.Value;
            switch (unit)
            {
                case MetricUnitEnum.Percent: return v.ToString("0.0", CultureInfo.InvariantCulture) + "%";
                case MetricUnitEnum.Bytes: return Bytes(v);
                case MetricUnitEnum.BytesPerSecond: return Bytes(v) + "/s";
                case MetricUnitEnum.PerSecond: return Number(v) + "/s";
                case MetricUnitEnum.Seconds: return Duration(v);
                default: return Number(v);
            }
        }

        /// <summary>
        /// Bytes with binary units, e.g. "1.5 GiB".
        /// </summary>
        /// <param name="bytes">Bytes.</param>
        /// <returns>Text.</returns>
        public static string Bytes(double bytes)
        {
            double v = Math.Abs(bytes);
            int unit = 0;
            while (v >= 1024 && unit < _ByteUnits.Length - 1)
            {
                v /= 1024;
                unit++;
            }

            string number = unit == 0 ? v.ToString("0", CultureInfo.InvariantCulture) : v.ToString(v < 10 ? "0.0" : "0", CultureInfo.InvariantCulture);
            return (bytes < 0 ? "-" : string.Empty) + number + " " + _ByteUnits[unit];
        }

        /// <summary>
        /// Duration as "3d 04:05:06", "04:05:06", or "05:06".
        /// </summary>
        /// <param name="seconds">Seconds.</param>
        /// <returns>Text.</returns>
        public static string Duration(double seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(Math.Max(0, seconds));
            if (t.TotalDays >= 1) return ((int)t.TotalDays).ToString(CultureInfo.InvariantCulture) + "d " + t.ToString("hh\\:mm\\:ss", CultureInfo.InvariantCulture);
            if (t.TotalHours >= 1) return t.ToString("hh\\:mm\\:ss", CultureInfo.InvariantCulture);
            return t.ToString("mm\\:ss", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Number with up to two decimals and thousands separators removed.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>Text.</returns>
        public static string Number(double value)
        {
            return Math.Abs(value - Math.Round(value)) < 0.005
                ? Math.Round(value).ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
