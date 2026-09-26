using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Items;

namespace RelayStation.Core.Tasks;

/*****
Date: 2026-09-26
Name: ConsumeTask
Description: 取用（吃/喝）任务（个人任务，需求驱动）；把「满足需求」拆成两段——**先按逐件模型取货，再按消耗时长消耗 1 个**，完成后恢复对应需求。
两种模式：①**就地消耗背包**（无地面目标、作业格 = 角色当前格，不寻路）；②**前往地面取用**（作业格 = 目标堆所在格）——抵达后先逐件拾入背包（单趟上限 `maxTransfer` 件且受容量限制，每件按 `PickupTask.TransferGameMinutesPerItem`），再按物品定义里的 `ConsumeGameMinutes` 消耗 1 件；若一件都装不下（背包已满），则直接就地消耗地面 1 件（不入包）。
物品的基础单位是「个」：转移、消耗、需求恢复全部按个结算。
*****/
public sealed class ConsumeTask : TaskBase
{
    /*****
    Date: 2026-09-26
    Name: _needs
    Description: 需求系统（消耗完成后恢复对应需求）。
    *****/
    private readonly INeedSystem _needs;

    /*****
    Date: 2026-09-26
    Name: _owner
    Description: 任务归属者（需求本人的个人任务）。
    *****/
    private readonly CharacterSim _owner;

    /*****
    Date: 2026-09-26
    Name: _def
    Description: 要消耗的物品定义。
    *****/
    private readonly IItemDef _def;

    /*****
    Date: 2026-09-26
    Name: _target
    Description: 地面目标堆（模式②）；null 表示模式①（就地消耗背包）。
    *****/
    private readonly ItemStack? _target;

    /*****
    Date: 2026-09-26
    Name: _registry
    Description: 地面物品注册表（模式②的取货来源；模式①为 null）。
    *****/
    private readonly ItemRegistry? _registry;

    /*****
    Date: 2026-09-26
    Name: _maxTransfer
    Description: 单趟最多拾入背包的件数（自动取用上限）。
    *****/
    private readonly int _maxTransfer;

    /*****
    Date: 2026-09-26
    Name: _consumeMinutes
    Description: 消耗 1 件所需的游戏分钟数（取自物品定义）。
    *****/
    private readonly double _consumeMinutes;

    /*****
    Date: 2026-09-26
    Name: _restoreAmount
    Description: 消耗 1 件后恢复的需求值（取自物品定义）。
    *****/
    private readonly float _restoreAmount;

    /*****
    Date: 2026-09-26
    Name: _need
    Description: 本次消耗恢复的需求项（由 ConsumableKind 映射而来，需求层不反向依赖物品层）。
    *****/
    private readonly NeedId _need;

    /*****
    Date: 2026-09-26
    Name: _accrued
    Description: 单件转移的时间累加器（仅取货阶段使用）。
    *****/
    private double _accrued;

    /*****
    Date: 2026-09-26
    Name: _planned
    Description: 计划拾入件数（抵达时按「堆数量」「单趟上限」「可装下件数」取小估算）。
    *****/
    private int _planned;

    /*****
    Date: 2026-09-26
    Name: _transferred
    Description: 已拾入背包的件数。
    *****/
    private int _transferred;

    /*****
    Date: 2026-09-26
    Name: _consuming
    Description: 是否已进入消耗阶段（取货阶段结束或本就无需取货）。
    *****/
    private bool _consuming;

    /*****
    Date: 2026-09-26
    Name: _consumeElapsed
    Description: 消耗阶段已累计的游戏分钟。
    *****/
    private double _consumeElapsed;

    /*****
    Date: 2026-09-26
    Name: _consumeFromGround
    Description: 本次是否直接从地面消耗（背包一件都装不下时；此时完成时从目标堆扣 1 件而非背包）。
    *****/
    private bool _consumeFromGround;

