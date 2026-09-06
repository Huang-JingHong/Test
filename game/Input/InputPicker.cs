using Godot;
using RelayStation.Core.Common;
using RelayStation.Core.Facilities;
using RelayStation.Game.Map;

namespace RelayStation.Game.Input;

/*****
Date: 2026-09-06
Name: InputPicker
Description: 点击拾取；将鼠标左键点击换算为格子坐标，优先精确命中、其次四邻命中设施，通过 FacilityPicked 事件通知选中（点击空处通知 null 以取消选中）。
*****/
public partial class InputPicker : Node
{
    /*****
    Date: 2026-09-06
    Name: NeighborOffsets
    Description: 四方向相邻偏移（邻接命中容差用）。
    *****/
    private static readonly Vector2I[] NeighborOffsets =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
    };

    /*****
    Date: 2026-09-06
    Name: _simulation
    Description: 模拟核心引用。
    *****/
    private Simulation? _simulation;

    /*****
    Date: 2026-09-06
    Name: _mapView
    Description: 地图视图（坐标换算）。
    *****/
    private MapView? _mapView;

    /*****
    Date: 2026-09-06
    Name: FacilityPicked
    Description: 点击选中设施事件；参数为命中的设施（点击空处为 null）。
    *****/
    public event Action<FacilitySim?>? FacilityPicked;

    /*****
    Date: 2026-09-06
    Name: Enabled
    Description: 输入拾取是否启用；编辑模式下设为 false 以禁用点击拾取，避免与画笔冲突。
    *****/
    public bool Enabled { get; set; } = true;

    /*****
    Date: 2026-09-06
    Name: Setup
    Description: 绑定模拟核心与地图视图。
    *****/
    public void Setup(Simulation simulation, MapView mapView)
    {
        _simulation = simulation;
        _mapView = mapView;
    }

    /*****
    Date: 2026-09-06
    Name: _UnhandledInput
    Description: 处理鼠标左键按下：换算格子并发出 FacilityPicked 事件。
    *****/
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Enabled) return;
        if (_simulation == null || _mapView == null) return;
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;

        Vector2I cell = _mapView.LocalToMap(_mapView.GetGlobalMousePosition());
        FacilityPicked?.Invoke(FindFacilityAt(cell));
    }

    /*****
    Date: 2026-09-06
    Name: FindFacilityAt
    Description: 查找占格命中的设施：先精确匹配点击格，再匹配四邻格（提高小目标命中率）；无命中返回 null。
    *****/
    private FacilitySim? FindFacilityAt(Vector2I cell)
    {
        foreach (FacilitySim facility in _simulation!.Facilities)
        {
            if (facility.Occupies(cell)) return facility;
        }

        foreach (Vector2I offset in NeighborOffsets)
        {
            Vector2I neighbor = cell + offset;
            foreach (FacilitySim facility in _simulation.Facilities)
            {
                if (facility.Occupies(neighbor)) return facility;
            }
        }
        return null;
    }
}
