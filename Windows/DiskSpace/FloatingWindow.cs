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
    private readonly ComboBox drives = new() { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
    private readonly Button pin;
    private readonly Button refresh;
    private VolumeSnapshot? current;
    private bool rendering;
    private bool loading;
    private readonly ToolTip tips = new();
    private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiskSpace", "window.json");
    private sealed record Placement(int X, int Y, bool Pinned);
    private sealed record DriveChoice(string Path, string Name) { public override string ToString() => Name; }
    public event Action<string>? Selected;
    public event Action? RefreshRequested;
    public event Action? DetailsRequested;
    public bool AllowExit { get; set; }

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    public FloatingWindow()
    {
        Text = "DiskSpace — フローティング";
        AccessibleName = "DiskSpace フローティング容量モニター";
        Icon = AppIcon.Create();
        ClientSize = new Size(392, 372);
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
        drives.SelectedIndexChanged += (_, _) =>
        {
            if (!rendering && drives.SelectedItem is DriveChoice choice) Selected?.Invoke(choice.Path);
        };
        Controls.Add(drives);
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
        Shown += (_, _) => RestorePlacement();
        Deactivate += (_, _) => SavePlacement();
        FormClosing += (_, e) =>
        {
            SavePlacement();
            if (!AllowExit && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        };
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
    }

    private Button MakeButton(string text, Rectangle bounds)
    {
        var button = new Button { Text = text, Bounds = bounds, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(27, 42, 59), ForeColor = Ink, Cursor = Cursors.Hand, TabStop = true };
        button.FlatAppearance.BorderColor = Color.FromArgb(44, 64, 84);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(39, 61, 79);
        Controls.Add(button);
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
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.ScaleTransform(DeviceDpi / 96f, DeviceDpi / 96f);
        using var background = new LinearGradientBrush(new Rectangle(0, 0, 392, 372), Color.FromArgb(23, 40, 59), Color.FromArgb(10, 18, 30), 75f);
        g.FillRectangle(background, 0, 0, 392, 372);
        using var edge = new Pen(Color.FromArgb(50, 72, 91));
        g.DrawRectangle(edge, 0, 0, 391, 371);
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
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg == 0x0084 && message.Result == (IntPtr)1)
        {
            var position = PointToClient(new Point(unchecked((short)(message.LParam.ToInt64() & 0xffff)), unchecked((short)((message.LParam.ToInt64() >> 16) & 0xffff))));
            if (position.Y < 54 * DeviceDpi / 96 && position.X < 225 * DeviceDpi / 96) message.Result = (IntPtr)2;
        }
    }

    private void RestorePlacement()
    {
        Placement? placement = null;
        try { if (File.Exists(SettingsPath)) placement = JsonSerializer.Deserialize<Placement>(File.ReadAllText(SettingsPath)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { }
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
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
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new Placement(Left, Top, TopMost)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) tips.Dispose();
        base.Dispose(disposing);
    }
}
