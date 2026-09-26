using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Items;

namespace RelayStation.Core.Tasks;

/*****
Date: 2026-09-26
Name: EquipOutcome
Description: 穿戴任务的结束结果；供表现层把「穿上了 / 物品没了 / 换不下来」转成世界提示。
*****/
public enum EquipOutcome
{
    /*****
    Date: 2026-09-26
    Name: Worn
    Description: 已穿上（同槽旧装备按规则回背包或就地落地）。
    *****/
    Worn,

    /*****
    Date: 2026-09-26
    Name: Gone
    Description: 目标（地面装备或背包内物品）在完成前消失。
    *****/
    Gone,

    /*****
    Date: 2026-09-26
    Name: Blocked
    Description: 穿不上（换下的旧装备既回不了背包也落不了地，或物品不可装备）。
    *****/
    Blocked,
}

/*****
Date: 2026-09-26
Name: EquipTask
Description: 穿戴任务（个人任务）；两模式：①**背包 → 槽位**（就地耗时 `EquipGameMinutes` 后 `TryEquip`，同槽旧装备回背包，回不去则视为穿不上）；②**地面 → 槽位**（走到物品所在格，按同一耗时计时——含取下与穿上两件事，中途不产生「拿在手上」的中间状态；完成时一次性「地面扣 1 件 + `TryWearDirect` 写入槽位」）。②中若同槽旧装备回不了背包则**就地落地**（经注入的 dropEquipped 回调，由模拟层挑可放置格），否则整笔拒绝并回调提示。
*****/
public sealed class EquipTask : TaskBase
{
    /*****
    Date: 2026-09-26
    Name: EquipGameMinutes
    Description: 穿戴耗时（游戏分钟）；背包路径与地面路径共用（地面路径含取下与穿上，不拆成两段）。
    *****/
    public const double EquipGameMinutes = 5.0;

    /*****
    Date: 2026-09-26
    Name: Tolerance
    Description: 超重比较容差（与 CharacterInventory 同一口径）。
    *****/
    private const float Tolerance = 1e-4f;

    /*****
    Date: 2026-09-26
    Name: _owner
    Description: 任务归属者（个人任务：只有本人可认领/加入）。
    *****/
    private readonly CharacterSim _owner;

    /*****
    Date: 2026-09-26
    Name: _def
    Description: 要穿戴的物品定义。
    *****/
    private readonly IItemDef _def;

    /*****
    Date: 2026-09-26
    Name: _groundTarget
    Description: 地面来源堆（模式②）；null 表示模式①（从背包穿）。
    *****/
    private readonly ItemStack? _groundTarget;

    /*****
    Date: 2026-09-26
    Name: _registry
    Description: 地面物品注册表（模式②的取货来源；模式①为 null）。
    *****/
    private readonly ItemRegistry? _registry;

    /*****
    Date: 2026-09-26
    Name: _dropEquipped
    Description: 「把指定槽位的装备就地落地」回调（由模拟层注入：挑可放置格、投放地面并清空槽位；无可放置格返回 false）。供「换下的旧装备回不了背包」时使用。
    *****/
    private readonly Func<CharacterSim, EquipmentSlot, bool>? _dropEquipped;

    /*****
    Date: 2026-09-26
    Name: _onOutcome
    Description: 结束回调（供表现层提示）；可选。
    *****/
    private readonly Action<EquipOutcome>? _onOutcome;

    /*****
    Date: 2026-09-26
    Name: _elapsed
    Description: 已累计的游戏分钟。
    *****/
    private double _elapsed;

    /*****
    Date: 2026-09-26
    Name: EquipTask
    Description: 构造函数；owner 为本人，def 为要穿的物品，groundTarget 为地面来源堆（null = 从背包穿），registry 为地面物品注册表（地面路径必填），dropEquipped 为落地回调（可选），onOutcome 为结束回调，priority 默认 5 档。
    *****/
    public EquipTask(CharacterSim owner, IItemDef def, ItemStack? groundTarget = null,
        ItemRegistry? registry = null, Func<CharacterSim, EquipmentSlot, bool>? dropEquipped = null,
        Action<EquipOutcome>? onOutcome = null, TaskPriority priority = TaskPriority.P5)
        : base(maxWorkers: 1, priority)
    {
        _owner = owner;
        _def = def;
        _groundTarget = groundTarget;
        _registry = registry;
        _dropEquipped = dropEquipped;
        _onOutcome = onOutcome;
    }

    /*****
    Date: 2026-09-26
    Name: ItemTarget
    Description: 地面来源堆（模式②）；从背包穿时为 null。
    *****/
    public override ItemStack? ItemTarget => _groundTarget;

    /*****
    Date: 2026-09-26
    Name: Owner
    Description: 归属者（个人任务）。
    *****/
    public override CharacterSim? Owner => _owner;

