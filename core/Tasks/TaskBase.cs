using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Facilities;
using RelayStation.Core.Items;

namespace RelayStation.Core.Tasks;

/*****
Date: 2026-09-26
Name: TaskBase
Description: 任务生命周期基类（纯 C#）；承载全部任务共用的状态机与协作机制——状态流转（Pending → Assigned → InProgress →〔Suspended〕→ Done/Cancelled）、工作者列表（首人经 MarkAssigned 进入、其余经 TryJoin 加入）、认领/加入资格（CanAssign/CanJoin/CanFit：含「角色须空闲」前置与 CanOwn 归属限制）、优先级、最大人数、状态变更事件、挂起/恢复与取消时释放全部工作者。
派生类只需：①构造时给定人数上限与优先级；②覆写业务成员（设施目标 / 物品目标 / 归属者 / 作业格 / 有效性 / 专长）；③实现 ProgressWork 表达自己的进度模型（设施作业 = 计时 + 实时扣零件；物品类作业 = 逐件转移累加）。设施作业见 FacilityWorkTask；物品类作业（拾取/取用/穿脱）见 PickupTask / ConsumeTask / EquipTask / UnequipTask。
*****/
public abstract class TaskBase : ITask
{
    /*****
    Date: 2026-09-26
    Name: _workers
    Description: 参与本任务的角色列表（首人经 MarkAssigned 进入，其余经 TryJoin 加入）。
    *****/
    private readonly List<CharacterSim> _workers = new();

    /*****
    Date: 2026-09-26
    Name: _maxWorkers
    Description: 最大投入人数（至少 1）。
    *****/
    private readonly int _maxWorkers;

    /*****
    Date: 2026-09-26
    Name: _priority
    Description: 任务优先级（提交时指定；任务板择优依据）。
    *****/
    private readonly TaskPriority _priority;

    /*****
    Date: 2026-09-26
    Name: TaskBase
    Description: 构造函数；给定最大投入人数（默认 1 单人）与优先级（默认 5 档）。
    *****/
    protected TaskBase(int maxWorkers = 1, TaskPriority priority = TaskPriority.P5)
    {
        _maxWorkers = Math.Max(1, maxWorkers);
        _priority = priority;
    }

    /*****
    Date: 2026-09-26
    Name: State
    Description: 任务当前状态；构造即 Pending。
    *****/
    public TaskState State { get; private set; } = TaskState.Pending;

    /*****
    Date: 2026-09-26
    Name: Assignee
    Description: 首个认领本任务的角色（多人协作时为工作者列表首人）；未被认领时为 null。
    *****/
    public CharacterSim? Assignee { get; private set; }

    /*****
    Date: 2026-09-26
    Name: Workers
    Description: 当前参与本任务的角色列表。
    *****/
    public IReadOnlyList<CharacterSim> Workers => _workers;

    /*****
    Date: 2026-09-26
    Name: MaxWorkers
    Description: 本任务允许的最大投入人数。
    *****/
    public int MaxWorkers => _maxWorkers;

    /*****
    Date: 2026-09-26
    Name: Priority
    Description: 任务优先级。
    *****/
    public TaskPriority Priority => _priority;

    /*****
    Date: 2026-09-26
    Name: Target
    Description: 任务目标设施；设施作业由子类覆写，物品类任务恒为 null。
    *****/
    public virtual FacilitySim? Target => null;

    /*****
    Date: 2026-09-26
    Name: ItemTarget
    Description: 任务目标物品堆；物品类任务由子类覆写，其余恒为 null。
    *****/
    public virtual ItemStack? ItemTarget => null;

    /*****
    Date: 2026-09-26
    Name: Owner
    Description: 任务归属者：个人任务由子类覆写为本人（只有该角色可认领/加入），公共任务恒为 null。
    *****/
    public virtual CharacterSim? Owner => null;

    /*****
    Date: 2026-09-26
    Name: RequiredSpecialty
    Description: 任务所属专长类型（决定角色专长效率加成是否匹配）；无专长限定时为 null（物品类任务均无）。
    *****/
    public virtual Specialty? RequiredSpecialty => null;

    /*****
    Date: 2026-09-26
    Name: PreferredWorkCell
    Description: 首选作业格；默认 null 表示沿用「设施占地格四邻选最近可通行格」，物品类任务覆写为物品所在格或角色当前格。
    *****/
    public virtual Vector2I? PreferredWorkCell(CharacterSim c) => null;

    /*****
    Date: 2026-09-26
    Name: IsStillValid
    Description: 任务目标是否仍然有效；默认恒 true，物品类任务覆写为「目标堆/背包物品仍在」。
    *****/
    public virtual bool IsStillValid() => true;

