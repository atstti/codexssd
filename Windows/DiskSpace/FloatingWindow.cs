using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.Json;
using DiskSpace.Core;

namespace DiskSpace.Windows;

internal sealed class FloatingWindow : Form
{
    private static readonly Color Ink = Color.FromArgb(229, 240, 250);
    private static readonly Color Muted = Color.FromArgb(137, 158, 180);
    private static readonly Color Teal = Color.FromArgb(51, 224, 203);
    private readonly ComboBox drives = new() { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
        DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 24 };
    private readonly Button pin;
    private readonly Button refresh;
    private readonly TrackBar opacitySlider = new() { Minimum = 40, Maximum = 100, Value = 100,
        TickStyle = TickStyle.None, SmallChange = 5, LargeChange = 10, AutoSize = false };
    private readonly Label opacityValue = new() { Text = "100%", TextAlign = ContentAlignment.MiddleRight };
    private readonly Dictionary<Control, Rectangle> layoutBounds = new();
    // WinForms may retain its current Font when a newly assigned Font is equal.
    // Keep all cached fonts alive until after the child controls are disposed.
    private readonly Dictionary<int, Font> layoutFonts = new();
    private bool layingOut;
    private bool fontsDisposed;
    private readonly string settingsPath;
    private const int DesignWidth = 392;
    private const int DesignHeight = 410;
    private VolumeSnapshot? current;
    private bool rendering;
    private bool loading;
    private readonly ToolTip tips = new();
    private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiskSpace", "window.json");
    private sealed record Placement(int X, int Y, bool Pinned, int Width = 392, int Height = 410, int Opacity = 100);
    private sealed record DriveChoice(string Path, string Name) { public override string ToString() => Name; }
    public event Action<string>? Selected;
    public event Action? RefreshRequested;
    public event Action? DetailsRequested;
    public bool AllowExit { get; set; }

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    public FloatingWindow(string? settingsFile = null)
    {
        settingsPath = settingsFile ?? SettingsPath;
        Text = "DiskSpace — フローティング";
        AccessibleName = "DiskSpace フローティング容量モニター";
        Icon = AppIcon.Create();
        ClientSize = new Size(DesignWidth, DesignHeight);
        MinimumSize = new Size(314, 328);
        ResizeRedraw = true;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(13, 23, 37);
        ForeColor = Ink;
        Font = new Font("Yu Gothic UI", 9);
        DoubleBuffered = true;

        pin = MakeButton("最前面 ON", new Rectangle(230, 14, 94, 28));
        pin.Click += (_, _) => { TopMost = !TopMost; pin.Text = TopMost ? "最前面 ON" : "最前面 OFF"; SavePlacement(); };
        var hide = MakeButton("—", new Rectangle(334, 14, 36, 28));
        hide.AccessibleName = "通知領域へ格納";
        tips.SetToolTip(hide, "通知領域へ格納（アイコンをクリックすると再表示）");
        hide.Click += (_, _) => { SavePlacement(); Hide(); };
        drives.SetBounds(22, 66, 348, 30);
        drives.BackColor = Color.FromArgb(28, 43, 61);
        drives.ForeColor = Ink;
        drives.AccessibleName = "表示するドライブ";
        drives.DrawItem += (_, e) =>
        {
            if (e.Bounds.Width <= 8 || e.Bounds.Height <= 0) return;
            using var background = new SolidBrush(drives.BackColor);
            e.Graphics.FillRectangle(background, e.Bounds);
            var name = e.Index >= 0 && e.Index < drives.Items.Count ? drives.Items[e.Index]?.ToString() ?? "" : "ドライブを取得中…";
            TextRenderer.DrawText(e.Graphics, name, drives.Font, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height),
                Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        };
        drives.SelectedIndexChanged += (_, _) =>
        {
            if (!rendering && drives.SelectedItem is DriveChoice choice) Selected?.Invoke(choice.Path);
        };
        Controls.Add(drives);
        layoutBounds[drives] = new Rectangle(22, 66, 348, 30);
        refresh = MakeButton("更新", new Rectangle(22, 318, 78, 32));
        refresh.Click += (_, _) => RefreshRequested?.Invoke();
        var details = MakeButton("詳細・比較", new Rectangle(110, 318, 160, 32));
        details.Click += (_, _) => DetailsRequested?.Invoke();
        var copy = MakeButton("コピー", new Rectangle(280, 318, 90, 32));
        copy.Click += (_, _) =>
        {
            if (current is null) return;
            try { Clipboard.SetText(current.Comparison); tips.Show("比較情報をコピーしました", copy, 0, -30, 1500); }
            catch (ExternalException) { tips.Show("コピーできません。再度お試しください", copy, 0, -30, 2000); }
        };
        var opacityLabel = new Label { Text = "不透明度", ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft };
        Controls.Add(opacityLabel);
        layoutBounds[opacityLabel] = new Rectangle(22, 362, 80, 26);
        opacitySlider.BackColor = BackColor;
        opacitySlider.AccessibleName = "パネルの不透明度（40%から100%）";
        Controls.Add(opacitySlider);
        layoutBounds[opacitySlider] = new Rectangle(104, 362, 218, 26);
        Controls.Add(opacityValue);
        layoutBounds[opacityValue] = new Rectangle(326, 362, 44, 26);
        opacitySlider.ValueChanged += (_, _) =>
        {
            Opacity = opacitySlider.Value / 100d;
            opacityValue.Text = $"{opacitySlider.Value}%";
        };
        opacitySlider.MouseUp += (_, _) => SavePlacement();
        opacitySlider.KeyUp += (_, _) => SavePlacement();
        Shown += (_, _) => RestorePlacement();
        Deactivate += (_, _) => SavePlacement();
        FormClosing += (_, e) =>
        {
            SavePlacement();
            if (!AllowExit && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        };
        AutoScaleMode = AutoScaleMode.None;
        LayoutContent();
    }

    private Button MakeButton(string text, Rectangle bounds)
    {
        var button = new Button { Text = text, Bounds = bounds, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(27, 42, 59), ForeColor = Ink, Cursor = Cursors.Hand, TabStop = true };
        button.FlatAppearance.BorderColor = Color.FromArgb(44, 64, 84);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(39, 61, 79);
        Controls.Add(button);
        layoutBounds[button] = bounds;
        return button;
    }

    public void Render(IReadOnlyList<VolumeSnapshot> volumes, string selected, bool refreshing)
    {
        current = volumes.FirstOrDefault(v => v.Path == selected);
        loading = refreshing;
        rendering = true;
        try
        {
            var choices = volumes.Select(v => new DriveChoice(v.Path, v.Name)).ToArray();
            if (!drives.Items.Cast<DriveChoice>().SequenceEqual(choices))
            {
                drives.Items.Clear();
                drives.Items.AddRange(choices);
            }
            drives.SelectedIndex = Array.FindIndex(choices, c => c.Path == selected);
        }
        finally { rendering = false; }
        refresh.Enabled = !refreshing;
        refresh.Text = refreshing ? "更新中" : "更新";
        AccessibleDescription = $"{current?.Name}、保存に使える空き {Capacity.Display(current?.AvailableToCaller)}";
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (ClientSize.Width <= 1 || ClientSize.Height <= 1) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var background = new LinearGradientBrush(ClientRectangle, Color.FromArgb(23, 40, 59), Color.FromArgb(10, 18, 30), 75f);
        g.FillRectangle(background, ClientRectangle);
        using var edge = new Pen(Color.FromArgb(50, 72, 91));
        g.DrawRectangle(edge, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        using var grip = new Pen(Muted);
        for (var i = 0; i < 3; i++) g.DrawLine(grip, ClientSize.Width - 6 - i * 4, ClientSize.Height - 5,
            ClientSize.Width - 5, ClientSize.Height - 6 - i * 4);
        var (scale, offsetX, offsetY) = ContentTransform();
        g.TranslateTransform(offsetX, offsetY);
        g.ScaleTransform(scale, scale);
        using var accent = new SolidBrush(Teal);
        g.FillEllipse(accent, 22, 25, 7, 7);
        Draw(g, "DISKSPACE", 39, 17, 170, 26, 13, Ink, FontStyle.Bold);
        using var track = new Pen(Color.FromArgb(38, 59, 78), 11);
        g.DrawEllipse(track, 28, 125, 126, 126);
        var ratio = Capacity.FreeRatio(current?.AvailableToCaller, current?.Total);
        if (ratio.HasValue && ratio.Value > 0)
        {
            using var arc = new Pen(Teal, 11) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(arc, 28, 125, 126, 126, -90, (float)(360 * ratio.Value));
        }
        Draw(g, "FREE", 48, 157, 88, 23, 10, Muted, alignment: StringAlignment.Center);
        Draw(g, ratio.HasValue ? $"{ratio.Value * 100:0}%" : "—", 36, 180, 112, 34, 22, Ink, FontStyle.Bold, StringAlignment.Center);
        Draw(g, "保存に使える空き", 181, 130, 193, 26, 10, Muted);
        Draw(g, Capacity.Display(current?.AvailableToCaller), 177, 160, 200, 46,
            current?.AvailableToCaller is null ? 17 : 25, Ink, FontStyle.Bold);
        Draw(g, loading ? "更新中…" : current is null ? "ドライブを取得中…" : $"{current.MeasuredAt.ToLocalTime():HH:mm:ss} 更新 · 30秒間隔", 181, 215, 192, 25, 9, Muted);
        using var separator = new Pen(Color.FromArgb(38, 56, 74));
        g.DrawLine(separator, 22, 275, 370, 275);
        Draw(g, $"総容量  {Capacity.Display(current?.Total)}", 22, 283, 348, 25, 10, Muted);
    }

    private static void Draw(Graphics g, string text, int x, int y, int width, int height, float size,
        Color color, FontStyle style = FontStyle.Regular, StringAlignment alignment = StringAlignment.Near)
    {
        using var font = new Font("Yu Gothic UI", size * 96 / 72, style, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = alignment, LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(text, font, brush, new RectangleF(x, y, width, height), format);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var preference = 2; // Rounded corners on Windows 11; ignored on earlier versions.
        DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int));
        MinimumSize = new Size((int)(314 * DeviceDpi / 96f), (int)(328 * DeviceDpi / 96f));
        ClientSize = new Size((int)(DesignWidth * DeviceDpi / 96f), (int)(DesignHeight * DeviceDpi / 96f));
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg == 0x0084 && message.Result == (IntPtr)1)
        {
            var position = PointToClient(new Point(unchecked((short)(message.LParam.ToInt64() & 0xffff)), unchecked((short)((message.LParam.ToInt64() >> 16) & 0xffff))));
            var edge = Math.Max(6, 8 * DeviceDpi / 96);
            var left = position.X < edge;
            var right = position.X >= ClientSize.Width - edge;
            var top = position.Y < edge;
            var bottom = position.Y >= ClientSize.Height - edge;
            var hit = (left, right, top, bottom) switch
            {
                (true, _, true, _) => 13, (_, true, true, _) => 14,
                (true, _, _, true) => 16, (_, true, _, true) => 17,
                (true, _, _, _) => 10, (_, true, _, _) => 11,
                (_, _, true, _) => 12, (_, _, _, true) => 15, _ => 1
            };
            var (scale, x, y) = ContentTransform();
            if (hit == 1 && position.Y >= y && position.Y < y + 54 * scale && position.X < x + 225 * scale) hit = 2;
            message.Result = (IntPtr)hit;
        }
    }

