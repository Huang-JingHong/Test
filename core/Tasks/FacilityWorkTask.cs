using RelayStation.Core.Characters;
using RelayStation.Core.Facilities;
using RelayStation.Core.Items;

namespace RelayStation.Core.Tasks;

/*****
Date: 2026-09-25
Name: FacilityWorkTask
Description: 设施作业任务抽象基类；在 TaskBase（状态机与协作机制）之上承载「修复/建造/拆除」三类作业的专属机制——按剩余游戏分钟推进、随进度实时整件消耗备用零件（小数累计）、进度查询（TotalGameMinutes / ProgressFraction）与中止/恢复（Suspend/Resume，保留进度与设施状态）。子类只需覆写专长与三个钩子（OnWorkStarted / OnWorkCompleted / OnWorkCancelled）即可表达差异化的设施状态迁移，扩展新作业类型零改框架。
2026-09-26 重构：状态机、工作者列表、认领资格、优先级、挂起/恢复与取消释放等公共机制上移至 TaskBase（与物品类任务共用同一套生命周期），本类只保留设施作业特有部分，行为逐字不变。
2026-09-27 零件改为「按人结算的份额制」：①**谁推进就从谁的背包扣料**（ProgressWork 带推进者），多名工人各扣各的、合计恰好等于全程成本，不再有「主工专属带料」；②新增 `UnclaimedPartsNeed`（剩余需求 − Σ在场工人已带的料 − Σ在途备料任务还打算取的件数）——认领者据此**自动分配份额**（再由背包容量封顶），多人可并行去取料而不超取；③某人的料不足以支付下一整件时，**本人这一轮到头、自己退出**（不挂起任务、不补料、不拉别人垫），由其余工人继续；若他是最后一个，模拟层的轮末扫描再决定「自动开下一轮」还是「转已中止」；④作业跑完时由最后推进者补齐余额（SettleRemainingParts），保证跑完恰好扣满成本。模拟层的取料入口见 Simulation.TrySendForParts。
*****/
public abstract class FacilityWorkTask : TaskBase
{
    /*****
    Date: 2026-09-25
    Name: _remainingGameMinutes
    Description: 剩余完成所需游戏分钟。
    *****/
    private double _remainingGameMinutes;

    /*****
    Date: 2026-09-25
    Name: _totalGameMinutes
    Description: 基准总耗时（游戏分钟）。
    *****/
    private readonly double _totalGameMinutes;

    /*****
    Date: 2026-09-25
    Name: _totalPartsCost
    Description: 全程消耗的备用零件总数（0 表示无消耗）。
    *****/
    private readonly int _totalPartsCost;

    /*****
    Date: 2026-09-27
    Name: _partsItem
    Description: 备用零件的物品定义（扣料就是扣工人自己背包里的这种物品）；null 表示不消耗（引擎外测试 / 开发者模式）。
    *****/
    private readonly IItemDef? _partsItem;

    /*****
    Date: 2026-09-25
    Name: _partsAccrued
    Description: 零件消耗小数累计器（按进度比例累计，凑整扣除后保留余数）。
    *****/
    private double _partsAccrued;

    /*****
    Date: 2026-09-25
    Name: _partsConsumed
    Description: 已实际消耗的备用零件数。
    *****/
    private int _partsConsumed;

    /*****
    Date: 2026-09-27
    Name: _fetches
    Description: 正在为本任务备料的**在途**拾取任务（工人认领了份额、人还在去料堆的路上或正在取）；用于「未认领需求」与「本轮是否还有人参与」的判定，任务终止时自动移出。
    *****/
    private readonly List<PickupTask> _fetches = new();

    /*****
    Date: 2026-09-25
    Name: Target
    Description: 任务目标设施。
    *****/
    public override FacilitySim? Target { get; }

    /*****
    Date: 2026-09-25
    Name: RemainingGameMinutes
    Description: 剩余完成所需的游戏分钟数。
    *****/
    public override double RemainingGameMinutes => _remainingGameMinutes;

    /*****
    Date: 2026-09-25
    Name: TotalGameMinutes
    Description: 基准总耗时（游戏分钟；效率加成前的基准值）。
    *****/
    public override double TotalGameMinutes => _totalGameMinutes;