    /*****
    Date: 2026-09-26
    Name: StateChanged
    Description: 任务状态变更时触发的事件（参数：任务本体、新状态）。
    *****/
    public event Action<ITask, TaskState>? StateChanged;

    /*****
    Date: 2026-09-26
    Name: RemainingGameMinutes
    Description: 剩余完成所需的游戏分钟数（由子类按各自的进度模型给出：设施作业=倒计时余量；物品类作业=剩余件数 × 每件耗时的估算值）。
    *****/
    public abstract double RemainingGameMinutes { get; }

    /*****
    Date: 2026-09-26
    Name: TotalGameMinutes
    Description: 基准总耗时（游戏分钟；效率加成前的基准值；物品类作业为「计划件数 × 每件耗时」的展示参考值）。
    *****/
    public abstract double TotalGameMinutes { get; }

    /*****
    Date: 2026-09-26
    Name: ProgressFraction
    Description: 进度比例（0~1，供进度条等 UI 使用）；由子类按各自的进度模型给出。
    *****/
    public abstract double ProgressFraction { get; }

    /*****
    Date: 2026-09-26
    Name: StartWork
    Description: 开始执行任务（角色抵达目标后由模拟层调用；Assigned → InProgress）；由子类表达开始时的副作用（设施状态迁移 / 转移阶段切换等）。
    *****/
    public abstract void StartWork();

    /*****
    Date: 2026-09-06
    Name: ProgressWork
    Description: 按给定工人与游戏分钟推进工作进度；任务完成时返回 true（完成与失效的判定由子类表达）。worker 供需要「按人结算」的子类使用（设施作业从推进者自己的背包扣零件）。
    *****/
    public abstract bool ProgressWork(CharacterSim worker, double gameMinutes);

    /*****
    Date: 2026-09-26
    Name: TotalPartsCost
    Description: 全程消耗的备用零件总数；0 表示无消耗。物品类作业恒为 0（不消耗零件），设施作业覆写为实际成本。
    *****/
    public virtual int TotalPartsCost => 0;

    /*****
    Date: 2026-09-26
    Name: PartsConsumed
    Description: 已实际消耗的备用零件数；物品类作业恒为 0，设施作业覆写为实际消耗量。
    *****/
    public virtual int PartsConsumed => 0;

    /*****
    Date: 2026-09-26
    Name: SuspendedByPartsShortage
    Description: 当前挂起是否由「备用零件耗尽」被动触发；物品类作业不消耗零件，恒为 false，设施作业覆写。
    *****/
    public virtual bool SuspendedByPartsShortage => false;

    /*****
    Date: 2026-09-26
    Name: CanOwn
    Description: 归属资格判定：公共任务（Owner 为 null）人人可做；个人任务只有归属者本人可做。供 CanAssign/CanJoin/CanFit 共用。
    *****/
    protected virtual bool CanOwn(CharacterSim c) => Owner == null || ReferenceEquals(c, Owner);

    /*****
    Date: 2026-09-27
    Name: CanTake
    Description: 业务前置判定（认领/加入的「够不够格」条件）；默认恒 true，由子类表达自己的开工门槛——今日唯一实现者是 FacilityWorkTask（要么已带料、要么任务还有未被认领的份额可去取料，见其覆写）。CanAssign / CanJoin / CanFit 三者共用：**抢占查询（CanFit）也必须看它**——否则手上有活的角色会为一条「别人已把料认领完、自己根本插不上手」的高优先任务白白丢掉当前工作。
    *****/
    protected virtual bool CanTake(CharacterSim c) => true;

    /*****
    Date: 2026-09-26
    Name: CanAssign
    Description: 空闲角色可认领处于 Pending 状态的本任务（个人任务另需为归属者本人；子类业务前置由 CanTake 表达）。
    *****/
    public bool CanAssign(CharacterSim c)
        => State == TaskState.Pending && c.State == CharacterState.Idle && CanOwn(c) && CanTake(c);

    /*****
    Date: 2026-09-26
    Name: CanJoin
    Description: 判断角色当前是否可加入本任务：任务已认领（Assigned，首人尚在途中）或进行中（InProgress）、未达 MaxWorkers、角色空闲且未在列、具备归属资格与业务前置。纯查询不改状态。
    *****/
    public bool CanJoin(CharacterSim c)
        => State is TaskState.Assigned or TaskState.InProgress
           && _workers.Count < _maxWorkers
           && c.State == CharacterState.Idle
           && !_workers.Contains(c)
           && CanOwn(c)
           && CanTake(c);

