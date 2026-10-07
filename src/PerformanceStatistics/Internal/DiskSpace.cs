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
        /// List the drives accepted by <paramref name="include"/>, counting drives that report an identical
        /// (format, capacity, free space) triple once: on macOS, APFS volumes in one container report the
        /// container's capacity and free space, and on Linux, bind mounts of one filesystem report that
        /// filesystem's numbers, so listing them all would multiply the totals.
        /// </summary>
        /// <param name="include">Platform filter; receives drives that are ready and have a non-zero size.</param>
        /// <returns>Volumes.</returns>
        public static List<VolumeInfo> GetVolumes(Func<DriveInfo, bool> include)
        {
            List<VolumeInfo> ret = new List<VolumeInfo>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            DriveInfo[] drives;
            try
            {
                drives = DriveInfo.GetDrives();
            }
            catch
            {
                return ret;
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
                    string format = drive.DriveFormat;
                    string key = format + "|" + size + "|" + available;
                    if (!seen.Add(key)) continue;

                    ret.Add(new VolumeInfo
                    {
                        Name = drive.Name,
                        Format = format,
                        TotalBytes = size,
                        FreeBytes = available
                    });
                }
                catch
                {
                    // Inaccessible or disappearing drive; skip it.
                }
            }

            return ret;
        }

        /// <summary>
        /// Sum capacity and available space across the volumes returned by <see cref="GetVolumes"/>.
        /// </summary>
        /// <param name="include">Platform filter.</param>
        /// <param name="free">Available bytes.</param>
        /// <param name="total">Capacity in bytes.</param>
        /// <returns>True if at least one drive was counted.</returns>
        public static bool TryGetTotals(Func<DriveInfo, bool> include, out long free, out long total)
        {
            free = 0;
            total = 0;
            List<VolumeInfo> volumes = GetVolumes(include);
            foreach (VolumeInfo volume in volumes)
            {
                free += volume.FreeBytes;
                total += volume.TotalBytes;
            }

            return volumes.Count > 0;
        }

        #endregion
    }
}
