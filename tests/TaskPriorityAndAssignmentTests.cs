using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Events;
using RelayStation.Core.Facilities;
using RelayStation.Core.Map;
using RelayStation.Core.Tasks;
using RelayStation.Core.Time;
using Xunit;
using GridMap = RelayStation.Core.Map.GridMap;

namespace RelayStation.Tests;

/*****
Date: 2026-09-25
Name: TaskPriorityAndAssignmentTests
Description: 任务优先级与即时派工测试；覆盖「任务发布当帧投入全部空闲人力（含加入 Assigned 状态任务）」、「优先级高者先被认领、同优先级按提交先后」、「完工当帧接续下一任务」，以及地图放大后的尺寸与连通性。
*****/
public sealed class TaskPriorityAndAssignmentTests
{
    /*****
    Date: 2026-09-25
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
    Date: 2026-09-25
    Name: CreateSimulation
    Description: 创建带共享占地集与阻塞寻路的最小模拟（无额外角色/设施）。
    *****/
    private static Simulation CreateSimulation(GridMap map, IEventBus bus)
    {
        var blocked = new HashSet<Vector2I>();
        return new Simulation(
            new GameClock(), map, new GridPathfinding(map, blocked.Contains),
            new TaskBoard(bus), new NeedSystem(), bus, blocked);
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
    Date: 2026-09-25
    Name: Submit_WithIdleWorkers_AssignsAllUpToMaxWorkersImmediately
    Description: 任务提交当帧即投入全部空闲人力：三名空闲角色在 Submit 同步调用后全部加入同一任务（此时任务仍为 Assigned，首人尚未抵达），验证「发布任务即刻使用全部空闲人手」。
    *****/
    [Fact]
    public void Submit_WithIdleWorkers_AssignsAllUpToMaxWorkersImmediately()
    {
        GridMap map = CreateFloorMap(8, 8);
        var bus = new EventBus();
        Simulation simulation = CreateSimulation(map, bus);

        var facility = new FacilitySim(null, new Vector2I(4, 0)); // 占地 (4,0)-(5,1)
        simulation.AddFacility(facility);

        var first = new CharacterSim(null, new Vector2I(0, 0));
        var second = new CharacterSim(null, new Vector2I(0, 1));
        var third = new CharacterSim(null, new Vector2I(0, 2));
        simulation.AddCharacter(first);
        simulation.AddCharacter(second);
        simulation.AddCharacter(third);

        var task = new RepairTask(facility, 30, 0, null, maxWorkers: 3);
        simulation.TaskBoard.Submit(task);

        // 无需推进任何帧：提交事件同步触发空闲人力派工
        Assert.Equal(TaskState.Assigned, task.State);
        Assert.Equal(3, task.Workers.Count);
        Assert.Same(task, first.CurrentTask);
        Assert.Same(task, second.CurrentTask);
        Assert.Same(task, third.CurrentTask);
        Assert.Equal(CharacterState.Moving, first.State);
        Assert.Equal(CharacterState.Moving, second.State);
        Assert.Equal(CharacterState.Moving, third.State);
    }

    /*****
    Date: 2026-09-25
    Name: Submit_RespectsMaxWorkersWhenIdleWorkersExceedCapacity
    Description: 空闲人力多于人力上限时按上限投入，多余者保持待机。
    *****/
    [Fact]
    public void Submit_RespectsMaxWorkersWhenIdleWorkersExceedCapacity()
    {
        GridMap map = CreateFloorMap(8, 8);
        var bus = new EventBus();
        Simulation simulation = CreateSimulation(map, bus);

        var facility = new FacilitySim(null, new Vector2I(4, 0));
        simulation.AddFacility(facility);

        var a = new CharacterSim(null, new Vector2I(0, 0));
        var b = new CharacterSim(null, new Vector2I(0, 1));
        var c = new CharacterSim(null, new Vector2I(0, 2));
        simulation.AddCharacter(a);
        simulation.AddCharacter(b);
        simulation.AddCharacter(c);

        var task = new RepairTask(facility, 30, 0, null, maxWorkers: 2);
        simulation.TaskBoard.Submit(task);

        Assert.Equal(2, task.Workers.Count);
        Assert.Equal(1, new[] { a, b, c }.Count(ch => ch.State == CharacterState.Idle));
    }

    /*****
    Date: 2026-09-25
    Name: PickFor_PrefersHigherPriority
    Description: 认领时优先级数值高者胜出（与提交先后无关）：先提交 1 档、后提交紧急任务，角色认领紧急任务。
    *****/
    [Fact]
    public void PickFor_PrefersHigherPriority()
    {
        var board = new TaskBoard(new EventBus());
        var lowFacility = new FacilitySim(null, new Vector2I(2, 2));
        var urgentFacility = new FacilitySim(null, new Vector2I(6, 6));

        var lowTask = new RepairTask(lowFacility, 30, 0, null, 1, TaskPriority.P1);
        var urgentTask = new RepairTask(urgentFacility, 30, 0, null, 1, TaskPriority.Urgent);
        board.Submit(lowTask);
        board.Submit(urgentTask);

        var character = new CharacterSim(null, new Vector2I(0, 0));
        ITask? picked = board.PickFor(character);

        Assert.Same(urgentTask, picked);
        Assert.Same(urgentTask, character.CurrentTask);
    }

    /*****
    Date: 2026-09-26
    Name: PickFor_NumericPriority_OrdersByValue
    Description: 数字档优先级按数值排序：同一角色面对 3 档与 7 档两个任务时取 7 档（数值越大越优先）。
    *****/
    [Fact]
    public void PickFor_NumericPriority_OrdersByValue()
    {
        var board = new TaskBoard(new EventBus());
        var lower = new RepairTask(new FacilitySim(null, new Vector2I(2, 2)), 30, 0, null, 1, TaskPriority.P3);
        var higher = new RepairTask(new FacilitySim(null, new Vector2I(6, 6)), 30, 0, null, 1, TaskPriority.P7);
        board.Submit(higher);
        board.Submit(lower);

        ITask? picked = board.PickFor(new CharacterSim(null, new Vector2I(0, 0)));

        Assert.Same(higher, picked);
    }

    /*****
    Date: 2026-09-25
    Name: PickFor_SamePriority_PrefersEarlierSubmitted
    Description: 同优先级按提交先后（先到先得）。
    *****/
    [Fact]
    public void PickFor_SamePriority_PrefersEarlierSubmitted()
    {
        var board = new TaskBoard(new EventBus());
        var earlierTask = new RepairTask(new FacilitySim(null, new Vector2I(2, 2)), 30);
        var laterTask = new RepairTask(new FacilitySim(null, new Vector2I(6, 6)), 30);
        board.Submit(earlierTask);
        board.Submit(laterTask);

        ITask? picked = board.PickFor(new CharacterSim(null, new Vector2I(0, 0)));

        Assert.Same(earlierTask, picked);
    }

    /*****
    Date: 2026-09-25
    Name: CompletingTask_ImmediatelyPicksNextAvailable
    Description: 完工当帧即接续下一件工作：角色完成首个任务的那一帧内已认领第二个任务并开始移动（不出现空转待机帧）。
    *****/
    [Fact]
    public void CompletingTask_ImmediatelyPicksNextAvailable()
    {
        GridMap map = CreateFloorMap(10, 10);
        var bus = new EventBus();
        Simulation simulation = CreateSimulation(map, bus);

        var nearFacility = new FacilitySim(null, new Vector2I(4, 0));
        var farFacility = new FacilitySim(null, new Vector2I(4, 6));
        simulation.AddFacility(nearFacility);
        simulation.AddFacility(farFacility);

        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);

        var firstTask = new RepairTask(nearFacility, 1);   // 1 游戏分钟，先提交先认领
        var nextTask = new RepairTask(farFacility, 10);
        simulation.TaskBoard.Submit(firstTask);
        simulation.TaskBoard.Submit(nextTask);

        Assert.Same(firstTask, character.CurrentTask); // 首个任务已被认领

        int guard = 0;
        while (firstTask.State != TaskState.Done && guard++ < 20000)
        {
            simulation.Update(0.1);
        }

        // 首任务完成的那一帧，角色应已接续下一任务（而非停留在 Idle 等待下一帧轮询）
        Assert.Equal(TaskState.Done, firstTask.State);
        Assert.Same(nextTask, character.CurrentTask);
        Assert.Equal(CharacterState.Moving, character.State);
    }

