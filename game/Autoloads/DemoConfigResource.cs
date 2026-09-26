using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Facilities;

namespace RelayStation.Game.Autoloads;

/*****
Date: 2026-09-06
Name: DemoConfigResource
Description: 演示配置资源（.tres 数据驱动）；持有本局出场的角色定义与设施定义数组。默认 1 名维修角色，改 characters 数组即可 3 人同屏（对应计划 §4.1「角色数量由配置驱动」）。
*****/
[GlobalClass]
public partial class DemoConfigResource : Resource
{
    /*****
    Date: 2026-09-06
    Name: Characters
    Description: 登场角色定义数组。
    *****/
    [Export] public CharacterDef[] Characters { get; set; } = Array.Empty<CharacterDef>();

    /*****
    Date: 2026-09-06
    Name: Facilities
    Description: 登场设施定义数组。
    *****/
    [Export] public FacilityDef[] Facilities { get; set; } = Array.Empty<FacilityDef>();

    /*****
    Date: 2026-09-25
    Name: StartingParts
    Description: 新游戏初始备用零件数量（占位资源系统）。
    *****/
    [Export] public int StartingParts { get; set; } = 100;

    /*****
    Date: 2026-09-26
    Name: StartingFood
    Description: 新游戏初始食物（压缩干粮）数量，散落到地上随机地板格；需求驱动的自动取用的来源。
    *****/
    [Export] public int StartingFood { get; set; } = 20;

    /*****
    Date: 2026-09-26
    Name: StartingWater
    Description: 新游戏初始饮用水数量，散落到地上随机地板格。
    *****/
    [Export] public int StartingWater { get; set; } = 20;
}
