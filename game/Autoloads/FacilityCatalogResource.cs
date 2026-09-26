using Godot;
using RelayStation.Core.Facilities;

namespace RelayStation.Game.Autoloads;

/*****
Date: 2026-09-25
Name: FacilityCatalogResource
Description: 可建造设施目录资源（.tres 数据驱动）；与 DemoConfigResource.Facilities（地图初始摆件）解耦——本目录只承载「建造面板可选」的设施定义，经由 FacilityCatalog 按分类分组。新增设施只需追加一项并在其 .tres 上标注 Category，框架与 UI 零改动。
*****/
[GlobalClass]
public partial class FacilityCatalogResource : Resource
{
    /*****
    Date: 2026-09-25
    Name: Facilities
    Description: 可建造设施定义数组（全部可作为建造面板的候选）。
    *****/
    [Export] public FacilityDef[] Facilities { get; set; } = Array.Empty<FacilityDef>();
}
