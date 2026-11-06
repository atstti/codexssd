using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using DiskSpace.Core;

namespace DiskSpace.Windows;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--smoke-test"))
        {
            try
            {
                using var window = new DiskWindow();
                var volumes = VolumeReader.Read();
                var boot = volumes.Single(v => string.Equals(v.Path, VolumeReader.StartupPath, StringComparison.OrdinalIgnoreCase));
                if (!boot.Total.HasValue || !boot.AvailableToCaller.HasValue || !boot.TotalFree.HasValue)
                    throw new InvalidOperationException(boot.Error ?? "Startup capacity unavailable");
                var reference = new DriveInfo(boot.Path);
                if (Math.Abs((decimal)boot.Total.Value - reference.TotalSize) > 1_048_576)
                    throw new InvalidOperationException("Total capacity differs from DriveInfo");
                window.Render(volumes, boot.Path, false);
                window.Show();
                Application.DoEvents();
                if (!window.Visible || !window.IsHandleCreated) throw new InvalidOperationException("Window did not open");
                Clipboard.SetText(boot.Comparison);
                if (Clipboard.GetText() != boot.Comparison) throw new InvalidOperationException("Clipboard mismatch");
                window.Close();
                File.WriteAllText("smoke-result.txt", $"PASS: actual startup drive API, window creation/display, capacity rendering, clipboard round trip.\n{boot.Comparison}");
                Environment.ExitCode = 0;
            }
            catch (Exception error)
            {
                File.WriteAllText("smoke-result.txt", "FAIL: " + error);
                Environment.ExitCode = 1;
            }
            return;
        }
        using var mutex = new Mutex(true, @"Local\DiskSpace.Windows.Tray", out var first);
        if (!first) return;
        Application.Run(new TrayContext());
    }
}

internal static class AppIcon
{
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
    public static Icon Create()
    {
        using var image = new Bitmap(64, 64);
        using var g = Graphics.FromImage(image);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var background = new SolidBrush(Color.FromArgb(18, 33, 55));
        g.FillEllipse(background, 1, 1, 62, 62);
        using var edge = new Pen(Color.FromArgb(174, 204, 232), 3);
        g.DrawRectangle(edge, 13, 11, 38, 42);
        using var track = new Pen(Color.FromArgb(61, 82, 104), 5);
        g.DrawEllipse(track, 22, 19, 20, 20);
        using var gauge = new Pen(Color.FromArgb(45, 222, 208), 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(gauge, 22, 19, 20, 20, -90, 265);
        g.DrawLine(gauge, 36, 46, 44, 46);
        var handle = image.GetHicon();
        try { using var borrowed = Icon.FromHandle(handle); return (Icon)borrowed.Clone(); }
        finally { DestroyIcon(handle); }
    }
}

internal sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon tray;
    private readonly DiskWindow window = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 30_000 };
    private List<VolumeSnapshot> volumes = [];
    private string selected = VolumeReader.StartupPath;
    private bool refreshing;
    private bool pending;

    public TrayContext()
    {
        tray = new NotifyIcon { Icon = AppIcon.Create(), Visible = true, Text = "DiskSpace — 取得中…" };
        var menu = new ContextMenuStrip();
        menu.Items.Add("容量一覧を開く", null, (_, _) => Show());
        menu.Items.Add("手動更新", null, async (_, _) => await Refresh());
        menu.Items.Add("終了", null, (_, _) => ExitThread());
        tray.ContextMenuStrip = menu;
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) Show(); };
        window.Selected += path => { selected = path; UpdateDisplay(); };
        window.RefreshRequested += async () => await Refresh();
        window.ExitRequested += () => ExitThread();
        window.DevicesChanged += async () => await Refresh();
        timer.Tick += async (_, _) => await Refresh();
        // Keep a hidden window handle alive to receive WM_DEVICECHANGE.
        _ = window.Handle;
        timer.Start();
        _ = Refresh();
    }

    private void Show()
    {
        window.Show();
        window.WindowState = FormWindowState.Normal;
        window.Activate();
        _ = Refresh();
    }

    private async Task Refresh()
    {
        if (refreshing) { pending = true; return; }
        refreshing = true;
        UpdateDisplay();
        try
        {
            volumes = await Task.Run(VolumeReader.Read);
            if (!volumes.Any(v => v.Path == selected)) selected = VolumeReader.StartupPath;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            volumes = [new(VolumeReader.StartupPath, "起動ドライブ", DateTimeOffset.Now, null, null, null, error.Message)];
        }
        finally
        {
            refreshing = false;
            if (!window.IsDisposed) UpdateDisplay();
        }
        if (pending && !window.IsDisposed) { pending = false; await Refresh(); }
    }

    private void UpdateDisplay()
    {
        var volume = volumes.FirstOrDefault(v => v.Path == selected);
        var tip = $"DiskSpace {selected} 残り {Capacity.Display(volume?.AvailableToCaller)}";
        tray.Text = tip.Length > 63 ? tip[..63] : tip;
        window.Render(volumes, selected, refreshing);
    }

    protected override void ExitThreadCore()
    {
        timer.Stop();
        timer.Dispose();
        tray.Visible = false;
        var icon = tray.Icon;
        tray.Dispose();
        icon?.Dispose();
        window.AllowExit = true;
        window.Dispose();
        base.ExitThreadCore();
    }
}
