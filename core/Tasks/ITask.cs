using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Facilities;
using RelayStation.Core.Items;

namespace RelayStation.Core.Tasks;

/*****
Date: 2026-09-06
Name: ITask
Description: 任务接口；描述一个可被角色认领并执行的工作单元。在计划 §8 草案基础上补充了生命周期驱动方法（MarkAssigned / MarkCancelled / StartWork / ProgressWork）与 Assignee / RequiredSpecialty / StateChanged 成员（2026-09-06 实现期修订）；2026-09-25 补充进度与实时消耗成员（TotalGameMinutes / ProgressFraction / TotalPartsCost / PartsConsumed，见计划文档 §8 变更记录）；2026-09-26 补充物品目标与个人任务成员（ItemTarget / Owner / PreferredWorkCell / IsStillValid，见《中继站开发计划_物品拾取与消费_装备穿脱_v0.1.md》§2）。
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
    Date: 2026-09-26
    Name: ItemTarget
    Description: 任务目标物品堆（拾取 / 取用 / 穿脱类任务）；设施任务与「就地」任务（如消耗背包内物品）返回 null。
    *****/
    ItemStack? ItemTarget { get; }

    /*****
    Date: 2026-09-26
    Name: Owner
    Description: 任务归属者：**个人任务**返回本人（只有该角色可认领/加入——玩家对某人下的指令、需求驱动的自主取用）；**公共任务**返回 null（设施作业，任一空闲角色可认领）。任务板以「Owner + 设施目标 + 物品目标」三元组去重。
    *****/
    CharacterSim? Owner { get; }

    /*****
    Date: 2026-09-26
    Name: PreferredWorkCell
    Description: 首选作业格：非空表示直接使用该格（物品任务 = 物品所在格；就地任务 = 角色当前格，模拟层不寻路）；null 表示沿用「目标设施占地格四邻中选最近可通行格」的惯例。
    *****/
    Vector2I? PreferredWorkCell(CharacterSim c);

    /*****
    Date: 2026-09-26
    Name: IsStillValid
    Description: 任务目标是否仍然有效（物品任务：目标堆仍在场上且数量 > 0；设施任务恒 true）。模拟层在认领时与作业推进前校验；失效即取消任务（表现层可据此提示），由角色当帧重新找活。
    *****/
    bool IsStillValid();

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
    Date: 2026-09-25
    Name: TotalGameMinutes
    Description: 任务基准总耗时（游戏分钟；效率加成前的基准值）。
    *****/
    double TotalGameMinutes { get; }

    /*****
    Date: 2026-09-25
    Name: ProgressFraction
    Description: 任务进度比例（0~1，按剩余量反推；供进度条等 UI 使用）。
    *****/
    double ProgressFraction { get; }

    /*****
    Date: 2026-09-25
    Name: TotalPartsCost
    Description: 任务全程消耗的备用零件总数；0 表示无消耗。
    *****/
    int TotalPartsCost { get; }

    /*****
    Date: 2026-09-25
    Name: PartsConsumed
    Description: 已实际消耗的备用零件数（随进度实时累计）。
    *****/
    int PartsConsumed { get; }

    /*****
    Date: 2026-09-25
    Name: Workers
    Description: 当前参与本任务的角色列表（多人维修；首人为 Assignee）。
    *****/
    IReadOnlyList<CharacterSim> Workers { get; }

    /*****
    Date: 2026-09-25
    Name: MaxWorkers
    Description: 本任务允许的最大投入人数（取自设施定义；1 为单人现状）。
    *****/
    int MaxWorkers { get; }

    /*****
    Date: 2026-09-25
    Name: Priority
    Description: 任务优先级（参考《缺氧》任务优先度）；任务板在全部可认领来源中择优时以此为首要排序依据。
    *****/
    TaskPriority Priority { get; }

    /*****
    Date: 2026-09-25
    Name: CanJoin
    Description: 判断指定角色当前是否可加入本任务（已认领/进行中且未达人数上限、角色空闲且未在列）；纯查询不改状态，供任务板择优后提交 TryJoin。
    *****/
    bool CanJoin(CharacterSim c);

    /*****
    Date: 2026-09-25
    Name: TryJoin
    Description: 空闲角色尝试加入进行中的任务（仅 InProgress 且未达 MaxWorkers 时成功）；成功仅登记，不改任务状态（角色 CurrentTask 由任务板写入）。
    *****/
    bool TryJoin(CharacterSim c);

    /*****
    Date: 2026-09-25
    Name: ReleaseWorker
    Description: 让指定角色脱离任务（如加入后寻路失败）：移出工作者列表并清理其任务引用（经 OnTaskCancelled 置 Interrupted）；任务本身不受影响。
    *****/
    void ReleaseWorker(CharacterSim c);

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
    Date: 2026-09-26
    Name: CanFit
    Description: 判断指定角色在**不考虑其当前行为状态**（视为空闲）时能否接手本任务；供「抢占」判定（`ITaskBoard.PeekFor`）使用——CanAssign/CanJoin 含「角色必须空闲」前置条件，无法回答「这个忙人若腾出手来能否做这件事」。
    *****/
    bool CanFit(CharacterSim c);

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
    Date: 2026-09-26
    Name: Suspend
    Description: 中止（挂起）任务——保留进度与目标设施状态、释放全部工作者、退出排队；已挂起/终态忽略。恢复用 Resume。
    *****/
    void Suspend();

    /*****
    Date: 2026-09-26
    Name: Resume
    Description: 恢复已挂起的任务（Suspended → Pending，回到待处理队列重新参与优先级排队）；非挂起状态忽略。
    *****/
    void Resume();

    /*****
    Date: 2026-09-26
    Name: SuspendedByPartsShortage
    Description: 当前挂起是否由「备用零件耗尽」被动触发（区别于玩家主动中止）；供 UI 说明中止原因。恢复后清除。
    *****/
    bool SuspendedByPartsShortage { get; }

    /*****
    Date: 2026-09-06
    Name: StartWork
    Description: 开始执行任务（角色抵达目标后调用；Assigned → InProgress，目标设施进入维修中）。
    *****/
    void StartWork();

    /*****
    Date: 2026-09-06
    Name: ProgressWork
    Description: 按给定游戏分钟推进工作进度；worker 为**正在推进的那名工人**（设施作业据此「谁推进就从谁的背包扣料」，多名工人各自结算自己那一份；单人任务可忽略该参数）。剩余量归零时任务完成并返回 true。
    *****/
    bool ProgressWork(CharacterSim worker, double gameMinutes);
}
