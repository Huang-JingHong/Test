using Godot;

namespace RelayStation.Core.Items;

/*****
Date: 2026-09-26
Name: ItemDef
Description: 物品静态定义（Godot Resource，.tres 数据驱动）；承载唯一标识、显示名称、单组最大堆叠数、图标贴图路径，以及背包/装备系统所需的单位重量、装备槽位与容器容量。物品无「损坏态」概念（与 FacilityDef 的关键差异），也不受 tile 对齐约束——只承载「怎么显示、一组最多多少个」这两类静态数据。继承 Resource 仅用于 .tres 数据装载，不属于 Node 体系（§2.1 约定之内的数据层例外）；对逻辑层只暴露 IItemDef，使物品堆逻辑可脱离引擎运行时测试。
*****/
[GlobalClass]
public partial class ItemDef : Resource, IItemDef
{
    /*****
    Date: 2026-09-26
    Name: Id
    Description: 物品唯一标识（如 general_purpose_parts）。
    *****/
    [Export] public string Id { get; set; } = "";

    /*****
    Date: 2026-09-26
    Name: DisplayName
    Description: 物品显示名称（如 通用零件）。
    *****/
    [Export] public string DisplayName { get; set; } = "";

    /*****
    Date: 2026-09-26
    Name: MaxStack
    Description: 单组最大数量（一组一图）；超过则按此值拆堆，如 110 个通用零件（MaxStack=100）拆为 100/100 与 10/100 两图。非法值（≤0）按 1 处理。
    *****/
    [Export] public int MaxStack { get; set; } = 1;

    /*****
    Date: 2026-09-26
    Name: IconTexturePath
    Description: 物品图标贴图的资源路径（res:// 协议）；空串表示未配置，表现层回退为占位色块。
    *****/
    [Export] public string IconTexturePath { get; set; } = "";

    /*****
    Date: 2026-09-26
    Name: WeightKg
    Description: 单位重量（kg）；一组物品的重量 = 单位重量 × 数量，角色背包的容量按总重校验。旧 .tres 缺字段取默认 0（无重量）。
    *****/
    [Export] public float WeightKg { get; set; }

    /*****
    Date: 2026-09-26
    Name: EquipSlot
    Description: 可穿戴物品的装备槽位（None=不可装备）；同一槽位最多装备一件，背包装备在 Back 槽并提供容器容量。
    *****/
    [Export] public EquipmentSlot EquipSlot { get; set; } = EquipmentSlot.None;

    /*****
    Date: 2026-09-26
    Name: ContainerCapacityKg
    Description: 作为容器（背包）时为穿戴者提供的额外容量（kg）；0 表示不是容器。
    *****/
    [Export] public float ContainerCapacityKg { get; set; }

    /*****
    Date: 2026-09-26
    Name: ConsumableKind
    Description: 可消耗种类（None = 不可消耗）；Food/Water 分别对应恢复「进食」「饮水」需求。
    *****/
    [Export] public ConsumableKind ConsumableKind { get; set; } = ConsumableKind.None;

    /*****
    Date: 2026-09-26
    Name: NeedRestoreAmount
    Description: 消耗 1 个后恢复的需求值（0 = 不恢复）。
    *****/
    [Export] public float NeedRestoreAmount { get; set; }

    /*****
    Date: 2026-09-26
    Name: ConsumeGameMinutes
    Description: 消耗 1 个所需的游戏分钟数（吃/喝的耗时）。
    *****/
    [Export] public double ConsumeGameMinutes { get; set; } = 10.0;

    /*****
    Date: 2026-09-26
    Name: WorkSpeedBonusPercent
    Description: 装备后提供的工作效率加成（0.1 = +10%；0 = 无加成）；对全部作业生效（不限专长匹配）。
    *****/
    [Export] public float WorkSpeedBonusPercent { get; set; }

    /*****
    Date: 2026-09-26
    Name: MoveSpeedBonusPercent
    Description: 装备后提供的移动速度加成（0.2 = +20%；0 = 无加成）。
    *****/
    [Export] public float MoveSpeedBonusPercent { get; set; }
}
