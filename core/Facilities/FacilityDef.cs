using Godot;
using RelayStation.Core.Map;

namespace RelayStation.Core.Facilities;

/*****
Date: 2026-09-06
Name: FacilityDef
Description: 设施静态定义（Godot Resource，.tres 数据驱动）；承载唯一标识、名称、占地格、修复耗时、初始状态与所属功能区。继承 Resource 仅用于 .tres 数据装载，不属于 Node 体系（§2.1 约定之内的数据层例外）。
*****/
[GlobalClass]
public partial class FacilityDef : Resource
{
    /*****
    Date: 2026-09-06
    Name: Id
    Description: 设施唯一标识。
    *****/
    [Export] public string Id { get; set; } = "";

    /*****
    Date: 2026-09-06
    Name: DisplayName
    Description: 设施显示名称。
    *****/
    [Export] public string DisplayName { get; set; } = "";

    /*****
    Date: 2026-09-06
    Name: Size
    Description: 占地格数（宽 × 高）。
    *****/
    [Export] public Vector2I Size { get; set; } = new(2, 2);

    /*****
    Date: 2026-09-06
    Name: RepairGameMinutes
    Description: 基准修复耗时（游戏分钟；实际耗时受角色专长效率影响）。
    *****/
    [Export] public double RepairGameMinutes { get; set; } = 30.0;

    /*****
    Date: 2026-09-06
    Name: InitialState
    Description: 设施初始状态。
    *****/
    [Export] public FacilityState InitialState { get; set; } = FacilityState.Damaged;

    /*****
    Date: 2026-09-06
    Name: Zone
    Description: 所属功能区（决定在环形基地中的放置位置）。
    *****/
    [Export] public ZoneId Zone { get; set; } = ZoneId.LifeSupport;

    /*****
    Date: 2026-09-06
    Name: IconTexturePath
    Description: 设施正常状态贴图的资源路径（res:// 协议）；空串表示未配置，表现层回退为状态色块。
    *****/
    [Export] public string IconTexturePath { get; set; } = "";

    /*****
    Date: 2026-09-06
    Name: BrokenTexturePath
    Description: 设施破损状态贴图的资源路径（损坏与维修中共用破损图）；空串表示未配置。
    *****/
    [Export] public string BrokenTexturePath { get; set; } = "";
}
