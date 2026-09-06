using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Events;
using RelayStation.Core.Facilities;
using RelayStation.Core.Map;
using RelayStation.Core.Tasks;
using RelayStation.Core.Time;

namespace RelayStation.Core.Common;

/*****
Date: 2026-09-06
Name: Simulation
Description: 模拟核心组装类；持有时钟、地图、寻路、任务板、需求系统与角色/设施集合，每帧驱动角色行为状态机（认领 → 寻路 → 移动 → 作业 → 完成）与需求衰减，并把角色/设施状态事件转发到事件总线。纯 C# 组装，可脱离引擎运行。
*****/
public sealed class Simulation
{
    /*****
    Date: 2026-09-06
    Name: WalkCellsPerGameMinute
    Description: 角色移动速度（格 / 游戏分钟）。
    *****/
    public const float WalkCellsPerGameMinute = 3f;

    /*****
    Date: 2026-09-06
    Name: NeighborOffsets
    Description: 四方向相邻偏移（用于查找设施旁的作业格）。
    *****/
    private static readonly Vector2I[] NeighborOffsets =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
    };

    /*****
    Date: 2026-09-06
    Name: _characters
    Description: 角色列表。
    *****/
    private readonly List<CharacterSim> _characters = new();

    /*****
    Date: 2026-09-06
    Name: _facilities
    Description: 设施列表。
    *****/
    private readonly List<FacilitySim> _facilities = new();

    /*****
    Date: 2026-09-06
    Name: _facilityHandlers
    Description: 设施到其状态事件转发订阅的映射表；使重复加入/移除（编辑器撤销）时订阅严格配对，避免状态事件重复发布。
    *****/
    private readonly Dictionary<FacilitySim, Action<FacilitySim, FacilityState>> _facilityHandlers = new();

    /*****
    Date: 2026-09-06
    Name: Clock
    Description: 游戏时钟。
    *****/
    public IGameClock Clock { get; }

    /*****
    Date: 2026-09-06
    Name: Map
    Description: 格子地图。
    *****/
    public IGridMap Map { get; }

    /*****
    Date: 2026-09-06
    Name: Pathfinding
    Description: 寻路服务。
    *****/
    public IPathfinding Pathfinding { get; }

    /*****
    Date: 2026-09-06
    Name: TaskBoard
    Description: 任务板。
    *****/
    public ITaskBoard TaskBoard { get; }

    /*****
    Date: 2026-09-06
    Name: NeedSystem
    Description: 需求系统（占位）。
    *****/
    public INeedSystem NeedSystem { get; }

    /*****
    Date: 2026-09-06
    Name: EventBus
    Description: 事件总线。
    *****/
    public IEventBus EventBus { get; }

    /*****
    Date: 2026-09-06
    Name: Characters
    Description: 角色列表（只读）。
    *****/
    public IReadOnlyList<CharacterSim> Characters => _characters;

    /*****
    Date: 2026-09-06
    Name: Facilities
    Description: 设施列表（只读）。
    *****/
    public IReadOnlyList<FacilitySim> Facilities => _facilities;

    /*****
    Date: 2026-09-06
    Name: Simulation
    Description: 构造函数；组装各服务并接线：时钟分钟事件 → 需求衰减；角色/设施状态事件 → 事件总线转发。
    *****/
    public Simulation(IGameClock clock, IGridMap map, IPathfinding pathfinding,
        ITaskBoard taskBoard, INeedSystem needSystem, IEventBus eventBus)
    {
        Clock = clock;
        Map = map;
        Pathfinding = pathfinding;
        TaskBoard = taskBoard;
        NeedSystem = needSystem;
        EventBus = eventBus;
        clock.GameMinuteElapsed += OnGameMinuteElapsed;
    }

    /*****
    Date: 2026-09-06
    Name: AddCharacter
    Description: 加入一名角色到模拟；接线其状态变更事件转发到事件总线。
    *****/
    public void AddCharacter(CharacterSim c)
    {
        _characters.Add(c);
        c.StateChanged += (character, newState) =>
            EventBus.Publish(new CharacterStateChangedEvent(character, newState));
    }

    /*****
    Date: 2026-09-06
    Name: AddFacility
    Description: 加入一台设施到模拟（已存在时忽略）；接线其状态变更事件转发到事件总线。
    *****/
    public void AddFacility(FacilitySim f)
    {
        if (_facilities.Contains(f)) return;
        Action<FacilitySim, FacilityState> handler = (facility, newState) =>
            EventBus.Publish(new FacilityStateChangedEvent(facility, newState));
        _facilityHandlers[f] = handler;
        _facilities.Add(f);
        f.StateChanged += handler;
    }

    /*****
    Date: 2026-09-06
    Name: RemoveFacility
    Description: 从模拟中移除指定设施：注销其状态事件订阅、占地格恢复为地板（保留功能区归属）并从列表删除。供地图编辑器拆除设施与撤销使用；设施不存在时返回 false。
    *****/
    public bool RemoveFacility(FacilitySim facility)
    {
        if (!_facilities.Remove(facility)) return false;
        if (_facilityHandlers.Remove(facility, out Action<FacilitySim, FacilityState>? handler))
        {
            facility.StateChanged -= handler;
        }
        foreach (Vector2I cell in facility.OccupiedCells())
        {
            Map.SetCell(cell, CellKind.Floor);
        }
        return true;
    }

    /*****
    Date: 2026-09-06
    Name: Update
    Description: 每帧推进模拟（由 GameRoot._Process 调用）：先推进时钟（触发分钟事件 → 需求衰减），再按本帧游戏分钟增量驱动各角色行为状态机；暂停时不驱动行为。
    *****/
    public void Update(double realSeconds)
    {
        Clock.Advance(realSeconds);
        if (Clock.IsPaused) return;

        double deltaGameMinutes = realSeconds * Clock.SpeedMultiplier;
        if (deltaGameMinutes <= 0) return;

        foreach (CharacterSim c in _characters.ToArray())
        {
            UpdateCharacter(c, deltaGameMinutes);
        }
    }

    /*****
    Date: 2026-09-06
    Name: OnGameMinuteElapsed
    Description: 每累计 1 游戏分钟：对所有角色做一次需求衰减（占位逻辑）。
    *****/
    private void OnGameMinuteElapsed(double realSeconds)
    {
        foreach (CharacterSim c in _characters)
        {
            NeedSystem.Update(c, 1.0);
        }
    }

    /*****
    Date: 2026-09-06
    Name: UpdateCharacter
    Description: 角色行为状态机分派：Idle 尝试认领任务；Moving 沿路径推进；Working 推进任务进度；Interrupted 回到 Idle。
    *****/
    private void UpdateCharacter(CharacterSim c, double deltaGameMinutes)
    {
        switch (c.State)
        {
            case CharacterState.Idle:
                TryPickTask(c);
                break;
            case CharacterState.Moving:
                AdvanceMovement(c, deltaGameMinutes);
                break;
            case CharacterState.Working:
                AdvanceWork(c, deltaGameMinutes);
                break;
            case CharacterState.Interrupted:
                c.SetState(CharacterState.Idle);
                break;
        }
    }

    /*****
    Date: 2026-09-06
    Name: TryPickTask
    Description: 空闲角色认领任务：从任务板取任务 → 找设施旁作业格 → 寻路；任一环节失败则取消任务（角色保持空闲），成功则进入 Moving。
    *****/
    private void TryPickTask(CharacterSim c)
    {
        ITask? task = TaskBoard.PickFor(c);
        if (task == null) return;

        if (task.Target is not { } facility)
        {
            TaskBoard.Cancel(task);
            return;
        }

        Vector2I? workCell = FindWorkCell(facility, c.Cell);
        if (workCell == null)
        {
            TaskBoard.Cancel(task);
            return;
        }

        IReadOnlyList<Vector2I> path = Pathfinding.FindPath(c.Cell, workCell.Value);
        if (path.Count == 0)
        {
            TaskBoard.Cancel(task);
            return;
        }

        c.BeginMove(path);
    }

    /*****
    Date: 2026-09-06
    Name: FindWorkCell
    Description: 在设施占地格的四邻中寻找可通行作业格，取距角色当前位置最近者。
    *****/
    private Vector2I? FindWorkCell(FacilitySim facility, Vector2I fromCell)
    {
        Vector2I? best = null;
        int bestDistance = int.MaxValue;
        foreach (Vector2I cell in facility.OccupiedCells())
        {
            foreach (Vector2I offset in NeighborOffsets)
            {
                Vector2I candidate = cell + offset;
                if (!Map.IsWalkable(candidate)) continue;
                int distance = Math.Abs(candidate.X - fromCell.X) + Math.Abs(candidate.Y - fromCell.Y);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
        }
        return best;
    }

    /*****
    Date: 2026-09-06
    Name: AdvanceMovement
    Description: 按移动预算（游戏分钟 × 移动速度）沿路径逐格推进角色；抵达目标格后进入 Working 并开始任务执行。
    *****/
    private void AdvanceMovement(CharacterSim c, double deltaGameMinutes)
    {
        if (c.CurrentTask == null || c.Path == null)
        {
            c.SetState(CharacterState.Idle);
            return;
        }

        double budget = deltaGameMinutes * WalkCellsPerGameMinute;
        IReadOnlyList<Vector2I> path = c.Path;

        while (budget > 0)
        {
            if (c.PathIndex >= path.Count)
            {
                ArriveAtWork(c);
                return;
            }

            double remaining = 1.0 - c.CellProgress;
            if (budget >= remaining)
            {
                budget -= remaining;
                c.CellProgress = 0f;
                c.Cell = path[c.PathIndex];
                c.PathIndex++;
            }
            else
            {
                c.CellProgress += (float)budget;
                budget = 0;
            }
        }
    }

    /*****
    Date: 2026-09-06
    Name: ArriveAtWork
    Description: 角色抵达作业格：进入 Working 状态并启动任务（任务 → InProgress，设施 → 维修中）。
    *****/
    private void ArriveAtWork(CharacterSim c)
    {
        c.SetState(CharacterState.Working);
        c.CurrentTask?.StartWork();
    }

    /*****
    Date: 2026-09-06
    Name: AdvanceWork
    Description: 推进任务进度（游戏分钟 × 专长匹配的效率倍率）；任务完成时清理角色任务引用并回到 Idle。任务被取消等异常状态时同样回 Idle。
    *****/
    private void AdvanceWork(CharacterSim c, double deltaGameMinutes)
    {
        ITask? task = c.CurrentTask;
        if (task == null || task.State is not (TaskState.Assigned or TaskState.InProgress))
        {
            c.CurrentTask = null;
            c.SetState(CharacterState.Idle);
            return;
        }

        float multiplier = task.RequiredSpecialty is { } specialty
            ? c.GetWorkMultiplierFor(specialty)
            : 1f;
        bool done = task.ProgressWork(deltaGameMinutes * multiplier);
        if (done)
        {
            c.CurrentTask = null;
            c.SetState(CharacterState.Idle);
        }
    }
}
