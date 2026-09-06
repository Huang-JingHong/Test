using Godot;
using RelayStation.Core.Facilities;
using RelayStation.Game.Map;

namespace RelayStation.Game.Facilities;

/*****
Date: 2026-09-06
Name: FacilityView
Description: 设施表现层；优先以贴图呈现设施占地（运行用正常贴图，损坏/维修中共用破损贴图），未配置贴图时回退为状态色块（损坏红 / 维修中黄 / 运行绿）；含名称标签与选中黄色描边。订阅 FacilitySim.StateChanged 驱动重绘。
*****/
public partial class FacilityView : Node2D
{
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
    Date: 2026-09-06
    Name: OutlineColor
    Description: 占地描边颜色。
    *****/
    private static readonly Color OutlineColor = new(0f, 0f, 0f, 0.6f);

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
    Date: 2026-09-06
    Name: _selected
    Description: 是否被选中。
    *****/
    private bool _selected;

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
    Date: 2026-09-06
    Name: Setup
    Description: 绑定设施模拟对象与地图视图：定位到占地原点左上角、创建名称标签并订阅状态变更。
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

        Vector2 tileSize = new(MapView.TileSize, MapView.TileSize);
        Position = mapView.MapToLocal(facility.OriginCell) - tileSize / 2f;

        var label = new Label
        {
            Text = facility.Def?.DisplayName ?? facility.OriginCell.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        label.Position = new Vector2(-20f, -24f);
        label.Size = new Vector2(facility.Size.X * MapView.TileSize + 40f, 20f);
        AddChild(label);

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
    Date: 2026-09-06
    Name: _Draw
    Description: 绘制设施占地：优先绘制状态贴图（损坏/维修中用破损图、运行用正常图，缩放铺满占地），未配置贴图时回退状态色块；附深色描边与选中黄框。
    *****/
    public override void _Draw()
    {
        if (_facility == null || _mapView == null) return;

        var size = new Vector2(
            _facility.Size.X * MapView.TileSize,
            _facility.Size.Y * MapView.TileSize);

        Texture2D? texture = _facility.State is FacilityState.Damaged or FacilityState.UnderRepair
            ? _brokenTexture
            : _iconTexture;
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
                _ => OperationalColor,
            };
            DrawRect(new Rect2(Vector2.Zero, size), color);
        }
        DrawRect(new Rect2(Vector2.Zero, size), OutlineColor, false, 2f);

        if (_selected)
        {
            DrawRect(new Rect2(new Vector2(-3f, -3f), size + new Vector2(6f, 6f)), SelectedOutlineColor, false, 3f);
        }
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
