using RelayStation.Core.Characters;
using RelayStation.Core.Facilities;

namespace RelayStation.Core.Tasks;

/*****
Date: 2026-09-06
Name: ITask
Description: 任务接口；描述一个可被角色认领并执行的工作单元。在计划 §8 草案基础上补充了生命周期驱动方法（MarkAssigned / MarkCancelled / StartWork / ProgressWork）与 Assignee / RequiredSpecialty / StateChanged 成员（2026-09-06 实现期修订，见计划文档 §8 变更记录）。
*****/
public interface ITask
{
    /*****
    Date: 2026-09-06
    Name: State
    Description: 任务当前状态。
    *****/
    TaskState State { get; }

    /*****
    Date: 2026-09-06
    Name: Target
    Description: 任务目标设施；无目标时返回 null。
    *****/
    FacilitySim? Target { get; }

    /*****
    Date: 2026-09-06
    Name: Assignee
    Description: 当前认领本任务的角色；未被认领时为 null。
    *****/
    CharacterSim? Assignee { get; }

    /*****
    Date: 2026-09-06
    Name: RemainingGameMinutes
    Description: 剩余完成所需的游戏分钟数。
    *****/
    double RemainingGameMinutes { get; }

    /*****
    Date: 2026-09-06
    Name: RequiredSpecialty
    Description: 任务所属专长类型（决定角色专长效率加成是否匹配）；无专长限定时为 null。
    *****/
    Specialty? RequiredSpecialty { get; }

    /*****
    Date: 2026-09-06
    Name: StateChanged
    Description: 任务状态变更时触发的事件（参数：任务本体、新状态）。
    *****/
    event Action<ITask, TaskState>? StateChanged;

    /*****
    Date: 2026-09-06
    Name: CanAssign
    Description: 判断指定角色是否可被指派执行本任务。
    *****/
    bool CanAssign(CharacterSim c);

    /*****
    Date: 2026-09-06
    Name: MarkAssigned
    Description: 标记任务被指定角色认领（Pending → Assigned）。
    *****/
    void MarkAssigned(CharacterSim assignee);

    /*****
    Date: 2026-09-06
    Name: MarkCancelled
    Description: 取消任务（仅未结状态可取消，终态忽略）。
    *****/
    void MarkCancelled();

    /*****
    Date: 2026-09-06
    Name: StartWork
    Description: 开始执行任务（角色抵达目标后调用；Assigned → InProgress，目标设施进入维修中）。
    *****/
    void StartWork();

    /*****
    Date: 2026-09-06
    Name: ProgressWork
    Description: 按给定游戏分钟推进工作进度；剩余量归零时任务完成并返回 true。
    *****/
    bool ProgressWork(double gameMinutes);
}
