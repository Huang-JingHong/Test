using Godot;
using RelayStation.Core.Facilities;
using RelayStation.Core.Tasks;
using RelayStation.Game.Autoloads;
using RelayStation.Game.Map;

namespace RelayStation.Game.Facilities;

/*****
Date: 2026-09-25
Name: FacilityView
Description: 设施表现层；以贴图呈现设施占地——**运行用完好贴图、损坏与维修中用破损贴图、建造中与拆除中用完好贴图**（作业态不再退化为纯色块），贴图缺失时才回退状态色块（损坏红 / 维修中黄 / 运行绿 / 建造中蓝灰 / 拆除中橙红）。**作业中（建造/拆除/维修）一律在设施贴图之上叠加「施工警戒线」**（按占地比例选图：正方形用 1:1、宽用 21:9、高用 9:16，拉伸铺满占地），故玩家一眼即可分辨哪些设施正在施工。选中时附黄色描边，**不绘制名称标签**（名称由设施面板与建造面板呈现）。订阅 FacilitySim.StateChanged 驱动重绘；作业中时在占地底边绘制进度条，未选中也能看到进度。
*****/
public partial class FacilityView : Node2D
{
    /*****
    Date: 2026-09-26
    Name: WarningLineSquarePath
    Description: 施工警戒线贴图（1:1，用于正方形占地：1×1、2×2、3×3…）。
    *****/
    private const string WarningLineSquarePath = "res://game/Textures/others/warning_line_1_1.png";

    /*****
    Date: 2026-09-26
    Name: WarningLineWidePath
    Description: 施工警戒线贴图（21:9，用于偏宽的占地：2×1、3×2…）。
    *****/
    private const string WarningLineWidePath = "res://game/Textures/others/warning_line_21_9.png";

    /*****
    Date: 2026-09-26
    Name: WarningLineTallPath
    Description: 施工警戒线贴图（9:16，用于偏高的占地：1×2、2×3…）。
    *****/
    private const string WarningLineTallPath = "res://game/Textures/others/warning_line_9_16.png";

    /*****
    Date: 2026-09-06
    Name: DamagedColor
    Description: 损坏状态色块颜色。
    *****/
    private static readonly Color DamagedColor = new(0.80f, 0.25f, 0.25f);

    /*****
    Date: 2026-09-06
    Name: UnderRepairColor
    Description: 维修中状态色块颜色。
    *****/
    private static readonly Color UnderRepairColor = new(0.92f, 0.62f, 0.15f);

    /*****
    Date: 2026-09-06
    Name: OperationalColor
    Description: 运行状态色块颜色。
    *****/
    private static readonly Color OperationalColor = new(0.25f, 0.78f, 0.40f);

    /*****
    Date: 2026-09-25
    Name: UnderConstructionColor
    Description: 建造中状态色块颜色（占位）。
    *****/
    private static readonly Color UnderConstructionColor = new(0.55f, 0.62f, 0.72f);

    /*****
    Date: 2026-09-25
    Name: DemolishingColor
    Description: 拆除中状态色块颜色（占位）。
    *****/
    private static readonly Color DemolishingColor = new(0.85f, 0.45f, 0.25f);

    /*****
    Date: 2026-09-06
    Name: SelectedOutlineColor
    Description: 选中描边颜色。
    *****/
    private static readonly Color SelectedOutlineColor = new(1f, 1f, 0.4f);

    /*****
    Date: 2026-09-06
    Name: _iconTexture
    Description: 正常状态贴图（取自设施定义 IconTexturePath）；未配置时为 null，绘制回退色块。
    *****/
    private Texture2D? _iconTexture;

    /*****
    Date: 2026-09-06
    Name: _brokenTexture
    Description: 破损状态贴图（取自设施定义 BrokenTexturePath，损坏与维修中共用）；未配置时为 null。
    *****/
    private Texture2D? _brokenTexture;

