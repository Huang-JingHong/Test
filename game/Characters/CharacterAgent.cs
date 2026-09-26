using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Facilities;
using RelayStation.Core.Map;
using RelayStation.Core.Tasks;
using RelayStation.Game.Map;

namespace RelayStation.Game.Characters;

/*****
Date: 2026-09-06
Name: CharacterAgent
Description: 角色表现层；实例化自 CharacterAgent.tscn（占位宇航员贴图 + 头顶任务标签），每帧按模拟层「当前格 → 下一格」插值平滑移动，并按状态切换立绘（移动=四向朝向贴图、作业=build_or_repair、其余=默认立绘，睡眠贴图暂未接入）。只读 CharacterSim 并订阅 StateChanged 刷新头顶标签。
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
    Date: 2026-09-26
    Name: ActionBuildOrRepair
    Description: 建造/拆除/维修作业的动作贴图文件名后缀（对应 ActionSpriteFolderPath 内的 build_or_repair 立绘）。
    *****/
    private const string ActionBuildOrRepair = "build_or_repair";

    /*****
    Date: 2026-09-26
    Name: _sprite
    Description: 角色地图立绘节点（Setup 时取一次，供默认立绘与动作立绘切换）。
    *****/
    private Sprite2D? _sprite;

    /*****
    Date: 2026-09-26
    Name: _defaultTexture
    Description: 默认立绘贴图（CharacterDef.SpriteTexturePath，未配置时为场景内置占位贴图）；待机/被打断时使用，动作贴图缺失时也回退到它。
    *****/
    private Texture2D? _defaultTexture;

    /*****
    Date: 2026-09-26
    Name: _actionTextures
    Description: 动作贴图解析结果缓存（动作名 → 贴图；缺失也缓存 null，避免每帧重复探测文件）。
    *****/
    private readonly Dictionary<string, Texture2D?> _actionTextures = new();

    /*****
    Date: 2026-09-06
    Name: Sim
    Description: 关联的角色模拟对象。
    *****/
    public CharacterSim Sim => _sim!;

    /*****
    Date: 2026-09-25
    Name: _selected
    Description: 是否被选中（选中圈显示状态）。
    *****/
    private bool _selected;

    /*****
    Date: 2026-09-25
    Name: Selected
    Description: 选中状态；变化时触发重绘以显示/隐藏脚下黄色选中圈。
    *****/
    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            QueueRedraw();
        }
    }

    /*****
    Date: 2026-09-25
    Name: Setup
    Description: 绑定角色模拟对象与地图视图：定位到出生格、按角色定义加载立绘（未配置则沿用场景内置占位贴图）并记录为默认（回退）立绘、按贴图尺寸自适配缩放、订阅状态变更。
    *****/
    public void Setup(CharacterSim sim, MapView mapView)
    {
        _sim = sim;
        _mapView = mapView;
        Position = mapView.MapToLocal(sim.Cell);

        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (_sprite != null)
        {
            Texture2D? texture = LoadSpriteTexture(sim);
            if (texture != null) _sprite.Texture = texture;
            _defaultTexture = _sprite.Texture;
            if (_defaultTexture is { } resolved) ApplySpriteScale(resolved);
        }

        _taskLabel = GetNodeOrNull<Label>("TaskLabel");
        sim.StateChanged += OnStateChanged;
        RefreshTaskLabel();
    }

    /*****
    Date: 2026-09-25
    Name: LoadSpriteTexture
    Description: 按 CharacterDef.SpriteTexturePath 加载角色地图立绘；未配置或加载失败返回 null（调用方沿用场景内置占位贴图）。
    *****/
    private static Texture2D? LoadSpriteTexture(CharacterSim sim)
    {
        string path = sim.Def?.SpriteTexturePath ?? "";
        if (string.IsNullOrEmpty(path)) return null;

        var texture = GD.Load<Texture2D>(path);
        if (texture == null) GD.PushWarning($"[CharacterAgent] 无法加载角色立绘：{path}");
        return texture;
    }

    /*****
    Date: 2026-09-06
    Name: _Process
    Description: 每帧按「当前格 → 路径下一格」的插值进度更新世界位置（Idle 时停在当前格中心），并按行为状态与行进方向刷新立绘。
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
        RefreshSprite();
    }

    /*****
    Date: 2026-09-26
    Name: RefreshSprite
    Description: 按当前行为状态刷新地图立绘：移动中取行进方向对应的 up/down/left/right 朝向贴图，作业（建造/拆除/维修）中取 build_or_repair，待机/被打断等其余状态用默认立绘；贴图缺失回退默认立绘，与当前贴图一致时不切换（睡眠贴图暂未接入）。
    *****/
    private void RefreshSprite()
    {
        if (_sprite == null || _sim == null) return;

        Texture2D? wanted = _sim.State switch
        {
            CharacterState.Moving => DirectionTexture(),
            CharacterState.Working => ResolveActionTexture(ActionBuildOrRepair) ?? _defaultTexture,
            _ => _defaultTexture,
        };
        if (wanted == null || ReferenceEquals(_sprite.Texture, wanted)) return;

        _sprite.Texture = wanted;
        ApplySpriteScale(wanted);
    }

    /*****
    Date: 2026-09-26
    Name: DirectionTexture
    Description: 取当前行进方向（当前格 → 路径下一格的格位移）对应的朝向贴图；无方向（如已抵达路径末格）时返回 null 表示保持当前贴图，避免抵站瞬间闪回默认立绘。
    *****/
    private Texture2D? DirectionTexture()
    {
        if (_sim?.Path == null || _sim.PathIndex >= _sim.Path.Count) return null;

        string? suffix = DirectionSuffix(_sim.Path[_sim.PathIndex] - _sim.Cell);
        return suffix == null ? null : ResolveActionTexture(suffix) ?? _defaultTexture;
    }

    /*****
    Date: 2026-09-26
    Name: DirectionSuffix
    Description: 格子位移 → 朝向贴图文件名后缀（+X 向右、-X 向左、+Y 向下、-Y 向上，与地图坐标 Y 轴向下一致）；位移为零返回 null。
    *****/
    private static string? DirectionSuffix(Vector2I step)
    {
        if (step.X > 0) return "right";
        if (step.X < 0) return "left";
        if (step.Y > 0) return "down";
        if (step.Y < 0) return "up";
        return null;
    }

    /*****
    Date: 2026-09-26
    Name: ResolveActionTexture
    Description: 按约定解析动作贴图：{CharacterDef.ActionSpriteFolderPath}/{CharacterDef.Id}_{动作}.png（如 .../characters_actions/char_field_up.png）；未配置文件夹、文件缺失或加载失败返回 null（结果缓存，含缺失），由调用方回退默认立绘。
    *****/
    private Texture2D? ResolveActionTexture(string action)
    {
        if (_actionTextures.TryGetValue(action, out Texture2D? cached)) return cached;

        Texture2D? texture = null;
        string folder = _sim?.Def?.ActionSpriteFolderPath ?? "";
        string id = _sim?.Def?.Id ?? "";
        if (!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(id))
        {
            string path = $"{folder}/{id}_{action}.png";
            if (ResourceLoader.Exists(path)) texture = GD.Load<Texture2D>(path);
            else GD.PushWarning($"[CharacterAgent] 无法加载动作贴图：{path}");
        }

        _actionTextures[action] = texture;
        return texture;
    }

    /*****
    Date: 2026-09-26
    Name: ApplySpriteScale
    Description: 按贴图原始高度自适配缩放，使角色渲染高度恒为 SpriteTargetHeightPx（默认立绘与动作立绘画布尺寸不同，切换贴图时需重算）。
    *****/
    private void ApplySpriteScale(Texture2D texture)
    {
        if (_sprite == null) return;
        _sprite.Scale = Vector2.One * (SpriteTargetHeightPx / texture.GetSize().Y);
    }

    /*****
    Date: 2026-09-25
    Name: _Draw
    Description: 选中时在角色脚下绘制黄色选中圈（占位视觉，后续可换素材）。
    *****/
    public override void _Draw()
    {
        if (!_selected) return;
        DrawArc(Vector2.Zero, 18f, 0f, Mathf.Tau, 32, new Color(1f, 0.85f, 0.2f), 2f);
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
            CharacterState.Working => $"{WorkVerb()}：{TaskTargetName()}",
            CharacterState.Interrupted => "被打断",
            _ => string.Empty,
        };
        if (_taskLabel != null) _taskLabel.Text = text;
    }

    /*****
    Date: 2026-09-25
    Name: WorkVerb
    Description: 按当前任务给出作业动词：物品类任务（拾取/取用/穿脱）用专门动词，设施作业按目标设施状态给出（建造中/拆除中/维修中）。
    *****/
    private string WorkVerb()
    {
        ITask? task = _sim?.CurrentTask;
        switch (task)
        {
            case PickupTask: return "拾取中";
            case ConsumeTask: return "取用中";
            case EquipTask: return "穿戴中";
            case UnequipTask: return "卸下中";
        }
        return task?.Target?.State switch
        {
            FacilityState.UnderConstruction => "建造中",
            FacilityState.Demolishing => "拆除中",
            _ => "维修中",
        };
    }

    /*****
    Date: 2026-09-06
    Name: TaskTargetName
    Description: 当前任务目标名称：物品类任务取目标物品名（地面堆 / 背包内要消耗或穿戴的物品 / 要卸下的装备），设施作业取设施名；无目标时显示「目标」。
    *****/
    private string TaskTargetName()
    {
        ITask? task = _sim?.CurrentTask;
        if (task == null) return "目标";
        if (task.ItemTarget is { } stack) return stack.Def.DisplayName;

        return task switch
        {
            ConsumeTask consume => consume.TargetDef.DisplayName,
            EquipTask equip => equip.TargetDef.DisplayName,
            UnequipTask unequip => _sim?.Inventory.GetEquipped(unequip.Slot)?.DisplayName ?? "装备",
            _ => task.Target?.Def?.DisplayName ?? "目标",
        };
    }

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
