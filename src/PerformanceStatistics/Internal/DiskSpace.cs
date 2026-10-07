using System;
using System.Collections.Generic;
using System.IO;

namespace PerformanceStatistics.Internal
{
    /// <summary>
    /// Aggregates capacity and free space across mounted drives.
    /// </summary>
    internal static class DiskSpace
    {
        #region Public-Methods

        /// <summary>
        /// Sum capacity and available space across the drives accepted by <paramref name="include"/>.
        /// Drives that report an identical (format, capacity, free space) triple are counted once: on macOS,
        /// APFS volumes in one container report the container's capacity and free space, and on Linux,
        /// bind mounts of one filesystem report that filesystem's numbers, so summing them would multiply
        /// the totals.
        /// </summary>
        /// <param name="include">Platform filter; receives drives that are ready and have a non-zero size.</param>
        /// <param name="free">Available bytes.</param>
        /// <param name="total">Capacity in bytes.</param>
        /// <returns>True if at least one drive was counted.</returns>
        public static bool TryGetTotals(Func<DriveInfo, bool> include, out long free, out long total)
        {
            free = 0;
            total = 0;
            bool any = false;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            DriveInfo[] drives;
            try
            {
                drives = DriveInfo.GetDrives();
            }
            catch
            {
                return false;
            }

            foreach (DriveInfo drive in drives)
            {
                try
                {
                    if (!drive.IsReady) continue;
                    long size = drive.TotalSize;
                    if (size <= 0) continue;
                    if (include != null && !include(drive)) continue;

                    long available = drive.AvailableFreeSpace;
                    string key = drive.DriveFormat + "|" + size + "|" + available;
                    if (!seen.Add(key)) continue;

                    free += available;
                    total += size;
                    any = true;
                }
                catch
                {
                    // Inaccessible or disappearing drive; skip it.
                }
            }

            return any;
        }

        #endregion
    }
}