    /*****
    Date: 2026-09-26
    Name: ConsumeTask
    Description: 构造函数；needs 为需求系统，owner 为本人，def 为要消耗的物品，target 为地面目标堆（null = 就地消耗背包），registry 为地面物品注册表（模式②必填），maxTransfer 为单趟拾入上限，priority 默认 Urgent（需求驱动优先于例行作业）。
    *****/
    public ConsumeTask(INeedSystem needs, CharacterSim owner, IItemDef def, ItemStack? target,
        ItemRegistry? registry, int maxTransfer = 3, TaskPriority priority = TaskPriority.Urgent)
        : base(maxWorkers: 1, priority)
    {
        _needs = needs;
        _owner = owner;
        _def = def;
        _target = target;
        _registry = registry;
        _maxTransfer = Math.Max(1, maxTransfer);
        _consumeMinutes = Math.Max(0.0, def.ConsumeGameMinutes);
        _restoreAmount = def.NeedRestoreAmount;
        _need = MapNeed(def.ConsumableKind);
    }

    /*****
    Date: 2026-09-26
    Name: ItemTarget
    Description: 地面目标堆（模式②）；就地模式为 null。
    *****/
    public override ItemStack? ItemTarget => _target;

    /*****
    Date: 2026-09-26
    Name: Owner
    Description: 归属者（个人任务：只有需求本人可认领/加入）。
    *****/
    public override CharacterSim? Owner => _owner;

    /*****
    Date: 2026-09-26
    Name: TargetDef
    Description: 本次消耗的物品定义；供模拟层的分钟扫描判断「本人是否已有同种取用任务」。
    *****/
    public IItemDef TargetDef => _def;

    /*****
    Date: 2026-09-26
    Name: PreferredWorkCell
    Description: 作业格：模式②为物品所在格；模式①为角色当前格（就地，模拟层不寻路）。
    *****/
    public override Vector2I? PreferredWorkCell(CharacterSim c) => _target?.Cell ?? c.Cell;

    /*****
    Date: 2026-09-26
    Name: IsStillValid
    Description: 背包里有该物品（模式①，或模式②已取货）或地面目标堆还有物品（模式②）。
    *****/
    public override bool IsStillValid()
        => _owner.Inventory.CountOf(_def) > 0 || (_target != null && _target.Count > 0);

    /*****
    Date: 2026-09-26
    Name: TotalGameMinutes
    Description: 展示用总时长（计划拾入件数 × 每件耗时 + 消耗耗时）。
    *****/
    public override double TotalGameMinutes
        => _planned * PickupTask.TransferGameMinutesPerItem + _consumeMinutes;

    /*****
    Date: 2026-09-26
    Name: RemainingGameMinutes
    Description: 展示用剩余时长（剩余件数 × 每件耗时 + 消耗剩余时间）。
    *****/
    public override double RemainingGameMinutes
        => Math.Max(0, _planned - _transferred) * PickupTask.TransferGameMinutesPerItem
           + Math.Max(0.0, _consumeMinutes - (_consuming ? _consumeElapsed : 0.0));

    /*****
    Date: 2026-09-26
    Name: ProgressFraction
    Description: 进度比例：以「计划拾入件数 + 1 个消耗步骤」为总工作量，已拾入件数与消耗阶段时间比例分别计入。
    *****/
    public override double ProgressFraction
    {
        get
        {
            double total = _planned + 1;
            if (total <= 0) return 0.0;
            double done = _transferred;
            if (_consuming && _consumeMinutes > 0)
            {
                done += Math.Clamp(_consumeElapsed / _consumeMinutes, 0.0, 1.0);
            }
            return Math.Clamp(done / total, 0.0, 1.0);
        }
    }

    /*****
    Date: 2026-09-26
    Name: StartWork
    Description: 抵达后进入取货阶段（Assigned → InProgress）：模式①直接进入消耗阶段；模式②估算计划件数 = min（堆数量，单趟上限，可装下件数），一件都装不下则改为「直接从地面消耗 1 件」。
    *****/
    public override void StartWork()
    {
        if (State != TaskState.Assigned) return;
        SetState(TaskState.InProgress);

        if (_target == null)
        {
            _planned = 0;
            _consuming = true;
            return;
        }

        int addable = _owner.Inventory.MaxAddable(_def);
        if (addable <= 0)
        {
            _planned = 0;
            _consuming = true;
            _consumeFromGround = true;
            return;
        }

        _planned = Math.Max(0, Math.Min(_target.Count, Math.Min(_maxTransfer, addable)));
        if (_planned <= 0)
        {
            _consuming = true;
            _consumeFromGround = true;
        }
    }

