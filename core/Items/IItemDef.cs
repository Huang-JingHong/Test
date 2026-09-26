namespace RelayStation.Core.Items;

/*****
Date: 2026-09-26
Name: IItemDef
Description: 物品静态定义的只读抽象；供 ItemStack / ItemRegistry 依赖，使物品堆的增删、拆堆、堆叠等纯逻辑可在无引擎运行时（单元测试）下验证——Godot Resource 在生产运行时需引擎支撑，故数据层实现（ItemDef）与逻辑层解耦于此接口。
*****/
public interface IItemDef
{
    /*****
    Date: 2026-09-26
    Name: Id
    Description: 物品唯一标识。
    *****/
    string Id { get; }

    /*****
    Date: 2026-09-26
    Name: DisplayName
    Description: 物品显示名称。
    *****/
    string DisplayName { get; }

    /*****
    Date: 2026-09-26
    Name: MaxStack
    Description: 单组最大数量（一组一图）。
    *****/
    int MaxStack { get; }

    /*****
    Date: 2026-09-26
    Name: IconTexturePath
    Description: 物品图标贴图的资源路径（res:// 协议）。
    *****/
    string IconTexturePath { get; }

    /*****
    Date: 2026-09-26
    Name: WeightKg
    Description: 单位重量（kg）；一组物品的重量 = 单位重量 × 数量，背包容量按总重校验。
    *****/
    float WeightKg { get; }

    /*****
    Date: 2026-09-26
    Name: EquipSlot
    Description: 可穿戴物品的装备槽位；None 表示不可装备。
    *****/
    EquipmentSlot EquipSlot { get; }

    /*****
    Date: 2026-09-26
    Name: ContainerCapacityKg
    Description: 作为容器（背包）时提供的额外容量（kg）；0 表示不是容器。
    *****/
    float ContainerCapacityKg { get; }

    /*****
    Date: 2026-09-26
    Name: ConsumableKind
    Description: 可消耗种类（None = 不可消耗）；取用任务据此选择需要恢复的需求项。
    *****/
    ConsumableKind ConsumableKind { get; }

    /*****
    Date: 2026-09-26
    Name: NeedRestoreAmount
    Description: 消耗 1 个后恢复的需求值（0 = 不恢复）；封顶由需求系统处理（不超过 100）。
    *****/
    float NeedRestoreAmount { get; }

    /*****
    Date: 2026-09-26
    Name: ConsumeGameMinutes
    Description: 消耗 1 个所需的游戏分钟数（吃/喝的耗时；≤0 按 0 处理）。
    *****/
    double ConsumeGameMinutes { get; }

    /*****
    Date: 2026-09-26
    Name: WorkSpeedBonusPercent
    Description: 装备后提供的工作效率加成（0.1 = +10%；0 = 无加成）；对全部作业生效。
    *****/
    float WorkSpeedBonusPercent { get; }

    /*****
    Date: 2026-09-26
    Name: MoveSpeedBonusPercent
    Description: 装备后提供的移动速度加成（0.2 = +20%；0 = 无加成）。
    *****/
    float MoveSpeedBonusPercent { get; }
}
