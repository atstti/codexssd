using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace DiskSpace.Core;

public static class Capacity
{
    public static double? FreeRatio(ulong? available, ulong? total) =>
        available.HasValue && total.HasValue && total.Value > 0
            ? Math.Clamp((double)available.Value / total.Value, 0, 1) : null;

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrFormatByteSizeEx(ulong bytes, uint flags, StringBuilder buffer, uint bufferSize);

    public static string Display(ulong? bytes)
    {
        if (!bytes.HasValue) return "取得できません";
        var buffer = new StringBuilder(128);
        // Use the Windows Shell formatter: binary units and truncation of undisplayed digits.
        // Small capacities keep meaningful precision rather than displaying everything as 0 GB.
        var result = StrFormatByteSizeEx(bytes.Value, 2, buffer, (uint)buffer.Capacity);
        return result >= 0 ? buffer.ToString() : "取得できません";
    }

    public static ulong? FromBlocks(ulong blocks, ulong size)
    {
        try { return checked(blocks * size); }
        catch (OverflowException) { return null; }
    }
}

public sealed record VolumeSnapshot(string Path, string Name, DateTimeOffset MeasuredAt,
    ulong? Total, ulong? AvailableToCaller, ulong? TotalFree, string? Error)
{
    public string Comparison => $"""
        DiskSpace Windows 比較情報
        ボリューム名: {Name}
        測定対象のパス: {Path}
        測定時刻: {MeasuredAt:O}
        取得API: GetDiskFreeSpaceExW
        総容量 (lpTotalNumberOfBytes): {Raw(Total)} bytes
        保存に使える空き (lpFreeBytesAvailableToCaller): {Raw(AvailableToCaller)} bytes
        ディスク全体の空き (lpTotalNumberOfFreeBytes): {Raw(TotalFree)} bytes
        取得状況: {Error ?? "成功"}
        表示形式: Windows Shell StrFormatByteSizeEx（1024進、表示桁未満は切り捨て）
        1 GB = 1,073,741,824 bytes。必要に応じてKB/MB/TB等で表示。
        保存に使える空きは現在の利用者向け。クォータ等により全体の空きと異なります。
        エクスプローラーとの完全一致やMacの推定値との同等性は保証しません。
        """;

    private static string Raw(ulong? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "取得できません";
}