    /*****
    Date: 2026-09-26
    Name: Submit_HigherPriorityTask_PreemptsBusyWorker
    Description: 优先级抢占（对齐《缺氧》即时切换）：角色正在做 2 档任务、且已进入作业，此时提交 8 档任务——提交当帧角色即被改派到高优先任务；原任务被释放（未取消）并留在未结集合等待他人接手。
    *****/
    [Fact]
    public void Submit_HigherPriorityTask_PreemptsBusyWorker()
    {
        GridMap map = CreateFloorMap(12, 12);
        var bus = new EventBus();
        Simulation simulation = CreateSimulation(map, bus);
        var lowFacility = new FacilitySim(null, new Vector2I(6, 0));
        var highFacility = new FacilitySim(null, new Vector2I(6, 8));
        simulation.AddFacility(lowFacility);
        simulation.AddFacility(highFacility);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);

        var lowTask = new RepairTask(lowFacility, 30, 0, null, 1, TaskPriority.P2);
        simulation.TaskBoard.Submit(lowTask);
        AdvanceUntilWorking(simulation, character);
        Assert.Equal(TaskState.InProgress, lowTask.State);

        var highTask = new RepairTask(highFacility, 30, 0, null, 1, TaskPriority.P8);
        simulation.TaskBoard.Submit(highTask);