    /*****
    Date: 2026-09-26
    Name: CanFit
    Description: 忽略「角色必须空闲」前置条件的接手可行性（供抢占判定）：Pending 且尚无人工作者可被认领；Assigned/InProgress 且未满员、本人不在列者可加入；其余状态不可接手。归属资格与业务前置（CanTake）照旧校验。
    *****/
    public bool CanFit(CharacterSim c) => State switch
    {
        TaskState.Pending => _workers.Count == 0 && CanOwn(c) && CanTake(c),
        TaskState.Assigned or TaskState.InProgress => _workers.Count < _maxWorkers && !_workers.Contains(c) && CanOwn(c) && CanTake(c),
        _ => false,
    };

    /*****
    Date: 2026-09-26
    Name: MarkAssigned
    Description: 标记任务被指定角色认领（Pending → Assigned）；认领人入工作者列表并成为 Assignee（首人）。
    *****/
    public void MarkAssigned(CharacterSim assignee)
    {
        if (State != TaskState.Pending) return;
        if (!_workers.Contains(assignee)) _workers.Add(assignee);
        Assignee ??= assignee;
        SetState(TaskState.Assigned);
    }

    /*****
    Date: 2026-09-26
    Name: TryJoin
    Description: 空闲角色尝试加入任务（多人协作）；经 CanJoin 校验后登记入列——若首人已脱离（如中途去备料），**由首位入列者继任 Assignee**（否则「以主工背包为准扣料」等逻辑会读到 null）；不改任务状态（角色 CurrentTask 由任务板在 PickFor 中调用 OnTaskAssigned 完成）。
    *****/
    public bool TryJoin(CharacterSim c)
    {
        if (!CanJoin(c)) return false;
        _workers.Add(c);
        Assignee ??= c;
        return true;
    }

    /*****
    Date: 2026-09-26
    Name: ReleaseWorker
    Description: 让指定角色脱离任务（如加入后寻路失败）：移出工作者列表并经 OnTaskCancelled 清理其任务引用（置 Interrupted）；任务与其他工作者不受影响。脱离者为首人时 Assignee 顺延为列表新首人。
    *****/
    public void ReleaseWorker(CharacterSim c)
    {
        if (!_workers.Remove(c)) return;
        if (ReferenceEquals(Assignee, c))
        {
            Assignee = _workers.Count > 0 ? _workers[0] : null;
        }
        c.OnTaskCancelled(this);
    }

    /*****
    Date: 2026-09-26
    Name: MarkCancelled
    Description: 取消任务（未结状态 → Cancelled；Done / Cancelled 终态忽略）。StateChanged 事件触发时工作者列表仍完整（任务板在事件中遍历释放全部工作者），事件返回后清空列表与 Assignee 并调用 OnCancelled 钩子（设施作业据此回滚设施状态）。
    *****/
    public void MarkCancelled()
    {
        if (State is TaskState.Done or TaskState.Cancelled) return;
        SetState(TaskState.Cancelled);
        _workers.Clear();
        Assignee = null;
        OnCancelled();
    }

    /*****
    Date: 2026-09-26
    Name: Suspend
    Description: 中止（挂起）任务：未结状态 → Suspended。与 MarkCancelled 同一约定——StateChanged 事件触发时工作者列表仍完整（任务板在事件中释放全部工作者），事件返回后清空列表与 Assignee。**不调用 OnCancelled**，故目标状态与已积累的进度保留；恢复时由 Resume 回到 Pending 重新排队。
    *****/
    public void Suspend()
    {
        if (State is TaskState.Done or TaskState.Cancelled or TaskState.Suspended) return;
        SetState(TaskState.Suspended);
        _workers.Clear();
        Assignee = null;
    }

    /*****
    Date: 2026-09-26
    Name: Resume
    Description: 恢复挂起的任务（Suspended → Pending）；任务板在状态事件中把它重新放回待处理队列，并由调用方（TaskBoard.Resume）补发提交事件触发空闲人力派工。非挂起状态忽略。
    *****/
    public virtual void Resume()
    {
        if (State != TaskState.Suspended) return;
        SetState(TaskState.Pending);
    }

    /*****
    Date: 2026-09-26
    Name: OnCancelled
    Description: 钩子；任务被取消且工作者已清空后调用（子类在此回滚目标状态）。
    *****/
    protected virtual void OnCancelled()
    {
    }

    /*****
    Date: 2026-09-26
    Name: SetState
    Description: 迁移任务状态并触发 StateChanged 事件。
    *****/
    protected void SetState(TaskState newState)
    {
        if (State == newState) return;
        State = newState;
        StateChanged?.Invoke(this, newState);
    }
}