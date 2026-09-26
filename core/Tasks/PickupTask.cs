using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Items;

namespace RelayStation.Core.Tasks;

/*****
Date: 2026-09-26
Name: PickupOutcome
Description: 拾取任务的结束结果；供表现层把「拿完了多少 / 为什么停下」转成世界提示。
*****/
public enum PickupOutcome
{
    /*****
    Date: 2026-09-26
    Name: Transferred
    Description: 已转移（含部分转移：背包再装不下或目标堆提前被取空，已转移的留在背包）。
    *****/
    Transferred,

    /*****
    Date: 2026-09-26
    Name: Gone
    Description: 一件都没拿到——目标堆在出发前或途中被别人取空/收走。
    *****/
    Gone,

    /*****
    Date: 2026-09-26
    Name: NoCapacity
    Description: 一件都装不下（背包已满 / 无剩余负重）。
    *****/
    NoCapacity,

    /*****
    Date: 2026-09-26
    Name: Unreachable
    Description: 目标位置不可抵达（物品被围住或不在可达区域内）——`Simulation.OrderPickup` 的即时预检结果。
    *****/
    Unreachable,
}

/*****
Date: 2026-09-26
Name: PickupTask
Description: 拾取任务（个人任务）；**逐件转移**（参考《僵尸毁灭工程》）——角色走到来源所在格后，每 `TransferGameMinutesPerItem` 游戏分钟从来源转移 **1 个**物品到背包（来源扣 1 件 + 背包加 1 件），直到：①再装不下下一件（容量耗尽）②来源已空 ③计划件数用尽。无固定总时长，完成条件是动态终点；取消/中止无回滚（已转移的在背包、未转移的留在来源）。物品的基础单位是「个」，堆（ItemStack）只是「一图多物」的表现分组——本任务的进度、容量与转移全部按个结算。
2026-09-27 泛化：取货口径由「地面物品堆 + 注册表」改由 `IItemSource` 抽象承载（今日实现为 `GroundItemSource`，后续容器设施实现同一接口即可接入）；新增 `maxCount` 计划上限与 `servedTask` 服务链接——为设施作业备料时传「分配给本人的份额」并挂上它服务的作业，玩家手动拾取则不限量、不挂链接。
*****/
public sealed class PickupTask : TaskBase
{
    /*****
    Date: 2026-09-26
    Name: TransferGameMinutesPerItem
    Description: 每转移 **1 个**物品所需的游戏分钟数（逐件节奏；可调常量）。装备的工作效率加成会让该节奏按倍率加快（由模拟层乘算推进量）。
    *****/
    public const double TransferGameMinutesPerItem = 0.5;

    /*****
    Date: 2026-09-26
    Name: _owner
    Description: 任务归属者（玩家指令指定的角色，或自动备料的受益人；只有本人可认领）。
    *****/
    private readonly CharacterSim _owner;

    /*****
    Date: 2026-09-27
    Name: _source
    Description: 取货来源（地面物品堆或后续的容器设施）；逐件从此处取出。
    *****/
    private readonly IItemSource _source;

    /*****
    Date: 2026-09-27
    Name: _maxCount
    Description: 计划转移件数上限（int.MaxValue = 不限量，装满为止）；为新设施作业备料时传「分配给本人的份额」。
    *****/
    private readonly int _maxCount;

    /*****
    Date: 2026-09-27
    Name: _servedTask
    Description: 本条拾取任务**服务的设施作业**（为它备料时由模拟层注入；玩家手动拾取为 null）。用途有二：①被中止时连同该作业一起退出（否则当场又会回来取料）；②登记的作业侧据此把「在途预留量」计入已认领，避免多人并行取料超取。
    *****/
    private readonly FacilityWorkTask? _servedTask;

    /*****
    Date: 2026-09-26
    Name: _onOutcome
    Description: 结束回调（供表现层提示「拿完了 / 物品已被取走 / 装不下」）；可选。
    *****/
    private readonly Action<PickupOutcome>? _onOutcome;

