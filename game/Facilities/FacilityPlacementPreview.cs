using Godot;
using RelayStation.Core.Common;
using RelayStation.Core.Facilities;
using RelayStation.Game.Map;

namespace RelayStation.Game.Facilities;

/*****
Date: 2026-09-26
Name: FacilityPlacementPreview
Description: 建造预览「幽灵」；建造工具选中设施后，在鼠标所指格以**半透明贴图**预示落位，并以边框颜色表达合法性——绿色=可放置，红色=不可放置（墙/真空/门、已被设施占用、格上有物品；材料不足不在此列）。合法性取自 Simulation.CheckFacilityPlacement，与真实放置共用一个判定，故预览不会与实际结果不一致。节点与设施视图同挂实体层，ZIndex 固定 3：高于人物（1）与选中人物（2）、低于框选矩形（10）。设施未配置贴图时回退为半透明色块。
*****/
public partial class FacilityPlacementPreview : Node2D
{
    /*****
    Date: 2026-09-26
    Name: ZOrder
    Description: 预览层级（高于人物与选中人物、低于框选矩形）。
    *****/
    public const int ZOrder = 3;

    /*****
    Date: 2026-09-26
    Name: GhostAlpha
    Description: 幽灵贴图的半透明度。
    *****/
    private const float GhostAlpha = 0.5f;

    /*****
    Date: 2026-09-26
    Name: ValidOutlineColor
    Description: 可放置时的边框颜色（绿）。
    *****/
    private static readonly Color ValidOutlineColor = new(0.30f, 0.90f, 0.40f);

    /*****
    Date: 2026-09-26
    Name: InvalidOutlineColor
    Description: 不可放置时的边框颜色（红）。
    *****/
    private static readonly Color InvalidOutlineColor = new(0.95f, 0.30f, 0.30f);

    /*****
    Date: 2026-09-26
    Name: PlaceholderColor
    Description: 设施未配置贴图时的半透明占位色块颜色。
    *****/
    private static readonly Color PlaceholderColor = new(0.72f, 0.76f, 0.84f, GhostAlpha);

    /*****
    Date: 2026-09-26
    Name: _simulation
    Description: 模拟层引用（放置合法性判定来源）。
    *****/
    private Simulation? _simulation;

    /*****
    Date: 2026-09-26
    Name: _mapView
    Description: 地图视图（格子坐标换算）。
    *****/
    private MapView? _mapView;

    /*****
    Date: 2026-09-26
    Name: _def
    Description: 当前预览的设施定义。
    *****/
    private FacilityDef? _def;

    /*****
    Date: 2026-09-26
    Name: _texture
    Description: 当前预览设施的贴图（未配置为 null，回退占位色块）。
    *****/
    private Texture2D? _texture;

    /*****
    Date: 2026-09-26
    Name: _valid
    Description: 当前落位是否合法（决定边框颜色）。
    *****/
    private bool _valid;

    /*****
    Date: 2026-09-26
    Name: Setup
    Description: 绑定模拟层与地图视图，设定层级并初始隐藏。
    *****/
    public void Setup(Simulation simulation, MapView mapView)
    {
        _simulation = simulation;
        _mapView = mapView;
        ZIndex = ZOrder;
        Clear();
    }

    /*****
    Date: 2026-09-26
    Name: ShowPlacement
    Description: 在指定占地原点显示预览：切换设施时加载其贴图，落位合法性经 CheckFacilityPlacement 判定（仅在定义或合法性变化时请求重绘，位置变化只改 Position），并定位于占地左上角。供编辑模式下的建造工具每帧调用。
    *****/
    public void ShowPlacement(FacilityDef def, Vector2I origin)
    {
        if (_simulation == null || _mapView == null) return;

        if (!ReferenceEquals(_def, def))
        {
            _def = def;
            _texture = string.IsNullOrEmpty(def.IconTexturePath)
                ? null
                : GD.Load<Texture2D>(def.IconTexturePath);
            QueueRedraw();
        }

        bool valid = _simulation.CheckFacilityPlacement(origin, def.Size) == PlacementObstacle.None;
        if (valid != _valid)
        {
            _valid = valid;
            QueueRedraw();
        }

        var tileSize = new Vector2(MapView.TileSize, MapView.TileSize);
        Position = _mapView.MapToLocal(origin) - tileSize / 2f;
        Visible = true;
    }

    /*****
    Date: 2026-09-26
    Name: Clear
    Description: 隐藏预览（退出编辑、切换工具、取消选中设施或鼠标移出地图时调用）；已隐藏时无操作。
    *****/
    public void Clear()
    {
        if (!Visible) return;
        Visible = false;
    }

    /*****
    Date: 2026-09-26
    Name: _Draw
    Description: 绘制半透明幽灵贴图（或占位色块）与合法性边框；尺寸按设施占地格数铺满。
    *****/
    public override void _Draw()
    {
        if (_def == null) return;

        var size = new Vector2(_def.Size.X * MapView.TileSize, _def.Size.Y * MapView.TileSize);
        var rect = new Rect2(Vector2.Zero, size);
        if (_texture != null)
            DrawTextureRect(_texture, rect, false, new Color(1f, 1f, 1f, GhostAlpha));
        else
            DrawRect(rect, PlaceholderColor);

        DrawRect(rect, _valid ? ValidOutlineColor : InvalidOutlineColor, false, 3f);
    }
}
