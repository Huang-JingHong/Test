using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Events;
using RelayStation.Core.Facilities;
using RelayStation.Core.Tasks;
using Xunit;

namespace RelayStation.Tests;

/*****
Date: 2026-09-06
Name: TaskBoardTests
Description: 任务板与修复任务流转单元测试；覆盖提交/同目标去重/认领/执行/完成/取消全闭环（Def 传 null 以脱离 Godot 引擎运行，模拟层不触碰 Godot 原生对象）。
*****/
public sealed class TaskBoardTests
{
    /*****
    Date: 2026-09-06
    Name: CreateBoard
    Description: 创建带独立事件总线的任务板。
    *****/
    private static TaskBoard CreateBoard() => new(new EventBus());

    /*****
    Date: 2026-09-06
    Name: CreateFacility
    Description: 创建无定义设施（引擎外可构造，默认损坏状态）。
    *****/
    private static FacilitySim CreateFacility() => new(null, new Vector2I(10, 10));

    /*****
    Date: 2026-09-06
    Name: CreateCharacter
    Description: 创建无定义空闲角色。
    *****/
    private static CharacterSim CreateCharacter() => new(null, new Vector2I(0, 0));

    /*****
    Date: 2026-09-06
    Name: Submit_AddsTaskToPending
    Description: 提交任务后进入待处理队列。
    *****/
    [Fact]
    public void Submit_AddsTaskToPending()
    {
        TaskBoard board = CreateBoard();
        var task = new RepairTask(CreateFacility(), 10);

        board.Submit(task);

        Assert.Single(board.Pending, task);
        Assert.Equal(TaskState.Pending, task.State);
    }

    /*****
    Date: 2026-09-06
    Name: Submit_SameTargetTwice_IgnoresDuplicate
    Description: 同一目标的未结任务重复提交时被忽略（防重复派单）。
    *****/
    [Fact]
    public void Submit_SameTargetTwice_IgnoresDuplicate()
    {
        TaskBoard board = CreateBoard();
        FacilitySim facility = CreateFacility();

        board.Submit(new RepairTask(facility, 10));
        board.Submit(new RepairTask(facility, 10));

        Assert.Single(board.Pending);
    }

    /*****
    Date: 2026-09-06
    Name: PickFor_IdleCharacter_AssignsTaskAndTracksOnCharacter
    Description: 空闲角色认领任务：任务转 Assigned、角色记录当前任务、队列移除。
    *****/
    [Fact]
    public void PickFor_IdleCharacter_AssignsTaskAndTracksOnCharacter()
    {
        TaskBoard board = CreateBoard();
        CharacterSim character = CreateCharacter();
        var task = new RepairTask(CreateFacility(), 10);
        board.Submit(task);

        ITask? picked = board.PickFor(character);

        Assert.Same(task, picked);
        Assert.Equal(TaskState.Assigned, task.State);
        Assert.Same(character, task.Assignee);
        Assert.Same(task, character.CurrentTask);
        Assert.Empty(board.Pending);
    }

    /*****
    Date: 2026-09-06
    Name: PickFor_NoPendingTask_ReturnsNull
    Description: 无待处理任务时返回 null。
    *****/
    [Fact]
    public void PickFor_NoPendingTask_ReturnsNull()
    {
        TaskBoard board = CreateBoard();
        Assert.Null(board.PickFor(CreateCharacter()));
    }

    /*****
    Date: 2026-09-06
    Name: PickFor_BusyCharacter_ReturnsNull
    Description: 非空闲（Working）角色无法认领任务。
    *****/
    [Fact]
    public void PickFor_BusyCharacter_ReturnsNull()
    {
        TaskBoard board = CreateBoard();
        CharacterSim busy = CreateCharacter();
        busy.State = CharacterState.Working;
        board.Submit(new RepairTask(CreateFacility(), 10));

        Assert.Null(board.PickFor(busy));
    }

    /*****
    Date: 2026-09-06
    Name: TaskFlow_AssignStartProgressComplete
    Description: 完整流转：认领 → 开始（设施转维修中）→ 推进进度 → 完成（设施转运行）。
    *****/
    [Fact]
    public void TaskFlow_AssignStartProgressComplete()
    {
        TaskBoard board = CreateBoard();
        FacilitySim facility = CreateFacility();
        CharacterSim character = CreateCharacter();
        var task = new RepairTask(facility, 10);
        board.Submit(task);
        board.PickFor(character);

        task.StartWork();
        Assert.Equal(TaskState.InProgress, task.State);
        Assert.Equal(FacilityState.UnderRepair, facility.State);

        Assert.False(task.ProgressWork(6));
        Assert.Equal(4, task.RemainingGameMinutes, 5);

        Assert.True(task.ProgressWork(6));
        Assert.Equal(TaskState.Done, task.State);
        Assert.Equal(FacilityState.Operational, facility.State);
        Assert.Empty(board.Pending);
    }

    /*****
    Date: 2026-09-06
    Name: Cancel_PendingTask_RemovesFromPending
    Description: 取消待处理任务：任务转 Cancelled 并移出队列。
    *****/
    [Fact]
    public void Cancel_PendingTask_RemovesFromPending()
    {
        TaskBoard board = CreateBoard();
        var task = new RepairTask(CreateFacility(), 10);
        board.Submit(task);

        board.Cancel(task);

        Assert.Equal(TaskState.Cancelled, task.State);
        Assert.Empty(board.Pending);
    }

    /*****
    Date: 2026-09-06
    Name: Cancel_AssignedTask_ReleasesCharacter
    Description: 取消已认领任务：认领角色的当前任务引用被清理并进入 Interrupted。
    *****/
    [Fact]
    public void Cancel_AssignedTask_ReleasesCharacter()
    {
        TaskBoard board = CreateBoard();
        CharacterSim character = CreateCharacter();
        var task = new RepairTask(CreateFacility(), 10);
        board.Submit(task);
        board.PickFor(character);

        board.Cancel(task);

        Assert.Equal(TaskState.Cancelled, task.State);
        Assert.Null(character.CurrentTask);
        Assert.Equal(CharacterState.Interrupted, character.State);
    }

    /*****
    Date: 2026-09-06
    Name: Cancel_AllowsNewTaskForSameTargetAfterTerminal
    Description: 任务终态（取消）后，同目标可再次提交新任务。
    *****/
    [Fact]
    public void Cancel_AllowsNewTaskForSameTargetAfterTerminal()
    {
        TaskBoard board = CreateBoard();
        FacilitySim facility = CreateFacility();
        var first = new RepairTask(facility, 10);
        board.Submit(first);
        board.Cancel(first);

        var second = new RepairTask(facility, 10);
        board.Submit(second);

        ITask resubmitted = Assert.Single(board.Pending);
        Assert.Same(second, resubmitted);
    }
}