    /*****
    Date: 2026-09-26
    Name: _accrued
    Description: 单件转移的时间累加器（凑满 `TransferGameMinutesPerItem` 即转移 1 件，余数保留）。
    *****/
    private double _accrued;

    /*****
    Date: 2026-09-26
    Name: _planned
    Description: 计划转移件数（抵达时按「来源件数」「计划上限」与「当时可装下件数」取小估算；仅用于进度条与时长展示，途中变化不影响结算）。
    *****/
    private int _planned;

    /*****
    Date: 2026-09-26
    Name: _transferred
    Description: 已实际转移的件数。
    *****/
    private int _transferred;

    /*****
    Date: 2026-09-27
    Name: PickupTask
    Description: 构造函数；owner 为指定的角色（个人任务），source 为取货来源，onOutcome 为结束回调，priority 默认 5 档（玩家指令默认档），maxCount 为计划件数上限（默认不限量），servedTask 为本任务服务的设施作业（默认 null = 玩家手动拾取）。
    *****/
    public PickupTask(CharacterSim owner, IItemSource source, Action<PickupOutcome>? onOutcome = null,
        TaskPriority priority = TaskPriority.P5, int maxCount = int.MaxValue,
        FacilityWorkTask? servedTask = null)
        : base(maxWorkers: 1, priority)
    {
        _owner = owner;
        _source = source;
        _onOutcome = onOutcome;
        _maxCount = Math.Max(0, maxCount);
        _servedTask = servedTask;
    }

    /*****
    Date: 2026-09-26
    Name: ItemTarget
    Description: 目标物品堆（ITask 成员实现）；来源为地面物品堆时给出该堆（表现层据此显示进度条），容器等其它来源为 null。
    *****/
    public override ItemStack? ItemTarget => (_source as GroundItemSource)?.Stack;

    /*****
    Date: 2026-09-26
    Name: Owner
    Description: 归属者（个人任务：只有本人可认领/加入）。
    *****/
    public override CharacterSim? Owner => _owner;

    /*****
    Date: 2026-09-27
    Name: SourceDef
    Description: 来源物品定义；供模拟层判断「本人是否已有一条取同种物品的备料任务」。
    *****/
    public IItemDef SourceDef => _source.Def;

    /*****
    Date: 2026-09-27
    Name: ServedTask
    Description: 本条拾取任务服务的设施作业（备料链接）；玩家手动拾取为 null。
    *****/
    public FacilityWorkTask? ServedTask => _servedTask;

    /*****
    Date: 2026-09-27
    Name: IntendedCount
    Description: 本条任务**总共**打算取多少件（在途预留量，含已经取到手里的）：未开工按计划上限、已开工按计划件数。供它服务的作业把「这一份已被认领」计入覆盖量——必须按**总量**而非「还没取的余量」算，否则已经在取料人兜里的那些件会漏算，别人会误以为还有份额可领而重复取料。
    *****/
    public int IntendedCount
        => _planned > 0 ? Math.Min(_planned, _maxCount) : _maxCount;

    /*****
    Date: 2026-09-26
    Name: PreferredWorkCell
    Description: 作业格即来源所在格（物品恒在地板格且不阻挡通行，可直接站上去）。
    *****/
    public override Vector2I? PreferredWorkCell(CharacterSim c) => _source.Cell;

    /*****
    Date: 2026-09-26
    Name: IsStillValid
    Description: 来源仍有可取物品。
    *****/
    public override bool IsStillValid() => _source.AvailableCount > 0;

    /*****
    Date: 2026-09-26
    Name: PlannedCount
    Description: 计划转移件数（抵达时的估算值）。
    *****/
    public int PlannedCount => _planned;

    /*****
    Date: 2026-09-26
    Name: TransferredCount
    Description: 已实际转移件数。
    *****/
    public int TransferredCount => _transferred;

    /*****
    Date: 2026-09-26
    Name: TotalGameMinutes
    Description: 展示用总时长（计划件数 × 每件耗时）；不参与完成判定（完成是动态终点）。
    *****/
    public override double TotalGameMinutes => _planned * TransferGameMinutesPerItem;

