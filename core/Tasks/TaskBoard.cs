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
    Date: 2026-09-06
    Name: TaskBoard
    Description: 构造函数；注入事件总线。
    *****/
    public TaskBoard(IEventBus bus) => _bus = bus;

    /*****
    Date: 2026-09-06
    Name: Submit
    Description: 提交任务到任务板（置入待处理队列并发布 TaskSubmittedEvent）；非 Pending 状态或已存在同目标未结任务时忽略。
    *****/
    public void Submit(ITask task)
    {
        if (task.State != TaskState.Pending) return;
        bool duplicate = _active.Exists(t =>
            ReferenceEquals(t.Target, task.Target) &&
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
    Date: 2026-09-06
    Name: PickFor
    Description: 为指定角色按提交顺序选取第一个可认领的任务：标记 Assigned、写入角色 CurrentTask 并移出待处理队列；无合适任务返回 null。
    *****/
    public ITask? PickFor(CharacterSim c)
    {
        ITask? task = null;
        foreach (ITask candidate in _pending)
        {
            if (candidate.CanAssign(c))
            {
                task = candidate;
                break;
            }
        }
        if (task == null) return null;

        _pending.Remove(task);
        task.MarkAssigned(c);
        c.OnTaskAssigned(task);
        return task;
    }

    /*****
    Date: 2026-09-06
    Name: OnTaskStateChanged
    Description: 任务状态变更回调：发布 TaskStateChangedEvent；进入终态（Done/Cancelled）时清理任务板记录并释放认领角色。
    *****/
    private void OnTaskStateChanged(ITask task, TaskState newState)
    {
        _bus.Publish(new TaskStateChangedEvent(task, newState));
        if (newState is not (TaskState.Done or TaskState.Cancelled)) return;

        _active.Remove(task);
        _pending.Remove(task);
        task.StateChanged -= OnTaskStateChanged;
        if (newState == TaskState.Cancelled && task.Assignee is { } assignee)
        {
            assignee.OnTaskCancelled(task);
        }
    }
}
