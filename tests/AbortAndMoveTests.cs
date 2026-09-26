using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Events;
using RelayStation.Core.Facilities;
using RelayStation.Core.Items;
using RelayStation.Core.Map;
using RelayStation.Core.Resources;
using RelayStation.Core.Tasks;
using RelayStation.Core.Time;
using Xunit;
using GridMap = RelayStation.Core.Map.GridMap;

namespace RelayStation.Tests;

/*****
Date: 2026-09-26
Name: AbortAndMoveTests
Description: 「中止体系」与「右键移动」单元/集成测试；覆盖设施中止（挂起：保留进度与设施状态、释放人员、退出排队、可恢复并继续）、人物中止（忽略当前任务实例并转做下一项、紧急任务不受忽略影响、重新下达指令后恢复排队）与手动移动（打断当前工作、抵达后回 Idle、多人落脚格不重叠）。
*****/
public sealed class AbortAndMoveTests
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
    Name: Suspend_KeepsProgressAndFacilityState_AndReleasesWorkers
    Description: 设施中止：任务转 Suspended、释放全部工作者、退出待处理队列但仍留在未结集合；设施状态**不回滚**（仍在维修中，施工警戒线得以保留），剩余进度与已耗零件保留。
    *****/
    [Fact]
    public void Suspend_KeepsProgressAndFacilityState_AndReleasesWorkers()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var facility = new FacilitySim(null, new Vector2I(6, 6));
        simulation.AddFacility(facility);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);

        var task = new RepairTask(facility, 30, 0, null, maxWorkers: 1);
        simulation.TaskBoard.Submit(task);
        AdvanceUntilWorking(simulation, character);
        simulation.Update(2.0); // 推进出可观测的进度

        Assert.Equal(TaskState.InProgress, task.State);
        Assert.Equal(FacilityState.UnderRepair, facility.State);
        double remainingBefore = task.RemainingGameMinutes;
        Assert.True(remainingBefore < 30.0);

        task.Suspend();

        Assert.Equal(TaskState.Suspended, task.State);
        Assert.Empty(task.Workers);
        Assert.Null(character.CurrentTask);
        Assert.Equal(FacilityState.UnderRepair, facility.State);          // 不回滚
        Assert.Equal(remainingBefore, task.RemainingGameMinutes, 6);      // 进度保留
        Assert.DoesNotContain(task, simulation.TaskBoard.Pending);        // 退出排队
        Assert.Contains(task, simulation.TaskBoard.Active);               // 仍为未结任务

        // 挂起后无人再来接手：推进多帧后任务仍为 Suspended
        for (int i = 0; i < 40; i++) simulation.Update(0.1);
        Assert.Equal(TaskState.Suspended, task.State);
        Assert.Equal(CharacterState.Idle, character.State);
    }

    /*****
    Date: 2026-09-26
    Name: Resume_RequeuesAndContinuesToCompletion
    Description: 设施「继续」：挂起任务回到 Pending 并重新派工（提交事件当帧投入空闲人力），随后从原进度继续至完成，设施转运行。
    *****/
    [Fact]
    public void Resume_RequeuesAndContinuesToCompletion()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var facility = new FacilitySim(null, new Vector2I(6, 6));
        simulation.AddFacility(facility);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);

        var task = new RepairTask(facility, 30, 0, null, maxWorkers: 1);
        simulation.TaskBoard.Submit(task);
        AdvanceUntilWorking(simulation, character);
        simulation.Update(5.0);
        double remainingBefore = task.RemainingGameMinutes;

        task.Suspend();
        for (int i = 0; i < 5; i++) simulation.Update(0.1); // 让角色回到 Idle

        simulation.TaskBoard.Resume(task);

        // 恢复与派工同帧完成：任务回到队列并立即被空闲人力接手（等价于一次新发布）
        Assert.Equal(TaskState.Assigned, task.State);
        Assert.Same(task, character.CurrentTask);
        Assert.DoesNotContain(task, simulation.TaskBoard.Pending);
        Assert.Equal(remainingBefore, task.RemainingGameMinutes, 6);

        for (int i = 0; i < 2000 && task.State != TaskState.Done; i++) simulation.Update(0.1);

        Assert.Equal(TaskState.Done, task.State);
        Assert.Equal(FacilityState.Operational, facility.State);
    }

    /*****
    Date: 2026-09-26
    Name: Suspend_BuildTask_KeepsUnderConstruction_AndDoesNotAbandon
    Description: 建造中止：不触发取消回调（不会撤销未建成设施），设施保持「建造中」，进度与零件消耗保留；恢复后回到 Pending。
    *****/
    [Fact]
    public void Suspend_BuildTask_KeepsUnderConstruction_AndDoesNotAbandon()
    {
        var facility = new FacilitySim(null, new Vector2I(4, 4));
        facility.SetState(FacilityState.UnderConstruction);
        bool abandoned = false;
        var task = new BuildTask(facility, 30, 0, null, 1, TaskPriority.P5, _ => abandoned = true);
        var worker = new CharacterSim(null, new Vector2I(0, 0));
        task.MarkAssigned(worker);
        task.StartWork();
        task.ProgressWork(worker, 10);

        task.Suspend();

        Assert.Equal(TaskState.Suspended, task.State);
        Assert.Equal(FacilityState.UnderConstruction, facility.State);
        Assert.False(abandoned);
        Assert.Empty(task.Workers);
        Assert.Equal(1.0 / 3.0, task.ProgressFraction, 5);

        task.Resume();
        Assert.Equal(TaskState.Pending, task.State);
    }

    /*****
    Date: 2026-09-26
    Name: AbortCharacter_IgnoresCurrentTask_AndPicksNext
    Description: 人物中止：释放当前任务并把它列入本人忽略清单，随即转做任务列表中的下一项（此例为低优先级任务）；被忽略的原任务留在未结集合、进度不受影响。
    *****/
    [Fact]
    public void AbortCharacter_IgnoresCurrentTask_AndPicksNext()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var highFacility = new FacilitySim(null, new Vector2I(6, 0));
        var lowFacility = new FacilitySim(null, new Vector2I(6, 8));
        simulation.AddFacility(highFacility);
        simulation.AddFacility(lowFacility);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);

        var highTask = new RepairTask(highFacility, 30, 0, null, 1, TaskPriority.P9);
        var lowTask = new RepairTask(lowFacility, 30, 0, null, 1, TaskPriority.P5);
        simulation.TaskBoard.Submit(highTask);
        simulation.TaskBoard.Submit(lowTask);
        Assert.Same(highTask, character.CurrentTask); // 数值高者先被认领

        simulation.AbortCharacterWork(character, ignoreCurrent: true);

        Assert.True(character.IsIgnoring(highTask));
        Assert.Same(lowTask, character.CurrentTask);   // 转向下一项
        Assert.Empty(highTask.Workers);                // 原任务被释放
        Assert.Contains(highTask, simulation.TaskBoard.Active);
        Assert.Equal(TaskState.Assigned, highTask.State);
    }

    /*****
    Date: 2026-09-26
    Name: AbortCharacter_WithNoOtherTask_StaysIdleInsteadOfResuming
    Description: 忽略清单确实生效：手上任务被中止后，即使没有其他任务，角色也保持待机而不会立刻重新认领被忽略的任务。
    *****/
    [Fact]
    public void AbortCharacter_WithNoOtherTask_StaysIdleInsteadOfResuming()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var facility = new FacilitySim(null, new Vector2I(6, 6));
        simulation.AddFacility(facility);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);

        var task = new RepairTask(facility, 30, 0, null, 1, TaskPriority.P5);
        simulation.TaskBoard.Submit(task);
        Assert.Same(task, character.CurrentTask);

        simulation.AbortCharacterWork(character, ignoreCurrent: true);

        Assert.Null(character.CurrentTask);
        for (int i = 0; i < 40; i++) simulation.Update(0.1);
        Assert.Null(character.CurrentTask);
        Assert.Equal(CharacterState.Idle, character.State);
        Assert.Equal(TaskState.Suspended, task.State); // 全场只有他一人且已忽略它 = 无人愿意接手 → 转「已中止」，等玩家点「继续」
    }

    /*****
    Date: 2026-09-26
    Name: AbortCharacter_UrgentTask_IsNotIgnored
    Description: 「紧急」不受人物中止忽略影响：小人只要没死就得干——中止后立刻重新认领同一紧急任务。
    *****/
    [Fact]
    public void AbortCharacter_UrgentTask_IsNotIgnored()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var facility = new FacilitySim(null, new Vector2I(6, 6));
        simulation.AddFacility(facility);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);

        var task = new RepairTask(facility, 30, 0, null, 1, TaskPriority.Urgent);
        simulation.TaskBoard.Submit(task);
        Assert.Same(task, character.CurrentTask);

        simulation.AbortCharacterWork(character, ignoreCurrent: true);

        Assert.Same(task, character.CurrentTask); // 紧急任务被立刻重新认领
    }

    /*****
    Date: 2026-09-26
    Name: ClearIgnoredTasksFor_MakesTaskPickableAgain
    Description: 「任务再次被发布」的等价路径：清除某设施的忽略记录后，该设施上的任务重新参与优先级排队（另一名空闲角色即可认领）。
    *****/
    [Fact]
    public void ClearIgnoredTasksFor_MakesTaskPickableAgain()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var highFacility = new FacilitySim(null, new Vector2I(6, 0));
        var lowFacility = new FacilitySim(null, new Vector2I(6, 8));
        simulation.AddFacility(highFacility);
        simulation.AddFacility(lowFacility);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);

        var highTask = new RepairTask(highFacility, 30, 0, null, 1, TaskPriority.P9);
        var lowTask = new RepairTask(lowFacility, 30, 0, null, 1, TaskPriority.P5);
        simulation.TaskBoard.Submit(highTask);
        simulation.TaskBoard.Submit(lowTask);

        simulation.AbortCharacterWork(character, ignoreCurrent: true); // 忽略 highTask
        Assert.True(character.IsIgnoring(highTask));

        simulation.ClearIgnoredTasksFor(highFacility);
        Assert.False(character.IsIgnoring(highTask));

        // 清除忽略后该任务重新参与优先级排队：另一名空闲角色即可认领（映射「任务再次被发布」）
        var other = new CharacterSim(null, new Vector2I(0, 1));
        simulation.AddCharacter(other);
        Assert.Same(highTask, simulation.TaskBoard.PickFor(other));
    }

    /*****
    Date: 2026-09-26
    Name: OrderMove_InterruptsWorkAndArrivesIdle
    Description: 右键移动：打断当前工作（脱离任务但任务不被取消），抵达终点后回到 Idle；无可用任务时保持待机。
    *****/
    [Fact]
    public void OrderMove_InterruptsWorkAndArrivesIdle()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);

        Assert.True(simulation.OrderMove(character, new Vector2I(11, 11)));

        Assert.Equal(CharacterState.Moving, character.State);
        Assert.True(character.IsManualMove);

        for (int i = 0; i < 4000 && character.State != CharacterState.Idle; i++) simulation.Update(0.1);

        Assert.Equal(CharacterState.Idle, character.State);
        Assert.Equal(new Vector2I(11, 11), character.Cell);
        Assert.False(character.IsManualMove);
    }

    /*****
    Date: 2026-09-27
    Name: OrderMove_AbortsTaskForThatCharacter_OtherTakesOver
    Description: 右键移动＝主动中止（对应反馈 2）：角色脱手任务并把它列入**本人**忽略清单（任务本身不取消、留在板上），抵达后**不会**折返复职；此时另有一名赋闲角色（未忽略）登场 → 轮末扫描发现有人愿意接手 → 任务自动开下一轮，由他做完。
    *****/
    [Fact]
    public void OrderMove_AbortsTaskForThatCharacter_OtherTakesOver()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var facility = new FacilitySim(null, new Vector2I(6, 6));
        simulation.AddFacility(facility);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);

        var task = new RepairTask(facility, 30, 0, null, maxWorkers: 2);
        simulation.TaskBoard.Submit(task);
        AdvanceUntilWorking(simulation, character);

        Assert.True(simulation.OrderMove(character, new Vector2I(0, 11)));

        Assert.Empty(task.Workers);
        Assert.Null(character.CurrentTask);
        Assert.Contains(task, simulation.TaskBoard.Active);
        Assert.NotEqual(TaskState.Cancelled, task.State);
        Assert.True(character.IsIgnoring(task)); // 本人退出该作业，不会再折返

        // 另一名赋闲角色登场（在轮末扫描之前）→ 自动接手并完工
        var other = new CharacterSim(null, new Vector2I(0, 5));
        simulation.AddCharacter(other);
        for (int i = 0; i < 8000 && task.State != TaskState.Done; i++) simulation.Update(0.1);

        Assert.Equal(new Vector2I(0, 11), character.Cell); // 他按玩家指令留在目的地
        Assert.Equal(CharacterState.Idle, character.State);
        Assert.Null(character.CurrentTask);
        Assert.Equal(TaskState.Done, task.State);         // 由另一人做完
        Assert.Equal(FacilityState.Operational, facility.State);
    }

    /*****
    Date: 2026-09-26
    Name: OrderMoveGroup_AssignsDistinctDestinations
    Description: 多人右键移动：每个角色分到互不相同的落脚格（首选点击格，其余走四邻），不会全部叠在同一格。
    *****/
    [Fact]
    public void OrderMoveGroup_AssignsDistinctDestinations()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var first = new CharacterSim(null, new Vector2I(0, 0));
        var second = new CharacterSim(null, new Vector2I(0, 1));
        var third = new CharacterSim(null, new Vector2I(0, 2));
        simulation.AddCharacter(first);
        simulation.AddCharacter(second);
        simulation.AddCharacter(third);

        int ordered = simulation.OrderMoveGroup(new[] { first, second, third }, new Vector2I(5, 5));

        Assert.Equal(3, ordered);
        Vector2I Dest(CharacterSim c) => c.Path![c.Path.Count - 1];
        Assert.Equal(3, new[] { Dest(first), Dest(second), Dest(third) }.Distinct().Count());
        Assert.Equal(new Vector2I(5, 5), Dest(first)); // 首人落脚点击格
    }

    /*****
    Date: 2026-09-26
    Name: CanMoveTo_RejectsWallsAndBlockingFacilities
    Description: 移动落脚校验：墙格与阻挡设施占地格不可作为落脚点，普通地板可。
    *****/
    [Fact]
    public void CanMoveTo_RejectsWallsAndBlockingFacilities()
    {
        GridMap map = CreateFloorMap(8, 8);
        map.SetCell(new Vector2I(1, 1), CellKind.Wall);
        Simulation simulation = CreateSimulation(map);

        Assert.False(simulation.CanMoveTo(new Vector2I(1, 1)));
        Assert.True(simulation.CanMoveTo(new Vector2I(2, 2)));

        var blocking = new FacilitySim(null, new Vector2I(4, 4), blocksMovement: true);
        simulation.AddFacility(blocking);
        Assert.False(simulation.CanMoveTo(new Vector2I(4, 4)));
        Assert.False(simulation.CanMoveTo(new Vector2I(5, 5)));
    }

    /*****
    Date: 2026-09-26
    Name: PartsExhausted_SuspendsPassively_WhenNoSourceLeft
    Description: 被动中止（集成，份额制）：工人自带零件用尽 → **本人自行退场**（任务不挂起、进度保留）；轮末扫描发现还有人愿意接手 → 自动开下一轮，但全基地已无料可取 → 任务转「已中止（零件不足）」等玩家介入；把料补到地上并点「继续」后，工人自行取料并跑完。
    *****/
    [Fact]
    public void PartsExhausted_SuspendsPassively_WhenNoSourceLeft()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var facility = new FacilitySim(null, new Vector2I(6, 6));
        simulation.AddFacility(facility);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);

        var parts = new FakeItemDef("parts", 100, weightKg: 0.5f);
        Assert.True(character.Inventory.TryAdd(parts, 5)); // 自带 5 件，成本 10 → 中途耗尽
        var task = new RepairTask(facility, 30, 10, parts, 1, TaskPriority.P5);
        simulation.TaskBoard.Submit(task);
        AdvanceUntilWorking(simulation, character);

        for (int i = 0; i < 4000 && task.State != TaskState.Suspended; i++) simulation.Update(0.5);

        Assert.Equal(TaskState.Suspended, task.State);
        Assert.True(task.SuspendedByPartsShortage); // 标明是被动中止（供 UI 说明原因）
        Assert.Empty(task.Workers);
        Assert.Null(character.CurrentTask);
        Assert.Equal(FacilityState.UnderRepair, facility.State); // 不回滚
        Assert.Equal(5, task.PartsConsumed);                     // 进度与消耗保留
        Assert.True(task.RemainingGameMinutes is > 0 and < 30);

        // 补料（把零件堆到工人脚下）+「继续」→ 自动取料并跑完
        simulation.Items.Add(parts, 5, new Vector2I(0, 0));
        simulation.TaskBoard.Resume(task);
        Assert.NotEqual(TaskState.Suspended, task.State);
        Assert.False(task.SuspendedByPartsShortage); // 恢复后清除原因标记
        for (int i = 0; i < 8000 && task.State != TaskState.Done; i++) simulation.Update(0.5);
        Assert.Equal(TaskState.Done, task.State);
        Assert.Equal(FacilityState.Operational, facility.State);
        Assert.Empty(simulation.Items.Stacks); // 补的 5 件全被取走
    }

    /*****
    Date: 2026-09-27
    Name: MoveCommand_WhileFetching_AbortsFetchAndAnotherTakesOver
    Description: 「中止停不下建造」的回归（对应反馈 2）：人物正在**取料**时右键移动 → 备料任务被取消、并连同它服务的那条设施作业一起列入本人忽略清单 → 他走到目的地后**不会**再自动折返取料；轮末扫描发现另有人愿意接手 → 任务自动开下一轮，由那名赋闲角色取料并完工。
    *****/
    [Fact]
    public void MoveCommand_WhileFetching_AbortsFetchAndAnotherTakesOver()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var facility = new FacilitySim(null, new Vector2I(6, 6));
        simulation.AddFacility(facility);
        var first = new CharacterSim(null, new Vector2I(0, 0));
        var second = new CharacterSim(null, new Vector2I(0, 11));
        simulation.AddCharacter(first);
        simulation.AddCharacter(second);

        var parts = new FakeItemDef("parts", 100, weightKg: 0.5f);
        ItemStack pile = simulation.Items.Add(parts, 40, new Vector2I(11, 0))[0];
        var task = new RepairTask(facility, 30, 20, parts, maxWorkers: 1);
        simulation.TaskBoard.Submit(task);

        // 第一帧：首人认领作业 → 分配份额 → 出发去取料
        simulation.Update(0.1);
        PickupTask fetch = Assert.Single(simulation.TaskBoard.Active.OfType<PickupTask>());
        Assert.Same(task, fetch.ServedTask);
        Assert.Same(first, fetch.Owner);

        // 玩家右键叫他走到别处：视同主动中止
        Assert.True(simulation.OrderMove(first, new Vector2I(0, 10)));
        Assert.Empty(simulation.TaskBoard.Active.OfType<PickupTask>()); // 备料被取消
        Assert.True(first.IsIgnoring(task));                            // 连它服务的作业一起退出
        Assert.True(first.IsManualMove);

        // 由另一名赋闲角色接手，并最终完工
        for (int i = 0; i < 8000 && task.State != TaskState.Done; i++) simulation.Update(0.1);

        Assert.Equal(TaskState.Done, task.State);
        Assert.Equal(FacilityState.Operational, facility.State);
        Assert.Equal(20, task.PartsConsumed);
        Assert.Equal(new Vector2I(0, 10), first.Cell);           // 他按玩家指令停在了目的地
        Assert.Null(first.CurrentTask);                          // 没有折返去取料/施工
        Assert.Equal(0, first.Inventory.CountOf(parts));
        Assert.Equal(0, second.Inventory.CountOf(parts));
        Assert.Equal(20, pile.Count);                            // 40 件里只用掉了 20
    }

    /*****
    Date: 2026-09-26
    Name: Submit_HighPriorityTask_DoesNotInterruptManualMove
    Description: 玩家右键移动指令不被任务抢占（无 CurrentTask 者不打断）：紧急任务提交后角色仍沿原路径抵达玩家指定格，到达后才重新找活。
    *****/
    [Fact]
    public void Submit_HighPriorityTask_DoesNotInterruptManualMove()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var facility = new FacilitySim(null, new Vector2I(6, 6));
        simulation.AddFacility(facility);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);

        Assert.True(simulation.OrderMove(character, new Vector2I(0, 11)));
        simulation.TaskBoard.Submit(new RepairTask(facility, 30, 0, null, 1, TaskPriority.Urgent));

        Assert.Null(character.CurrentTask); // 未被抢占
        Assert.True(character.IsManualMove);
        Assert.Equal(CharacterState.Moving, character.State);

        for (int i = 0; i < 4000 && character.Cell != new Vector2I(0, 11); i++) simulation.Update(0.1);
        Assert.Equal(new Vector2I(0, 11), character.Cell); // 仍按玩家指令抵达
    }

    /*****
    Date: 2026-09-27
    Name: MoveCommand_CancelsPersonalPickupTask
    Description: 右键「走到此处」对**个人任务**（拾取）视同主动中止——任务当即取消、物品一件未取留在原地，抵达目的地后也不会「折返继续拾取」；对**公共任务**（修理）仍只脱离本人（任务留在板上、可被本人或他人再次接手）。
    *****/
    [Fact]
    public void MoveCommand_CancelsPersonalPickupTask()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);
        var def = new FakeItemDef("parts", 100, weightKg: 0.5f);
        ItemStack stack = simulation.Items.Add(def, 5, new Vector2I(4, 4))[0];

        Assert.True(simulation.OrderPickup(character, stack));
        Assert.IsType<PickupTask>(character.CurrentTask);

        Assert.True(simulation.OrderMove(character, new Vector2I(0, 11)));

        Assert.Empty(simulation.TaskBoard.Active); // 个人任务被取消
        Assert.Null(character.CurrentTask);
        Assert.True(character.IsManualMove);
        Assert.Equal(5, stack.Count); // 一件未取，留在原地

        for (int i = 0; i < 4000 && character.Cell != new Vector2I(0, 11); i++) simulation.Update(0.1);
        Assert.Equal(new Vector2I(0, 11), character.Cell);
        Assert.Empty(simulation.TaskBoard.Active); // 抵达后也不会「恢复」拾取
        Assert.Equal(5, stack.Count);
    }

    /*****
    Date: 2026-09-27
    Name: MoveCommand_AbortsPublicWork_AndTaskWaitsForOthers
    Description: 右键「走到此处」对公共任务（修理）＝**主动中止**：本人脱离并把它列入自己的忽略清单，任务留在板上（未取消）；他抵达目的地后不再折返复职。本例全场只有他一人且已忽略它 → 无人愿意接手 → 任务转「已中止」，等玩家点「继续」（或另有人手后由玩家恢复）。
    *****/
    [Fact]
    public void MoveCommand_AbortsPublicWork_AndTaskWaitsForOthers()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var facility = new FacilitySim(null, new Vector2I(6, 6));
        simulation.AddFacility(facility);
        var character = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(character);

        var task = new RepairTask(facility, 30, 0, null, 1, TaskPriority.P5);
        simulation.TaskBoard.Submit(task);
        AdvanceUntilWorking(simulation, character);

        Assert.True(simulation.OrderMove(character, new Vector2I(0, 11)));

        Assert.Contains(task, simulation.TaskBoard.Active);
        Assert.NotEqual(TaskState.Cancelled, task.State);
        Assert.True(character.IsIgnoring(task)); // 右键移动＝中止该工作（本人退出，别人不受影响）
        Assert.Null(character.CurrentTask);
        Assert.True(character.IsManualMove);

        for (int i = 0; i < 4000 && character.Cell != new Vector2I(0, 11); i++) simulation.Update(0.1);
        Assert.Equal(new Vector2I(0, 11), character.Cell);
        Assert.Null(character.CurrentTask);            // 抵达后不会折返复职
        Assert.NotEqual(TaskState.Done, task.State);
        Assert.Equal(TaskState.Suspended, task.State); // 无人愿意接手 → 相当于中止，等玩家点「继续」
    }
}
