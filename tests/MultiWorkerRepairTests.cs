using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Events;
using RelayStation.Core.Facilities;
using RelayStation.Core.Map;
using RelayStation.Core.Tasks;
using RelayStation.Core.Time;
using Xunit;

namespace RelayStation.Tests;

/*****
Date: 2026-09-25
Name: MultiWorkerRepairTests
Description: 多人维修单元与集成测试；覆盖 PickFor 旁路加入、MaxWorkers 上限、单人回归、取消释放全部工作者、完成兜底回 Idle、StartWork 幂等、双人推进速率叠加与 ReleaseWorker 仅脱离本人。
*****/
public sealed class MultiWorkerRepairTests
{
    /*****
    Date: 2026-09-25
    Name: CreateFacility
    Description: 创建无定义损坏设施（引擎外可构造）。
    *****/
    private static FacilitySim CreateFacility() => new(null, new Vector2I(10, 10));

    /*****
    Date: 2026-09-25
    Name: CreateCharacter
    Description: 创建无定义空闲角色。
    *****/
    private static CharacterSim CreateCharacter() => new(null, new Vector2I(0, 0));

    /*****
    Date: 2026-09-25
    Name: MultiWorkerLoop_JoinProgressTogetherAndComplete
    Description: 双人维修闭环（Simulation 集成）：第二人经 PickFor 旁路加入进行中任务，双人同速率推进（总速度×2），完成后双方兜底回到 Idle，设施转运行。
    *****/
    [Fact]
    public void MultiWorkerLoop_JoinProgressTogetherAndComplete()
    {
        RingMapData ring = RingMapGenerator.Generate();
        var clock = new GameClock();
        var eventBus = new EventBus();
        var simulation = new Simulation(
            clock,
            ring.Map,
            new GridPathfinding(ring.Map),
            new TaskBoard(eventBus),
            new NeedSystem(),
            eventBus);

        var facility = new FacilitySim(null, ring.Rooms[ZoneId.LifeSupport].FacilityAnchor!.Value);
        simulation.AddFacility(facility);

        var first = new CharacterSim(null, ring.Rooms[ZoneId.Living].SpawnCells[0]);
        var second = new CharacterSim(null, ring.Rooms[ZoneId.Living].SpawnCells[1]);
        simulation.AddCharacter(first);
        simulation.AddCharacter(second);

        var task = new RepairTask(facility, 30, 0, null, maxWorkers: 2);
        simulation.TaskBoard.Submit(task);

        // 推进到两人都在作业（第二人在首人开始维修后经旁路加入）
        int frames = 0;
        while ((first.State != CharacterState.Working || second.State != CharacterState.Working) && frames < 6000)
        {
            simulation.Update(0.1);
            frames++;
        }
        Assert.Equal(CharacterState.Working, first.State);
        Assert.Equal(CharacterState.Working, second.State);
        Assert.Equal(2, task.Workers.Count);
        Assert.Equal(TaskState.InProgress, task.State);

        // 双人同速推进：1 分钟帧两人各推 1 分钟（Def 为 null → 倍率 1），剩余应减约 2
        double before = task.RemainingGameMinutes;
        simulation.Update(1.0);
        Assert.Equal(before - 2.0, task.RemainingGameMinutes, 2);

        // 快进到完成：双方兜底回 Idle，任务完成，设施运行
        for (int i = 0; i < 600 && task.State != TaskState.Done; i++)
        {
            simulation.Update(0.1);
        }
        simulation.Update(0.1); // 额外一帧驱动未完成方的终态兜底分支

        Assert.Equal(TaskState.Done, task.State);
        Assert.Equal(FacilityState.Operational, facility.State);
        Assert.Equal(CharacterState.Idle, first.State);
        Assert.Equal(CharacterState.Idle, second.State);
        Assert.Null(first.CurrentTask);
        Assert.Null(second.CurrentTask);
    }

    /*****
    Date: 2026-09-25
    Name: TryJoin_ExceedsMaxWorkers_Rejected
    Description: 超过 MaxWorkers 上限的加入请求被拒绝。
    *****/
    [Fact]
    public void TryJoin_ExceedsMaxWorkers_Rejected()
    {
        RepairTask task = StartTask(CreateFacility(), maxWorkers: 2);
        CharacterSim second = CreateCharacter();
        CharacterSim third = CreateCharacter();

        Assert.True(task.TryJoin(second));
        Assert.False(task.TryJoin(third));
        Assert.Equal(2, task.Workers.Count);
    }

    /*****
    Date: 2026-09-25
    Name: MaxWorkersOne_SecondPickReturnsNull
    Description: MaxWorkers=1 时单人现状回归：任务进行中第二人 PickFor 拿不到任务。
    *****/
    [Fact]
    public void MaxWorkersOne_SecondPickReturnsNull()
    {
        var board = new TaskBoard(new EventBus());
        RepairTask task = StartTask(CreateFacility(), maxWorkers: 1);
        board.Submit(task);
        CharacterSim second = CreateCharacter();

        Assert.Null(board.PickFor(second));
        Assert.Single(task.Workers);
    }

