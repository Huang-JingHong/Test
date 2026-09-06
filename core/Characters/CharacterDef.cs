using Godot;

namespace RelayStation.Core.Characters;

/*****
Date: 2026-09-06
Name: CharacterDef
Description: 角色静态定义（Godot Resource，.tres 数据驱动）；承载唯一标识、姓名、出身、专长与特质占位，供策划不改代码调整数值。继承 Resource 仅用于 .tres 数据装载，不属于 Node 体系（§2.1 约定之内的数据层例外）。
*****/
[GlobalClass]
public partial class CharacterDef : Resource
{
    /*****
    Date: 2026-09-06
    Name: Id
    Description: 角色唯一标识。
    *****/
    [Export] public string Id { get; set; } = "";

    /*****
    Date: 2026-09-06
    Name: DisplayName
    Description: 角色显示姓名。
    *****/
    [Export] public string DisplayName { get; set; } = "";

    /*****
    Date: 2026-09-06
    Name: Origin
    Description: 角色出身背景。
    *****/
    [Export] public string Origin { get; set; } = "";

    /*****
    Date: 2026-09-06
    Name: Specialty
    Description: 角色专长类型。
    *****/
    [Export] public Specialty Specialty { get; set; } = Specialty.Maintenance;

    /*****
    Date: 2026-09-06
    Name: SpecialtyMultiplier
    Description: 专长作业的效率倍率（专长匹配的任务生效）。
    *****/
    [Export] public float SpecialtyMultiplier { get; set; } = 1.5f;

    /*****
    Date: 2026-09-06
    Name: Traits
    Description: 特质占位列表（阶段一无实际效果）。
    *****/
    [Export] public string[] Traits { get; set; } = Array.Empty<string>();
}
