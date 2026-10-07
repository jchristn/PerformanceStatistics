using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// One metric: identity, display name, unit, description, and per-platform availability.
    /// </summary>
    public class MetricDefinition
    {
        #region Public-Members

        /// <summary>
        /// Stable ID (see <see cref="MetricIds"/>).
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Short column header.
        /// </summary>
        public string ShortName { get; set; } = string.Empty;

        /// <summary>
        /// Unit.
        /// </summary>
        public MetricUnitEnum Unit { get; set; } = MetricUnitEnum.Count;

        /// <summary>
        /// Scope.
        /// </summary>
        public MetricScopeEnum Scope { get; set; } = MetricScopeEnum.System;

        /// <summary>
        /// Definition, identical on every platform.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Availability keyed by platform name: "Windows", "Linux", "Mac".
        /// </summary>
        public Dictionary<string, MetricAvailability> Availability { get; set; } = new Dictionary<string, MetricAvailability>(StringComparer.Ordinal);

        /// <summary>
        /// True if the metric can be charted over time.
        /// </summary>
        public bool Chartable { get; set; } = true;

        #endregion

        #region Internal-Members

        [JsonIgnore]
        internal Func<SystemSnapshot, double?> SystemSelector { get; set; } = null;

        [JsonIgnore]
        internal Func<ProcessSnapshot, double?> ProcessSelector { get; set; } = null;

        [JsonIgnore]
        internal Func<ProcessSnapshot, string> ProcessText { get; set; } = null;

        [JsonIgnore]
        internal Func<NetworkInterfaceSnapshot, double?> InterfaceSelector { get; set; } = null;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Availability on a platform (Full if not listed).
        /// </summary>
        /// <param name="platform">"Windows", "Linux", or "Mac".</param>
        /// <returns>Availability.</returns>
        public MetricAvailability For(string platform)
        {
            MetricAvailability a;
            if (platform != null && Availability.TryGetValue(platform, out a)) return a;
            return new MetricAvailability();
        }

        #endregion
    }
}
