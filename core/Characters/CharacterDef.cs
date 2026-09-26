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
    Date: 2026-09-25
    Name: Bio
    Description: 角色背景介绍（多行文本，供对话/角色面板展示）。
    *****/
    [Export] public string Bio { get; set; } = "";

    /*****
    Date: 2026-09-25
    Name: IsCaptain
    Description: 是否为队长（开场叙事聚焦对象等用途）。
    *****/
    [Export] public bool IsCaptain { get; set; }

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

    /*****
    Date: 2026-09-25
    Name: Gender
    Description: 角色性别（人物面板展示；后续对话/立绘分支依据）。
    *****/
    [Export] public Gender Gender { get; set; } = Gender.Male;

    /*****
    Date: 2026-09-25
    Name: Age
    Description: 角色年龄（岁）；人物面板展示，供后续年龄相关系统使用。
    *****/
    [Export] public int Age { get; set; } = 30;

    /*****
    Date: 2026-09-26
    Name: DefaultBaseCarryCapacityKg
    Description: 基础负重默认值（kg）；CharacterDef 缺省（引擎外测试等场景）时同样取此值。
    *****/
    public const float DefaultBaseCarryCapacityKg = 15f;

    /*****
    Date: 2026-09-26
    Name: BaseCarryCapacityKg
    Description: 角色自身基础负重（kg）；背包容量上限 = 本值 + 已装备容器（背包）提供的容量之和，物品按单位重量×数量计（只管重量，不涉及体积）。
    *****/
    [Export] public float BaseCarryCapacityKg { get; set; } = DefaultBaseCarryCapacityKg;

    /*****
    Date: 2026-09-25
    Name: SpriteTexturePath
    Description: 地图立绘（全身像）贴图路径；留空则沿用 CharacterAgent.tscn 内置的占位贴图。
    *****/
    [Export] public string SpriteTexturePath { get; set; } = "";

    /*****
    Date: 2026-09-26
    Name: ActionSpriteFolderPath
    Description: 动作立绘（朝向/作业）所在文件夹路径；约定其内文件名为「角色 Id_动作名.png」（如 .../characters_actions/char_field_up.png，动作名：up/down/left/right/build_or_repair）；留空表示无动作立绘，一律沿用 SpriteTexturePath 默认立绘。
    *****/
    [Export] public string ActionSpriteFolderPath { get; set; } = "";

    /*****
    Date: 2026-09-25
    Name: PortraitTexturePath
    Description: 对话框默认（平静）头像贴图路径；其它表情由 CharacterPortraitLibrary 按「路径扩展名前追加 _表情后缀」约定解析，缺失自动回退本默认头像。
    *****/
    [Export] public string PortraitTexturePath { get; set; } = "";
}