    /*****
    Date: 2026-09-26
    Name: RemainingGameMinutes
    Description: 展示用剩余时长（剩余件数 × 每件耗时）；不参与完成判定。
    *****/
    public override double RemainingGameMinutes
        => Math.Max(0, _planned - _transferred) * TransferGameMinutesPerItem;

    /*****
    Date: 2026-09-26
    Name: ProgressFraction
    Description: 进度比例 = 已转移件数 / 计划件数（计划为 0 时按 0）。
    *****/
    public override double ProgressFraction
        => _planned <= 0 ? 0.0 : Math.Clamp((double)_transferred / _planned, 0.0, 1.0);

    /*****
    Date: 2026-09-26
    Name: StartWork
    Description: 抵达来源格后开始转移（Assigned → InProgress）：估算计划件数 = min（来源件数，计划上限，当时可装下件数）；一件都装不下时立即以 `NoCapacity` 收尾（不动物品）。
    *****/
    public override void StartWork()
    {
        if (State != TaskState.Assigned) return;
        SetState(TaskState.InProgress);

        if (!IsStillValid())
        {
            Finish(PickupOutcome.Gone); // 途中已被他人取空
            return;
        }

        int addable = _owner.Inventory.MaxAddable(_source.Def);
        _planned = Math.Max(0, Math.Min(Math.Min(_source.AvailableCount, addable), _maxCount));
        if (_planned <= 0)
        {
            Finish(PickupOutcome.NoCapacity);
        }
    }

    /*****
    Date: 2026-09-26
    Name: ProgressWork
    Description: 逐件推进：累加时间，每凑满一个单件周期就转移 1 个（来源 −1、背包 +1）；来源失效、容量耗尽或计划用尽即收尾。返回 true 表示任务已完成。
    *****/
    public override bool ProgressWork(CharacterSim worker, double gameMinutes)
    {
        if (State != TaskState.InProgress) return false;

        _accrued += Math.Max(0, gameMinutes);
        while (_accrued >= TransferGameMinutesPerItem)
        {
            _accrued -= TransferGameMinutesPerItem;
            if (!TransferOne()) return State == TaskState.Done;
        }
        return false;
    }

    /*****
    Date: 2026-09-26
    Name: TransferOne
    Description: 转移 1 个物品（来源扣 1 件 + 背包加 1 件）；返回 false 表示本任务已收尾（完成或取消），调用方应停止继续转移。收尾原因按「有没有拿到过」区分：一件都没拿到 → Gone，拿到过 → Transferred（含部分转移）。
    *****/
    private bool TransferOne()
    {
        if (!IsStillValid())
        {
            Finish(PickupOutcome.Gone);
            return false;
        }
        if (_transferred >= _planned || _owner.Inventory.MaxAddable(_source.Def) <= 0)
        {
            Finish(PickupOutcome.Transferred);
            return false;
        }
        if (!_source.TryTake(1, out int taken) || taken <= 0)
        {
            Finish(PickupOutcome.Gone);
            return false;
        }
        if (!_owner.Inventory.TryAdd(_source.Def, 1))
        {
            // 兜底：预检已保证装得下，理论不会走到这里；真的发生则把刚取下的 1 件原样放回来源
            _source.Return(1);
            Finish(PickupOutcome.Transferred);
            return false;
        }

        _transferred++;
        if (!IsStillValid() || _transferred >= _planned || _owner.Inventory.MaxAddable(_source.Def) <= 0)
        {
            Finish(PickupOutcome.Transferred);
            return false;
        }
        return true;
    }

    /*****
    Date: 2026-09-26
    Name: Finish
    Description: 收尾：转 Done 并回调结束结果（一件都没拿到时把 Transferred 修正为 Gone）。
    *****/
    private void Finish(PickupOutcome outcome)
    {
        if (outcome == PickupOutcome.Transferred && _transferred == 0) outcome = PickupOutcome.Gone;
        SetState(TaskState.Done);
        _onOutcome?.Invoke(outcome);
    }
}