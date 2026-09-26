using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Events;
using RelayStation.Core.Facilities;
using RelayStation.Core.Items;
using RelayStation.Core.Map;
using RelayStation.Core.Tasks;
using RelayStation.Core.Time;

namespace RelayStation.Core.Common;

/*****
Date: 2026-09-06
Name: Simulation
Description: 模拟核心组装类；持有时钟、地图、寻路、任务板、需求系统与角色/设施集合，每帧驱动角色行为状态机（认领 → 寻路 → 移动 → 作业 → 完成）与需求衰减，并把角色/设施状态事件转发到事件总线。纯 C# 组装，可脱离引擎运行。
2026-09-26 扩展：①任务目标不再限于设施——认领时按 `ITask.IsStillValid` 校验有效性、按 `PreferredWorkCell` 解析作业格，作业格等于角色当前格时就地开工（不寻路）；②新增物品类作业的写入口（拾取 / 穿戴 / 卸下 / 落地）与需求驱动的自动取用（每分钟扫描）；③装备加成生效于作业效率与移动速度。
2026-09-27 扩展（份额制）：设施作业的零件由**各人自己的背包**承载——认领后按「未认领需求」自动分配份额并派并行备料（TrySendForParts：背包优先、没有就去最近料堆取自己那一份），谁推进就从谁的背包扣；某人料尽即为「他这一轮到头」自行退场，不再有中途补料与串行守卫。任务失去全部参与者后由**轮末统一扫描**（SettleFinishedRounds）收尾：还有人愿意接手就自动开下一轮（挂起再恢复＝重新发布），全员都中止过它才转入「已中止」等玩家点「继续」；全基地无料可取时直接挂起（面板显示「零件不足」）。另：右键移动＝主动中止该工作（含备料任务连带的作业），本人不再折返，他人接手。
*****/
public sealed class Simulation
{
    /*****
    Date: 2026-09-26
    Name: AutoPickupMaxTrip
    Description: 需求驱动自动取用的「单趟拾入上限」（件）：一趟最多把这么多件对应消耗品拾入背包，随后就地消耗 1 件。
    *****/
    public const int AutoPickupMaxTrip = 3;

    /*****
    Date: 2026-09-26
    Name: AutoConsumeThreshold
    Description: 自动取用触发阈值：进食/饮水低于该值时按 P8 提交取用任务（抢占 P5 例行工作，但可被「人物中止」忽略）。
    *****/
    public const float AutoConsumeThreshold = 30f;

    /*****
    Date: 2026-09-26
    Name: CriticalConsumeThreshold
    Description: 自动取用危急阈值：进食/饮水低于该值时按 Urgent 提交（不可被「人物中止」忽略）。
    *****/
    public const float CriticalConsumeThreshold = 10f;

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
    Date: 2026-09-25
    Name: _blockedCells
    Description: 设施占地格集合（动态阻挡：CellKind.FacilitySlot 移除后，设施占地阻挡寻路由本集合维护；与 GridPathfinding 注入的阻塞委托共享同一实例）。**只含阻挡通行的设施**（FacilitySim.BlocksMovement 为 true），不阻挡的家具类设施不进入本集合。
    *****/
    private readonly ISet<Vector2I> _blockedCells;

    /*****
    Date: 2026-09-26
    Name: _occupiedCells
    Description: 全部设施（含不阻挡通行的家具）的占地格集合；用于放置防重叠（IsCellOccupied），与寻路用的 _blockedCells 分离——家具可以走上去，但仍不能被另一个设施叠放。
    *****/
    private readonly ISet<Vector2I> _occupiedCells = new HashSet<Vector2I>();

    /*****
    Date: 2026-09-26
    Name: _items
    Description: 地面物品堆注册表（场上全部散落物品的唯一权威）；物品逻辑绑定格子，与设施占地互斥，但不阻挡角色通行。
    *****/
    private readonly ItemRegistry _items = new();

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
    Date: 2026-09-26
    Name: Items
    Description: 地面物品堆注册表（场上散落物品的唯一权威）；供表现层渲染与资源统计读取。
    *****/
    public ItemRegistry Items => _items;

    /*****
    Date: 2026-09-26
    Name: Consumables
    Description: 可消耗物品注册表（吃/喝物品定义）；由装配方（GameRoot）按固定路径注册，需求驱动的自动取用据此按种类反查物品。未注册时自动取用静默不触发。
    *****/
    public ConsumableCatalog Consumables { get; } = new();

    /*****
    Date: 2026-09-25
    Name: Simulation
    Description: 构造函数；组装各服务并接线：时钟分钟事件 → 需求衰减；角色/设施状态事件 → 事件总线转发；任务提交事件 → 立即扫描空闲角色派工（发布任务即刻投入全部空闲人力）。可选注入占地格集合（与寻路阻塞委托共享同一实例，缺省自建）。
    *****/
    public Simulation(IGameClock clock, IGridMap map, IPathfinding pathfinding,
        ITaskBoard taskBoard, INeedSystem needSystem, IEventBus eventBus,
        ISet<Vector2I>? blockedCells = null)
    {
        Clock = clock;
        Map = map;
        Pathfinding = pathfinding;
        TaskBoard = taskBoard;
        NeedSystem = needSystem;
        EventBus = eventBus;
        _blockedCells = blockedCells ?? new HashSet<Vector2I>();
        clock.GameMinuteElapsed += OnGameMinuteElapsed;
        eventBus.Subscribe<TaskSubmittedEvent>(OnTaskSubmitted);

        // 需求推进 → 事件总线转发（供 UI 刷新需求数值显示）
        needSystem.NeedsChanged += (character, snapshot) =>
            EventBus.Publish(new NeedsChangedEvent(character, snapshot));
    }

    /*****
    Date: 2026-09-25
    Name: OnTaskSubmitted
    Description: 任务提交事件回调：①立即扫描全部空闲角色派工，使新任务在提交当帧即被（尽可能多的）空闲人力接手；②再按优先级抢占——把手上有更低优先级活的角色改派到更高优先级任务上（对齐《缺氧》的即时切换）。
    *****/
    private void OnTaskSubmitted(TaskSubmittedEvent e)
    {
        AssignIdleWorkers();
        PreemptForHigherPriority();
    }