        Assert.Same(highTask, character.CurrentTask);
        Assert.Equal(CharacterState.Moving, character.State);
        Assert.Empty(lowTask.Workers);
        Assert.Contains(lowTask, simulation.TaskBoard.Active);
        Assert.NotEqual(TaskState.Cancelled, lowTask.State);
    }

    /*****
    Date: 2026-09-26
    Name: Submit_SamePriorityTask_DoesNotPreempt
    Description: 同优先级不抢占（沿用「先到先得」，避免无谓抖动）：角色继续做手上的任务。
    *****/
    [Fact]
    public void Submit_SamePriorityTask_DoesNotPreempt()
    {
        GridMap map = CreateFloorMap(12, 12);
        var bus = new EventBus();
        Simulation simulation = CreateSimulation(map, bus);
        var firstFacility = new FacilitySim(null, new Vector2I(6, 0));
        var secondFacility = new FacilitySim(null, new Vector2I(6, 8));
        simulation.AddFacility(firstFacility);
        simulation.AddFacility(secondFacility);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);

        var firstTask = new RepairTask(firstFacility, 30, 0, null, 1, TaskPriority.P5);
        simulation.TaskBoard.Submit(firstTask);
        AdvanceUntilWorking(simulation, character);

        simulation.TaskBoard.Submit(new RepairTask(secondFacility, 30, 0, null, 1, TaskPriority.P5));

        Assert.Same(firstTask, character.CurrentTask);
        Assert.Equal(CharacterState.Working, character.State);
    }

    /*****
    Date: 2026-09-26
    Name: Submit_UrgentTask_PreemptsNumericTask
    Description: 「紧急」抢占数字档任务：9 档进行中时提交紧急任务，角色立即改做紧急任务。
    *****/
    [Fact]
    public void Submit_UrgentTask_PreemptsNumericTask()
    {
        GridMap map = CreateFloorMap(12, 12);
        var bus = new EventBus();
        Simulation simulation = CreateSimulation(map, bus);
        var numericFacility = new FacilitySim(null, new Vector2I(6, 0));
        var urgentFacility = new FacilitySim(null, new Vector2I(6, 8));
        simulation.AddFacility(numericFacility);
        simulation.AddFacility(urgentFacility);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);

        var numericTask = new RepairTask(numericFacility, 30, 0, null, 1, TaskPriority.P9);
        simulation.TaskBoard.Submit(numericTask);
        AdvanceUntilWorking(simulation, character);

        var urgentTask = new RepairTask(urgentFacility, 30, 0, null, 1, TaskPriority.Urgent);
        simulation.TaskBoard.Submit(urgentTask);

        Assert.Same(urgentTask, character.CurrentTask);
        Assert.Empty(numericTask.Workers);
    }

    /*****
    Date: 2026-09-26
    Name: PeekFor_IsPureQuery
    Description: PeekFor 为纯查询：忙人查询返回「若腾出手来」可接手的最优任务（此处为另一件 2 档任务；自己在做的 7 档因本人已在列而不可接手），且不改任务状态、不写角色 CurrentTask——抢占判定正是拿它与当前任务优先级比较（2 ≤ 7 → 不抢占）。
    *****/
    [Fact]
    public void PeekFor_IsPureQuery()
    {
        var board = new TaskBoard(new EventBus());
        var otherFacility = new FacilitySim(null, new Vector2I(2, 2));
        var busyFacility = new FacilitySim(null, new Vector2I(6, 6));
        var otherTask = new RepairTask(otherFacility, 30, 0, null, 1, TaskPriority.P2);
        var busyTask = new RepairTask(busyFacility, 30, 0, null, 1, TaskPriority.P7);
        board.Submit(otherTask);
        board.Submit(busyTask);

        var character = new CharacterSim(null, new Vector2I(0, 0));
        board.PickFor(character); // 认领数值最高的 7 档
        Assert.Same(busyTask, character.CurrentTask);

        Assert.Same(otherTask, board.PeekFor(character));                  // 纯查询返回可接手的另一件
        Assert.Equal(TaskState.Pending, otherTask.State);                  // 未改状态
        Assert.Same(busyTask, character.CurrentTask);                      // 未写角色
    }

    /*****
    Date: 2026-09-25
    Name: RingMap_KeepsStationBaseSizeWithVacuumPaddingAndZonesConnected
    Description: 地图为 72×72 且空间站本体保持基准尺寸（半径 16~24 格）——环外（含四角）一律为真空，即扩大地图只增加真空区域；五个功能区仍有设施锚点与出生格且跨区连通。
    *****/
    [Fact]
    public void RingMap_KeepsStationBaseSizeWithVacuumPaddingAndZonesConnected()
    {
        Assert.Equal(72, RingMapGenerator.MapSize);
        Assert.Equal(16f, RingMapGenerator.CorridorInnerRadius);
        Assert.Equal(19f, RingMapGenerator.CorridorOuterRadius);
        Assert.Equal(24f, RingMapGenerator.RoomWallOuterRadius);

        RingMapData ring = RingMapGenerator.Generate();
        Assert.Equal(72, ring.Map.Width);
        Assert.Equal(72, ring.Map.Height);

        // 环外为真空：空间站未随地图放大（四角距中心约 50 格 > 外壳半径 24）
        Assert.Equal(CellKind.Vacuum, ring.Map.GetCell(new Vector2I(0, 0)));
        Assert.Equal(CellKind.Vacuum, ring.Map.GetCell(new Vector2I(71, 71)));
        Assert.Equal(CellKind.Vacuum, ring.Map.GetCell(new Vector2I(0, 71)));
        Assert.Equal(CellKind.Vacuum, ring.Map.GetCell(new Vector2I(71, 0)));

        var pathfinding = new GridPathfinding(ring.Map);
        ZoneId[] zones = Enum.GetValues<ZoneId>();
        foreach (ZoneId zone in zones)
        {
            Assert.True(ring.Rooms[zone].FacilityAnchor.HasValue, $"功能区 {zone} 缺少设施锚点");
            Assert.NotEmpty(ring.Rooms[zone].SpawnCells);
        }

        for (int i = 0; i < zones.Length; i++)
        {
            for (int j = i + 1; j < zones.Length; j++)
            {
                Vector2I from = ring.Rooms[zones[i]].SpawnCells[0];
                Vector2I to = ring.Rooms[zones[j]].SpawnCells[0];
                Assert.True(pathfinding.FindPath(from, to).Count > 0, $"{zones[i]} → {zones[j]} 不可达");
            }
        }
    }
}
