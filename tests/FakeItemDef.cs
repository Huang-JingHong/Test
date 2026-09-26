using RelayStation.Core.Items;

namespace RelayStation.Tests;

/*****
Date: 2026-09-26
Name: FakeItemDef
Description: 测试用物品定义替身（实现 IItemDef）。单元测试宿主没有 Godot 引擎运行时，无法构造 ItemDef（Resource，构造会击穿测试进程），故物品堆与背包相关的逻辑用纯 C# 替身驱动——这与 FacilityDef / GridMapData 的 Resource 用例被标记 Skip 是同一原因。
*****/
internal sealed class FakeItemDef : IItemDef
{
    /*****
    Date: 2026-09-26
    Name: FakeItemDef
    Description: 构造函数；以标识与单组上限创建替身定义，显示名/图标路径/重量/装备槽/容器容量/消耗与装备效果均可省略。
    *****/
    public FakeItemDef(string id, int maxStack, string displayName = "", string iconTexturePath = "",
        float weightKg = 0f, EquipmentSlot equipSlot = EquipmentSlot.None, float containerCapacityKg = 0f,
        ConsumableKind consumableKind = ConsumableKind.None, float needRestoreAmount = 0f,
        double consumeGameMinutes = 10.0, float workSpeedBonusPercent = 0f, float moveSpeedBonusPercent = 0f)
    {
        Id = id;
        MaxStack = maxStack;
        DisplayName = displayName;
        IconTexturePath = iconTexturePath;
        WeightKg = weightKg;
        EquipSlot = equipSlot;
        ContainerCapacityKg = containerCapacityKg;
        ConsumableKind = consumableKind;
        NeedRestoreAmount = needRestoreAmount;
        ConsumeGameMinutes = consumeGameMinutes;
        WorkSpeedBonusPercent = workSpeedBonusPercent;
        MoveSpeedBonusPercent = moveSpeedBonusPercent;
    }

    /*****
    Date: 2026-09-26
    Name: Id
    Description: 物品唯一标识。
    *****/
    public string Id { get; }

    /*****
    Date: 2026-09-26
    Name: DisplayName
    Description: 物品显示名称。
    *****/
    public string DisplayName { get; }

    /*****
    Date: 2026-09-26
    Name: MaxStack
    Description: 单组最大数量。
    *****/
    public int MaxStack { get; }

    /*****
    Date: 2026-09-26
    Name: IconTexturePath
    Description: 物品图标贴图路径。
    *****/
    public string IconTexturePath { get; }

    /*****
    Date: 2026-09-26
    Name: WeightKg
    Description: 单位重量（kg）。
    *****/
    public float WeightKg { get; }

    /*****
    Date: 2026-09-26
    Name: EquipSlot
    Description: 装备槽位（None=不可装备）。
    *****/
    public EquipmentSlot EquipSlot { get; }

    /*****
    Date: 2026-09-26
    Name: ContainerCapacityKg
    Description: 作为容器时提供的额外容量（kg）。
    *****/
    public float ContainerCapacityKg { get; }

    /*****
    Date: 2026-09-26
    Name: ConsumableKind
    Description: 可消耗种类（None=不可消耗）。
    *****/
    public ConsumableKind ConsumableKind { get; }

    /*****
    Date: 2026-09-26
    Name: NeedRestoreAmount
    Description: 消耗 1 个恢复的需求值。
    *****/
    public float NeedRestoreAmount { get; }

    /*****
    Date: 2026-09-26
    Name: ConsumeGameMinutes
    Description: 消耗 1 个所需的游戏分钟数。
    *****/
    public double ConsumeGameMinutes { get; }

    /*****
    Date: 2026-09-26
    Name: WorkSpeedBonusPercent
    Description: 装备后的工作效率加成（0.1 = +10%）。
    *****/
    public float WorkSpeedBonusPercent { get; }

    /*****
    Date: 2026-09-26
    Name: MoveSpeedBonusPercent
    Description: 装备后的移动速度加成（0.2 = +20%）。
    *****/
    public float MoveSpeedBonusPercent { get; }
}