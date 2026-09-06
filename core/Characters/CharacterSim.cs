using Godot;
using RelayStation.Core.Tasks;

namespace RelayStation.Core.Characters;

/*****
Date: 2026-09-06
Name: CharacterSim
Description: 角色模拟对象；持有角色定义、行为状态、所在格、当前任务、寻路路径与效率倍率等运行时数据。状态迁移由 Simulation 驱动，表现层只读并订阅 StateChanged 事件。
*****/
public sealed class CharacterSim
{
    /*****
    Date: 2026-09-06
    Name: Def
    Description: 角色的静态定义（出身、专长、特质等）；引擎外单元测试等场景可为 null。
    *****/
    public CharacterDef? Def { get; }

    /*****
    Date: 2026-09-06
    Name: State
    Description: 角色当前行为状态。
    *****/
    public CharacterState State { get; internal set; } = CharacterState.Idle;

    /*****
    Date: 2026-09-06
    Name: Cell
    Description: 角色当前所在格子坐标。
    *****/
    public Vector2I Cell { get; internal set; }

    /*****
    Date: 2026-09-06
    Name: CurrentTask
    Description: 角色当前正在执行的任务；空闲时为 null。
    *****/
    public ITask? CurrentTask { get; internal set; }

    /*****
    Date: 2026-09-06
    Name: WorkSpeedMultiplier
    Description: 工作效率倍率；取自角色专长倍率（Def 缺省时为 1），对专长匹配的任务生效。
    *****/
    public float WorkSpeedMultiplier { get; }

    /*****
    Date: 2026-09-06
    Name: Path
    Description: 当前寻路路径（含起点与终点）；不在移动状态时为 null。
    *****/
    public IReadOnlyList<Vector2I>? Path { get; internal set; }

    /*****
    Date: 2026-09-06
    Name: PathIndex
    Description: 路径中下一步的下标（Cell 已抵达 Path[PathIndex-1]）。
    *****/
    public int PathIndex { get; internal set; } = 1;

    /*****
    Date: 2026-09-06
    Name: CellProgress
    Description: 从当前格向下一格移动的插值进度（0~1），供表现层平滑插值。
    *****/
    public float CellProgress { get; internal set; }

    /*****
    Date: 2026-09-06
    Name: StateChanged
    Description: 角色行为状态变更时触发的事件（参数：角色本体、新状态）。
    *****/
    public event Action<CharacterSim, CharacterState>? StateChanged;

    /*****
    Date: 2026-09-06
    Name: CharacterSim
    Description: 构造函数；以指定定义与出生格创建角色（初始为 Idle 状态）。
    *****/
    public CharacterSim(CharacterDef? def, Vector2I spawnCell)
    {
        Def = def;
        Cell = spawnCell;
        WorkSpeedMultiplier = def?.SpecialtyMultiplier ?? 1f;
    }

    /*****
    Date: 2026-09-06
    Name: GetWorkMultiplierFor
    Description: 计算执行指定专长类任务的实际效率倍率；角色专长与任务专长匹配时返回 WorkSpeedMultiplier，否则返回 1。
    *****/
    public float GetWorkMultiplierFor(Specialty taskSpecialty)
        => Def != null && Def.Specialty == taskSpecialty ? WorkSpeedMultiplier : 1f;

    /*****
    Date: 2026-09-06
    Name: BeginMove
    Description: 携带寻路结果进入移动状态（由 Simulation 在认领任务并寻路成功后调用）。
    *****/
    internal void BeginMove(IReadOnlyList<Vector2I> path)
    {
        Path = path;
        PathIndex = 1;
        CellProgress = 0f;
        SetState(CharacterState.Moving);
    }

    /*****
    Date: 2026-09-06
    Name: OnTaskAssigned
    Description: 记录被认领的任务（由 TaskBoard.PickFor 调用）。
    *****/
    internal void OnTaskAssigned(ITask task) => CurrentTask = task;

    /*****
    Date: 2026-09-06
    Name: OnTaskCancelled
    Description: 任务被取消时清理角色身上的任务引用与路径，进入 Interrupted（下一帧回 Idle）。
    *****/
    internal void OnTaskCancelled(ITask task)
    {
        if (!ReferenceEquals(CurrentTask, task)) return;
        CurrentTask = null;
        Path = null;
        SetState(CharacterState.Interrupted);
    }

    /*****
    Date: 2026-09-06
    Name: SetState
    Description: 迁移行为状态；状态实际发生变化时触发 StateChanged 事件。
    *****/
    internal void SetState(CharacterState newState)
    {
        if (State == newState) return;
        State = newState;
        StateChanged?.Invoke(this, newState);
    }
}
