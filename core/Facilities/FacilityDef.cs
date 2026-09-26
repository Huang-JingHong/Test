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
    Date: 2026-09-25
    Name: RepairPartsCost
    Description: 修复全程消耗的备用零件总数（随修复进度实时整件扣除；开发者模式免费）。0 表示无消耗。
    *****/
    [Export] public int RepairPartsCost { get; set; } = 10;

    /*****
    Date: 2026-09-25
    Name: MaxWorkers
    Description: 同时维修本设施的最大人数；超过 1 时多名角色可加入同一维修任务，总速度≈各自效率倍率之和。
    *****/
    [Export] public int MaxWorkers { get; set; } = 1;

    /*****
    Date: 2026-09-26
    Name: BlocksMovement
    Description: 是否阻挡角色通行。true（默认）时占地格计入寻路阻塞集，角色绕行并在相邻格作业；false 时角色可直接走到设施格上（家具类，如床/沙发/马桶/淋浴间）。注意：**无论是否阻挡，占地格一律计入「已占用」**——编辑器放置仍禁止两个设施叠放。
    *****/
    [Export] public bool BlocksMovement { get; set; } = true;

    /*****
    Date: 2026-09-25
    Name: BuildCost
    Description: 建造全程消耗的备用零件总数（随建造进度实时整件扣除，与修复消耗同机制；开发者模式免费、不注入库存）。
    *****/
    [Export] public int BuildCost { get; set; } = 20;

    /*****
    Date: 2026-09-25
    Name: BuildGameMinutes
    Description: 基准建造耗时（游戏分钟；实际耗时受角色专长效率影响）。不复用修复耗时，二者可独立配置。
    *****/
    [Export] public double BuildGameMinutes { get; set; } = 30.0;

    /*****
    Date: 2026-09-25
    Name: DemolishGameMinutes
    Description: 基准拆除耗时（游戏分钟；实际耗时受角色专长效率影响）。拆除不消耗零件，完成时按 BuildCost 的 50% 返还。
    *****/
    [Export] public double DemolishGameMinutes { get; set; } = 20.0;

    /*****
    Date: 2026-09-25
    Name: Category
    Description: 设施分类（建造面板按类过滤的依据）；数据驱动，新增分类无需改动框架。
    *****/
    [Export] public FacilityCategory Category { get; set; } = FacilityCategory.Furniture;

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
