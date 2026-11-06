using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using DiskSpace.Core;

namespace DiskSpace.Windows;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--smoke-test")) Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
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
                var settingsDirectory = Path.Combine(Path.GetTempPath(), "DiskSpaceSmoke-" + Guid.NewGuid());
                var settingsFile = Path.Combine(settingsDirectory, "window.json");
                using var floating = new FloatingWindow(settingsFile);
                floating.Render(volumes, boot.Path, false);
                floating.Show();
                Application.DoEvents();
                // Width-only changes retain the same content scale; repaint every step.
                for (var step = 0; step < 240; step++)
                {
                    var offset = (step % 20) * 8;
                    floating.ClientSize = step switch
                    {
                        < 80 => new Size(392 + offset, 410),
                        < 160 => new Size(392, 410 + offset),
                        _ => new Size(392 + offset, 410 + offset)
                    };
                    floating.Refresh();
                    foreach (Control child in floating.Controls)
                    {
                        _ = child.Font.Height;
                        child.Refresh();
                    }
                    Application.DoEvents();
                }
                floating.ClientSize = new Size(588, 615);
                Application.DoEvents();
                if (floating.Controls.Cast<Control>().Any(c => !floating.ClientRectangle.Contains(c.Bounds)))
                    throw new InvalidOperationException("Control clipped after resize");
                floating.Controls.OfType<TrackBar>().Single().Value = 65;
                if (Math.Abs(floating.Opacity - 0.65) > 0.01) throw new InvalidOperationException("Opacity did not change");
                floating.SavePlacement();
                using (var restored = new FloatingWindow(settingsFile))
                {
                    restored.Show();
                    Application.DoEvents();
                    if (Math.Abs(restored.Opacity - 0.65) > 0.01 || restored.ClientSize != floating.ClientSize)
                        throw new InvalidOperationException("Size/opacity did not restore");
                    restored.AllowExit = true;
                    restored.Close();
                }
                floating.ClientSize = floating.MinimumSize;
                Application.DoEvents();
                if (floating.Controls.Cast<Control>().Any(c => !floating.ClientRectangle.Contains(c.Bounds)))
                    throw new InvalidOperationException("Control clipped at minimum size");
                floating.ClientSize = new Size(392, 410);
                floating.Controls.OfType<TrackBar>().Single().Value = 100;
                floating.Show();
                Application.DoEvents();
                if (!floating.Visible || !floating.TopMost || floating.FormBorderStyle != FormBorderStyle.None)
                    throw new InvalidOperationException("Floating panel did not open correctly");
                // Render a deterministic preview with representative values, then restore actual data.
                floating.Render([new(boot.Path, "System SSD (C:)（起動ドライブ）", DateTimeOffset.Now,
                    1_000_000_000_000, 326_480_000_000, 326_480_000_000, null)], boot.Path, false);
                using (var preview = new Bitmap(floating.Width, floating.Height))
                {
                    floating.DrawToBitmap(preview, new Rectangle(Point.Empty, preview.Size));
                    preview.Save("floating-preview.png", System.Drawing.Imaging.ImageFormat.Png);
                }
                floating.Render([new(boot.Path, "Test", DateTimeOffset.Now, 1000, 0, 0, null)], boot.Path, false);
                floating.Render([new(boot.Path, "Test", DateTimeOffset.Now, null, null, null, "unavailable")], boot.Path, false);
                floating.Render(volumes, boot.Path, false);
                floating.Hide();
                if (floating.Visible) throw new InvalidOperationException("Floating panel did not hide");
                floating.Show();
                Application.DoEvents();
                if (!floating.Visible) throw new InvalidOperationException("Floating panel did not restore");
                floating.AllowExit = true;
                floating.Close();
                Directory.Delete(settingsDirectory, true);
                window.Close();
                File.WriteAllText("smoke-result.txt", $"PASS: actual startup drive API, details and floating display, borderless/topmost, hide/restore, zero/unavailable rendering, clipboard, 240 continuous width/height/proportional resizes with native control repaint and live font checks, minimum control bounds, 65% opacity, size/opacity persistence. Preview uses sample values.\n{boot.Comparison}");
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
    private readonly FloatingWindow floating = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 30_000 };
    private List<VolumeSnapshot> volumes = [];
    private string selected = VolumeReader.StartupPath;
    private bool refreshing;
    private bool pending;

    public TrayContext()
    {
        tray = new NotifyIcon { Icon = AppIcon.Create(), Visible = true, Text = "DiskSpace — 取得中…" };
        var menu = new ContextMenuStrip();
        menu.Items.Add("フローティングを表示", null, (_, _) => Show());
        menu.Items.Add("詳細・比較情報", null, (_, _) => ShowDetails());
        menu.Items.Add("手動更新", null, async (_, _) => await Refresh());
        menu.Items.Add("終了", null, (_, _) => ExitThread());
        tray.ContextMenuStrip = menu;
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) Show(); };
        window.Selected += path => { selected = path; UpdateDisplay(); };
        window.RefreshRequested += async () => await Refresh();
        window.ExitRequested += () => ExitThread();
        window.DevicesChanged += async () => await Refresh();
        floating.Selected += path => { selected = path; UpdateDisplay(); };
        floating.RefreshRequested += async () => await Refresh();
        floating.DetailsRequested += ShowDetails;
        timer.Tick += async (_, _) => await Refresh();
        // Keep a hidden window handle alive to receive WM_DEVICECHANGE.
        _ = window.Handle;
        floating.Show();
        timer.Start();
        _ = Refresh();
    }

    private void Show()
    {
        floating.Show();
        floating.Activate();
        _ = Refresh();
    }

    private void ShowDetails()
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
        floating.Render(volumes, selected, refreshing);
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
        floating.SavePlacement();
        floating.AllowExit = true;
        floating.Dispose();
        window.Dispose();
        base.ExitThreadCore();
    }
}
