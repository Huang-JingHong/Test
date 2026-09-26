using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Items;
using Xunit;

/*****
Date: 2026-09-26
Name: CharacterInventoryTests
Description: 角色背包与装备单元测试；验证基础负重与容器容量、单位重量×数量的总重校验（超重整笔拒绝）、同种物品合并、装备槽互斥与换装回包、卸下超重拒绝、装备物品不计入背包条目，以及存档恢复（含超重原样带回）。
*****/
namespace RelayStation.Tests;

public sealed class CharacterInventoryTests
{
    /*****
    Date: 2026-09-26
    Name: Parts
    Description: 普通物品（不可装备，单位重 2kg）。
    *****/
    private static readonly FakeItemDef Parts = new("parts", 100, "零件", weightKg: 2f);

    /*****
    Date: 2026-09-26
    Name: Backpack
    Description: 背包（背部槽，自重 1.5kg，提供 20kg 容量）。
    *****/
    private static readonly FakeItemDef Backpack =
        new("backpack", 1, "背包", weightKg: 1.5f, equipSlot: EquipmentSlot.Back, containerCapacityKg: 20f);

    /*****
    Date: 2026-09-26
    Name: SuitA
    Description: 身体槽装备 A（自重 3kg）。
    *****/
    private static readonly FakeItemDef SuitA = new("suit_a", 1, "工作服A", weightKg: 3f, equipSlot: EquipmentSlot.Body);

    /*****
    Date: 2026-09-26
    Name: SuitB
    Description: 身体槽装备 B（自重 5kg）。
    *****/
    private static readonly FakeItemDef SuitB = new("suit_b", 1, "工作服B", weightKg: 5f, equipSlot: EquipmentSlot.Body);

    /*****
    Date: 2026-09-26
    Name: NewInventory_IsEmptyWithBaseCapacity
    Description: 新背包为空：总重 0、容量＝基础负重、不超重。
    *****/
    [Fact]
    public void NewInventory_IsEmptyWithBaseCapacity()
    {
        var inventory = new CharacterInventory(15f);

        Assert.Empty(inventory.Entries);
        Assert.Empty(inventory.Equipped);
        Assert.Equal(15f, inventory.CapacityKg, 3);
        Assert.Equal(0f, inventory.TotalWeightKg, 3);
        Assert.False(inventory.IsOverloaded);
    }

    /*****
    Date: 2026-09-26
    Name: TryAdd_MergesSameDef_AndCountsWeight
    Description: 装入成功并按「单位重量×数量」计重；同种物品合并为一条记录。
    *****/
    [Fact]
    public void TryAdd_MergesSameDef_AndCountsWeight()
    {
        var inventory = new CharacterInventory(15f);

        Assert.True(inventory.TryAdd(Parts, 3));
        Assert.Equal(6f, inventory.TotalWeightKg, 3);
        Assert.True(inventory.TryAdd(Parts, 2));

        Assert.Single(inventory.Entries);
        Assert.Equal(5, inventory.Entries[0].Count);
        Assert.Equal(10f, inventory.TotalWeightKg, 3);
        Assert.Equal(5, inventory.CountOf(Parts));
    }

    /*****
    Date: 2026-09-26
    Name: TryAdd_OverCapacity_RejectsWholeAmount
    Description: 超过容量上限时整笔拒绝（背包保持原样）。
    *****/
    [Fact]
    public void TryAdd_OverCapacity_RejectsWholeAmount()
    {
        var inventory = new CharacterInventory(5f);

        Assert.False(inventory.TryAdd(Parts, 3)); // 6kg > 5kg
        Assert.Empty(inventory.Entries);
        Assert.Equal(0f, inventory.TotalWeightKg, 3);
    }

    /*****
    Date: 2026-09-26
    Name: EquippingBackpack_RaisesCapacity_AndRemovesItFromBag
    Description: 装备背包后容量提升（基础 + 容器容量），背包本身不再计入携带总重与条目列表。
    *****/
    [Fact]
    public void EquippingBackpack_RaisesCapacity_AndRemovesItFromBag()
    {
        var inventory = new CharacterInventory(15f);
        Assert.True(inventory.TryAdd(Backpack, 1));

        Assert.True(inventory.TryEquip(Backpack));

        Assert.Equal(35f, inventory.CapacityKg, 3);
        Assert.Equal(0f, inventory.TotalWeightKg, 3);
        Assert.Equal(Backpack, inventory.GetEquipped(EquipmentSlot.Back));
        Assert.Empty(inventory.Entries);
        Assert.Equal(0, inventory.CountOf(Backpack));
    }

