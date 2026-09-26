using RelayStation.Core.Characters;

namespace RelayStation.Core.Tasks;

/*****
Date: 2026-09-06
Name: ITaskBoard
Description: 任务板接口；管理待处理任务并负责将任务分配给角色。
*****/
public interface ITaskBoard
{
    /*****
    Date: 2026-09-06
    Name: Pending
    Description: 当前待处理（未被认领）的任务列表。
    *****/
    IReadOnlyList<ITask> Pending { get; }

    /*****
    Date: 2026-09-25
    Name: Active
    Description: 全部未结任务列表（含待处理/已认领/执行中）；终态后移除。供表现层查询同目标任务的投入人力等。
    *****/
    IReadOnlyList<ITask> Active { get; }

    /*****
    Date: 2026-09-06
    Name: Submit
    Description: 提交一个新任务到任务板；若已存在同目标的未结任务则忽略（防重复派单）。
    *****/
    void Submit(ITask task);

    /*****
    Date: 2026-09-06
    Name: Cancel
    Description: 取消指定任务；已认领的任务会同时释放认领角色。
    *****/
    void Cancel(ITask task);

    /*****
    Date: 2026-09-26
    Name: Resume
    Description: 恢复被中止（挂起）的任务：回到待处理队列重新排队，并即刻触发空闲人力派工；非挂起状态忽略。
    *****/
    void Resume(ITask task);

    /*****
    Date: 2026-09-06
    Name: PickFor
    Description: 为指定角色选取一个任务；阶段一为按提交顺序的显式认领，阶段二扩展为需求+优先级自动选择。
    *****/
    ITask? PickFor(CharacterSim c);

    /*****
    Date: 2026-09-26
    Name: PeekFor
    Description: 纯查询版择优（不改任何状态、不写入角色）：返回指定角色**若腾出手来**可以接手的最优任务（`ITask.CanFit` 忽略角色当前是否空闲）；供「抢占」判定使用——忙人当前任务优先级低于此值时才值得打断。无合适任务返回 null。
    *****/
    ITask? PeekFor(CharacterSim c);
}
