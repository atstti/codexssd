using System.Globalization;

namespace DiskSpace.Core;

public static class Capacity
{
    public static double? FreeRatio(ulong? available, ulong? total) =>
        available.HasValue && total.HasValue && total.Value > 0
            ? Math.Clamp((double)available.Value / total.Value, 0, 1) : null;

    public static string Display(ulong? bytes)
    {
        if (!bytes.HasValue) return "取得できません";
        string[] units = ["バイト", "KB", "MB", "GB", "TB", "PB", "EB"];
        decimal value = bytes.Value;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture) + " " + units[unit];
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
        表示形式: Windowsと同じ1024進の単位、小数第2位まで四捨五入
        1 GB = 1,073,741,824 bytes。必要に応じてKB/MB/TB等で表示。
        保存に使える空きは現在の利用者向け。クォータ等により全体の空きと異なります。
        エクスプローラーとの完全一致やMacの推定値との同等性は保証しません。
        """;

    private static string Raw(ulong? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "取得できません";
}