    /*****
    Date: 2026-09-26
    Name: ProgressWork
    Description: 推进两个阶段——取货阶段逐件拾入背包（每件一个单件周期），随后消耗阶段按 `ConsumeGameMinutes` 计时；完成时扣掉 1 件并恢复需求。目标失效（背包无货且地面堆已空）即取消。
    *****/
    public override bool ProgressWork(CharacterSim worker, double gameMinutes)
    {
        if (State != TaskState.InProgress) return false;
        if (!IsStillValid())
        {
            MarkCancelled();
            return false;
        }

        if (!_consuming)
        {
            _accrued += Math.Max(0, gameMinutes);
            while (!_consuming && _accrued >= PickupTask.TransferGameMinutesPerItem)
            {
                _accrued -= PickupTask.TransferGameMinutesPerItem;
                TransferOne();
            }
        }

        if (!_consuming) return false;

        _consumeElapsed += Math.Max(0, gameMinutes);
        if (_consumeElapsed < _consumeMinutes) return false;

        Complete();
        return State == TaskState.Done;
    }

    /*****
    Date: 2026-09-26
    Name: TransferOne
    Description: 从地面转移 **1 件**到背包（逐件模型）；堆空、容量耗尽或计划用尽时切到消耗阶段（已拿到的先吃掉，没拿到的就地消耗地面 1 件）。
    *****/
    private void TransferOne()
    {
        if (_target == null || _target.Count <= 0 || _registry == null)
        {
            EnterConsumePhase(fromGround: _owner.Inventory.CountOf(_def) <= 0);
            return;
        }
        if (_owner.Inventory.MaxAddable(_def) <= 0 || _transferred >= _planned)
        {
            EnterConsumePhase(fromGround: _owner.Inventory.CountOf(_def) <= 0);
            return;
        }
        if (!_registry.Remove(_target, 1))
        {
            EnterConsumePhase(fromGround: _owner.Inventory.CountOf(_def) <= 0);
            return;
        }
        if (!_owner.Inventory.TryAdd(_def, 1))
        {
            // 兜底：预检已保证装得下，理论不会走到这里；真的发生则把刚取下的 1 件放回地面并改走地面消耗
            _registry.Add(_def, 1, _target.Cell, _target.PixelOffset);
            EnterConsumePhase(fromGround: _owner.Inventory.CountOf(_def) <= 0);
            return;
        }

        _transferred++;
        if (_transferred >= _planned || _target.Count <= 0 || _owner.Inventory.MaxAddable(_def) <= 0)
        {
            EnterConsumePhase(fromGround: false);
        }
    }

    /*****
    Date: 2026-09-26
    Name: EnterConsumePhase
    Description: 切到消耗阶段：清空取货阶段的余数时间；fromGround 为 true 表示本次从地面堆直接消耗（背包里没有本次取得的货）。
    *****/
    private void EnterConsumePhase(bool fromGround)
    {
        _consuming = true;
        _accrued = 0.0;
        _consumeFromGround = fromGround;
    }

    /*****
    Date: 2026-09-26
    Name: Complete
    Description: 完成消耗：从地面堆（地面消耗路径）或背包扣 1 件，恢复对应需求；扣件失败（物品在最后一刻被取走）则取消任务。
    *****/
    private void Complete()
    {
        if (_consumeFromGround)
        {
            if (_target == null || _registry == null || !_registry.Remove(_target, 1))
            {
                MarkCancelled();
                return;
            }
        }
        else if (_owner.Inventory.Remove(_def, 1) <= 0)
        {
            MarkCancelled();
            return;
        }

        _needs.Restore(_owner, _need, _restoreAmount);
        SetState(TaskState.Done);
    }

    /*****
    Date: 2026-09-26
    Name: MapNeed
    Description: 可消耗种类 → 需求项映射（Food → 进食、Water → 饮水；None 兜底映射为进食，正常路径不会出现）。
    *****/
    private static NeedId MapNeed(ConsumableKind kind)
        => kind == ConsumableKind.Water ? NeedId.Water : NeedId.Food;
}