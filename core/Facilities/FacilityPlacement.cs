using Godot;

namespace RelayStation.Core.Facilities;

/*****
Date: 2026-09-06
Name: FacilityPlacement
Description: 设施放置条目（Godot Resource）；记录已放置设施的静态定义引用、占地原点与当前状态，作为地图数据中设施列表的持久化单元（为后续基地建造功能预留）。
*****/
[GlobalClass]
public partial class FacilityPlacement : Resource
{
    /*****
    Date: 2026-09-06
    Name: Def
    Description: 设施的静态定义引用（名称、占地格、修复耗时等）；可为 null。
    *****/
    [Export] public FacilityDef? Def { get; set; }

    /*****
    Date: 2026-09-06
    Name: Origin
    Description: 设施占地范围的原点格子坐标（左上角）。
    *****/
    [Export] public Vector2I Origin { get; set; }

    /*****
    Date: 2026-09-06
    Name: State
    Description: 保存时的设施状态。
    *****/
    [Export] public FacilityState State { get; set; } = FacilityState.Damaged;
}
