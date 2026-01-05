using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PerformanceStatistics
{
    /// <summary>
    /// Factory for creating platform-appropriate performance statistics instances.
    /// </summary>
    public static class PerformanceStatisticsFactory
    {
        /// <summary>
        /// Creates a performance statistics instance for the current platform.
        /// </summary>
        /// <param name="monitoredProcesses">Optional list of process names to monitor.</param>
        /// <returns>Platform-specific IPerformanceStatistics implementation.</returns>
        /// <exception cref="PlatformNotSupportedException">
        /// Thrown when the current platform is not supported.
        /// </exception>
        public static IPerformanceStatistics Create(List<string> monitoredProcesses = null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsPerformanceStatisticsAdapter(monitoredProcesses);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new Linux.LinuxPerformanceStatistics(monitoredProcesses);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new Mac.MacPerformanceStatistics(monitoredProcesses);
            }
            else
            {
                throw new PlatformNotSupportedException(
                    $"Platform '{RuntimeInformation.OSDescription}' is not supported. " +
                    "Supported platforms: Windows, Linux, macOS.");
            }
        }

        /// <summary>
        /// Gets the current platform type.
        /// </summary>
        public static PlatformType CurrentPlatform
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return PlatformType.Windows;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return PlatformType.Linux;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return PlatformType.Mac;
                return PlatformType.Unknown;
            }
        }

        /// <summary>
        /// Checks if the current platform is supported.
        /// </summary>
        public static bool IsPlatformSupported
        {
            get
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    || RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            }
        }
    }
}