    /*****
    Date: 2026-09-26
    Name: _warningTexture
    Description: 施工警戒线贴图（按本设施占地比例在 Setup 时选定并加载一次）；未配置时为 null，作业态即只画设施贴图。
    *****/
    private Texture2D? _warningTexture;

    /*****
    Date: 2026-09-06
    Name: _facility
    Description: 关联的设施模拟对象（只读）。
    *****/
    private FacilitySim? _facility;

    /*****
    Date: 2026-09-06
    Name: _mapView
    Description: 地图视图（格子坐标换算）。
    *****/
    private MapView? _mapView;

    /*****
    Date: 2026-09-25
    Name: _selected
    Description: 是否被选中。
    *****/
    private bool _selected;

    /*****
    Date: 2026-09-25
    Name: _showProgress
    Description: 是否绘制作业进度条（设施处于作业态且存在未结任务）。
    *****/
    private bool _showProgress;

    /*****
    Date: 2026-09-25
    Name: _progress
    Description: 作业进度比例（0~1），取自同目标未结任务的 ProgressFraction。
    *****/
    private double _progress;

    /*****
    Date: 2026-09-06
    Name: Facility
    Description: 关联的设施模拟对象。
    *****/
    public FacilitySim Facility => _facility!;

    /*****
    Date: 2026-09-06
    Name: Selected
    Description: 是否被选中；变化时触发重绘。
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
    Date: 2026-09-26
    Name: Setup
    Description: 绑定设施模拟对象与地图视图：加载完好/破损贴图与按占地比例选定的施工警戒线贴图，定位到占地原点左上角并订阅状态变更。**不再绘制名称标签**（地图上只有图形，名称由选中后的设施面板与建造面板呈现），避免标签遮挡画面。
    *****/
    public void Setup(FacilitySim facility, MapView mapView)
    {
        _facility = facility;
        _mapView = mapView;

        if (facility.Def != null)
        {
            if (!string.IsNullOrEmpty(facility.Def.IconTexturePath))
                _iconTexture = GD.Load<Texture2D>(facility.Def.IconTexturePath);
            if (!string.IsNullOrEmpty(facility.Def.BrokenTexturePath))
                _brokenTexture = GD.Load<Texture2D>(facility.Def.BrokenTexturePath);
        }
        _warningTexture = GD.Load<Texture2D>(WarningLinePathFor(facility.Size));

        Vector2 tileSize = new(MapView.TileSize, MapView.TileSize);
        Position = mapView.MapToLocal(facility.OriginCell) - tileSize / 2f;

        facility.StateChanged += OnStateChanged;
        QueueRedraw();
    }

    /*****
    Date: 2026-09-06
    Name: OnStateChanged
    Description: 设施状态变更时请求重绘。
    *****/
    private void OnStateChanged(FacilitySim facility, FacilityState newState) => QueueRedraw();

    /*****
    Date: 2026-09-26
    Name: WarningLinePathFor
    Description: 按占地比例选择施工警戒线贴图：正方形（X==Y，如 1×1 / 2×2 / 3×3）用 1:1；偏宽（X>Y，如 2×1 / 3×2）用 21:9；偏高（X<Y，如 1×2 / 2×3）用 9:16。三种图使用时一律拉伸铺满占地（不保持原比例），故此处只按"宽/高/方"三分即可。
    *****/
    public static string WarningLinePathFor(Vector2I size)
        => size.X > size.Y ? WarningLineWidePath
            : size.X < size.Y ? WarningLineTallPath
            : WarningLineSquarePath;

    /*****
    Date: 2026-09-26
    Name: IsWorkingState
    Description: 是否作业中状态（建造中 / 拆除中 / 维修中）——这三类状态需要在设施贴图之上叠加施工警戒线。
    *****/
    private static bool IsWorkingState(FacilityState state)
        => state is FacilityState.UnderConstruction or FacilityState.Demolishing or FacilityState.UnderRepair;

