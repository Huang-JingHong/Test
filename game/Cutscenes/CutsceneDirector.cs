using Godot;
using RelayStation.Core.Common;
using RelayStation.Game.Autoloads;
using RelayStation.Game.Input;
using RelayStation.Game.Map;

namespace RelayStation.Game.Cutscenes;

/*****
Date: 2026-09-25
Name: CutsceneDirector
Description: 剧情动画导演器；顺序执行一段动画脚本的动作序列。开始时暂停时钟并禁用输入拾取/相机/地图编辑器（记录先前状态），结束时全部恢复（不强制值，避免与编辑模式等互相踩）；动画期间 GameRoot.CutsceneActive 置位供 UI 拦截输入。
*****/
public partial class CutsceneDirector : Node
{
    /*****
    Date: 2026-09-25
    Name: Finished
    Description: 一段动画播放完毕（正常结束）时触发。
    *****/
    public event Action? Finished;

    /*****
    Date: 2026-09-25
    Name: _context
    Description: 动画上下文（各动作共用引用）。
    *****/
    private CutsceneContext? _context;

    /*****
    Date: 2026-09-25
    Name: _inputPicker
    Description: 输入拾取器（动画期间禁用）。
    *****/
    private InputPicker? _inputPicker;

    /*****
    Date: 2026-09-25
    Name: _cameraController
    Description: 相机控制（动画期间禁用）。
    *****/
    private CameraController? _cameraController;

    /*****
    Date: 2026-09-25
    Name: _mapEditor
    Description: 地图编辑器（动画期间禁用）。
    *****/
    private MapEditor? _mapEditor;

    /*****
    Date: 2026-09-25
    Name: _script
    Description: 正在播放的动画脚本；null 表示空闲。
    *****/
    private CutsceneScript? _script;

    /*****
    Date: 2026-09-25
    Name: _actions
    Description: 当前脚本的动作列表。
    *****/
    private IReadOnlyList<CutsceneAction>? _actions;

    /*****
    Date: 2026-09-25
    Name: _actionIndex
    Description: 当前执行到的动作下标。
    *****/
    private int _actionIndex;

    /*****
    Date: 2026-09-25
    Name: _wasClockPaused
    Description: 进入动画前的时钟暂停状态（结束后恢复）。
    *****/
    private bool _wasClockPaused;

    /*****
    Date: 2026-09-25
    Name: _wasPickerEnabled
    Description: 进入动画前的拾取器启用状态。
    *****/
    private bool _wasPickerEnabled;

    /*****
    Date: 2026-09-25
    Name: _wasCameraEnabled
    Description: 进入动画前的相机控制启用状态。
    *****/
    private bool _wasCameraEnabled;

    /*****
    Date: 2026-09-25
    Name: _wasEditorEnabled
    Description: 进入动画前的编辑器启用状态。
    *****/
    private bool _wasEditorEnabled;

    /*****
    Date: 2026-09-25
    Name: IsPlaying
    Description: 是否有动画正在播放。
    *****/
    public bool IsPlaying => _script != null;

    /*****
    Date: 2026-09-25
    Name: Setup
    Description: 绑定动画上下文与需要禁用/恢复的输入组件。
    *****/
    public void Setup(CutsceneContext context, InputPicker inputPicker, CameraController cameraController, MapEditor mapEditor)
    {
        _context = context;
        _inputPicker = inputPicker;
        _cameraController = cameraController;
        _mapEditor = mapEditor;
    }

    /*****
    Date: 2026-09-25
    Name: Play
    Description: 开始播放一段动画：暂停时钟、禁用输入组件并逐个执行动作；已有动画播放中则忽略（不打断）。
    *****/
    public void Play(CutsceneScript script)
    {
        if (IsPlaying || _context == null) return;
        GameRoot? root = GameRoot.Instance;
        Simulation? simulation = root?.Simulation;
        if (root == null || simulation == null) return;

        _script = script;
        _actions = script.Actions;
        _actionIndex = 0;

        _wasClockPaused = simulation.Clock.IsPaused;
        simulation.Clock.SetPaused(true);
        if (_inputPicker != null) { _wasPickerEnabled = _inputPicker.Enabled; _inputPicker.Enabled = false; }
        if (_cameraController != null) { _wasCameraEnabled = _cameraController.Enabled; _cameraController.Enabled = false; }
        if (_mapEditor != null) { _wasEditorEnabled = _mapEditor.Enabled; _mapEditor.Enabled = false; }
        root.CutsceneActive = true;

        if (_actions.Count == 0)
        {
            Finish();
            return;
        }
        _actions[0].Start(_context);
    }

    /*****
    Date: 2026-09-25
    Name: PlayMusic
    Description: 独立播放一首曲目（不经动画序列；供音乐触发器使用）。
    *****/
    public void PlayMusic(string trackId) => _context?.PlayMusic(trackId);

    /*****
    Date: 2026-09-25
    Name: _Process
    Description: 每帧驱动当前动作；完成后进入下一动作，全部完成时结束动画。
    *****/
    public override void _Process(double delta)
    {
        if (_script == null || _actions == null || _context == null) return;

        if (_actionIndex >= _actions.Count)
        {
            Finish();
            return;
        }

        if (_actions[_actionIndex].Tick(delta))
        {
            _actionIndex++;
            if (_actionIndex >= _actions.Count)
            {
                Finish();
                return;
            }
            _actions[_actionIndex].Start(_context);
        }
    }

    /*****
    Date: 2026-09-25
    Name: Finish
    Description: 结束动画：恢复时钟暂停状态与各输入组件的先前启用状态，清除 CutsceneActive 并发出 Finished 事件。
    *****/
    private void Finish()
    {
        Simulation? simulation = GameRoot.Instance?.Simulation;
        simulation?.Clock.SetPaused(_wasClockPaused);
        if (_inputPicker != null) _inputPicker.Enabled = _wasPickerEnabled;
        if (_cameraController != null) _cameraController.Enabled = _wasCameraEnabled;
        if (_mapEditor != null) _mapEditor.Enabled = _wasEditorEnabled;
        if (GameRoot.Instance != null) GameRoot.Instance.CutsceneActive = false;

        _script = null;
        _actions = null;
        _actionIndex = 0;
        Finished?.Invoke();
    }
}