    /*****
    Date: 2026-09-26
    Name: TryAdd_AllowsWeightWithinBackpackCapacity
    Description: 装备背包后可装入的总重按「基础 + 容器容量」校验（30kg 在 35kg 内可装入）。
    *****/
    [Fact]
    public void TryAdd_AllowsWeightWithinBackpackCapacity()
    {
        var inventory = new CharacterInventory(15f);
        inventory.TryAdd(Backpack, 1);
        inventory.TryEquip(Backpack);

        Assert.True(inventory.TryAdd(Parts, 15)); // 30kg ≤ 35kg
        Assert.Equal(30f, inventory.TotalWeightKg, 3);
    }

    /*****
    Date: 2026-09-26
    Name: TryEquip_NotInBagOrNotEquippable_Fails
    Description: 不在背包内或槽位为 None（不可装备）时装备失败。
    *****/
    [Fact]
    public void TryEquip_NotInBagOrNotEquippable_Fails()
    {
        var inventory = new CharacterInventory(15f);

        Assert.False(inventory.TryEquip(SuitA)); // 不在背包内
        inventory.TryAdd(Parts, 1);
        Assert.False(inventory.TryEquip(Parts)); // 不可装备
        Assert.Null(inventory.GetEquipped(EquipmentSlot.Body));
    }

    /*****
    Date: 2026-09-26
    Name: TryEquip_SameSlot_ReplacesAndReturnsOldToBag
    Description: 同槽换装：新装备上槽位，旧装备回到背包条目。
    *****/
    [Fact]
    public void TryEquip_SameSlot_ReplacesAndReturnsOldToBag()
    {
        var inventory = new CharacterInventory(15f);
        inventory.TryAdd(SuitA, 1);
        inventory.TryAdd(SuitB, 1);
        Assert.True(inventory.TryEquip(SuitA));

        Assert.True(inventory.TryEquip(SuitB));

        Assert.Equal(SuitB, inventory.GetEquipped(EquipmentSlot.Body));
        Assert.Equal(1, inventory.CountOf(SuitA)); // 旧装备回包
        Assert.Equal(0, inventory.CountOf(SuitB));
        Assert.Equal(3f, inventory.TotalWeightKg, 3); // 只计回包的旧装备
    }

    /*****
    Date: 2026-09-26
    Name: Unequip_OverCapacity_IsRejected
    Description: 卸下后超重时拒绝：槽位与背包均不变。
    *****/
    [Fact]
    public void Unequip_OverCapacity_IsRejected()
    {
        var inventory = new CharacterInventory(15f);
        inventory.TryAdd(Backpack, 1);
        inventory.TryEquip(Backpack);      // 容量 35kg
        inventory.TryAdd(Parts, 17);       // 34kg

        Assert.False(inventory.Unequip(EquipmentSlot.Back)); // 卸下后容量回 15kg、重量 35.5kg → 超重

        Assert.Equal(Backpack, inventory.GetEquipped(EquipmentSlot.Back));
        Assert.Equal(34f, inventory.TotalWeightKg, 3);
    }

    /*****
    Date: 2026-09-26
    Name: Remove_MoreThanHeld_TakesAllAndEmptiesEntry
    Description: 取出数量超过持有量时取出全部，条目即时移除。
    *****/
    [Fact]
    public void Remove_MoreThanHeld_TakesAllAndEmptiesEntry()
    {
        var inventory = new CharacterInventory(15f);
        inventory.TryAdd(Parts, 5);

        Assert.Equal(5, inventory.Remove(Parts, 10));

        Assert.Empty(inventory.Entries);
        Assert.Equal(0f, inventory.TotalWeightKg, 3);
    }

    /*****
    Date: 2026-09-26
    Name: Restore_ReappliesOverweightState
    Description: 存档恢复不做容量校验：可带回超重状态（装备与条目原样恢复）。
    *****/
    [Fact]
    public void Restore_ReappliesOverweightState()
    {
        var inventory = new CharacterInventory(15f);

        inventory.Restore(
            new[] { (EquipmentSlot.Back, (IItemDef)Backpack) },
            new[] { ((IItemDef)Parts, 20) }); // 40kg > 35kg 容量

        Assert.Equal(Backpack, inventory.GetEquipped(EquipmentSlot.Back));
        Assert.Equal(40f, inventory.TotalWeightKg, 3);
        Assert.Equal(35f, inventory.CapacityKg, 3);
        Assert.True(inventory.IsOverloaded);
    }

    /*****
    Date: 2026-09-26
    Name: Character_DefaultsToBaseCarryCapacity
    Description: 角色（无定义）默认按 CharacterDef.DefaultBaseCarryCapacityKg 创建背包。
    *****/
    [Fact]
    public void Character_DefaultsToBaseCarryCapacity()
    {
        var c = new CharacterSim(null, new Vector2I(0, 0));
        Assert.Equal(CharacterDef.DefaultBaseCarryCapacityKg, c.Inventory.BaseCapacityKg, 3);
    }
}