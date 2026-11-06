using DiskSpace.Core;

var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}
var observed = Capacity.FromBlocks(16_242_320, 1_024);
Check(observed == 16_632_135_680 && Capacity.Display(observed) == "16.63 GB", "observed byte conversion");
Check(Capacity.Display(0) == "0.00 GB" && Capacity.Display(null) == "取得できません", "zero differs from unavailable");
Check(Capacity.Display(1_000_000_000) == "1.00 GB", "decimal GB");
Check(Capacity.FromBlocks(ulong.MaxValue, 2) == null, "overflow is unavailable");
Check(Capacity.FreeRatio(0, 100) == 0 && Capacity.FreeRatio(null, 100) == null &&
      Capacity.FreeRatio(10, 0) == null && Capacity.FreeRatio(150, 100) == 1 &&
      Capacity.FreeRatio(25, 100) == 0.25, "gauge handles zero, unavailable, zero total, overflow ratio and normal ratio");
var volume = new VolumeSnapshot(@"C:\", "Test", DateTimeOffset.Parse("2026-11-06T00:00:00Z"), 1000, 0, null, "test error");
Check(volume.Comparison.Contains("lpFreeBytesAvailableToCaller): 0 bytes") &&
      volume.Comparison.Contains("lpTotalNumberOfFreeBytes): 取得できません bytes") &&
      volume.Comparison.Contains("2026-11-06T00:00:00") && volume.Comparison.Contains(@"C:\"), "comparison preserves raw values and identity");
Console.WriteLine($"{passed} tests passed, 0 failed");