    /*****
    Date: 2026-09-26
    Name: ProgressFraction
    Description: 进度比例（0~1，供进度条等 UI 使用）：以「已耗零件 / 全程零件」为准——把「可见进度」与「已实际消耗」严格锁死，使「任务中止」功能无法被"先跑进度却不耗料 → 中止 → 重开 → 免费完成"钻空子（时间照旧推进、多人照旧按专长倍率倍速，只是展示口径改为零件口径）。零件成本为 0、或未注入零件定义（开发者模式 / 引擎外测试）时，回退为按时间比例换算；已完成恒为 1.0。
    *****/
    public override double ProgressFraction
    {
        get
        {
            if (State == TaskState.Done) return 1.0;
            if (_totalPartsCost > 0 && _partsItem != null)
            {
                return Math.Clamp((double)_partsConsumed / _totalPartsCost, 0.0, 1.0);
            }
            return _totalGameMinutes <= 0
                ? 0.0
                : Math.Clamp(1.0 - _remainingGameMinutes / _totalGameMinutes, 0.0, 1.0);
        }
    }

    /*****
    Date: 2026-09-25
    Name: TotalPartsCost
    Description: 全程消耗的备用零件总数；0 表示无消耗。
    *****/
    public override int TotalPartsCost => _totalPartsCost;

    /*****
    Date: 2026-09-25
    Name: PartsConsumed
    Description: 已实际消耗的备用零件数（随进度实时累计）。
    *****/
    public override int PartsConsumed => _partsConsumed;

    /*****
    Date: 2026-09-27
    Name: PartsItem
    Description: 备用零件的物品定义（供模拟层按它去找「最近的零件来源」并预检背包容量）；null 表示本任务不消耗零件（免费模式）。
    *****/
    public IItemDef? PartsItem => _partsItem;

    /*****
    Date: 2026-09-27
    Name: RemainingPartsNeed
    Description: 从此刻起仍需消耗的备用零件总数（全程成本 − 已耗）；0 表示后半程不再需要零件，或本任务不消耗零件。
    *****/
    public int RemainingPartsNeed
        => _partsItem == null ? 0 : Math.Max(0, _totalPartsCost - _partsConsumed);

    /*****
    Date: 2026-09-27
    Name: UnclaimedPartsNeed
    Description: **尚未被任何人认领**的零件需求 = 剩余需求 − Σ(在场工人背包里该种零件的件数) − Σ(在途备料任务还打算取的件数)。新认领者的「份额」即取此值（再由背包容量封顶），因此多人并行认领既不会超取、也不会有人白跑一趟。
    *****/
    public int UnclaimedPartsNeed
    {
        get
        {
            if (_partsItem == null) return 0;
            int covered = 0;
            foreach (CharacterSim worker in Workers) covered += worker.Inventory.CountOf(_partsItem);
            foreach (PickupTask fetch in _fetches) covered += fetch.IntendedCount;
            return Math.Max(0, RemainingPartsNeed - covered);
        }
    }

    /*****
    Date: 2026-09-27
    Name: InvolvedCount
    Description: 已被占用的投入名额 = 在场工人 + 正在为本任务备料的人（**在途取料者也占名额**：他们取完料就会回来接着干）。用于限制本轮参与者不超过 `MaxWorkers`（超了会出现「MaxWorkers=2 的活却有三个人去搬一趟」）与估算分份人数。
    *****/
    public int InvolvedCount => Workers.Count + _fetches.Count;

    /*****
    Date: 2026-09-27
    Name: HasFetchInFlight
    Description: 是否有人在途备料（认领了份额、去取料但还没回来）；供模拟层的轮末判定——在途的人也算「本轮还有人参与」。
    *****/
    public bool HasFetchInFlight => _fetches.Count > 0;

    /*****
    Date: 2026-09-27
    Name: RegisterFetch
    Description: 登记一条在途备料任务（由模拟层在提交备料后立刻调用，使同一帧内的其他认领者能把它算进「已认领」而不超取）；任务转入终态或挂起时自动移出。
    *****/
    public void RegisterFetch(PickupTask pickup)
    {
        if (_fetches.Contains(pickup)) return;
        _fetches.Add(pickup);
        pickup.StateChanged += OnFetchStateChanged;
    }

    /*****
    Date: 2026-09-27
    Name: UnregisterFetch
    Description: 撤回在途备料登记（备料没派出去时用，如任务板去重把提交吃掉了）。
    *****/
    public void UnregisterFetch(PickupTask pickup)
    {
        if (_fetches.Remove(pickup)) pickup.StateChanged -= OnFetchStateChanged;
    }

    /*****
    Date: 2026-09-27
    Name: OnFetchStateChanged
    Description: 在途备料任务的状态回调：转入终态或挂起即移出登记（份额回到「未认领」，等下一轮重新分配）。
    *****/
    private void OnFetchStateChanged(ITask task, TaskState state)
    {
        if (state is not (TaskState.Done or TaskState.Cancelled or TaskState.Suspended)) return;
        if (task is PickupTask pickup) UnregisterFetch(pickup);
    }

    /*****
    Date: 2026-09-25
    Name: PriorState
    Description: 任务发起前设施的原始状态（供子类在取消时回滚，避免设施卡在作业态）；目标为 null 时取 Damaged。
    *****/
    protected FacilityState PriorState { get; }

