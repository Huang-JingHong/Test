using Godot;
using RelayStation.Core.Facilities;
using RelayStation.Core.Tasks;

namespace RelayStation.Core.Characters;

/*****
Date: 2026-09-06
Name: CharacterSim
Description: 角色模拟对象；持有角色定义、行为状态、所在格、当前任务、寻路路径与效率倍率等运行时数据。状态迁移由 Simulation 驱动，表现层只读并订阅 StateChanged 事件。
2026-09-26 增补「人物中止」支持：被中止时把当时手上的任务实例加入忽略清单，认领择优（TaskBoard.PickFor）据此跳过这些任务，直到玩家对该设施重新下达指令（Simulation.ClearIgnoredTasksFor）；紧急任务不受忽略影响。
*****/
public sealed class CharacterSim
{
    /*****
    Date: 2026-09-26
    Name: _ignoredTasks
    Description: 被本角色「暂时忽略」的任务实例（人物中止的那一刻手上的任务）；只按实例比对——同一设施重新发布任务是新实例，不受影响。
    *****/
    private readonly HashSet<ITask> _ignoredTasks = new();

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
    Date: 2026-09-26
    Name: MaxHealth
    Description: 生命值上限（0~100）；当前为固定常量，后续如需按体质/装备差异化再迁到 CharacterDef。
    *****/
    public const float MaxHealth = 100f;

    /*****
    Date: 2026-09-26
    Name: Health
    Description: 当前生命值（0~MaxHealth），初始满值；本阶段仅数值与 UI 显示，伤害来源与死亡结算后续接入（写入方为存档恢复与 ApplyHealthDelta）。
    *****/
    public float Health { get; internal set; } = MaxHealth;

    /*****
    Date: 2026-09-26
    Name: ApplyHealthDelta
    Description: 施加生命值增减（正数治疗、负数受伤），结果夹在 0~MaxHealth 之间；供后续伤害/治疗系统与测试使用。
    *****/
    public void ApplyHealthDelta(float delta)
        => Health = Math.Clamp(Health + delta, 0f, MaxHealth);

    /*****
    Date: 2026-09-26
    Name: Inventory
    Description: 角色背包与装备；容量按「基础负重（Def.BaseCarryCapacityKg，缺省 15kg）+ 已装备容器容量」校验，只管重量不涉及体积。构造时为空背包（无物品、无装备），内容经 Simulation 的写入口或存档恢复填充。
    *****/
    public CharacterInventory Inventory { get; }

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
    Date: 2026-09-26
    Name: IsManualMove
    Description: 本次移动是否由玩家手动下达（右键点地）；为 true 时抵达终点后回 Idle 重新找活，而非进入作业。
    *****/
    public bool IsManualMove { get; internal set; }

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
        Inventory = new CharacterInventory(def?.BaseCarryCapacityKg ?? CharacterDef.DefaultBaseCarryCapacityKg);
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
        IsManualMove = false; // 缺省为任务驱动；手动移动由 Simulation.OrderMove 在调用后置位
        SetState(CharacterState.Moving);
    }

    /*****
    Date: 2026-09-26
    Name: IsIgnoring
    Description: 本角色当前是否把指定任务实例列入「暂时忽略」（人物中止的产物）；由 TaskBoard.PickFor 在择优时查询（紧急任务另作 bypass）。
    *****/
    public bool IsIgnoring(ITask task) => _ignoredTasks.Contains(task);

    /*****
    Date: 2026-09-26
    Name: IgnoreTask
    Description: 把任务实例加入忽略清单（由 Simulation 在人物中止时调用）。
    *****/
    internal void IgnoreTask(ITask task) => _ignoredTasks.Add(task);

    /*****
    Date: 2026-09-26
    Name: ClearIgnoredTasks
    Description: 清空忽略清单（人物每次中止时先清后加，避免忽略项无限累积）。
    *****/
    internal void ClearIgnoredTasks() => _ignoredTasks.Clear();

    /*****
    Date: 2026-09-26
    Name: ClearIgnoredTasksFor
    Description: 清除忽略清单中目标为指定设施的任务（玩家对该设施重新下达指令时调用），使该任务重新参与优先级排队。
    *****/
    internal void ClearIgnoredTasksFor(FacilitySim target)
        => _ignoredTasks.RemoveWhere(t => ReferenceEquals(t.Target, target));

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
