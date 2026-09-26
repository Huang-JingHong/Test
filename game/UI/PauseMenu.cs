using Godot;
using RelayStation.Game.Autoloads;
using RelayStation.Game.Save;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-25
Name: PauseMenu
Description: ESC 暂停菜单；正式游戏内按 Esc 打开：暂停时钟并拦截全部世界输入（菜单打开期间消费未处理输入）。提供「继续游戏」「设置（框架占位，功能未实现）」「保存游戏（与主菜单读档共享三槽）」「退出游戏（二级确认：退出至主菜单 / 退出至桌面）」。剧情动画播放中不响应 Esc。Esc 逐层关闭：退出确认 → 设置面板 → 存档对话框 → 菜单本体。
*****/
public partial class PauseMenu : Control
{
    /*****
    Date: 2026-09-25
    Name: TitleScenePath
    Description: 标题界面场景路径（退出至主菜单用）。
    *****/
    private const string TitleScenePath = "res://game/TitleScreen/TitleScreen.tscn";

    /*****
    Date: 2026-09-25
    Name: _saveDialog
    Description: 共享的存档槽对话框（与主菜单加载存档共用三槽）。
    *****/
    private SaveSlotDialog? _saveDialog;

    /*****
    Date: 2026-09-25
    Name: _settingsPanel
    Description: 设置面板（框架占位，功能未实现）。
    *****/
    private Control? _settingsPanel;

    /*****
    Date: 2026-09-25
    Name: _quitDialog
    Description: 退出确认对话框（退出至主菜单 / 退出至桌面）。
    *****/
    private Control? _quitDialog;

    /*****
    Date: 2026-09-25
    Name: _wasClockPaused
    Description: 打开菜单前的时钟暂停状态（关闭时恢复）。
    *****/
    private bool _wasClockPaused;

