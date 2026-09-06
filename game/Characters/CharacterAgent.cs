using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Map;
using RelayStation.Game.Map;

namespace RelayStation.Game.Characters;

/*****
Date: 2026-09-06
Name: CharacterAgent
Description: 角色表现层；实例化自 CharacterAgent.tscn（占位宇航员贴图 + 头顶任务标签），每帧按模拟层「当前格 → 下一格」插值平滑移动。只读 CharacterSim 并订阅 StateChanged 刷新头顶标签。
*****/
public partial class CharacterAgent : Node2D
{
    /*****
    Date: 2026-09-06
    Name: SpriteTargetHeightPx
    Description: 占位贴图的目标渲染高度（像素），按贴图原始尺寸自适配缩放。
    *****/
    private const float SpriteTargetHeightPx = 30f;

    /*****
    Date: 2026-09-06
    Name: _sim
    Description: 关联的角色模拟对象（只读）。
    *****/
    private CharacterSim? _sim;

    /*****
    Date: 2026-09-06
    Name: _mapView
    Description: 地图视图（格子坐标换算）。
    *****/
    private MapView? _mapView;

    /*****
    Date: 2026-09-06
    Name: _taskLabel
    Description: 头顶任务标签。
    *****/
    private Label? _taskLabel;

    /*****
    Date: 2026-09-06
    Name: Sim
    Description: 关联的角色模拟对象。
    *****/
    public CharacterSim Sim => _sim!;

    /*****
    Date: 2026-09-06
    Name: Setup
    Description: 绑定角色模拟对象与地图视图：定位到出生格、按贴图尺寸自适配缩放、订阅状态变更。
    *****/
    public void Setup(CharacterSim sim, MapView mapView)
    {
        _sim = sim;
        _mapView = mapView;
        Position = mapView.MapToLocal(sim.Cell);

        if (GetNodeOrNull<Sprite2D>("Sprite2D") is { } sprite && sprite.Texture is { } texture)
        {
            sprite.Scale = Vector2.One * (SpriteTargetHeightPx / texture.GetSize().Y);
        }

        _taskLabel = GetNodeOrNull<Label>("TaskLabel");
        sim.StateChanged += OnStateChanged;
        RefreshTaskLabel();
    }

    /*****
    Date: 2026-09-06
    Name: _Process
    Description: 每帧按「当前格 → 路径下一格」的插值进度更新世界位置（Idle 时停在当前格中心）。
    *****/
    public override void _Process(double delta)
    {
        if (_sim == null || _mapView == null) return;

        Vector2 position = _mapView.MapToLocal(_sim.Cell);
        if (_sim.Path != null && _sim.PathIndex < _sim.Path.Count)
        {
            Vector2 next = _mapView.MapToLocal(_sim.Path[_sim.PathIndex]);
            position = position.Lerp(next, _sim.CellProgress);
        }
        Position = position;
    }

    /*****
    Date: 2026-09-06
    Name: OnStateChanged
    Description: 角色状态变化时刷新头顶任务标签。
    *****/
    private void OnStateChanged(CharacterSim sim, CharacterState newState) => RefreshTaskLabel();

    /*****
    Date: 2026-09-06
    Name: RefreshTaskLabel
    Description: 根据当前任务与状态生成头顶文字（待机 / 前往 / 维修中 / 被打断）。
    *****/
    private void RefreshTaskLabel()
    {
        if (_sim == null) return;
        string text = _sim.State switch
        {
            CharacterState.Idle => "待机",
            CharacterState.Moving => $"前往：{TaskTargetName()}",
            CharacterState.Working => $"维修中：{TaskTargetName()}",
            CharacterState.Interrupted => "被打断",
            _ => string.Empty,
        };
        if (_taskLabel != null) _taskLabel.Text = text;
    }

    /*****
    Date: 2026-09-06
    Name: TaskTargetName
    Description: 当前任务目标设施名称；无目标时显示「目标」。
    *****/
    private string TaskTargetName()
        => _sim?.CurrentTask?.Target?.Def?.DisplayName ?? "目标";

    /*****
    Date: 2026-09-06
    Name: _ExitTree
    Description: 节点退出时注销事件订阅，防止悬空回调。
    *****/
    public override void _ExitTree()
    {
        if (_sim != null) _sim.StateChanged -= OnStateChanged;
    }
}
