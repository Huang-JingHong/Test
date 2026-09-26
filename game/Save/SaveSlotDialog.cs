using Godot;

namespace RelayStation.Game.Save;

/*****
Date: 2026-09-25
Name: SaveSlotDialog
Description: 存档槽选择对话框（读档/保存双模式）；显示 3 个槽位的保存时间与游戏天数，读档模式空槽置灰，保存模式覆盖已有槽位需二次点击确认。确认后发出 SlotConfirmed 事件（参数：槽位号 1..3），由调用方执行加载或保存。
*****/
public partial class SaveSlotDialog : Control
{
    /*****
    Date: 2026-09-25
    Name: DialogMode
    Description: 对话框模式枚举（读档 / 保存）。
    *****/
    public enum DialogMode { Load, Save }

    /*****
    Date: 2026-09-25
    Name: SlotConfirmed
    Description: 玩家确认选择槽位时触发（参数：槽位号 1..3）。
    *****/
    public event Action<int>? SlotConfirmed;

    /*****
    Date: 2026-09-25
    Name: _mode
    Description: 当前对话框模式。
    *****/
    private DialogMode _mode = DialogMode.Load;

    /*****
    Date: 2026-09-25
    Name: _confirmSlot
    Description: 保存模式下等待二次确认覆盖的槽位号（0 表示无待确认项）。
    *****/
    private int _confirmSlot;

    /*****
    Date: 2026-09-25
    Name: _titleLabel
    Description: 对话框标题标签。
    *****/
    private Label? _titleLabel;

    /*****
    Date: 2026-09-25
    Name: _slotButtons
    Description: 槽位按钮列表（下标 0 对应槽位 1）。
    *****/
    private readonly List<Button> _slotButtons = new();

    /*****
    Date: 2026-09-25
    Name: SaveSlotDialog
    Description: 构造函数；纯代码构建全屏遮罩 + 居中面板（标题 + 3 个槽位按钮 + 取消按钮），初始隐藏。
    *****/
    public SaveSlotDialog()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;

        var overlay = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f) };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer();
        center.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);

        _titleLabel = new Label { Text = "选择存档槽", HorizontalAlignment = HorizontalAlignment.Center };
        _titleLabel.AddThemeFontSizeOverride("font_size", 20);
        box.AddChild(_titleLabel);

        for (int slot = 1; slot <= SaveGameService.SlotCount; slot++)
        {
            int captured = slot;
            var btn = new Button { CustomMinimumSize = new Vector2(300, 38) };
            btn.Pressed += () => OnSlotPressed(captured);
            box.AddChild(btn);
            _slotButtons.Add(btn);
        }

        var cancelButton = new Button { Text = "取消", CustomMinimumSize = new Vector2(300, 32) };
        cancelButton.Pressed += Close;
        box.AddChild(cancelButton);
    }

    /*****
    Date: 2026-09-25
    Name: Open
    Description: 以指定模式打开对话框并刷新槽位信息（读档模式空槽置灰）。
    *****/
    public void Open(DialogMode mode)
    {
        _mode = mode;
        _confirmSlot = 0;
        if (_titleLabel != null)
            _titleLabel.Text = mode == DialogMode.Load ? "读取存档" : "保存到存档槽";
        RefreshSlots();
        Visible = true;
    }

    /*****
    Date: 2026-09-25
    Name: Close
    Description: 关闭对话框并重置覆盖确认状态。
    *****/
    public void Close()
    {
        _confirmSlot = 0;
        Visible = false;
    }

    /*****
    Date: 2026-09-25
    Name: RefreshSlots
    Description: 刷新各槽位按钮文本（保存时间 · 第几天；空槽显示「空槽」）与可用状态。
    *****/
    private void RefreshSlots()
    {
        for (int i = 0; i < _slotButtons.Count; i++)
        {
            int slot = i + 1;
            Button btn = _slotButtons[i];
            SaveGameResource? save = SaveGameService.LoadFromSlot(slot);
            if (save == null)
            {
                btn.Text = $"槽位 {slot}：空槽";
                btn.Disabled = _mode == DialogMode.Load;
            }
            else
            {
                int day = (int)(save.ClockMinutes / 1440.0) + 1;
                btn.Text = $"槽位 {slot}：{save.SavedAtIso} · 第 {day} 天";
                if (_mode == DialogMode.Save && _confirmSlot == slot)
                    btn.Text = $"槽位 {slot}：再次点击确认覆盖";
                btn.Disabled = false;
            }
        }
    }

    /*****
    Date: 2026-09-25
    Name: OnSlotPressed
    Description: 槽位按钮点击：读档直接确认；保存模式覆盖已有槽位需二次点击确认；确认后触发事件并关闭。
    *****/
    private void OnSlotPressed(int slot)
    {
        if (_mode == DialogMode.Save && SaveGameService.SlotExists(slot))
        {
            if (_confirmSlot == slot)
            {
                _confirmSlot = 0;
                SlotConfirmed?.Invoke(slot);
                Close();
                return;
            }
            _confirmSlot = slot;
            RefreshSlots();
            return;
        }

        SlotConfirmed?.Invoke(slot);
        Close();
    }
}