    /*****
    Date: 2026-09-25
    Name: PauseMenu
    Description: 构造函数；纯代码构建全屏遮罩 + 居中主面板 + 设置面板 + 退出确认对话框，初始隐藏。
    *****/
    public PauseMenu()
    {
        Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var overlay = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f) };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        // 主面板
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer();
        center.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);

        var titleLabel = new Label { Text = "暂停", HorizontalAlignment = HorizontalAlignment.Center };
        titleLabel.AddThemeFontSizeOverride("font_size", 24);
        box.AddChild(titleLabel);

        var resumeButton = new Button { Text = "继续游戏", CustomMinimumSize = new Vector2(200, 40) };
        resumeButton.Pressed += Close;
        box.AddChild(resumeButton);

        var settingsButton = new Button { Text = "设置", CustomMinimumSize = new Vector2(200, 40) };
        settingsButton.Pressed += () => _settingsPanel?.Show();
        box.AddChild(settingsButton);

        var saveButton = new Button { Text = "保存游戏", CustomMinimumSize = new Vector2(200, 40) };
        saveButton.Pressed += OnSavePressed;
        box.AddChild(saveButton);

        var quitButton = new Button { Text = "退出游戏", CustomMinimumSize = new Vector2(200, 40) };
        quitButton.Pressed += () => _quitDialog?.Show();
        box.AddChild(quitButton);

        BuildSettingsPanel();
        BuildQuitDialog();
    }

    /*****
    Date: 2026-09-25
    Name: BuildSettingsPanel
    Description: 构建设置面板（框架占位）：音量滑条与画面分组仅展示控件，不接功能；返回按钮关闭面板。
    *****/
    private void BuildSettingsPanel()
    {
        _settingsPanel = new Control { Visible = false };
        _settingsPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _settingsPanel.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_settingsPanel);

        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.5f) };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _settingsPanel.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _settingsPanel.AddChild(center);

        var panel = new PanelContainer();
        center.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);

        var titleLabel = new Label { Text = "设置（占位，功能未实现）", HorizontalAlignment = HorizontalAlignment.Center };
        titleLabel.AddThemeFontSizeOverride("font_size", 20);
        box.AddChild(titleLabel);

        var volumeRow = new HBoxContainer();
        volumeRow.AddThemeConstantOverride("separation", 8);
        volumeRow.AddChild(new Label { Text = "音量" });
        var volumeSlider = new HSlider { MinValue = 0, MaxValue = 100, Value = 80, CustomMinimumSize = new Vector2(200, 20) };
        volumeRow.AddChild(volumeSlider);
        box.AddChild(volumeRow);

        box.AddChild(new Label { Text = "画面设置（占位）" });

        var backButton = new Button { Text = "返回", CustomMinimumSize = new Vector2(200, 36) };
        backButton.Pressed += () => _settingsPanel?.Hide();
        box.AddChild(backButton);
    }

    /*****
    Date: 2026-09-25
    Name: BuildQuitDialog
    Description: 构建退出确认对话框：「退出游戏至主菜单」（重置会话并回标题）与「退出游戏至桌面」（结束进程），含取消。
    *****/
    private void BuildQuitDialog()
    {
        _quitDialog = new Control { Visible = false };
        _quitDialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _quitDialog.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_quitDialog);

        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.5f) };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _quitDialog.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _quitDialog.AddChild(center);

        var panel = new PanelContainer();
        center.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);

        var titleLabel = new Label { Text = "确认退出？", HorizontalAlignment = HorizontalAlignment.Center };
        titleLabel.AddThemeFontSizeOverride("font_size", 20);
        box.AddChild(titleLabel);

        var toTitleButton = new Button { Text = "退出游戏至主菜单", CustomMinimumSize = new Vector2(240, 40) };
        toTitleButton.Pressed += OnQuitToTitle;
        box.AddChild(toTitleButton);

        var toDesktopButton = new Button { Text = "退出游戏至桌面", CustomMinimumSize = new Vector2(240, 40) };
        toDesktopButton.Pressed += OnQuitToDesktop;
        box.AddChild(toDesktopButton);

        var cancelButton = new Button { Text = "取消", CustomMinimumSize = new Vector2(240, 36) };
        cancelButton.Pressed += () => _quitDialog?.Hide();
        box.AddChild(cancelButton);
    }

    /*****
    Date: 2026-09-25
    Name: Setup
    Description: 绑定共享的存档槽对话框。
    *****/
    public void Setup(SaveSlotDialog saveDialog)
    {
        _saveDialog = saveDialog;
    }

    /*****
    Date: 2026-09-25
    Name: Open
    Description: 打开菜单：记录并暂停时钟，显示菜单。
    *****/
    private void Open()
    {
        var clock = GameRoot.Instance?.Simulation?.Clock;
        if (clock == null) return;
        _wasClockPaused = clock.IsPaused;
        clock.SetPaused(true);
        _settingsPanel?.Hide();
        _quitDialog?.Hide();
        Visible = true;
    }

    /*****
    Date: 2026-09-25
    Name: Close
    Description: 关闭菜单及全部子面板，恢复时钟先前暂停状态。
    *****/
    private void Close()
    {
        _settingsPanel?.Hide();
        _quitDialog?.Hide();
        Visible = false;
        GameRoot.Instance?.Simulation?.Clock.SetPaused(_wasClockPaused);
    }

    /*****
    Date: 2026-09-25
    Name: OnSavePressed
    Description: 「保存游戏」：打开共享存档槽对话框（保存模式，与主菜单读档共用三槽）。
    *****/
    private void OnSavePressed() => _saveDialog?.Open(SaveSlotDialog.DialogMode.Save);

    /*****
    Date: 2026-09-25
    Name: OnQuitToTitle
    Description: 「退出游戏至主菜单」：重置会话状态并切换到标题界面。
    *****/
    private void OnQuitToTitle()
    {
        Close();
        GameRoot.Instance?.ReturnToTitle();
        GetTree().ChangeSceneToFile(TitleScenePath);
    }

    /*****
    Date: 2026-09-25
    Name: OnQuitToDesktop
    Description: 「退出游戏至桌面」：结束进程。
    *****/
    private void OnQuitToDesktop() => GetTree().Quit();

    /*****
    Date: 2026-09-25
    Name: _UnhandledInput
    Description: Esc 逐层关闭（退出确认 → 设置 → 存档对话框 → 菜单本体），菜单未打开且非剧情动画时打开；菜单打开期间消费全部未处理输入以冻结世界交互（F12/相机/拾取等）。
    *****/
    public override void _UnhandledInput(InputEvent @event)
    {
        bool escPressed = @event is InputEventKey { Pressed: true, Keycode: Key.Escape };
        if (escPressed)
        {
            if (Visible)
            {
                GetViewport().SetInputAsHandled();
                if (_quitDialog is { Visible: true }) _quitDialog.Hide();
                else if (_settingsPanel is { Visible: true }) _settingsPanel.Hide();
                else if (_saveDialog is { Visible: true }) _saveDialog.Close();
                else Close();
            }
            else if (GameRoot.Instance?.CutsceneActive != true)
            {
                GetViewport().SetInputAsHandled();
                Open();
            }
            return;
        }

        if (Visible) GetViewport().SetInputAsHandled();
    }
}