    /*****
    Date: 2026-09-25
    Name: Cancel_ReleasesAllWorkers
    Description: 取消多人任务释放全部工作者（均置 Interrupted 并清理任务引用）。
    *****/
    [Fact]
    public void Cancel_ReleasesAllWorkers()
    {
        var board = new TaskBoard(new EventBus());
        FacilitySim facility = CreateFacility();
        CharacterSim first = CreateCharacter();
        RepairTask task = StartSubmittedTask(board, facility, maxWorkers: 2, first);
        CharacterSim second = CreateCharacter();

        Assert.Same(task, board.PickFor(second)); // 第二人经旁路加入
        Assert.Equal(2, task.Workers.Count);

        board.Cancel(task);

        Assert.Empty(task.Workers); // 事件释放完成后 MarkCancelled 清空工作者列表
        Assert.Null(first.CurrentTask);
        Assert.Null(second.CurrentTask);
        Assert.Equal(CharacterState.Interrupted, second.State);
    }

    /*****
    Date: 2026-09-25
    Name: SecondWorkerArrival_StartWorkIsIdempotent
    Description: 第二人抵达调用 StartWork 幂等：设施 UnderRepair 仅置一次。
    *****/
    [Fact]
    public void SecondWorkerArrival_StartWorkIsIdempotent()
    {
        FacilitySim facility = CreateFacility();
        int underRepairCount = 0;
        facility.StateChanged += (_, newState) => { if (newState == FacilityState.UnderRepair) underRepairCount++; };

        RepairTask task = StartTask(facility, maxWorkers: 2);
        CharacterSim second = CreateCharacter();

        Assert.True(task.TryJoin(second));
        task.StartWork(); // 模拟第二人抵达（任务已 InProgress）

        Assert.Equal(1, underRepairCount);
        Assert.Equal(TaskState.InProgress, task.State);
    }

    /*****
    Date: 2026-09-25
    Name: TwoWorkers_ProgressAddsUp
    Description: 双人各自调用 ProgressWork 推进量叠加（每人 10 分钟 → 剩余减 20）。
    *****/
    [Fact]
    public void TwoWorkers_ProgressAddsUp()
    {
        RepairTask task = StartTask(CreateFacility(), maxWorkers: 2);
        CharacterSim second = CreateCharacter();
        task.TryJoin(second);

        Assert.False(task.ProgressWork(task.Assignee!, 10));
        Assert.Equal(20, task.RemainingGameMinutes, 5);
        Assert.False(task.ProgressWork(second, 10));
        Assert.Equal(10, task.RemainingGameMinutes, 5);
        Assert.Equal(2.0 / 3.0, task.ProgressFraction, 5);
    }

    /*****
    Date: 2026-09-25
    Name: ReleaseWorker_RemovesOnlyJoiner
    Description: ReleaseWorker 仅脱离本人（角色置 Interrupted、任务引用清理），任务与其他工作者不受影响。
    *****/
    [Fact]
    public void ReleaseWorker_RemovesOnlyJoiner()
    {
        var board = new TaskBoard(new EventBus());
        CharacterSim first = CreateCharacter();
        RepairTask task = StartSubmittedTask(board, CreateFacility(), maxWorkers: 2, first);
        CharacterSim second = CreateCharacter();
        board.PickFor(second); // 旁路加入并写入 CurrentTask

        task.ReleaseWorker(second);

        Assert.Single(task.Workers);
        Assert.Equal(TaskState.InProgress, task.State);
        Assert.Null(second.CurrentTask);
        Assert.Equal(CharacterState.Interrupted, second.State);
        Assert.Same(task, first.CurrentTask); // 首人不受影响
        Assert.Same(first, task.Assignee);
    }

    /*****
    Date: 2026-09-25
    Name: StartTask
    Description: 构造任务并推进到 InProgress（首人经 MarkAssigned → StartWork；不入任务板，供任务级用例）。
    *****/
    private static RepairTask StartTask(FacilitySim facility, int maxWorkers)
    {
        var task = new RepairTask(facility, 30, 0, null, maxWorkers);
        task.MarkAssigned(CreateCharacter());
        task.StartWork();
        return task;
    }

    /*****
    Date: 2026-09-25
    Name: StartSubmittedTask
    Description: 构造任务并先提交任务板（Pending 态），再经 PickFor 真实认领路径（写入首人 CurrentTask）推进到 InProgress；供需要 PickFor 旁路的用例。
    *****/
    private static RepairTask StartSubmittedTask(TaskBoard board, FacilitySim facility, int maxWorkers, CharacterSim first)
    {
        var task = new RepairTask(facility, 30, 0, null, maxWorkers);
        board.Submit(task);
        board.PickFor(first);
        task.StartWork();
        return task;
    }
}
