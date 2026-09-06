using RelayStation.Core.Characters;
using RelayStation.Core.Facilities;

namespace RelayStation.Core.Tasks;

/*****
Date: 2026-09-06
Name: RepairTask
Description: 修复任务（阶段一唯一任务类型）；目标为损坏设施，按剩余游戏分钟推进（受角色专长效率加成影响，由 Simulation 计算），完成后设施转为 Operational。
*****/
public sealed class RepairTask : ITask
{
    /*****
    Date: 2026-09-06
    Name: _remainingGameMinutes
    Description: 剩余修复所需游戏分钟。
    *****/
    private double _remainingGameMinutes;

    /*****
    Date: 2026-09-06
    Name: Target
    Description: 任务目标设施。
    *****/
    public FacilitySim? Target { get; }

    /*****
    Date: 2026-09-06
    Name: State
    Description: 任务当前状态；构造即 Pending。
    *****/
    public TaskState State { get; private set; } = TaskState.Pending;

    /*****
    Date: 2026-09-06
    Name: Assignee
    Description: 当前认领本任务的角色；未被认领时为 null。
    *****/
    public CharacterSim? Assignee { get; private set; }

    /*****
    Date: 2026-09-06
    Name: RemainingGameMinutes
    Description: 剩余完成所需的游戏分钟数。
    *****/
    public double RemainingGameMinutes => _remainingGameMinutes;

    /*****
    Date: 2026-09-06
    Name: RequiredSpecialty
    Description: 修复任务属维修专长。
    *****/
    public Specialty? RequiredSpecialty => Specialty.Maintenance;

    /*****
    Date: 2026-09-06
    Name: StateChanged
    Description: 任务状态变更时触发的事件（参数：任务本体、新状态）。
    *****/
    public event Action<ITask, TaskState>? StateChanged;

    /*****
    Date: 2026-09-06
    Name: RepairTask
    Description: 构造函数；target 为目标设施，repairGameMinutes 为基准修复耗时（由设施定义提供，引擎外测试可直接指定）。
    *****/
    public RepairTask(FacilitySim target, double repairGameMinutes)
    {
        Target = target;
        _remainingGameMinutes = repairGameMinutes;
    }

    /*****
    Date: 2026-09-06
    Name: CanAssign
    Description: 空闲角色可认领处于 Pending 状态的本任务。
    *****/
    public bool CanAssign(CharacterSim c)
        => State == TaskState.Pending && c.State == CharacterState.Idle;

    /*****
    Date: 2026-09-06
    Name: MarkAssigned
    Description: 标记任务被指定角色认领（Pending → Assigned）。
    *****/
    public void MarkAssigned(CharacterSim assignee)
    {
        if (State != TaskState.Pending) return;
        Assignee = assignee;
        SetState(TaskState.Assigned);
    }

    /*****
    Date: 2026-09-06
    Name: MarkCancelled
    Description: 取消任务（未结状态 → Cancelled；Done / Cancelled 终态忽略）。先触发状态事件（此时 Assignee 仍可被任务板读取以释放角色），再清理认领人。
    *****/
    public void MarkCancelled()
    {
        if (State is TaskState.Done or TaskState.Cancelled) return;
        SetState(TaskState.Cancelled);
        Assignee = null;
    }

    /*****
    Date: 2026-09-06
    Name: StartWork
    Description: 开始执行（Assigned → InProgress），目标设施转入维修中。
    *****/
    public void StartWork()
    {
        if (State != TaskState.Assigned) return;
        SetState(TaskState.InProgress);
        Target?.SetState(FacilityState.UnderRepair);
    }

    /*****
    Date: 2026-09-06
    Name: ProgressWork
    Description: 按给定游戏分钟扣减剩余量；归零时任务完成（→ Done）且设施转 Operational，返回 true；仅在 InProgress 状态有效。
    *****/
    public bool ProgressWork(double gameMinutes)
    {
        if (State != TaskState.InProgress) return false;
        _remainingGameMinutes -= gameMinutes;
        if (_remainingGameMinutes > 0) return false;

        _remainingGameMinutes = 0;
        SetState(TaskState.Done);
        Target?.SetState(FacilityState.Operational);
        return true;
    }

    /*****
    Date: 2026-09-06
    Name: SetState
    Description: 迁移任务状态并触发 StateChanged 事件。
    *****/
    private void SetState(TaskState newState)
    {
        if (State == newState) return;
        State = newState;
        StateChanged?.Invoke(this, newState);
    }
}