    /*****
    Date: 2026-09-26
    Name: TargetDef
    Description: 本次要穿戴的物品定义；供表现层（角色头顶标签）显示目标名。
    *****/
    public IItemDef TargetDef => _def;

    /*****
    Date: 2026-09-26
    Name: PreferredWorkCell
    Description: 作业格：地面路径为物品所在格；背包路径为角色当前格（就地，模拟层不寻路）。
    *****/
    public override Vector2I? PreferredWorkCell(CharacterSim c) => _groundTarget?.Cell ?? c.Cell;

    /*****
    Date: 2026-09-26
    Name: IsStillValid
    Description: 地面路径要求堆仍有物品；背包路径要求背包里仍有该物品。
    *****/
    public override bool IsStillValid()
        => _groundTarget != null ? _groundTarget.Count > 0 : _owner.Inventory.CountOf(_def) > 0;

    /*****
    Date: 2026-09-26
    Name: TotalGameMinutes
    Description: 基准总耗时（穿戴耗时）。
    *****/
    public override double TotalGameMinutes => EquipGameMinutes;

    /*****
    Date: 2026-09-26
    Name: RemainingGameMinutes
    Description: 剩余完成所需的游戏分钟数。
    *****/
    public override double RemainingGameMinutes => Math.Max(0.0, EquipGameMinutes - _elapsed);

    /*****
    Date: 2026-09-26
    Name: ProgressFraction
    Description: 进度比例（0~1）。
    *****/
    public override double ProgressFraction
        => EquipGameMinutes <= 0 ? 1.0 : Math.Clamp(_elapsed / EquipGameMinutes, 0.0, 1.0);

    /*****
    Date: 2026-09-26
    Name: StartWork
    Description: 开始穿戴（Assigned → InProgress）。
    *****/
    public override void StartWork()
    {
        if (State != TaskState.Assigned) return;
        SetState(TaskState.InProgress);
    }

    /*****
    Date: 2026-09-26
    Name: ProgressWork
    Description: 计时推进；到时完成穿戴（见 Complete）。物品在过程中消失 → 取消并回调 `Gone`。
    *****/
    public override bool ProgressWork(CharacterSim worker, double gameMinutes)
    {
        if (State != TaskState.InProgress) return false;
        if (!IsStillValid())
        {
            MarkCancelled();
            _onOutcome?.Invoke(EquipOutcome.Gone);
            return false;
        }

        _elapsed += Math.Max(0, gameMinutes);
        if (_elapsed < EquipGameMinutes) return false;

        Complete();
        return State == TaskState.Done;
    }

    /*****
    Date: 2026-09-26
    Name: Complete
    Description: 完成穿戴：背包路径走 `TryEquip`（旧装备回背包，回不去则拒绝）；地面路径先做可行性预检（旧装备若需落地，则确认背包装得下与可落地），再「地面扣 1 件 + 旧装备落地 + TryWearDirect」；任一环节失败都回滚到「物品不丢」的状态并取消。
    *****/
    private void Complete()
    {
        if (_groundTarget == null)
        {
            if (_owner.Inventory.TryEquip(_def))
            {
                SetState(TaskState.Done);
                _onOutcome?.Invoke(EquipOutcome.Worn);
            }
            else
            {
                MarkCancelled();
                _onOutcome?.Invoke(EquipOutcome.Blocked);
            }
            return;
        }

        EquipmentSlot slot = _def.EquipSlot;
        if (slot == EquipmentSlot.None || _registry == null)
        {
            MarkCancelled();
            _onOutcome?.Invoke(EquipOutcome.Blocked);
            return;
        }

        IItemDef? old = _owner.Inventory.GetEquipped(slot);
        bool oldToGround = old != null && !ReferenceEquals(old, _def) && !_owner.Inventory.CanAdd(old, 1);
        if (oldToGround)
        {
            // 旧装备回不了背包：需能就地落地，且穿上新装备后不得超重
            float capacity = _owner.Inventory.CapacityWithout(slot) + MathF.Max(0f, _def.ContainerCapacityKg);
            if (_dropEquipped == null || _owner.Inventory.TotalWeightKg > capacity + Tolerance)
            {
                MarkCancelled();
                _onOutcome?.Invoke(EquipOutcome.Blocked);
                return;
            }
        }

        if (!_registry.Remove(_groundTarget, 1))
        {
            MarkCancelled();
            _onOutcome?.Invoke(EquipOutcome.Gone);
            return;
        }

        if (oldToGround) _dropEquipped!(_owner, slot); // 预检已过：旧装备落地、槽位清空

        if (!_owner.Inventory.TryWearDirect(_def))
        {
            _registry.Add(_def, 1, _groundTarget.Cell, _groundTarget.PixelOffset); // 回滚：新装备放回地面，物品不丢
            MarkCancelled();
            _onOutcome?.Invoke(EquipOutcome.Blocked);
            return;
        }

        SetState(TaskState.Done);
        _onOutcome?.Invoke(EquipOutcome.Worn);
    }
}