    private (float Scale, float X, float Y) ContentTransform()
    {
        var scale = Math.Max(0.1f, Math.Min(ClientSize.Width / (float)DesignWidth, ClientSize.Height / (float)DesignHeight));
        return (scale, (ClientSize.Width - DesignWidth * scale) / 2, (ClientSize.Height - DesignHeight * scale) / 2);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutContent();
    }

    protected override void OnResizeEnd(EventArgs e)
    {
        base.OnResizeEnd(e);
        SavePlacement();
    }

    private void LayoutContent()
    {
        if (layoutBounds is null || layoutFonts is null || layingOut || IsDisposed || Disposing) return;
        layingOut = true;
        SuspendLayout();
        try
        {
            var (scale, x, y) = ContentTransform();
            // Quantize to quarter points and bound the cache even during long resize sessions.
            var fontKey = Math.Clamp((int)Math.Round(9 * scale * 96 / Math.Max(96, DeviceDpi) * 4), 24, 384);
            if (!layoutFonts.TryGetValue(fontKey, out var font))
            {
                font = new Font("Yu Gothic UI", fontKey / 4f);
                layoutFonts.Add(fontKey, font);
            }
            foreach (var (control, bounds) in layoutBounds)
            {
                if (!control.Font.Equals(font)) control.Font = font;
                control.SetBounds((int)(x + bounds.X * scale), (int)(y + bounds.Y * scale),
                    Math.Max(1, (int)(bounds.Width * scale)), Math.Max(1, (int)(bounds.Height * scale)));
            }
            drives.ItemHeight = Math.Max(16, (int)(24 * scale));
            Invalidate();
        }
        finally
        {
            ResumeLayout(false);
            layingOut = false;
        }
    }

