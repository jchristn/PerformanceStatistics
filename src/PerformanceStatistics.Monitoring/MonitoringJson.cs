using System.Text.Json;
using System.Text.Json.Serialization;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// JSON settings shared by the agent, server, TUI client, and web app: camelCase, enums as strings, nulls
    /// omitted (an omitted field means "unavailable").
    /// </summary>
    public static class MonitoringJson
    {
        #region Public-Members

        /// <summary>
        /// Serializer options.
        /// </summary>
        public static JsonSerializerOptions Options { get; } = Create();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Serialize.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="value">Value.</param>
        /// <returns>JSON.</returns>
        public static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, Options);
        }

        /// <summary>
        /// Deserialize.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="json">JSON.</param>
        /// <returns>Value.</returns>
        public static T Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }

        #endregion

        #region Private-Methods

        private static JsonSerializerOptions Create()
        {
            JsonSerializerOptions options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        #endregion
    }
}
