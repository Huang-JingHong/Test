using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Events;
using RelayStation.Core.Facilities;
using RelayStation.Core.Items;
using RelayStation.Core.Map;
using RelayStation.Core.Tasks;
using RelayStation.Core.Time;
using Xunit;
using GridMap = RelayStation.Core.Map.GridMap;

namespace RelayStation.Tests;

/*****
Date: 2026-09-26
Name: ItemTaskFrameworkTests
Description: 物品类任务框架与拾取任务的单元/集成测试；覆盖——任务板三元组（Owner + 设施目标 + 物品目标）去重、个人任务只有归属者可认领、「人物中止」对个人任务=取消而对公共任务=忽略、以及拾取任务（PickupTask）的**逐件转移**节奏、动态终点（容量耗尽 / 堆空）、即时预检回调（装不下 / 已被取走 / 不可达）与「取消无回滚」（已转移保留、未转移留地面）。
*****/
public sealed class ItemTaskFrameworkTests
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
    Name: PickupTask_TransfersOneItemPerPeriod
    Description: 逐件转移节奏：抵达后每 0.5 游戏分钟恰转移 1 件（推进 0.5 → 1 件；再推进 1.5 → 共 4 件）；进度比例为「已转移 / 计划」。
    *****/
    [Fact]
    public void PickupTask_TransfersOneItemPerPeriod()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);
        var def = new FakeItemDef("parts", 100, weightKg: 0.5f);
        ItemStack stack = simulation.Items.Add(def, 10, new Vector2I(2, 0))[0];

        Assert.True(simulation.OrderPickup(character, stack));
        AdvanceUntilWorking(simulation, character); // 走到物品格（2 格）
        Assert.Equal(0, character.Inventory.CountOf(def)); // 抵达当帧尚未转移

        simulation.Update(0.5); // 恰好一个单件周期
        Assert.Equal(1, character.Inventory.CountOf(def));
        Assert.Equal(9, stack.Count);

        simulation.Update(1.5); // 再 3 个周期
        Assert.Equal(4, character.Inventory.CountOf(def));
        Assert.Equal(6, stack.Count);

        PickupTask task = Assert.Single(simulation.TaskBoard.Active.OfType<PickupTask>());
        Assert.Equal(10, task.PlannedCount);            // 计划 = min（堆 10，可装下 30）
        Assert.Equal(4, task.TransferredCount);
        Assert.Equal(0.4, task.ProgressFraction, 3);
        Assert.Equal(TaskState.InProgress, task.State);
    }

    /*****
    Date: 2026-09-26
    Name: PickupTask_StopsWhenCapacityRunsOut
    Description: 动态终点（容量耗尽）：基础负重 15kg、单件 5kg → 装满 3 件即提前收尾（按正常完成处理），剩余件数留在地面，回调 `Transferred`。
    *****/
    [Fact]
    public void PickupTask_StopsWhenCapacityRunsOut()
    {
        GridMap map = CreateFloorMap(8, 8);
        Simulation simulation = CreateSimulation(map);
        var character = new CharacterSim(null, new Vector2I(3, 3));
        simulation.AddCharacter(character);
        var def = new FakeItemDef("heavy", 100, weightKg: 5f);
        ItemStack stack = simulation.Items.Add(def, 10, new Vector2I(3, 3))[0]; // 与角色同格 → 就地转移

        var outcomes = new List<PickupOutcome>();
        Assert.True(simulation.OrderPickup(character, stack, outcomes.Add));
        AdvanceUntilWorking(simulation, character);
        PickupTask task = Assert.Single(simulation.TaskBoard.Active.OfType<PickupTask>());

        Advanced(simulation, 40); // 4 游戏分钟（远超 3 件所需的 1.5 分钟）
        Assert.Equal(3, character.Inventory.CountOf(def));
        Assert.Equal(7, stack.Count);
        Assert.Equal(PickupOutcome.Transferred, Assert.Single(outcomes));
        Assert.Equal(TaskState.Done, task.State); // 完成后任务离开未结集合
    }

    /*****
    Date: 2026-09-26
    Name: PickupTask_PrecheckReportsNoCapacity
    Description: 即时预检（一件都装不下）：单件 20kg 超过基础负重 15kg → 不下达任务、回调 `NoCapacity`、物品原样留在地面。
    *****/
    [Fact]
    public void PickupTask_PrecheckReportsNoCapacity()
    {
        GridMap map = CreateFloorMap(8, 8);
        Simulation simulation = CreateSimulation(map);
        var character = new CharacterSim(null, new Vector2I(3, 3));
        simulation.AddCharacter(character);
        var def = new FakeItemDef("oversized", 100, weightKg: 20f);
        ItemStack stack = simulation.Items.Add(def, 2, new Vector2I(3, 3))[0];

        var outcomes = new List<PickupOutcome>();
        Assert.False(simulation.OrderPickup(character, stack, outcomes.Add));

        Assert.Equal(PickupOutcome.NoCapacity, Assert.Single(outcomes));
        Assert.Equal(2, stack.Count);
        Assert.Empty(simulation.TaskBoard.Active);
    }

    /*****
    Date: 2026-09-26
    Name: PickupTask_ReportsGoneWhenStackTakenBeforeArrival
    Description: 目标在途中被取空：任务以 `Gone` 收尾且一件都没拿到。
    *****/
    [Fact]
    public void PickupTask_ReportsGoneWhenStackTakenBeforeArrival()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);
        var def = new FakeItemDef("parts", 100, weightKg: 0.5f);
        ItemStack stack = simulation.Items.Add(def, 3, new Vector2I(6, 0))[0];

        var outcomes = new List<PickupOutcome>();
        Assert.True(simulation.OrderPickup(character, stack, outcomes.Add));
        simulation.Items.Remove(stack, 3); // 途中被他人取空

        Advanced(simulation, 60);
        Assert.Equal(0, character.Inventory.CountOf(def));
        Assert.Equal(PickupOutcome.Gone, Assert.Single(outcomes));
    }

    /*****
    Date: 2026-09-26
    Name: PickupTask_CancelKeepsTransferredItems
    Description: 取消无回滚：转移 2 件后「人物中止」（个人任务 = 直接取消）——已转移的 2 件留在背包、未转移的 8 件留在地面。
    *****/
    [Fact]
    public void PickupTask_CancelKeepsTransferredItems()
    {
        GridMap map = CreateFloorMap(8, 8);
        Simulation simulation = CreateSimulation(map);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);
        var def = new FakeItemDef("parts", 100, weightKg: 0.5f);
        ItemStack stack = simulation.Items.Add(def, 10, new Vector2I(0, 0))[0];

        Assert.True(simulation.OrderPickup(character, stack));
        AdvanceUntilWorking(simulation, character);
        simulation.Update(1.1); // 2 件（0.5×2）+ 余数 0.1
        Assert.Equal(2, character.Inventory.CountOf(def));

        PickupTask task = Assert.Single(simulation.TaskBoard.Active.OfType<PickupTask>());
        simulation.AbortCharacterWork(character, ignoreCurrent: true);

        Assert.Equal(TaskState.Cancelled, task.State);
        Assert.Equal(2, character.Inventory.CountOf(def));
        Assert.Equal(8, stack.Count);
        Assert.Null(character.CurrentTask);
    }

    /*****
    Date: 2026-09-26
    Name: TaskBoard_DedupesByOwnerFacilityAndItemTarget
    Description: 任务板去重键为「Owner + 设施目标 + 物品目标」三元组：同人同物品目标只留一条（后提交者被忽略）；不同人可各自针对同一堆提交（两名角色可各自去同一堆取用）。
    *****/
    [Fact]
    public void TaskBoard_DedupesByOwnerFacilityAndItemTarget()
    {
        var board = new TaskBoard(new EventBus());
        var needs = new NeedSystem();
        var a = new CharacterSim(null, new Vector2I(0, 0));
        var b = new CharacterSim(null, new Vector2I(1, 0));
        var registry = new ItemRegistry();
        var def = new FakeItemDef("ration", 20, consumableKind: ConsumableKind.Food);
        ItemStack stack = registry.Add(def, 5, new Vector2I(2, 2))[0];

        var first = new ConsumeTask(needs, a, def, stack, registry);
        var duplicate = new ConsumeTask(needs, a, def, stack, registry);
        var other = new ConsumeTask(needs, b, def, stack, registry);
        board.Submit(first);
        board.Submit(duplicate);
        board.Submit(other);

        Assert.Equal(2, board.Active.Count);
        Assert.Contains(first, board.Active);
        Assert.Contains(other, board.Active);
        Assert.DoesNotContain(duplicate, board.Active);
    }

    /*****
    Date: 2026-09-26
    Name: PersonalTask_IsClaimedOnlyByOwner
    Description: 个人任务只有归属者可认领：另一名更早登场的空闲角色不会被派工接手。
    *****/
    [Fact]
    public void PersonalTask_IsClaimedOnlyByOwner()
    {
        GridMap map = CreateFloorMap(8, 8);
        Simulation simulation = CreateSimulation(map);
        var other = new CharacterSim(null, new Vector2I(1, 0));
        var owner = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(other);
        simulation.AddCharacter(owner);
        var def = new FakeItemDef("parts", 100, weightKg: 0.5f);
        ItemStack stack = simulation.Items.Add(def, 5, new Vector2I(4, 4))[0];

        simulation.TaskBoard.Submit(new PickupTask(owner, new GroundItemSource(stack, simulation.Items))); // 提交即触发空闲派工

        Assert.Null(other.CurrentTask);
        Assert.Equal(CharacterState.Idle, other.State);
        Assert.Equal(CharacterState.Moving, owner.State);
    }

    /*****
    Date: 2026-09-26
    Name: Abort_CancelsPersonalTaskAndIgnoresPublicTask
    Description: 「人物中止」按归属分流——个人任务（拾取）直接取消（个人指令无法转交他人）；公共任务（修理）留在板上（非终态、仍可被他人接手）并列入本人的忽略清单。
    *****/
    [Fact]
    public void Abort_CancelsPersonalTaskAndIgnoresPublicTask()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);
        var registry = simulation.Items;
        var def = new FakeItemDef("parts", 100, weightKg: 0.5f);

        // 个人任务：中止 = 取消
        ItemStack stack = registry.Add(def, 5, new Vector2I(2, 0))[0];
        Assert.True(simulation.OrderPickup(character, stack));
        PickupTask pickup = Assert.Single(simulation.TaskBoard.Active.OfType<PickupTask>());
        simulation.AbortCharacterWork(character, ignoreCurrent: true);
        Assert.Equal(TaskState.Cancelled, pickup.State);
        Assert.Empty(simulation.TaskBoard.Active);

        // 公共任务：中止 = 忽略（任务留在板上，仍可被他人接手）
        var facility = new FacilitySim(null, new Vector2I(8, 8));
        simulation.AddFacility(facility);
        var repair = new RepairTask(facility, 30, 0, null, maxWorkers: 1);
        simulation.TaskBoard.Submit(repair);
        AdvanceUntilWorking(simulation, character);
        simulation.AbortCharacterWork(character, ignoreCurrent: true);

        Assert.Contains(repair, simulation.TaskBoard.Active);
        Assert.NotEqual(TaskState.Cancelled, repair.State);
        Assert.True(character.IsIgnoring(repair));
        Assert.Null(character.CurrentTask);
    }
}