    private void RestorePlacement()
    {
        Placement? placement = null;
        try { if (File.Exists(settingsPath)) placement = JsonSerializer.Deserialize<Placement>(File.ReadAllText(settingsPath)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { }
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        if (placement is not null)
        {
            var dpi = DeviceDpi / 96d;
            ClientSize = new Size(Math.Min(area.Width, (int)(Math.Clamp(placement.Width, 314, 1568) * dpi)),
                Math.Min(area.Height, (int)(Math.Clamp(placement.Height, 328, 1640) * dpi)));
            opacitySlider.Value = Math.Clamp(placement.Opacity, 40, 100);
        }
        Location = new Point(area.Right - Width - 24, area.Bottom - Height - 24);
        if (placement is not null)
        {
            var desired = new Rectangle(placement.X, placement.Y, Width, Height);
            if (Screen.AllScreens.Any(s => s.WorkingArea.Contains(desired))) Location = desired.Location;
            TopMost = placement.Pinned;
        }
        pin.Text = TopMost ? "最前面 ON" : "最前面 OFF";
    }

    public void SavePlacement()
    {
        if (!Visible) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(settingsPath))!);
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(new Placement(Left, Top, TopMost,
                (int)Math.Round(ClientSize.Width * 96d / DeviceDpi), (int)Math.Round(ClientSize.Height * 96d / DeviceDpi), opacitySlider.Value)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing) tips.Dispose();
            base.Dispose(disposing);
        }
        finally
        {
            if (disposing && !fontsDisposed)
            {
                fontsDisposed = true;
                foreach (var font in layoutFonts.Values) font.Dispose();
                layoutFonts.Clear();
            }
        }
    }
}