    /*****
    Date: 2026-09-26
    Name: PreemptForHigherPriority
    Description: 优先级抢占扫描（每次任务提交/恢复后执行）：对每个手上有任务的角色，用任务板纯查询 `PeekFor` 求「若腾出手来」的最优可接手任务，**仅当其优先级严格高于当前任务**时才打断——脱离当前工作（不加入忽略清单，故该任务仍可被他人接手或被本人稍后接手）并当帧改做更高优先的任务。
    严格大于保证：同优先级不打断（沿用「先到先得」，不会无谓抖动）；优先级数值有限且每次只升不降，故不会出现来回抢占。正在执行玩家右键移动指令（无 CurrentTask）者不打断——玩家指令优先；空闲者由 AssignIdleWorkers 覆盖。
    *****/
    private void PreemptForHigherPriority()
    {
        foreach (CharacterSim c in _characters.ToArray())
        {
            if (c.CurrentTask is not { } current) continue;
            ITask? better = TaskBoard.PeekFor(c);
            if (better == null || better.Priority <= current.Priority) continue;
            AbortCharacterWork(c, ignoreCurrent: false);
        }
    }

    /*****
    Date: 2026-09-25
    Name: AssignIdleWorkers
    Description: 空闲人力派工扫描：遍历当前空闲角色并逐个尝试认领/加入任务（每人一次 TryPickTask）；供任务提交当帧调用，也供表现层在需要时主动触发。角色在扫描中状态变化（已派工）会被跳过。
    *****/
    public void AssignIdleWorkers()
    {
        foreach (CharacterSim c in _characters.ToArray())
        {
            if (c.State == CharacterState.Idle) TryPickTask(c);
        }
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
    Date: 2026-09-26
    Name: AddFacility
    Description: 加入一台设施到模拟（已存在时忽略）；占地格一律计入「已占用」集合，**仅当设施阻挡通行（BlocksMovement）时**才同时计入寻路阻塞集合；并接线其状态变更事件转发到事件总线。设备直接放置于地板上，不修改格子地形。
    *****/
    public void AddFacility(FacilitySim f)
    {
        if (_facilities.Contains(f)) return;
        Action<FacilitySim, FacilityState> handler = (facility, newState) =>
            EventBus.Publish(new FacilityStateChangedEvent(facility, newState));
        _facilityHandlers[f] = handler;
        _facilities.Add(f);
        f.StateChanged += handler;
        foreach (Vector2I cell in f.OccupiedCells())
        {
            _occupiedCells.Add(cell);
            if (f.BlocksMovement) _blockedCells.Add(cell);
        }
    }

    /*****
    Date: 2026-09-26
    Name: IsCellOccupied
    Description: 判断指定格是否被任一设施占地占用（**含不阻挡通行的家具**）；供编辑器放置防重叠校验使用。寻路是否可通行由阻塞集合（_blockedCells，注入给 GridPathfinding）决定，二者已分离。
    *****/
    public bool IsCellOccupied(Vector2I cell) => _occupiedCells.Contains(cell);

    /*****
    Date: 2026-09-26
    Name: CanMoveTo
    Description: 指定格是否可作为「右键移动」的落脚格：地形可通行（地板/门）且未被阻挡设施占用（家具可行走）。供表现层在发出移动指令前做即时校验与提示。
    *****/
    public bool CanMoveTo(Vector2I cell)
        => Map.IsWalkable(cell) && !_blockedCells.Contains(cell);

    /*****
    Date: 2026-09-26
    Name: CheckFacilityPlacement
    Description: 多格设施放置校验（放置合法性的唯一权威，供「实际放置」与「建造预览」共用，避免两处判定漂移）：逐格询问 CheckFacilityCell，返回首个受阻格的原因；全部通过返回 None。
    *****/
    public PlacementObstacle CheckFacilityPlacement(Vector2I origin, Vector2I size)
    {
        for (int dy = 0; dy < Math.Max(1, size.Y); dy++)
        {
            for (int dx = 0; dx < Math.Max(1, size.X); dx++)
            {
                PlacementObstacle obstacle = CheckFacilityCell(new Vector2I(origin.X + dx, origin.Y + dy));
                if (obstacle != PlacementObstacle.None) return obstacle;
            }
        }
        return PlacementObstacle.None;
    }

    /*****
    Date: 2026-09-26
    Name: CheckFacilityCell
    Description: 单格设施放置校验并给出**受阻原因**：须为地板、未被任一设施占地、且格上无物品——「设施与物品不可同格共存」的互斥规则在此集中落地。地形为墙/真空/门、或越界的格子一律返回 NotFloor。材料不足不属于放置阻挡（只影响建造进度）。
    *****/
    public PlacementObstacle CheckFacilityCell(Vector2I cell)
    {
        if (Map.GetCell(cell) != CellKind.Floor) return PlacementObstacle.NotFloor;
        if (_occupiedCells.Contains(cell)) return PlacementObstacle.FacilityOccupied;
        if (_items.HasItemAt(cell)) return PlacementObstacle.ItemsPresent;
        return PlacementObstacle.None;
    }

    /*****
    Date: 2026-09-26
    Name: CanPlaceItemsAt
    Description: 物品投放校验：须为地板且未被任一设施占地（物品可与角色同格——不阻挡通行，也不与物品叠放冲突，多组物品堆在同一格是允许的）。不做物品种类与数量校验。
    *****/
    public bool CanPlaceItemsAt(Vector2I cell)
        => Map.GetCell(cell) == CellKind.Floor
           && !_occupiedCells.Contains(cell);

    /*****
    Date: 2026-09-26
    Name: ComputeReachableCells
    Description: 以全部角色所在格为源点、沿四方向对「地形可通行（地板/门）且未被阻挡设施占用」的格子做多源 BFS，返回可达格集合；供「地上可达零件数」一类统计使用。物品不阻挡通行，故不影响可达性；无角色时返回空集（无人在场即无可达资源）。属**惰性**接口——仅在需要时调用，不做逐帧计算。
    *****/
    public ISet<Vector2I> ComputeReachableCells()
    {
        var reachable = new HashSet<Vector2I>();
        var queue = new Queue<Vector2I>();
        foreach (CharacterSim c in _characters)
        {
            if (reachable.Add(c.Cell)) queue.Enqueue(c.Cell);
        }

        while (queue.Count > 0)
        {
            Vector2I cell = queue.Dequeue();
            foreach (Vector2I offset in NeighborOffsets)
            {
                Vector2I next = cell + offset;
                if (!Map.IsWalkable(next) || _blockedCells.Contains(next)) continue;
                if (reachable.Add(next)) queue.Enqueue(next);
            }
        }
        return reachable;
    }

    /*****
    Date: 2026-09-26
    Name: ScatterItems
    Description: 在随机空闲地板格上散落投放物品（开局初始零件用）：候选格为「地板 + 未被任何设施占用 + 格上无物品」，Fisher–Yates 洗牌后取前 groupCount 个，把 totalCount 均分到各组（不能整除时余数并入最后一组）；候选格不足时按实际可用格数少投。格内像素偏移取 0（居中摆放），需要"散得更开"时由调用方按需给定偏移。rng 由调用方注入以便测试可复现；返回实际建出的物品堆。
    *****/
    public IReadOnlyList<ItemStack> ScatterItems(IItemDef def, int totalCount, int groupCount, Random rng)
    {
        if (totalCount <= 0 || groupCount <= 0) return Array.Empty<ItemStack>();

        var candidates = new List<Vector2I>();
        for (int y = 0; y < Map.Height; y++)
        {
            for (int x = 0; x < Map.Width; x++)
            {
                var cell = new Vector2I(x, y);
                if (Map.GetCell(cell) != CellKind.Floor) continue;
                if (_occupiedCells.Contains(cell) || _items.HasItemAt(cell)) continue;
                candidates.Add(cell);
            }
        }

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int groups = Math.Min(groupCount, candidates.Count);
        var created = new List<ItemStack>();
        int perGroup = totalCount / groups;
        int remainder = totalCount % groups;
        for (int i = 0; i < groups; i++)
        {
            int count = perGroup + (i == groups - 1 ? remainder : 0);
            if (count <= 0) continue;
            created.AddRange(_items.Add(def, count, candidates[i]));
        }
        return created;
    }

    /*****
    Date: 2026-09-26
    Name: RemoveFacility
    Description: 从模拟中移除指定设施：注销其状态事件订阅、占地格同时退出「已占用」与「寻路阻塞」两个集合并从列表删除。供地图编辑器拆除设施与撤销使用；设施不存在时返回 false。不修改格子地形（设备本就放置于地板上）。
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
            _occupiedCells.Remove(cell);
            _blockedCells.Remove(cell);
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

        SettleFinishedRounds();
    }

    /*****
    Date: 2026-09-27
    Name: SettleFinishedRounds
    Description: **轮末统一扫描**（每帧驱动完角色后执行一次）：对「已经没有工人、也没有人在途备料」的未结设施作业做一次收尾——
    ①**还有人愿意接手**（存在未把它列入忽略清单的角色）→ **自动开下一轮**：先挂起再恢复（等价于「重新发布一次任务」），重新排队、按未认领需求重新分配份额，循环往复直到完成；
    ②**没有任何人愿意接手**（参与者全被中止过它）→ 转入「已中止」，等玩家点「继续」（复用既有挂起/恢复 UI）。
    判定放在**下一帧**统一做（而非某个工人离开的瞬间），是为了避开「取完料的人当帧回来重新认领」「同一帧里多人陆续退场」这类竞态；在途备料（HasFetchInFlight）也算「本轮还有人在参与」，不会被误判为收工。
    *****/
    private void SettleFinishedRounds()
    {
        foreach (ITask task in TaskBoard.Active.ToArray())
        {
            if (task is not FacilityWorkTask work) continue;
            if (work.State is not (TaskState.Assigned or TaskState.InProgress)) continue;
            if (work.Workers.Count > 0 || work.HasFetchInFlight) continue;

            work.Suspend();
            if (HasWillingTaker(work)) TaskBoard.Resume(work); // 有人接手 → 自动开下一轮（挂起再恢复＝重新发布）
        }
    }

    /*****
    Date: 2026-09-27
    Name: HasWillingTaker
    Description: 是否还有人愿意接手该作业（**放宽口径**）：只要存在一名没把它列入忽略清单的角色即可——他此刻可能在忙别的活，但人手一空出来就会接手，故任务留在队列里等着即可（无需玩家操作）。全员都中止过它（都在忽略清单里）才算「没人接手」，此时才转「已中止」等玩家点「继续」。
    *****/
    private bool HasWillingTaker(ITask task)
    {
        foreach (CharacterSim c in _characters)
        {
            if (!c.IsIgnoring(task)) return true;
        }
        return false;
    }

    /*****
    Date: 2026-09-06
    Name: OnGameMinuteElapsed
    Description: 每累计 1 游戏分钟：①对所有角色做一次需求衰减；②随后做一次**需求驱动的自动取用**扫描（见 MaybeOrderAutoConsume）。
    *****/
    private void OnGameMinuteElapsed(double realSeconds)
    {
        foreach (CharacterSim c in _characters)
        {
            NeedSystem.Update(c, 1.0);
        }
        foreach (CharacterSim c in _characters.ToArray())
        {
            MaybeOrderAutoConsume(c);
        }
    }

    /*****
    Date: 2026-09-26
    Name: MaybeOrderAutoConsume
    Description: 需求驱动的自动取用（每分钟评估一次，紧跟在需求推进之后）：进食/饮水低于阈值时，为本人提交一条个人取用任务（ConsumeTask）——
    ①本人已有未结取用任务 → 跳过（不重复提交）；
    ②**背包优先**：背包里已有对应消耗品 → 就地消耗（不寻路）；
    ③否则在场上找「种类匹配 + 未被其他未结任务占用 + 本人可寻路抵达」的最近堆 → 前往取用（抵达后逐件拾入背包再就地消耗）；
    ④优先级：需求 < 30 → P8（抢占 P5 例行工作、可被「人物中止」忽略），需求 < 10 → Urgent（不可忽略）；
    ⑤未注册对应可消耗物品（ConsumableCatalog 未登记）或场上无可达物品 → 本次不提交（下一分钟再评估）。
    任务提交经既有「提交即派工 + 优先级抢占」链路当帧生效。
    *****/
    private void MaybeOrderAutoConsume(CharacterSim c)
    {
        NeedSnapshot needs = NeedSystem.Read(c);
        ConsumableKind kind = PickConsumeKind(needs);
        if (kind == ConsumableKind.None) return;

        IItemDef? def = Consumables.Find(kind);
        if (def == null) return;
        if (HasOpenConsumeTask(c)) return;

        float value = kind == ConsumableKind.Water ? needs.Water : needs.Food;
        TaskPriority priority = value < CriticalConsumeThreshold ? TaskPriority.Urgent : TaskPriority.P8;

        if (c.Inventory.CountOf(def) > 0)
        {
            TaskBoard.Submit(new ConsumeTask(NeedSystem, c, def, target: null, registry: null, AutoPickupMaxTrip, priority));
            return;
        }

        ItemStack? target = FindNearestConsumableStack(c, def);
        if (target == null) return;
        TaskBoard.Submit(new ConsumeTask(NeedSystem, c, def, target, _items, AutoPickupMaxTrip, priority));
    }

    /*****
    Date: 2026-09-26
    Name: PickConsumeKind
    Description: 按需求值挑选本次要满足的种类：进食/饮水都低于阈值时取**更低者**（并列取饮水——饮水衰减更快）；都未低于阈值返回 None。
    *****/
    private static ConsumableKind PickConsumeKind(NeedSnapshot needs)
    {
        bool hungry = needs.Food < AutoConsumeThreshold;
        bool thirsty = needs.Water < AutoConsumeThreshold;
        if (!hungry && !thirsty) return ConsumableKind.None;
        if (hungry && thirsty) return needs.Water <= needs.Food ? ConsumableKind.Water : ConsumableKind.Food;
        return hungry ? ConsumableKind.Food : ConsumableKind.Water;
    }

    /*****
    Date: 2026-09-26
    Name: HasOpenConsumeTask
    Description: 本人是否已有未结（Pending/Assigned/InProgress）的取用任务；供分钟扫描避免重复提交同一角色的取用指令。
    *****/
    private bool HasOpenConsumeTask(CharacterSim c)
    {
        foreach (ITask task in TaskBoard.Active)
        {
            if (task is ConsumeTask consume
                && ReferenceEquals(consume.Owner, c)
                && task.State is TaskState.Pending or TaskState.Assigned or TaskState.InProgress)
            {
                return true;
            }
        }
        return false;
    }

    /*****
    Date: 2026-09-26
    Name: FindNearestConsumableStack
    Description: 在地面物品中找「定义为该消耗品 + 未被其他未结任务占用 + 本人可寻路抵达」的最近堆；无可达堆返回 null。
    *****/
    private ItemStack? FindNearestConsumableStack(CharacterSim c, IItemDef def)
        => FindNearestStack(c, def, skipClaimed: true);

    /*****
    Date: 2026-09-27
    Name: FindNearestStack
    Description: 在地面物品中找「定义匹配 + 本人可寻路抵达」的最近堆（按曼哈顿距离取最近，并列取创建顺序在前者）；skipClaimed 为 true 时跳过已被未结任务预定的堆（避免多人抢同一堆）。可达性预检避免提交注定认领失败的任务；无可达堆返回 null。
    *****/
    private ItemStack? FindNearestStack(CharacterSim c, IItemDef def, bool skipClaimed)
    {
        ItemStack? best = null;
        int bestDistance = int.MaxValue;
        foreach (ItemStack stack in _items.Stacks)
        {
            if (!ReferenceEquals(stack.Def, def) || stack.Count <= 0) continue;
            if (skipClaimed && IsStackClaimed(stack)) continue;

            int distance = Math.Abs(stack.Cell.X - c.Cell.X) + Math.Abs(stack.Cell.Y - c.Cell.Y);
            if (distance >= bestDistance) continue;
            if (Pathfinding.FindPath(c.Cell, stack.Cell).Count == 0) continue;

            bestDistance = distance;
            best = stack;
        }
        return best;
    }

    /*****
    Date: 2026-09-26
    Name: IsStackClaimed
    Description: 指定物品堆是否已被某条未结任务占用（按 `ItemTarget` 比对）；供自动取用把多名角色分散到不同的堆上，避免同堆重复派单。
    *****/
    private bool IsStackClaimed(ItemStack stack)
    {
        foreach (ITask task in TaskBoard.Active)
        {
            if (ReferenceEquals(task.ItemTarget, stack)
                && task.State is TaskState.Pending or TaskState.Assigned or TaskState.InProgress)
            {
                return true;
            }
        }
        return false;
    }

    /*****
    Date: 2026-09-27
    Name: TrySendForParts
    Description: 认领设施作业后的「**自动分配份额 + 并行备料**」（份额制入口）：本人背包已有该种零件（或本任务免费）→ 返回 false，直接去开工（背包优先）；否则**按本轮预计参与者均分**未认领需求得到本人这一份（再由背包容量封顶），去最近的料堆取料——提交一条服务于本任务（`servedTask`）的个人拾取任务并退出作业本人（作业留在板上等料，**不取消、不挂起**），取回后自然重新认领。
    份额规则：**份额 = ⌈未认领需求 ÷ 本轮预计参与者数⌉**（参与者 = 本人 + 场上空闲、没忽略本任务、还装得下零件的角色，且不超过任务剩余空位）。这样「**谁闲着谁就一起去搬自己那一份**」：需求 40 + 三人空闲 → 14 / 13 / 13，三人都出发、三人都投入施工（总速度≈3 倍）；只有一人空闲时才由他一人按容量搬运（此时参与者=1，份额＝全部剩余，不会因均分而多跑几趟）。
    兜底：全基地找不到任何可达的零件来源 → 直接把任务挂起（面板显示「零件不足」），等玩家补料后点「继续」；不做「反复发布→取不到→退出」的空转。返回是否已改为去取料。
    *****/
    private bool TrySendForParts(CharacterSim c, FacilityWorkTask work)
    {
        if (work.PartsItem is not { } partsDef) return false;         // 免费模式：不耗料，直接开工
        if (c.Inventory.CountOf(partsDef) > 0) return false;          // 背包优先：手里有料就直接干，用完再说

        int participants = 1 + Math.Min(
            CountIdleHelpers(work, c, partsDef),
            Math.Max(0, work.MaxWorkers - work.InvolvedCount));
        int fairShare = (int)Math.Ceiling((double)work.UnclaimedPartsNeed / participants);
        int share = Math.Min(fairShare, c.Inventory.MaxAddable(partsDef));
        if (share <= 0) return false;                                 // 没有可认领的份额 / 装不下：直接开工（料尽自退）

        IItemSource? source = FindNearestPartsSource(c, partsDef, includeClaimed: false)
                              ?? FindNearestPartsSource(c, partsDef, includeClaimed: true);
        if (source == null)
        {
            work.SuspendForPartsShortage(); // 全基地无料可取：直接挂起（避免空转），补料后由玩家点「继续」
            return true;
        }

        var pickup = new PickupTask(c, source, priority: work.Priority, maxCount: share, servedTask: work);
        work.RegisterFetch(pickup); // 先登记在途预留量：同一帧里其他认领者据此扣减份额，不会重复取同一份料
        ReleaseCurrentWork(c);
        c.Path = null;
        c.IsManualMove = false;
        c.SetState(CharacterState.Idle);
        TaskBoard.Submit(pickup);
        if (!ReferenceEquals(c.CurrentTask, pickup)) work.UnregisterFetch(pickup); // 没派上（被去重等）→ 撤回预留
        return true;
    }

    /*****
    Date: 2026-09-27
    Name: CountIdleHelpers
    Description: 除本人以外，此刻还有几名「能一起搬这一份料」的角色——空闲、没把该作业列入忽略清单、且背包还装得下该种零件。供分份时估算本轮参与者数，使份额接近**人均**而不是让先来的一个人按容量包揽（否则开局那种「成本 15 件、一人能背 30 件」的小活永远只有一个人去取料）。
    *****/
    private int CountIdleHelpers(ITask task, CharacterSim except, IItemDef def)
    {
        int count = 0;
        foreach (CharacterSim ch in _characters)
        {
            if (ReferenceEquals(ch, except) || ch.State != CharacterState.Idle) continue;
            if (ch.IsIgnoring(task)) continue;
            if (ch.Inventory.MaxAddable(def) <= 0) continue;
            count++;
        }
        return count;
    }

    /*****
    Date: 2026-09-27
    Name: FindNearestPartsSource
    Description: 在场上找「定义匹配 + 本人可寻路抵达」的最近零件来源（今日只有地面物品堆；includeClaimed 为 false 时跳过已被未结任务预定的堆）。无可达来源返回 null（调用方据此决定「等待」还是「被动中止」）。
    扩展点：**从容器取料**（如家具柜 resources/facilities/furniture_cabinet.tres）后续只须实现 IItemSource 的容器版并在此并入候选集，任务与调用方零改动。
    *****/
    private IItemSource? FindNearestPartsSource(CharacterSim c, IItemDef def, bool includeClaimed)
    {
        // TODO（容器取料）：追加「容器设施来源」（实现 IItemSource）后与地面堆一同按距离择优。
        ItemStack? stack = FindNearestStack(c, def, skipClaimed: !includeClaimed);
        return stack == null ? null : new GroundItemSource(stack, _items);
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
    Date: 2026-09-26
    Name: TryPickTask
    Description: 空闲角色认领任务：从任务板取任务（新任务或加入进行中的多人维修）→ **校验目标有效性**（物品类任务的目标可能已被他人取走）→ **设施作业先自动分配份额并派备料**（TrySendForParts：背包已有料就直接开工，没有就去最近的料堆取自己那一份）→ 解析作业格（物品任务=物品所在格；就地任务=角色当前格；设施作业=占地格四邻最近可通行格）→ 寻路；任一环节失败时按人数处置（首人失败取消任务，后加入者失败仅脱离本人），成功则进入 Moving。**作业格等于角色当前格时就地开工**（不寻路）。
    2026-09-27 调整：原「无活可接 → 为它单独派一条备料任务」的前置取消——份额制下备料由**认领动作本身**触发（先认领、再按未认领需求分配份额去取料），不再需要预读任务板另起一条。
    *****/
    private void TryPickTask(CharacterSim c)
    {
        ITask? task = TaskBoard.PickFor(c);
        if (task == null) return;

        if (!task.IsStillValid())
        {
            FailPick(task, c);
            return;
        }

        if (task is FacilityWorkTask work && TrySendForParts(c, work)) return; // 已改为去取自己的份额

        Vector2I? preferred = task.PreferredWorkCell(c);
        Vector2I? workCell = preferred ?? (task.Target is { } facility ? FindWorkCell(facility, c.Cell) : null);
        if (workCell == null)
        {
            FailPick(task, c);
            return;
        }

        if (preferred != null && workCell.Value == c.Cell)
        {
            ArriveAtWork(c); // 就地作业（物品类任务）：不寻路，直接进入 Working 并启动任务
            return;
        }

        IReadOnlyList<Vector2I> path = Pathfinding.FindPath(c.Cell, workCell.Value);
        if (path.Count == 0)
        {
            FailPick(task, c);
            return;
        }

        c.BeginMove(path);
    }

    /*****
    Date: 2026-09-25
    Name: FailPick
    Description: 认领后环节失败（无目标/无作业格/寻路失败）的处置：单人任务取消整个任务（保留原语义）；多人任务中后加入者仅脱离本人，任务与其他工作者继续。
    *****/
    private void FailPick(ITask task, CharacterSim c)
    {
        if (task.Workers.Count > 1) task.ReleaseWorker(c);
        else TaskBoard.Cancel(task);
    }

    /*****
    Date: 2026-09-25
    Name: FindWorkCell
    Description: 在设施占地格的四邻中寻找可通行作业格（地形可通行且未被其他设施占地占用），取距角色当前位置最近者。
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
                if (!Map.IsWalkable(candidate) || _blockedCells.Contains(candidate)) continue;
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
    Date: 2026-09-26
    Name: AbortCharacterWork
    Description: 「人物中止」：让指定角色停止当前工作并转去做任务列表的下一项（当帧重新认领）。ignoreCurrent 为 true 时按任务归属分流——**公共任务**（设施作业）把任务实例列入该角色的忽略清单（该任务不再被本人认领，其他角色不受影响，直到玩家对该设施重新下达指令 ClearIgnoredTasksFor；紧急任务不受忽略影响见 TaskBoard.PickFor）；**个人任务**（物品类作业，Owner 非空）无法转交他人，改为**直接取消该任务**（否则会留下无人可认领的僵尸任务）。角色若正在手动移动，则一并取消该移动。对空闲角色调用无害（等价于立即找活）。
    *****/
    public void AbortCharacterWork(CharacterSim c, bool ignoreCurrent)
    {
        if (ignoreCurrent && c.CurrentTask != null)
        {
            // 每次中止重设忽略清单：先清后加，避免忽略项无限累积
            c.ClearIgnoredTasks();
            AbortPersonalWork(c); // 按归属分流：公共任务＝忽略并脱离；个人任务＝取消（备料另连它服务的作业一起忽略）
        }

        ReleaseCurrentWork(c);
        c.Path = null;
        c.IsManualMove = false;
        c.SetState(CharacterState.Idle);
        TryPickTask(c);
    }

    /*****
    Date: 2026-09-26
    Name: ClearIgnoredTasksFor
    Description: 清除全部角色对指定设施的「暂时忽略」（玩家对该设施重新下达修复/建造/拆除/恢复指令时调用）：使该设施上的任务重新按紧急度参与优先级排队。
    *****/
    public void ClearIgnoredTasksFor(FacilitySim target)
    {
        foreach (CharacterSim c in _characters)
        {
            c.ClearIgnoredTasksFor(target);
        }
    }

    /*****
    Date: 2026-09-26
    Name: OrderMoveGroup
    Description: 「右键移动到指定位置」：为每个选中角色各分配一个落脚格（首选点击格，其次其四邻中尚未被同批分配者）并依次下令移动，避免多人叠在同一格。逐人独立寻路，失败者跳过。返回实际下令成功的人数。
    *****/
    public int OrderMoveGroup(IReadOnlyList<CharacterSim> characters, Vector2I targetCell)
    {
        var assigned = new HashSet<Vector2I>();
        int ordered = 0;
        foreach (CharacterSim c in characters)
        {
            Vector2I? destination = FindMoveDestination(targetCell, assigned);
            if (destination == null) continue;
            if (!OrderMove(c, destination.Value)) continue;
            assigned.Add(destination.Value);
            ordered++;
        }
        return ordered;
    }

    /*****
    Date: 2026-09-26
    Name: FindMoveDestination
    Description: 为一次手动移动挑选落脚格：优先点击格本身，其次四邻；须可通行且未被阻挡设施占用（家具可走上去），且尚未被本次同批移动分配给他人。全部不可用返回 null。
    *****/
    private Vector2I? FindMoveDestination(Vector2I preferred, ISet<Vector2I> taken)
    {
        if (IsMoveCellFree(preferred, taken)) return preferred;
        foreach (Vector2I offset in NeighborOffsets)
        {
            Vector2I candidate = preferred + offset;
            if (IsMoveCellFree(candidate, taken)) return candidate;
        }
        return null;
    }

    /*****
    Date: 2026-09-26
    Name: IsMoveCellFree
    Description: 手动移动落脚格是否可用（地形可通行、未被阻挡设施占用、且未被同批他人预定）。
    *****/
    private bool IsMoveCellFree(Vector2I cell, ISet<Vector2I> taken)
        => !taken.Contains(cell) && Map.IsWalkable(cell) && !_blockedCells.Contains(cell);

    /*****
    Date: 2026-09-26
    Name: OrderMove
    Description: 让指定角色前往指定格：先**视同主动中止**地退出当前工作（`AbortPersonalWork`——公共任务列入本人的忽略清单、个人任务直接取消，备料任务另连它服务的作业一起忽略），再寻路并进入移动状态（标记为手动移动，抵达后回 Idle 重新找活）。也就是说右键移动＝「中止该工作 + 走过去」：本人不会再自动折返原工作，另一名赋闲的人会接手（轮末扫描会把无人接手的作业自动开下一轮）。已在目标格则直接回 Idle 找活；寻路失败返回 false（不改变现状）。
    *****/
    public bool OrderMove(CharacterSim c, Vector2I destination)
    {
        if (c.Cell == destination)
        {
            AbortPersonalWork(c);
            c.Path = null;
            c.IsManualMove = false;
            c.SetState(CharacterState.Idle);
            TryPickTask(c);
            return true;
        }

        IReadOnlyList<Vector2I> path = Pathfinding.FindPath(c.Cell, destination);
        if (path.Count == 0) return false;

        AbortPersonalWork(c);
        c.BeginMove(path);
        c.IsManualMove = true;
        return true;
    }

    /*****
    Date: 2026-09-27
    Name: AbortPersonalWork
    Description: 让角色**退出当前工作**（供「人物中止」与右键移动共用，语义＝主动中止）：①**公共任务**（设施作业）——把它列入**本人**的忽略清单（本人不再接手这条作业、别人不受影响）并脱离本人，于是「该人物退出、另一个赋闲的人补上」；②**个人任务**（拾取/取用/穿脱）——无法转交他人，直接取消；③若被取消的个人任务是**为某条设施作业备料**的（`ServedTask` 非空），把它服务的那条作业**一并列入忽略清单**——否则本人当帧就会重新去为同一条作业取料，「中止」形同无效（这正是「中止停不下建造」的根因）。
    *****/
    private void AbortPersonalWork(CharacterSim c)
    {
        if (c.CurrentTask is not { } task) return;

        if (task is PickupTask { ServedTask: { } served }) c.IgnoreTask(served); // 备料被中止：连它服务的作业一起退出

        if (task.Owner == null)
        {
            c.IgnoreTask(task); // 公共任务：视同主动中止（本人不再接手，他人不受影响）
            ReleaseCurrentWork(c);
            return;
        }

        ReleaseCurrentWork(c);
        TaskBoard.Cancel(task); // 个人任务：直接取消（个人指令无法转交他人）
    }

    /*****
    Date: 2026-09-26
    Name: TryAddItemToInventory
    Description: 往角色背包里装入物品（超重则整笔拒绝）；当前供存档恢复与后续「拾取/搬运」接入，无 UI 入口。返回是否装入成功。
    *****/
    public bool TryAddItemToInventory(CharacterSim c, IItemDef def, int count)
        => c.Inventory.TryAdd(def, count);

    /*****
    Date: 2026-09-26
    Name: TryEquipItem
    Description: 让角色装备背包内的一件可穿戴物品（须在背包内且槽位合法；同槽换装时旧装备回背包，换装后超重则整笔拒绝）。返回是否装备成功。
    *****/
    public bool TryEquipItem(CharacterSim c, IItemDef def) => c.Inventory.TryEquip(def);

    /*****
    Date: 2026-09-26
    Name: UnequipItem
    Description: 让角色卸下指定槽位的装备（回到背包）；卸下后超重则拒绝（槽位与背包均不变）。返回是否卸下成功。**注意：这是即时的数据层写入口**（存档恢复与测试用）；玩家在面板上点「卸下」走的是任务化的 `OrderUnequip`。
    *****/
    public bool UnequipItem(CharacterSim c, EquipmentSlot slot) => c.Inventory.Unequip(slot);

    /*****
    Date: 2026-09-26
    Name: OrderPickup
    Description: 玩家「拾取」指令（右键菜单）：预检——目标堆仍有物品、本人至少还能装下 1 件、目标格可寻路抵达（或在脚下）；任一不过则直接回调原因并返回 false（不改动任何状态，表现层据此提示）。通过后**打断本人当前工作**（与「走到此处」同约定：脱离任务但不加入忽略清单，之后仍可回到原任务）、提交个人拾取任务（Owner = 本人，来源为该地面物品堆、逐件转移、不限量）并在当帧尝试认领。返回是否已下达。
    *****/
    public bool OrderPickup(CharacterSim c, ItemStack stack, Action<PickupOutcome>? onOutcome = null,
        TaskPriority priority = TaskPriority.P5)
    {
        if (stack.Count <= 0)
        {
            onOutcome?.Invoke(PickupOutcome.Gone);
            return false;
        }
        if (c.Inventory.MaxAddable(stack.Def) <= 0)
        {
            onOutcome?.Invoke(PickupOutcome.NoCapacity);
            return false;
        }
        if (!CanReach(c, stack.Cell))
        {
            onOutcome?.Invoke(PickupOutcome.Unreachable);
            return false;
        }

        StartPersonalItemTask(c, new PickupTask(c, new GroundItemSource(stack, _items), onOutcome, priority));
        return true;
    }

    /*****
    Date: 2026-09-26
    Name: OrderEquip
    Description: 玩家「装备」指令（人物面板）：把背包内的一件可穿戴物品穿上（任务化：就地耗时 `EquipTask.EquipGameMinutes` 后生效）。预检物品在背包内且可装备；通过后打断本人当前工作并提交个人穿戴任务。返回是否已下达。
    *****/
    public bool OrderEquip(CharacterSim c, IItemDef def, Action<EquipOutcome>? onOutcome = null,
        TaskPriority priority = TaskPriority.P5)
    {
        if (def.EquipSlot == EquipmentSlot.None)
        {
            onOutcome?.Invoke(EquipOutcome.Blocked);
            return false;
        }
        if (c.Inventory.CountOf(def) <= 0)
        {
            onOutcome?.Invoke(EquipOutcome.Gone);
            return false;
        }

        StartPersonalItemTask(c, new EquipTask(c, def, groundTarget: null, registry: null, TryDropEquipped, onOutcome, priority));
        return true;
    }

    /*****
    Date: 2026-09-26
    Name: OrderWearFromGround
    Description: 玩家「穿戴」指令（右键地面装备）：走到物品所在格 → 耗时后「取下 1 件并直接穿上（不先占背包）」。预检堆仍有物品、物品可装备、目标格可抵达；通过后打断本人当前工作并提交个人穿戴任务。返回是否已下达。
    *****/
    public bool OrderWearFromGround(CharacterSim c, ItemStack stack, Action<EquipOutcome>? onOutcome = null,
        TaskPriority priority = TaskPriority.P5)
    {
        if (stack.Count <= 0 || stack.Def.EquipSlot == EquipmentSlot.None)
        {
            onOutcome?.Invoke(EquipOutcome.Blocked);
            return false;
        }
        if (!CanReach(c, stack.Cell))
        {
            onOutcome?.Invoke(EquipOutcome.Gone);
            return false;
        }

        StartPersonalItemTask(c, new EquipTask(c, stack.Def, stack, _items, TryDropEquipped, onOutcome, priority));
        return true;
    }

    /*****
    Date: 2026-09-26
    Name: OrderUnequip
    Description: 玩家「卸下」指令（人物面板）：就地耗时 `UnequipTask.UnequipGameMinutes` 后卸下——优先回背包，背包放不下（超重）则**就地落地**（`TryDropEquipped`），两者都不行则保持装备并回调提示。预检槽位有装备；通过后打断本人当前工作并提交个人卸下任务。返回是否已下达。
    *****/
    public bool OrderUnequip(CharacterSim c, EquipmentSlot slot, Action<UnequipOutcome>? onOutcome = null,
        TaskPriority priority = TaskPriority.P5)
    {
        if (c.Inventory.GetEquipped(slot) == null)
        {
            onOutcome?.Invoke(UnequipOutcome.Blocked);
            return false;
        }

        StartPersonalItemTask(c, new UnequipTask(c, slot, TryDropEquipped, onOutcome, priority));
        return true;
    }

    /*****
    Date: 2026-09-26
    Name: StartPersonalItemTask
    Description: 下达一条个人物品类任务的共用流程：打断本人当前工作（不加入忽略清单）→ **先把本人置 Idle 再提交**（提交会立刻触发空闲派工，本人当帧即可接手；若顺序反了，派工后又被置 Idle 会把已开工的任务晾在原地）→ 若本人仍未拿到任务（例如更高优先级的活被抢先派给他）则再尝试一次认领。
    *****/
    private void StartPersonalItemTask(CharacterSim c, ITask task)
    {
        ReleaseCurrentWork(c);
        c.Path = null;
        c.IsManualMove = false;
        c.SetState(CharacterState.Idle);
        TaskBoard.Submit(task);
        if (c.CurrentTask == null) TryPickTask(c);
    }

    /*****
    Date: 2026-09-26
    Name: CanReach
    Description: 指定角色能否抵达目标格：目标格即脚下直接算可达；否则以寻路结果是否为空判定。供玩家指令的即时预检（避免下达注定失败的任务）。
    *****/
    private bool CanReach(CharacterSim c, Vector2I target)
        => c.Cell == target || Pathfinding.FindPath(c.Cell, target).Count > 0;

    /*****
    Date: 2026-09-26
    Name: TryDropEquipped
    Description: 把指定槽位的装备**就地落地**（卸下/换装时背包放不下的回退路径）：先找可放置格（角色所在格优先，其次四邻；须为地板且未被任一设施占地），再把装备从槽位取下并在该格投放一组物品（1 个）。无可放置格时**不改动任何状态**并返回 false（调用方据此保持装备并提示）。
    *****/
    public bool TryDropEquipped(CharacterSim c, EquipmentSlot slot)
    {
        if (c.Inventory.GetEquipped(slot) == null) return false;

        Vector2I? cell = FindDropCell(c.Cell);
        if (cell == null) return false;

        IItemDef? def = c.Inventory.TakeEquipped(slot);
        if (def == null) return false;

        _items.Add(def, 1, cell.Value);
        return true;
    }

    /*****
    Date: 2026-09-26
    Name: FindDropCell
    Description: 挑一个可放置物品的落点：优先指定格本身，其次四邻（须为地板且未被任一设施占地——与 `CanPlaceItemsAt` 同一口径）；全部不可用返回 null。
    *****/
    private Vector2I? FindDropCell(Vector2I preferred)
    {
        if (CanPlaceItemsAt(preferred)) return preferred;
        foreach (Vector2I offset in NeighborOffsets)
        {
            Vector2I candidate = preferred + offset;
            if (CanPlaceItemsAt(candidate)) return candidate;
        }
        return null;
    }

    /*****
    Date: 2026-09-26
    Name: ReleaseCurrentWork
    Description: 让角色脱离当前任务而不取消任务本身：任务侧经 ReleaseWorker 移除该工作者（其 CurrentTask 清空、状态置 Interrupted），其余工作者与任务进度不受影响；单人任务失去唯一工作者后仍留在未结集合，可被其他空闲角色经 CanJoin 重新接手。角色未持有任务时无操作。
    *****/
    private void ReleaseCurrentWork(CharacterSim c)
    {
        if (c.CurrentTask is not { } task) return;
        if (task.Workers.Contains(c))
        {
            task.ReleaseWorker(c); // 内部经 OnTaskCancelled 清理 CurrentTask 并置 Interrupted
        }
        else
        {
            c.CurrentTask = null;
        }
    }

    /*****
    Date: 2026-09-06
    Name: AdvanceMovement
    Description: 按移动预算（游戏分钟 × 移动速度 × 装备移速加成）沿路径逐格推进角色；抵达目标格后按移动来源分流——任务驱动进入 Working 开始作业，手动移动（右键点地）回到 Idle 并立即重新找活。
    *****/
    private void AdvanceMovement(CharacterSim c, double deltaGameMinutes)
    {
        if (c.Path == null)
        {
            c.SetState(CharacterState.Idle);
            return;
        }

        double budget = deltaGameMinutes * WalkCellsPerGameMinute * (1.0 + c.Inventory.MoveSpeedBonusPercent);
        IReadOnlyList<Vector2I> path = c.Path;

        while (budget > 0)
        {
            if (c.PathIndex >= path.Count)
            {
                OnArrived(c);
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
    Date: 2026-09-26
    Name: OnArrived
    Description: 抵达路径终点：手动移动（右键点地）→ 清路径、回 Idle 并立即找活（受忽略清单与优先级约束）；任务驱动 → 进入 Working 并启动任务。
    *****/
    private void OnArrived(CharacterSim c)
    {
        if (c.IsManualMove)
        {
            c.Path = null;
            c.IsManualMove = false;
            c.SetState(CharacterState.Idle);
            TryPickTask(c);
            return;
        }
        ArriveAtWork(c);
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
    Date: 2026-09-25
    Name: AdvanceWork
    Description: 推进任务进度（游戏分钟 × 专长效率倍率 × **装备工作效率加成**，并把推进者本人交给任务——设施作业据此「谁推进就从谁的背包扣料」）；任务完成或任务已失效时清理角色任务引用、回到 Idle 并立即重新找活（TryPickTask）——干完一件事当帧即接续下一件，直到确实无事可做。
    2026-09-27 调整：原来的「主工缺料先脱离去补料」前置取消——份额制下**料尽即本人退场**（由 FacilityWorkTask.ProgressWork 内部让该工人脱离），任务的续行由轮末统一扫描决定（见 SettleFinishedRounds）。
    *****/
    private void AdvanceWork(CharacterSim c, double deltaGameMinutes)
    {
        ITask? task = c.CurrentTask;
        if (task == null || task.State is not (TaskState.Assigned or TaskState.InProgress))
        {
            c.CurrentTask = null;
            c.SetState(CharacterState.Idle);
            TryPickTask(c); // 任务已失效（取消/完成）：立即找下一件
            return;
        }

        float multiplier = (task.RequiredSpecialty is { } specialty
            ? c.GetWorkMultiplierFor(specialty)
            : 1f) * (1f + c.Inventory.WorkSpeedBonusPercent);
        bool done = task.ProgressWork(c, deltaGameMinutes * multiplier);
        if (done)
        {
            c.CurrentTask = null;
            c.SetState(CharacterState.Idle);
            TryPickTask(c); // 完工即接活
        }
    }
}
