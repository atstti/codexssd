using System.Runtime.InteropServices;
using DiskSpace.Core;

namespace DiskSpace.Windows;

internal sealed class DiskWindow : Form
{
    private readonly Label selectedLabel = new() { Dock = DockStyle.Fill, AutoSize = true };
    private readonly Button refresh = new() { Text = "手動更新", AutoSize = true };
    private readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
        BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells
    };
    public event Action<string>? Selected;
    public event Action? RefreshRequested;
    public event Action? ExitRequested;
    public event Action? DevicesChanged;
    public bool AllowExit { get; set; }

    public DiskWindow()
    {
        Text = "DiskSpace";
        Icon = AppIcon.Create();
        Font = new Font("Yu Gothic UI", 10);
        ClientSize = new Size(1120, 550);
        MinimumSize = new Size(900, 440);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), RowCount = 4, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.Controls.Add(selectedLabel, 0, 0);
        foreach (var (title, weight) in new[] { ("ボリューム", 160f), ("総容量", 90f), ("保存に使える空き", 110f), ("ディスク全体の空き", 110f), ("測定時刻", 90f) })
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = title, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.Columns.Add(new DataGridViewButtonColumn { Name = "Select", HeaderText = "通知領域の対象", FillWeight = 90, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.Columns.Add(new DataGridViewButtonColumn { Name = "Copy", HeaderText = "比較情報", Text = "コピー", UseColumnTextForButtonValue = true, FillWeight = 80, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.CellContentClick += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Rows[e.RowIndex].Tag is not VolumeSnapshot volume) return;
            if (grid.Columns[e.ColumnIndex].Name == "Select") Selected?.Invoke(volume.Path);
            if (grid.Columns[e.ColumnIndex].Name == "Copy")
            {
                try { Clipboard.SetText(volume.Comparison); }
                catch (ExternalException) { MessageBox.Show(this, "クリップボードを使用できません。少し待ってから再度お試しください。", "DiskSpace"); }
            }
        };
        layout.Controls.Add(grid, 0, 1);
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "保存に使える空き：GetDiskFreeSpaceExW が返す現在の利用者向けの空き容量（通知領域にも表示）。\nディスク全体の空き：同APIが返す全体の空き容量。クォータ等により異なる場合があります。\nMacの「重要なファイル用（推定）」とは別の指標です。Windowsの形式で表示（1024進）。30秒ごとに更新。\n右上の×は通知領域へ格納します。終了は「終了」から。アイコンが見えない場合はタスクバーの「∧」を確認してください。",
            ForeColor = Color.FromArgb(65, 75, 90)
        }, 0, 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill };
        refresh.Click += (_, _) => RefreshRequested?.Invoke();
        buttons.Controls.Add(refresh);
        var quit = new Button { Text = "終了", AutoSize = true };
        quit.Click += (_, _) => ExitRequested?.Invoke();
        buttons.Controls.Add(quit);
        layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout);
        FormClosing += (_, e) =>
        {
            if (!AllowExit && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        };
    }

    public void Render(IReadOnlyList<VolumeSnapshot> volumes, string selected, bool loading)
    {
        var active = volumes.FirstOrDefault(v => v.Path == selected);
        selectedLabel.Text = $"通知領域の対象：{active?.Name ?? selected}\n保存に使える空き：{Capacity.Display(active?.AvailableToCaller)}";
        refresh.Enabled = !loading;
        refresh.Text = loading ? "更新中…" : "手動更新";
        var firstVisible = grid.FirstDisplayedScrollingRowIndex;
        grid.Rows.Clear();
        foreach (var volume in volumes)
        {
            var i = grid.Rows.Add(volume.Name, Capacity.Display(volume.Total),
                Capacity.Display(volume.AvailableToCaller), Capacity.Display(volume.TotalFree),
                volume.MeasuredAt.ToLocalTime().ToString("HH:mm:ss"),
                volume.Path == selected ? "選択中" : "表示する", "コピー");
            grid.Rows[i].Tag = volume;
            foreach (DataGridViewCell cell in grid.Rows[i].Cells) cell.ToolTipText = volume.Error ?? volume.Path;
            if (volume.Path == selected) grid.Rows[i].DefaultCellStyle.BackColor = Color.FromArgb(224, 246, 244);
        }
        if (firstVisible >= 0 && firstVisible < grid.Rows.Count) grid.FirstDisplayedScrollingRowIndex = firstVisible;
    }

    protected override void WndProc(ref Message message)
    {
        const int WM_DEVICECHANGE = 0x0219;
        const int DBT_DEVICEARRIVAL = 0x8000;
        const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        base.WndProc(ref message);
        if (message.Msg == WM_DEVICECHANGE && (message.WParam.ToInt64() == DBT_DEVICEARRIVAL || message.WParam.ToInt64() == DBT_DEVICEREMOVECOMPLETE)
            && !IsDisposed && IsHandleCreated)
            BeginInvoke(new Action(() => DevicesChanged?.Invoke()));
    }
}
