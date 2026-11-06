using DiskSpace.Core;

var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}
var observed = Capacity.FromBlocks(16_242_320, 1_024);
Check(observed == 16_632_135_680, "observed byte conversion remains exact");
Check(Capacity.Display(0) != "取得できません" && Capacity.Display(null) == "取得できません", "zero differs from unavailable");
Check(Capacity.Display(394_110_242_816).Replace('\u00a0', ' ') == "367 GB" &&
      Capacity.Display(998_976_253_952).Replace('\u00a0', ' ') == "930 GB", "user screenshot matches Windows free and total labels");
Check(Capacity.Display(512).Contains("512") && Capacity.Display(536_870_912).Contains("MB"), "sub-GB capacities retain meaningful units");
Check(Capacity.FromBlocks(ulong.MaxValue, 2) == null, "overflow is unavailable");
Check(Capacity.FreeRatio(0, 100) == 0 && Capacity.FreeRatio(null, 100) == null &&
      Capacity.FreeRatio(10, 0) == null && Capacity.FreeRatio(150, 100) == 1 &&
      Capacity.FreeRatio(25, 100) == 0.25, "gauge handles zero, unavailable, zero total, overflow ratio and normal ratio");
var volume = new VolumeSnapshot(@"C:\", "Test", DateTimeOffset.Parse("2026-11-06T00:00:00Z"), 1000, 0, null, "test error");
Check(volume.Comparison.Contains("lpFreeBytesAvailableToCaller): 0 bytes") &&
      volume.Comparison.Contains("lpTotalNumberOfFreeBytes): 取得できません bytes") &&
      volume.Comparison.Contains("2026-11-06T00:00:00") && volume.Comparison.Contains(@"C:\"), "comparison preserves raw values and identity");
Console.WriteLine($"{passed} tests passed, 0 failed");
