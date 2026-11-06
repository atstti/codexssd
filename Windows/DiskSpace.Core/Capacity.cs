using System.Globalization;

namespace DiskSpace.Core;

public static class Capacity
{
    public static string Display(ulong? bytes) => bytes.HasValue
        ? (bytes.Value / 1_000_000_000m).ToString("F2", CultureInfo.InvariantCulture) + " GB"
        : "取得できません";

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
        表示単位: 1 GB = 1,000,000,000 bytes
        保存に使える空きは現在の利用者向け。クォータ等により全体の空きと異なります。
        エクスプローラーとの完全一致やMacの推定値との同等性は保証しません。
        """;

    private static string Raw(ulong? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "取得できません";
}
