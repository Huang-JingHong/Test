using RelayStation.Core.Characters;
using RelayStation.Core.Events;

namespace RelayStation.Core.Tasks;

/*****
Date: 2026-09-06
Name: TaskBoard
Description: 任务板默认实现；管理待处理队列与未结任务集合，负责提交（同目标去重）、取消（释放认领角色）与按提交顺序认领，并通过事件总线发布任务提交/状态变更事件。
*****/
public sealed class TaskBoard : ITaskBoard
{
    /*****
    Date: 2026-09-06
    Name: _bus
    Description: 事件总线（发布任务相关事件）。
    *****/
    private readonly IEventBus _bus;

    /*****
    Date: 2026-09-06
    Name: _pending
    Description: 待处理任务队列（按提交顺序）。
    *****/
    private readonly List<ITask> _pending = new();

    /*****
    Date: 2026-09-06
    Name: _active
    Description: 所有未结任务（含已认领/执行中），终态后移除。
    *****/
    private readonly List<ITask> _active = new();

    /*****
    Date: 2026-09-06
    Name: Pending
    Description: 当前待处理（未被认领）的任务列表。
    *****/
    public IReadOnlyList<ITask> Pending => _pending;

    /*****
    Date: 2026-09-25
    Name: Active
    Description: 全部未结任务列表（含待处理/已认领/执行中）；终态后移除。
    *****/
    public IReadOnlyList<ITask> Active => _active;

    /*****
    Date: 2026-09-06
    Name: TaskBoard
    Description: 构造函数；注入事件总线。
    *****/
    public TaskBoard(IEventBus bus) => _bus = bus;

    /*****
    Date: 2026-09-06
    Name: Submit
    Description: 提交任务到任务板（置入待处理队列并发布 TaskSubmittedEvent）；非 Pending 状态或已存在**同 Owner + 同设施目标 + 同物品目标**的未结任务时忽略。2026-09-26 去重键由「同 Target」扩展为三元组：设施任务（Owner=null）行为与旧规则逐字等价；个人任务（Owner=角色）按人各存一份（两名角色可各自去同一堆取用），同一人同一目标只留一条。
    *****/
    public void Submit(ITask task)
    {
        if (task.State != TaskState.Pending) return;
        bool duplicate = _active.Exists(t =>
            ReferenceEquals(t.Owner, task.Owner) &&
            ReferenceEquals(t.Target, task.Target) &&
            ReferenceEquals(t.ItemTarget, task.ItemTarget) &&
            t.State is TaskState.Pending or TaskState.Assigned or TaskState.InProgress);
        if (duplicate) return;

        _active.Add(task);
        _pending.Add(task);
        task.StateChanged += OnTaskStateChanged;
        _bus.Publish(new TaskSubmittedEvent(task));
    }

    /*****
    Date: 2026-09-06
    Name: Cancel
    Description: 取消指定任务；状态流转经 StateChanged 事件联动完成列表清理与认领角色释放。
    *****/
    public void Cancel(ITask task) => task.MarkCancelled();

    /*****
    Date: 2026-09-26
    Name: Resume
    Description: 恢复已中止（挂起）的任务：任务回到 Pending 并由状态事件重新入队（见 OnTaskStateChanged），随后补发 TaskSubmittedEvent 使空闲人力当帧重新投入（与 Submit 同一派工路径）；非挂起状态忽略。
    *****/
    public void Resume(ITask task)
    {
        if (task.State != TaskState.Suspended) return;
        task.Resume();
        _bus.Publish(new TaskSubmittedEvent(task));
    }