    /*****
    Date: 2026-09-25
    Name: RequiredSpecialty
    Description: 任务所属专长类型（由子类指定，决定角色专长效率加成是否匹配）。
    *****/
    public abstract override Specialty? RequiredSpecialty { get; }

    /*****
    Date: 2026-09-25
    Name: FacilityWorkTask
    Description: 构造函数；target 为目标设施，totalGameMinutes 为基准耗时，totalPartsCost 为全程零件消耗（默认 0 不消耗），partsItem 为工人背包里的零件物品定义（默认 null 不扣件），maxWorkers 为最大投入人数（默认 1），priority 为任务优先级（默认 5 档）。
    *****/
    protected FacilityWorkTask(FacilitySim? target, double totalGameMinutes,
        int totalPartsCost = 0, IItemDef? partsItem = null, int maxWorkers = 1,
        TaskPriority priority = TaskPriority.P5)
        : base(maxWorkers, priority)
    {
        Target = target;
        PriorState = target?.State ?? FacilityState.Damaged;
        _totalGameMinutes = totalGameMinutes;
        _remainingGameMinutes = totalGameMinutes;
        _totalPartsCost = Math.Max(0, totalPartsCost);
        _partsItem = partsItem;
    }

    /*****
    Date: 2026-09-27
    Name: CanTake
    Description: 开工门槛：**有得干才入列**——先挡两类白跑：①**名额已满**（在场工人 + 在途备料者已达 `MaxWorkers`，含他本人即将占的那个名额）；②**已有人（含本人）为本任务备料**（`HasOpenFetchFor`）。再按业务前置放行：免费模式、任务已无零件需求（收尾阶段）、本人背包已带该种零件、或任务还有未被认领的份额且装得下——四选一成立即可认领/加入；全被别人认领完、且自己两手空空者不入列（否则白跑一趟或站在设施前干不了活）。装不下零件的（剩余负重为 0）同样不入列——去了也搬不动。
    *****/
    protected override bool CanTake(CharacterSim c)
    {
        if (Workers.Count + _fetches.Count >= MaxWorkers) return false; // 名额已满（在途备料者也占名额）
        if (HasOpenFetchFor(c)) return false;
        return _partsItem == null
               || RemainingPartsNeed <= 0
               || c.Inventory.CountOf(_partsItem) > 0
               || (UnclaimedPartsNeed > 0 && c.Inventory.MaxAddable(_partsItem) > 0);
    }

    /*****
    Date: 2026-09-27
    Name: HasOpenFetchFor
    Description: 指定角色此刻是否正为本任务备料（有一条未结的备料拾取任务归他所有）：是则本任务对他不可认领——同优先级下任务板偏好更早提交者（本任务），若不挡住，他会被反复派回本任务，永远走不上自己的取料任务（实测死锁）。
    *****/
    private bool HasOpenFetchFor(CharacterSim c)
    {
        foreach (PickupTask fetch in _fetches)
        {
            if (ReferenceEquals(fetch.Owner, c)
                && fetch.State is TaskState.Pending or TaskState.Assigned or TaskState.InProgress)
            {
                return true;
            }
        }
        return false;
    }

    /*****
    Date: 2026-09-25
    Name: OnWorkStarted
    Description: 钩子；角色抵达目标、任务转入 InProgress 时调用（子类在此迁移设施状态）。
    *****/
    protected virtual void OnWorkStarted()
    {
    }

    /*****
    Date: 2026-09-25
    Name: OnWorkCompleted
    Description: 钩子；进度归零、任务转入 Done 后调用（子类在此迁移设施状态、返还资源或移除设施）。
    *****/
    protected virtual void OnWorkCompleted()
    {
    }

    /*****
    Date: 2026-09-25
    Name: OnWorkCancelled
    Description: 钩子；任务被取消且工作者已清空后调用（子类在此回滚设施状态或移除未完成设施）。
    *****/
    protected virtual void OnWorkCancelled()
    {
    }

    /*****
    Date: 2026-09-26
    Name: OnCancelled
    Description: 覆写基类取消钩子转调设施专用钩子 OnWorkCancelled（保持子类既有覆写点不变）。
    *****/
    protected override void OnCancelled() => OnWorkCancelled();

    /*****
    Date: 2026-09-26
    Name: SuspendedByPartsShortage
    Description: 本次挂起是否因「备用零件不足」被动触发；供 UI 说明原因，Resume 时清除。
    *****/
    public override bool SuspendedByPartsShortage => _suspendedByPartsShortage;

    /*****
    Date: 2026-09-26
    Name: _suspendedByPartsShortage
    Description: 「因零件不足被动中止」标记的存储字段（覆写只读属性后由字段承载可变状态）。
    *****/
    private bool _suspendedByPartsShortage;

