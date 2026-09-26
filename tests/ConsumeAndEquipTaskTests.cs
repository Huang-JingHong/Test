using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Events;
using RelayStation.Core.Items;
using RelayStation.Core.Map;
using RelayStation.Core.Tasks;
using RelayStation.Core.Time;
using Xunit;
using GridMap = RelayStation.Core.Map.GridMap;

namespace RelayStation.Tests;

/*****
Date: 2026-09-26
Name: ConsumeAndEquipTaskTests
Description: 物品消耗与装备穿脱的单元/集成测试；覆盖——需求恢复写入口（NeedSystem.Restore）、取用任务（ConsumeTask）的两种模式（就地消耗背包 / 前往地面逐件取用后消耗）与需求驱动的分钟扫描（背包优先、同堆分散、无物品不提交、两档优先级、不重复提交）、穿戴任务（EquipTask）的背包与地面两条路径（含旧装备改走落地）、卸下任务（UnequipTask）的回包与落地回退，以及装备效果（工作效率 / 移动速度）与背包容量查询（MaxAddable）。
*****/
public sealed class ConsumeAndEquipTaskTests
{
    /*****
    Date: 2026-09-26
    Name: CreateFloorMap
    Description: 创建全地板地图。
    *****/
    private static GridMap CreateFloorMap(int width, int height)
    {
        var map = new GridMap(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                map.SetCell(new Vector2I(x, y), CellKind.Floor);
            }
        }
        return map;
    }

    /*****
    Date: 2026-09-26
    Name: CreateSimulation
    Description: 创建带共享占地集与阻塞寻路的最小模拟（无角色/设施）。
    *****/
    private static Simulation CreateSimulation(GridMap map)
    {
        var bus = new EventBus();
        var blocked = new HashSet<Vector2I>();
        return new Simulation(
            new GameClock(), map, new GridPathfinding(map, blocked.Contains),
            new TaskBoard(bus), new NeedSystem(), bus, blocked);
    }

    /*****
    Date: 2026-09-26
    Name: Advanced
    Description: 以 0.1 游戏分钟为步长推进指定步数（1 现实秒 = 1 游戏分钟且倍速 1，故 0.1 步 = 0.1 游戏分钟）。
    *****/
    private static void Advanced(Simulation simulation, int steps)
    {
        for (int i = 0; i < steps; i++) simulation.Update(0.1);
    }

    /*****
    Date: 2026-09-26
    Name: AdvanceUntilWorking
    Description: 推进模拟直到指定角色进入作业状态（上限保护）。
    *****/
    private static void AdvanceUntilWorking(Simulation simulation, CharacterSim character)
    {
        int guard = 0;
        while (character.State != CharacterState.Working && guard++ < 20000)
        {
            simulation.Update(0.1);
        }
        Assert.Equal(CharacterState.Working, character.State);
    }

    /*****
    Date: 2026-09-26
    Name: Restore_RaisesSingleNeedAndRaisesEvent
    Description: NeedSystem.Restore：只提升指定需求、封顶 100、触发 NeedsChanged；amount ≤ 0 时无操作也不触发事件。
    *****/
    [Fact]
    public void Restore_RaisesSingleNeedAndRaisesEvent()
    {
        var needs = new NeedSystem();
        var character = new CharacterSim(null, new Vector2I(0, 0));
        var events = new List<NeedSnapshot>();
        needs.NeedsChanged += (_, snapshot) => events.Add(snapshot);
        needs.Set(character, NeedSnapshot.Full.Set(NeedId.Food, 50f));

        needs.Restore(character, NeedId.Food, 40f);
        Assert.Equal(90f, needs.Read(character).Food, 3);
        Assert.Equal(NeedSnapshot.Max, needs.Read(character).Water, 3); // 其它需求不受影响
        Assert.Single(events);

        needs.Restore(character, NeedId.Food, 40f); // 封顶
        Assert.Equal(NeedSnapshot.Max, needs.Read(character).Food, 3);
        Assert.Equal(2, events.Count);

        needs.Restore(character, NeedId.Food, 0f); // 无操作、不触发
        Assert.Equal(2, events.Count);
    }

    /*****
    Date: 2026-09-26
    Name: AutoConsume_PrefersBagAndConsumesInPlace
    Description: 需求驱动自动取用（背包优先）：进食低于阈值且背包有存粮 → 生成**就地**取用任务（无物品目标），耗时后扣 1 件并恢复进食。
    *****/
    [Fact]
    public void AutoConsume_PrefersBagAndConsumesInPlace()
    {
        GridMap map = CreateFloorMap(8, 8);
        Simulation simulation = CreateSimulation(map);
        var character = new CharacterSim(null, new Vector2I(3, 3));
        simulation.AddCharacter(character);
        var ration = new FakeItemDef("ration", 20, "压缩干粮", "", weightKg: 0.4f,
            consumableKind: ConsumableKind.Food, needRestoreAmount: 40f, consumeGameMinutes: 10.0);
        simulation.Consumables.Register(ration);
        simulation.NeedSystem.Set(character, NeedSnapshot.Full.Set(NeedId.Food, 20f));
        Assert.True(simulation.TryAddItemToInventory(character, ration, 2));

        simulation.Update(1.0); // 触发分钟事件 → 自动取用

        ConsumeTask task = Assert.Single(simulation.TaskBoard.Active.OfType<ConsumeTask>());
        Assert.Null(task.ItemTarget);                  // 背包优先 → 就地
        Assert.Equal(CharacterState.Working, character.State);

        Advanced(simulation, 110);                     // 消耗耗时 10 分钟
        Assert.Equal(TaskState.Done, task.State);
        Assert.Equal(1, character.Inventory.CountOf(ration));
        Assert.Equal(59.8f, simulation.NeedSystem.Read(character).Food, 1); // 20 − 衰减 + 40
    }

    /*****
    Date: 2026-09-26
    Name: AutoConsume_TravelsToGroundStackAndTransfersBatch
    Description: 需求驱动自动取用（无存粮）：走到最近的可达堆 → **逐件**拾入背包（单趟上限 3 件）→ 消耗 1 件 → 进食恢复；背包余 2 件、地面减 3 件。
    *****/
    [Fact]
    public void AutoConsume_TravelsToGroundStackAndTransfersBatch()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);
        var ration = new FakeItemDef("ration", 20, "压缩干粮", "", weightKg: 0.4f,
            consumableKind: ConsumableKind.Food, needRestoreAmount: 40f, consumeGameMinutes: 2.0);
        simulation.Consumables.Register(ration);
        simulation.NeedSystem.Set(character, NeedSnapshot.Full.Set(NeedId.Food, 25f));
        ItemStack stack = simulation.Items.Add(ration, 5, new Vector2I(2, 0))[0];

        simulation.Update(1.0);

        ConsumeTask task = Assert.Single(simulation.TaskBoard.Active.OfType<ConsumeTask>());
        Assert.Same(stack, task.ItemTarget);

        AdvanceUntilWorking(simulation, character);    // 走到物品格
        Advanced(simulation, 60);                      // 3 件 × 0.5 分钟拾入 + 2 分钟消耗

        Assert.Equal(TaskState.Done, task.State);
        Assert.Equal(2, character.Inventory.CountOf(ration)); // 3 件里吃掉 1 件
        Assert.Equal(2, stack.Count);
        Assert.True(simulation.NeedSystem.Read(character).Food > 60f);
    }

    /*****
    Date: 2026-09-26
    Name: AutoConsume_NoReachableItem_SubmitsNothing
    Description: 无**已注册**可消耗物品或场上无可达对应物品时不提交任务（下一分钟再评估）；连续多分钟也不产生重复任务。
    *****/
    [Fact]
    public void AutoConsume_NoReachableItem_SubmitsNothing()
    {
        GridMap map = CreateFloorMap(8, 8);
        Simulation simulation = CreateSimulation(map);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);
        simulation.NeedSystem.Set(character, NeedSnapshot.Full.Set(NeedId.Food, 5f));

        Write(3); // 未注册任何可消耗物品
        Assert.Empty(simulation.TaskBoard.Active);

        var ration = new FakeItemDef("ration", 20, consumableKind: ConsumableKind.Food, needRestoreAmount: 40f);
        simulation.Consumables.Register(ration);
        Write(3); // 已注册但场上没有对应物品
        Assert.Empty(simulation.TaskBoard.Active);

        void Write(int minutes)
        {
            for (int i = 0; i < minutes; i++) simulation.Update(1.0);
        }
    }

    /*****
    Date: 2026-09-26
    Name: AutoConsume_UsesPriorityTiersAndDoesNotDuplicate
    Description: 两档优先级（<30 → P8、<10 → Urgent）与「已有未结任务不重复提交」。
    *****/
    [Fact]
    public void AutoConsume_UsesPriorityTiersAndDoesNotDuplicate()
    {
        GridMap map = CreateFloorMap(8, 8);
        Simulation simulation = CreateSimulation(map);
        var character = new CharacterSim(null, new Vector2I(3, 3));
        simulation.AddCharacter(character);
        var ration = new FakeItemDef("ration", 20, consumableKind: ConsumableKind.Food,
            needRestoreAmount: 40f, consumeGameMinutes: 30.0);
        simulation.Consumables.Register(ration);
        Assert.True(simulation.TryAddItemToInventory(character, ration, 3));

        simulation.NeedSystem.Set(character, NeedSnapshot.Full.Set(NeedId.Food, 20f));
        simulation.Update(1.0);
        ConsumeTask mild = Assert.Single(simulation.TaskBoard.Active.OfType<ConsumeTask>());
        Assert.Equal(TaskPriority.P8, mild.Priority);

        simulation.Update(1.0); // 已有未结任务 → 不重复提交
        simulation.Update(1.0);
        Assert.Single(simulation.TaskBoard.Active.OfType<ConsumeTask>());

        // 危急（< 10）→ Urgent：另起一名角色（其自身没有未结任务）
        var critical = new CharacterSim(null, new Vector2I(3, 3));
        simulation.AddCharacter(critical);
        Assert.True(simulation.TryAddItemToInventory(critical, ration, 1));
        simulation.NeedSystem.Set(critical, NeedSnapshot.Full.Set(NeedId.Food, 5f));
        simulation.Update(1.0);

        ConsumeTask urgent = Assert.Single(
            simulation.TaskBoard.Active.OfType<ConsumeTask>(),
            t => ReferenceEquals(t.Owner, critical));
        Assert.Equal(TaskPriority.Urgent, urgent.Priority);
    }

    /*****
    Date: 2026-09-26
    Name: EquipTask_FromBag_WearsAfterDuration
    Description: 穿戴（背包路径）：就地耗时 5 游戏分钟后生效——5 分钟前未穿、之后入槽且背包不再持有。
    *****/
    [Fact]
    public void EquipTask_FromBag_WearsAfterDuration()
    {
        GridMap map = CreateFloorMap(8, 8);
        Simulation simulation = CreateSimulation(map);
        var character = new CharacterSim(null, new Vector2I(3, 3));
        simulation.AddCharacter(character);
        var helmet = new FakeItemDef("helmet", 1, weightKg: 0.8f, equipSlot: EquipmentSlot.Head);
        Assert.True(simulation.TryAddItemToInventory(character, helmet, 1));

        Assert.True(simulation.OrderEquip(character, helmet));
        Assert.Equal(CharacterState.Working, character.State); // 就地：当帧开工

        Advanced(simulation, 45);
        Assert.Null(character.Inventory.GetEquipped(EquipmentSlot.Head)); // 4.5 分钟：尚未生效
        Advanced(simulation, 8);                                          // 累计 > 5 分钟
        Assert.Same(helmet, character.Inventory.GetEquipped(EquipmentSlot.Head));
        Assert.Equal(0, character.Inventory.CountOf(helmet));
    }

    /*****
    Date: 2026-09-26
    Name: EquipTask_FromGround_TakesOffAndWearsWithoutBag
    Description: 穿戴（地面路径）：走到物品格 → 耗时后「地面扣 1 件 + 直接入槽」，不占背包。
    *****/
    [Fact]
    public void EquipTask_FromGround_TakesOffAndWearsWithoutBag()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);
        var boots = new FakeItemDef("boots", 1, weightKg: 0.6f, equipSlot: EquipmentSlot.Feet);
        ItemStack stack = simulation.Items.Add(boots, 1, new Vector2I(5, 0))[0];

        Assert.True(simulation.OrderWearFromGround(character, stack));
        Advanced(simulation, 80); // 走 5 格（约 1.67 分钟）+ 穿戴 5 分钟

        Assert.Same(boots, character.Inventory.GetEquipped(EquipmentSlot.Feet));
        Assert.Equal(0, character.Inventory.CountOf(boots)); // 不占背包
        Assert.Equal(0, stack.Count);                        // 地面已扣空（注册表回收该堆）
        Assert.Empty(simulation.Items.Stacks);
    }

    /*****
    Date: 2026-09-26
    Name: EquipTask_SwapOldToGroundWhenBagIsFull
    Description: 穿戴（地面路径）遇到「旧装备回不了背包」：旧装备就地落地、新装备入槽，背包重量不变、物品不丢。
    *****/
    [Fact]
    public void EquipTask_SwapOldToGroundWhenBagIsFull()
    {
        GridMap map = CreateFloorMap(8, 8);
        Simulation simulation = CreateSimulation(map);
        var character = new CharacterSim(null, new Vector2I(3, 3));
        simulation.AddCharacter(character);
        var oldBoots = new FakeItemDef("boots_old", 1, weightKg: 0.6f, equipSlot: EquipmentSlot.Feet);
        var newBoots = new FakeItemDef("boots_new", 1, weightKg: 0.6f, equipSlot: EquipmentSlot.Feet);
        var filler = new FakeItemDef("filler", 100, weightKg: 4.8f);
        var scrap = new FakeItemDef("scrap", 100, weightKg: 0.3f);

        Assert.True(simulation.TryAddItemToInventory(character, filler, 3));  // 14.4kg
        Assert.True(simulation.TryAddItemToInventory(character, oldBoots, 1)); // 15.0kg（吃满基础负重）
        Assert.True(simulation.TryEquipItem(character, oldBoots));             // 背包回落到 14.4kg
        Assert.True(simulation.TryAddItemToInventory(character, scrap, 2));    // 再补到 15.0kg → 旧靴回不了包

        ItemStack stack = simulation.Items.Add(newBoots, 1, new Vector2I(3, 3))[0];
        Assert.True(simulation.OrderWearFromGround(character, stack));
        Advanced(simulation, 60);

        Assert.Same(newBoots, character.Inventory.GetEquipped(EquipmentSlot.Feet));
        Assert.Equal(15f, character.Inventory.TotalWeightKg, 2);
        Assert.Contains(simulation.Items.Stacks, s => ReferenceEquals(s.Def, oldBoots)); // 旧靴落地
        Assert.DoesNotContain(simulation.Items.Stacks, s => ReferenceEquals(s.Def, newBoots));
    }

    /*****
    Date: 2026-09-26
    Name: UnequipTask_StowsToBagOrDropsWhenFull
    Description: 卸下：正常时收回背包；背包放不下（超重）时就地落地；两种情况下槽位都清空、物品都不丢。
    *****/
    [Fact]
    public void UnequipTask_StowsToBagOrDropsWhenFull()
    {
        GridMap map = CreateFloorMap(8, 8);
        Simulation simulation = CreateSimulation(map);
        var character = new CharacterSim(null, new Vector2I(3, 3));
        simulation.AddCharacter(character);
        var helmet = new FakeItemDef("helmet", 1, weightKg: 0.8f, equipSlot: EquipmentSlot.Head);
        var filler = new FakeItemDef("filler", 100, weightKg: 4.8f);

        // 回包路径
        Assert.True(simulation.TryAddItemToInventory(character, helmet, 1));
        Assert.True(simulation.TryEquipItem(character, helmet));
        Assert.True(simulation.OrderUnequip(character, EquipmentSlot.Head));
        Advanced(simulation, 60);
        Assert.Null(character.Inventory.GetEquipped(EquipmentSlot.Head));
        Assert.Equal(1, character.Inventory.CountOf(helmet));

        // 落地路径：重新穿上并把背包吃满（15.0kg）后卸下 → 超重 → 就地落地
        Assert.True(simulation.TryEquipItem(character, helmet));
        Assert.True(simulation.TryAddItemToInventory(character, filler, 3)); // 14.4kg
        var scrap = new FakeItemDef("scrap", 100, weightKg: 0.6f);
        Assert.True(simulation.TryAddItemToInventory(character, scrap, 1));  // 15.0kg（卸下即超重）

        Assert.True(simulation.OrderUnequip(character, EquipmentSlot.Head));
        Advanced(simulation, 60);

        Assert.Null(character.Inventory.GetEquipped(EquipmentSlot.Head));
        Assert.Equal(0, character.Inventory.CountOf(helmet));                      // 没进背包
        Assert.Contains(simulation.Items.Stacks, s => ReferenceEquals(s.Def, helmet)); // 落地
    }

    /*****
    Date: 2026-09-26
    Name: EquipmentWorkBonus_SpeedsUpTasks
    Description: 装备效率加成生效：戴工作手套（+10%）后就地穿戴同一件装备只需约 1/1.1 的时间（45 步未完成、46 步完成；无加成则需 50 步）。
    *****/
    [Fact]
    public void EquipmentWorkBonus_SpeedsUpTasks()
    {
        GridMap map = CreateFloorMap(8, 8);
        Simulation simulation = CreateSimulation(map);
        var character = new CharacterSim(null, new Vector2I(3, 3));
        simulation.AddCharacter(character);
        var gloves = new FakeItemDef("gloves", 1, weightKg: 0.2f, equipSlot: EquipmentSlot.Hands,
            workSpeedBonusPercent: 0.1f);
        var helmet = new FakeItemDef("helmet", 1, weightKg: 0.8f, equipSlot: EquipmentSlot.Head);
        Assert.True(simulation.TryAddItemToInventory(character, gloves, 1));
        Assert.True(simulation.TryEquipItem(character, gloves));
        Assert.True(simulation.TryAddItemToInventory(character, helmet, 1));
        Assert.Equal(0.1f, character.Inventory.WorkSpeedBonusPercent, 3);

        Assert.True(simulation.OrderEquip(character, helmet));

        Advanced(simulation, 45); // 45 × 0.1 × 1.1 = 4.95 游戏分钟
        Assert.Null(character.Inventory.GetEquipped(EquipmentSlot.Head));
        Advanced(simulation, 1);  // 累计 5.06 → 完成
        Assert.Same(helmet, character.Inventory.GetEquipped(EquipmentSlot.Head));
    }

    /*****
    Date: 2026-09-26
    Name: EquipmentMoveBonus_SpeedsUpMovement
    Description: 装备移速加成生效：穿磁力靴（+20%）后 6 格路程在第 17 步抵达（每步 0.36 格），无加成则需 20 步。
    *****/
    [Fact]
    public void EquipmentMoveBonus_SpeedsUpMovement()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);
        var boots = new FakeItemDef("boots", 1, weightKg: 0.6f, equipSlot: EquipmentSlot.Feet,
            moveSpeedBonusPercent: 0.2f);
        Assert.True(simulation.TryAddItemToInventory(character, boots, 1));
        Assert.True(simulation.TryEquipItem(character, boots));
        Assert.Equal(0.2f, character.Inventory.MoveSpeedBonusPercent, 3);

        Assert.True(simulation.OrderMove(character, new Vector2I(6, 0)));
        Advanced(simulation, 16); // 16 × 0.3 × 1.2 = 5.76 格
        Assert.Equal(new Vector2I(5, 0), character.Cell);
        Advanced(simulation, 1);  // 6.12 格 → 抵达
        Assert.Equal(new Vector2I(6, 0), character.Cell);
        Assert.Equal(CharacterState.Idle, character.State);
    }

    /*****
    Date: 2026-09-26
    Name: Inventory_MaxAddableAndDirectWear
    Description: 背包新查询/写入：`MaxAddable` 按剩余容量 ÷ 单位重量向下取整（0 重量物品不受限）、`TryWearDirect` 直接穿戴不在背包内的物品（同槽旧装备回包，回不去则拒绝）、`TakeEquipped` 无校验脱下。
    *****/
    [Fact]
    public void Inventory_MaxAddableAndDirectWear()
    {
        var inventory = new CharacterInventory(CharacterDef.DefaultBaseCarryCapacityKg);
        var heavy = new FakeItemDef("heavy", 100, weightKg: 4f);
        var free = new FakeItemDef("free", 100);
        Assert.Equal(3, inventory.MaxAddable(heavy));      // 15 / 4 = 3
        Assert.Equal(int.MaxValue, inventory.MaxAddable(free));

        Assert.True(inventory.TryAdd(heavy, 3));           // 12kg
        Assert.Equal(0, inventory.MaxAddable(heavy));      // 余 3kg 装不下 4kg 的

        var oldHelmet = new FakeItemDef("helmet_old", 1, weightKg: 0.5f, equipSlot: EquipmentSlot.Head);
        Assert.True(inventory.TryAdd(oldHelmet, 1));       // 12.5kg
        Assert.True(inventory.TryEquip(oldHelmet));        // 12kg
        var newHelmet = new FakeItemDef("helmet_new", 1, weightKg: 3.5f, equipSlot: EquipmentSlot.Head);
        Assert.True(inventory.TryWearDirect(newHelmet));   // 12 + 0.5 = 12.5 ≤ 15 → 旧头盔回包
        Assert.Same(newHelmet, inventory.GetEquipped(EquipmentSlot.Head));
        Assert.Equal(1, inventory.CountOf(oldHelmet));

        var taken = inventory.TakeEquipped(EquipmentSlot.Head);
        Assert.Same(newHelmet, taken);
        Assert.Null(inventory.GetEquipped(EquipmentSlot.Head));
    }
}