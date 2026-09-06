using Godot;
using RelayStation.Core.Time;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-06
Name: TimeBar
Description: 顶部时间条 UI；显示游戏时钟（第 X 天 HH:mm），提供暂停与 1×/2×/4× 倍速切换按钮（对应计划 §6/§7 时间条组件）。
*****/
public partial class TimeBar : PanelContainer
{
    /*****
    Date: 2026-09-06
    Name: _clock
    Description: 游戏时钟引用。
    *****/
    private IGameClock? _clock;

    /*****
    Date: 2026-09-06
    Name: _clockLabel
    Description: 时钟文字标签。
    *****/
    private Label? _clockLabel;

    /*****
    Date: 2026-09-06
    Name: _pauseButton
    Description: 暂停按钮。
    *****/
    private Button? _pauseButton;

    /*****
    Date: 2026-09-06
    Name: _speedButtons
    Description: 倍速值到按钮的映射表。
    *****/
    private readonly Dictionary<int, Button> _speedButtons = new();

    /*****
    Date: 2026-09-06
    Name: Setup
    Description: 绑定时钟并构建控件（时钟标签 + 暂停 + 1×/2×/4×）。
    *****/
    public void Setup(IGameClock clock)
    {
        _clock = clock;
        BuildControls();
        RefreshButtonStates();
    }

    /*****
    Date: 2026-09-06
    Name: BuildControls
    Description: 构建水平布局：时钟标签、暂停按钮与三个倍速按钮并接线。
    *****/
    private void BuildControls()
    {
        var box = new HBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        AddChild(box);

        _clockLabel = new Label
        {
            Text = "第 1 天 00:00",
            CustomMinimumSize = new Vector2(150, 0),
        };
        box.AddChild(_clockLabel);

        _pauseButton = MakeToggleButton("暂停");
        _pauseButton.Pressed += OnPausePressed;
        box.AddChild(_pauseButton);

        foreach (int speed in new[] { 1, 2, 4 })
        {
            Button button = MakeToggleButton($"{speed}×");
            button.Pressed += () => OnSpeedPressed(speed);
            _speedButtons[speed] = button;
            box.AddChild(button);
        }
    }

    /*****
    Date: 2026-09-06
    Name: MakeToggleButton
    Description: 创建统一样式的切换按钮。
    *****/
    private static Button MakeToggleButton(string text)
        => new() { Text = text, ToggleMode = true, CustomMinimumSize = new Vector2(56, 32) };

    /*****
    Date: 2026-09-06
    Name: OnPausePressed
    Description: 切换暂停状态并刷新按钮态。
    *****/
    private void OnPausePressed()
    {
        if (_clock == null) return;
        _clock.SetPaused(!_clock.IsPaused);
        RefreshButtonStates();
    }

    /*****
    Date: 2026-09-06
    Name: OnSpeedPressed
    Description: 设置倍速并解除暂停。
    *****/
    private void OnSpeedPressed(int speed)
    {
        if (_clock == null) return;
        _clock.SetPaused(false);
        _clock.SetSpeed(speed);
        RefreshButtonStates();
    }

    /*****
    Date: 2026-09-06
    Name: _Process
    Description: 每帧刷新时钟显示与按钮按下态。
    *****/
    public override void _Process(double delta)
    {
        if (_clock == null || _clockLabel == null) return;

        long totalMinutes = (long)_clock.TotalGameMinutes;
        long day = totalMinutes / (24 * 60) + 1;
        long minutesOfDay = totalMinutes % (24 * 60);
        _clockLabel.Text = $"第 {day} 天 {minutesOfDay / 60:00}:{minutesOfDay % 60:00}";
        RefreshButtonStates();
    }

    /*****
    Date: 2026-09-06
    Name: RefreshButtonStates
    Description: 按时钟当前状态刷新各按钮的按下态。
    *****/
    private void RefreshButtonStates()
    {
        if (_clock == null) return;
        if (_pauseButton != null) _pauseButton.ButtonPressed = _clock.IsPaused;
        foreach ((int speed, Button button) in _speedButtons)
        {
            button.ButtonPressed = !_clock.IsPaused && _clock.SpeedMultiplier == speed;
        }
    }
}
