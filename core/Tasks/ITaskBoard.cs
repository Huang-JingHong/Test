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
    Date: 2026-09-06
    Name: PickFor
    Description: 为指定角色选取一个任务；阶段一为按提交顺序的显式认领，阶段二扩展为需求+优先级自动选择。
    *****/
    ITask? PickFor(CharacterSim c);
}
