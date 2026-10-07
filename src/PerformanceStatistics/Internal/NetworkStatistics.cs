using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace PerformanceStatistics.Internal
{
#pragma warning disable CA1416 // Counters unsupported on a platform throw and are caught per property.
    /// <summary>
    /// Cross-platform network interface counters via <see cref="NetworkInterface"/>.
    /// </summary>
    internal static class NetworkStatistics
    {
        #region Public-Methods

        /// <summary>
        /// Read cumulative counters for every non-loopback interface that is up. Must not throw.
        /// </summary>
        /// <param name="totals">Totals keyed by interface name.</param>
        /// <returns>True if interfaces could be enumerated.</returns>
        public static bool TryReadTotals(out Dictionary<string, NetworkTotals> totals)
        {
            totals = new Dictionary<string, NetworkTotals>(StringComparer.Ordinal);
            NetworkInterface[] interfaces;
            try
            {
                interfaces = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch
            {
                return false;
            }

            foreach (NetworkInterface ni in interfaces)
            {
                try
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    NetworkTotals t = new NetworkTotals();
                    t.Description = ni.Description ?? string.Empty;
                    t.Addresses = ReadAddresses(ni);
                    t.MacAddress = ReadMac(ni);
                    t.InterfaceType = ni.NetworkInterfaceType.ToString();
                    t.Speed = TryGet(() => ni.Speed > 0 ? (long?)ni.Speed : null);

                    IPInterfaceStatistics stats = GetStatistics(ni);
                    if (stats == null) continue;

                    long? received = TryGet(() => (long?)stats.BytesReceived);
                    long? sent = TryGet(() => (long?)stats.BytesSent);
                    if (!received.HasValue || !sent.HasValue) continue;

                    t.ReceivedBytes = received.Value;
                    t.SentBytes = sent.Value;
                    // Some packet counters are unsupported on some platforms (NonUnicastPacketsSent on Linux); read each separately.
                    long? unicastIn = TryGet(() => (long?)stats.UnicastPacketsReceived);
                    long? unicastOut = TryGet(() => (long?)stats.UnicastPacketsSent);
                    long? nonUnicastIn = TryGet(() => (long?)stats.NonUnicastPacketsReceived);
                    long? nonUnicastOut = TryGet(() => (long?)stats.NonUnicastPacketsSent);
                    t.ReceivedPackets = unicastIn.HasValue ? unicastIn + (nonUnicastIn ?? 0) : null;
                    t.SentPackets = unicastOut.HasValue ? unicastOut + (nonUnicastOut ?? 0) : null;
                    totals[ni.Name] = t;
                }
                catch
                {
                    // Interface vanished or a property is unsupported on this platform.
                }
            }

            return true;
        }

        /// <summary>
        /// Compute per-interface rates for interfaces present in both samples, sorted by name.
        /// </summary>
        /// <param name="previous">Earlier sample.</param>
        /// <param name="current">Later sample.</param>
        /// <param name="elapsed">Time between samples.</param>
        /// <returns>Per-interface counters.</returns>
        public static List<NetworkInterfaceCounters> Compute(
            Dictionary<string, NetworkTotals> previous,
            Dictionary<string, NetworkTotals> current,
            TimeSpan elapsed)
        {
            List<NetworkInterfaceCounters> ret = new List<NetworkInterfaceCounters>();
            double seconds = elapsed.TotalSeconds;
            if (seconds <= 0) return ret;

            foreach (KeyValuePair<string, NetworkTotals> entry in current)
            {
                NetworkTotals prev;
                if (!previous.TryGetValue(entry.Key, out prev)) continue;
                NetworkTotals curr = entry.Value;

                NetworkInterfaceCounters counters = new NetworkInterfaceCounters();
                counters.Name = entry.Key;
                counters.Description = curr.Description;
                counters.SpeedBitsPerSecond = curr.Speed;
                counters.Addresses = curr.Addresses ?? new List<string>();
                counters.MacAddress = curr.MacAddress ?? string.Empty;
                counters.InterfaceType = curr.InterfaceType ?? string.Empty;
                counters.ReceivedBytesPerSecond = Math.Round(Math.Max(0, curr.ReceivedBytes - prev.ReceivedBytes) / seconds, 0);
                counters.SentBytesPerSecond = Math.Round(Math.Max(0, curr.SentBytes - prev.SentBytes) / seconds, 0);
                if (curr.ReceivedPackets.HasValue && prev.ReceivedPackets.HasValue)
                    counters.ReceivedPacketsPerSecond = Math.Round(Math.Max(0, curr.ReceivedPackets.Value - prev.ReceivedPackets.Value) / seconds, 0);
                if (curr.SentPackets.HasValue && prev.SentPackets.HasValue)
                    counters.SentPacketsPerSecond = Math.Round(Math.Max(0, curr.SentPackets.Value - prev.SentPackets.Value) / seconds, 0);
                ret.Add(counters);
            }

            ret.Sort((a, b) => String.CompareOrdinal(a.Name, b.Name));
            return ret;
        }

        #endregion

        #region Private-Methods

        private static List<string> ReadAddresses(NetworkInterface ni)
        {
            List<string> ret = new List<string>();
            try
            {
                foreach (UnicastIPAddressInformation a in ni.GetIPProperties().UnicastAddresses)
                {
                    string address = a.Address.ToString();
                    int prefix = -1;
                    try { prefix = a.PrefixLength; } catch { }
                    ret.Add(prefix >= 0 ? address + "/" + prefix.ToString(System.Globalization.CultureInfo.InvariantCulture) : address);
                }
            }
            catch
            {
                // Address information unavailable on this platform or interface.
            }

            // IPv4 first, then IPv6, each in the order the OS reports them.
            ret.Sort((x, y) => (x.Contains(":") ? 1 : 0).CompareTo(y.Contains(":") ? 1 : 0));
            return ret;
        }

        private static string ReadMac(NetworkInterface ni)
        {
            try
            {
                byte[] bytes = ni.GetPhysicalAddress().GetAddressBytes();
                if (bytes.Length == 0) return string.Empty;
                return String.Join(":", Array.ConvertAll(bytes, b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static IPInterfaceStatistics GetStatistics(NetworkInterface ni)
        {
            try
            {
                return ni.GetIPStatistics();
            }
            catch
            {
                return null;
            }
        }

        private static long? TryGet(Func<long?> reader)
        {
            try
            {
                return reader();
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
#pragma warning restore CA1416
}