    /*****
    Date: 2026-09-26
    Name: SuspendForPartsShortage
    Description: 被动中止：因零件（料）不足而挂起——与主动 Suspend 同一语义，仅额外打上原因标记供 UI 显示「零件不足」。触发点为「全基地没有任何可达的零件来源」：模拟层想替认领者派备料却无处可取时直接挂起本任务（见 Simulation.TrySendForParts），避免「发布 → 取不到料 → 退出 → 又发布」的空转；玩家补料后点「继续」恢复。
    *****/
    public void SuspendForPartsShortage()
    {
        _suspendedByPartsShortage = true;
        Suspend();
    }

    /*****
    Date: 2026-09-26
    Name: Resume
    Description: 恢复挂起的任务（Suspended → Pending）：先清除「因零件不足被动中止」标记，再走基类的恢复（任务板在状态事件中重新入队并由调用方补发提交事件）。
    *****/
    public override void Resume()
    {
        if (State != TaskState.Suspended) return;
        _suspendedByPartsShortage = false;
        base.Resume();
    }

    /*****
    Date: 2026-09-25
    Name: StartWork
    Description: 开始执行（Assigned → InProgress）并调用 OnWorkStarted；对已 InProgress 的调用幂等无操作（多人协作时后到者不会重复迁移设施状态，队友取料回来后重新加入也不会重复置状态）。
    *****/
    public override void StartWork()
    {
        if (State != TaskState.Assigned) return;
        SetState(TaskState.InProgress);
        OnWorkStarted();
    }

    /*****
    Date: 2026-09-25
    Name: ProgressWork
    Description: 按给定游戏分钟扣减剩余量，同时按有效进度比例**从推进者自己的背包**实时消耗备用零件（整件扣除、小数累计）——多人各扣各的，合计恰好等于全程成本。**推进者自己的料不足以支付下一整件时，视为「他这一轮到头」**：让他脱离任务（不动进度、不挂起任务、不补料、不拉别人垫），由其余工人继续；若他是最后一个，模拟层的轮末扫描再决定「自动开下一轮」还是「转已中止」。剩余量归零时任务完成（→ Done）并调用 OnWorkCompleted，返回 true；仅在 InProgress 状态有效。
    *****/
    public override bool ProgressWork(CharacterSim worker, double gameMinutes)
    {
        if (State != TaskState.InProgress) return false;

        // 实时消耗：以本帧有效进度（不超过剩余量）折算零件，从推进者自己的背包扣除
        double effectiveMinutes = Math.Min(gameMinutes, _remainingGameMinutes);
        if (effectiveMinutes > 0 && _totalGameMinutes > 0 && _partsItem != null && _totalPartsCost > 0)
        {
            double fraction = effectiveMinutes / _totalGameMinutes;
            double needed = _partsAccrued + _totalPartsCost * fraction;
            int whole = (int)needed;
            if (whole > 0)
            {
                if (worker.Inventory.CountOf(_partsItem) < whole)
                {
                    ReleaseWorker(worker); // 份额用尽：本人这一轮到头，自己退出（任务与其余工人不受影响）
                    return false;
                }
                worker.Inventory.Remove(_partsItem, whole);
                _partsConsumed += whole;
                _partsAccrued = needed - whole;
            }
            else
            {
                _partsAccrued = needed;
            }
        }

        _remainingGameMinutes -= gameMinutes;
        if (_remainingGameMinutes > 0) return false;

        // 收尾结算：整段作业已完成，先把小数累计器的余额补齐，再判定完成
        if (!SettleRemainingParts(worker)) return false;

        _remainingGameMinutes = 0;
        SetState(TaskState.Done);
        OnWorkCompleted();
        return true;
    }

    /*****
    Date: 2026-09-27
    Name: SettleRemainingParts
    Description: 收尾结算：作业跑完时把「全程成本 − 已扣件数」的余额从推进者背包一次补齐（含小数累计器里的余数），保证**跑完的作业恰好扣满成本**——逐帧按比例累计会有浮点误差，成本 10 的作业可能只扣到 9 就判定完成。推进者料不够则让他脱离（返回 false），由轮末扫描决定是否开下一轮。
    *****/
    private bool SettleRemainingParts(CharacterSim worker)
    {
        if (_partsItem == null || _partsConsumed >= _totalPartsCost) return true;

        int settle = _totalPartsCost - _partsConsumed;
        if (worker.Inventory.CountOf(_partsItem) < settle)
        {
            ReleaseWorker(worker);
            return false;
        }

        worker.Inventory.Remove(_partsItem, settle);
        _partsConsumed += settle;
        _partsAccrued = 0;
        return true;
    }
}