    /*****
    Date: 2026-09-26
    Name: _Draw
    Description: 绘制设施占地：按状态选贴图（运行用完好图、损坏与维修中用破损图、建造中与拆除中用完好图），贴图缺失时回退状态色块；**作业中（建造/拆除/维修）再叠加施工警戒线**（拉伸铺满占地）；随后绘制作业进度条与选中黄框。
    *****/
    public override void _Draw()
    {
        if (_facility == null || _mapView == null) return;

        var size = new Vector2(
            _facility.Size.X * MapView.TileSize,
            _facility.Size.Y * MapView.TileSize);

        Texture2D? texture = _facility.State switch
        {
            FacilityState.Operational => _iconTexture,
            FacilityState.Damaged or FacilityState.UnderRepair => _brokenTexture,
            _ => _iconTexture, // 建造中/拆除中：完好贴图 + 施工警戒线
        };
        if (texture != null)
        {
            DrawTextureRect(texture, new Rect2(Vector2.Zero, size), false);
        }
        else
        {
            Color color = _facility.State switch
            {
                FacilityState.Damaged => DamagedColor,
                FacilityState.UnderRepair => UnderRepairColor,
                FacilityState.UnderConstruction => UnderConstructionColor,
                FacilityState.Demolishing => DemolishingColor,
                _ => OperationalColor,
            };
            DrawRect(new Rect2(Vector2.Zero, size), color);
        }

        // 施工警戒线：作业态（建造/拆除/维修）叠加在设施贴图之上，拉伸铺满整个占地
        if (IsWorkingState(_facility.State) && _warningTexture != null)
        {
            DrawTextureRect(_warningTexture, new Rect2(Vector2.Zero, size), false);
        }

        // 作业进度条（修复/建造/拆除中）：贴占地底边绘制，未选中也能看到进度
        if (_showProgress)
        {
            const float barHeight = 6f;
            var barOrigin = new Vector2(0f, size.Y - barHeight);
            DrawRect(new Rect2(barOrigin, new Vector2(size.X, barHeight)), new Color(0f, 0f, 0f, 0.65f));
            DrawRect(new Rect2(barOrigin, new Vector2(size.X * (float)_progress, barHeight)), new Color(0.35f, 0.85f, 0.45f));
        }

        if (_selected)
        {
            DrawRect(new Rect2(new Vector2(-3f, -3f), size + new Vector2(6f, 6f)), SelectedOutlineColor, false, 3f);
        }
    }

    /*****
    Date: 2026-09-25
    Name: _Process
    Description: 每帧同步作业进度（仅作业态需要）：取同目标未结任务的进度比例，变化时请求重绘，使地图上的进度条随时间平滑推进。
    *****/
    public override void _Process(double delta)
    {
        if (_facility == null) return;

        bool show = _facility.State is FacilityState.UnderRepair or FacilityState.UnderConstruction or FacilityState.Demolishing;
        double progress = 0.0;
        if (show && FindActiveTask() is { } task) progress = task.ProgressFraction;

        if (show == _showProgress && Math.Abs(progress - _progress) < 0.0001) return;
        _showProgress = show;
        _progress = progress;
        QueueRedraw();
    }

    /*****
    Date: 2026-09-25
    Name: FindActiveTask
    Description: 查找当前设施的同目标未结任务（含已挂起者——中止后进度条仍显示保留的进度，施工警戒线由设施状态维持）；无则返回 null。
    *****/
    private ITask? FindActiveTask()
    {
        if (_facility == null) return null;
        foreach (ITask task in GameRoot.Instance?.Simulation?.TaskBoard.Active ?? Array.Empty<ITask>())
        {
            if (ReferenceEquals(task.Target, _facility)
                && task.State is TaskState.Pending or TaskState.Assigned or TaskState.InProgress or TaskState.Suspended)
            {
                return task;
            }
        }
        return null;
    }

    /*****
    Date: 2026-09-06
    Name: _ExitTree
    Description: 节点退出时注销事件订阅，防止悬空回调。
    *****/
    public override void _ExitTree()
    {
        if (_facility != null) _facility.StateChanged -= OnStateChanged;
    }
}