    /*****
    Date: 2026-09-25
    Name: PickFor
    Description: 为指定角色择优选取任务（参考《缺氧》任务优先度）：在全部可认领来源——待处理新任务（CanAssign）与已认领/进行中且未满员的多人维修任务（CanJoin）——中按「优先级数值高者优先，同优先级按提交先后」选取，随后提交（新任务走认领路径并出队、既有任务走加入路径）并写入角色 CurrentTask。无合适任务返回 null。
    跳过角色因「人物中止」而暂时忽略的任务实例（CharacterSim.IsIgnoring）；但**紧急（Urgent）不受忽略影响**——小人只要没死就得干（将来接入需求惩罚时同样 bypass）。
    *****/
    public ITask? PickFor(CharacterSim c)
    {
        ITask? best = FindBest(c, assumeFree: false);
        if (best == null) return null;

        if (best.State == TaskState.Pending)
        {
            _pending.Remove(best);
            best.MarkAssigned(c);
        }
        else
        {
            best.TryJoin(c);
        }
        c.OnTaskAssigned(best);
        return best;
    }

    /*****
    Date: 2026-09-26
    Name: PeekFor
    Description: 纯查询版择优：返回指定角色「若腾出手来」可以接手的最优任务（按 CanFit，忽略其当前是否空闲），不改任何状态、不写入角色；供 Simulation 的「抢占」判定比较优先级。无合适任务返回 null。
    *****/
    public ITask? PeekFor(CharacterSim c) => FindBest(c, assumeFree: true);

    /*****
    Date: 2026-09-26
    Name: FindBest
    Description: 择优共用实现：单次遍历 `_active`（保持提交顺序，同时覆盖「待处理新任务」与「已认领/进行中可加入任务」两类来源），跳过角色因「人物中止」而忽略的任务实例（**紧急除外**——小人只要没死就得干），按「优先级数值高者优先，严格大于故同数值保留更早提交者」取最优。assumeFree 为 true 时用 `CanFit`（忽略角色空闲前置条件，供抢占查询），否则用 CanAssign/CanJoin（供实际认领）。
    *****/
    private ITask? FindBest(CharacterSim c, bool assumeFree)
    {
        ITask? best = null;
        foreach (ITask candidate in _active)
        {
            if (candidate.Priority != TaskPriority.Urgent && c.IsIgnoring(candidate)) continue;
            bool available = assumeFree
                ? candidate.CanFit(c)
                : candidate.State == TaskState.Pending ? candidate.CanAssign(c) : candidate.CanJoin(c);
            if (!available) continue;
            if (best == null || candidate.Priority > best.Priority) best = candidate;
        }
        return best;
    }

    /*****
    Date: 2026-09-06
    Name: OnTaskStateChanged
    Description: 任务状态变更回调：发布 TaskStateChangedEvent；进入终态（Done/Cancelled）时清理任务板记录并释放认领角色。
    2026-09-26 增补两条分支：①Pending（仅由 Resume 触发）→ 重新入队待处理（任务仍在 _active，故不重复登记、不重复订阅、不再发提交事件——提交事件由 TaskBoard.Resume 补发）；②Suspended → 退出待处理队列并释放全部工作者（与 Cancel 同一约定），但任务保留在 _active（可恢复），已耗零件与进度由任务自身保留。
    *****/
    private void OnTaskStateChanged(ITask task, TaskState newState)
    {
        _bus.Publish(new TaskStateChangedEvent(task, newState));

        if (newState == TaskState.Pending)
        {
            if (!_pending.Contains(task)) _pending.Add(task);
            return;
        }

        if (newState == TaskState.Suspended)
        {
            _pending.Remove(task);
            foreach (CharacterSim worker in task.Workers)
            {
                worker.OnTaskCancelled(task);
            }
            return;
        }

        if (newState is not (TaskState.Done or TaskState.Cancelled)) return;

        _active.Remove(task);
        _pending.Remove(task);
        task.StateChanged -= OnTaskStateChanged;
        if (newState == TaskState.Cancelled)
        {
            // 释放全部工作者（含首人 Assignee）；OnTaskCancelled 有 ReferenceEquals 守卫，未参与或已脱离者自动跳过
            foreach (CharacterSim worker in task.Workers)
            {
                worker.OnTaskCancelled(task);
            }
        }
    }
}
