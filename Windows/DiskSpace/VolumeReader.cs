using System.ComponentModel;
using System.Runtime.InteropServices;
using DiskSpace.Core;

namespace DiskSpace.Windows;

internal static class VolumeReader
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceExW(string directory, out ulong available,
        out ulong total, out ulong free);

    public static string StartupPath => Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";

    public static List<VolumeSnapshot> Read()
    {
        var result = new List<VolumeSnapshot>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            // Optical, network and RAM disks are outside the HDD/SSD workflow.
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
            try { if (!drive.IsReady) continue; }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
            var name = drive.Name;
            try
            {
                var label = drive.VolumeLabel;
                if (!string.IsNullOrWhiteSpace(label)) name = $"{label} ({drive.Name})";
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            if (string.Equals(drive.Name, StartupPath, StringComparison.OrdinalIgnoreCase)) name += "（起動ドライブ）";
            var success = GetDiskFreeSpaceExW(drive.Name, out var available, out var total, out var free);
            var error = success ? null : new Win32Exception(Marshal.GetLastWin32Error()).Message;
            result.Add(new(drive.Name, name, DateTimeOffset.Now, success ? total : null,
                success ? available : null, success ? free : null, error));
        }
        return result.OrderByDescending(v => string.Equals(v.Path, StartupPath, StringComparison.OrdinalIgnoreCase))
            .ThenBy(v => v.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
