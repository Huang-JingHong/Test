using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Items;

namespace RelayStation.Core.Tasks;

/*****
Date: 2026-09-26
Name: UnequipOutcome
Description: 卸下任务的结束结果；供表现层把「收进背包 / 就地落地 / 卸不下来」转成世界提示。
*****/
public enum UnequipOutcome
{
    /*****
    Date: 2026-09-26
    Name: Stowed
    Description: 已卸下并收回背包。
    *****/
    Stowed,

    /*****
    Date: 2026-09-26
    Name: Dropped
    Description: 已卸下但背包放不下，改为**就地落地**。
    *****/
    Dropped,

    /*****
    Date: 2026-09-26
    Name: Blocked
    Description: 卸不下来（背包放不下且遍地无可放置格，装备保持原样）。
    *****/
    Blocked,
}

/*****
Date: 2026-09-26
Name: UnequipTask
Description: 卸下任务（个人任务）；就地耗时 `UnequipGameMinutes` 后卸下指定槽位的装备——优先回背包（`Unequip`）；**背包放不下（超重）则就地落地**（经注入的 dropEquipped 回调，由模拟层挑可放置格）；两者都不行则保持装备并回调提示（不阻断取消）。
*****/
public sealed class UnequipTask : TaskBase
{
    /*****
    Date: 2026-09-26
    Name: UnequipGameMinutes
    Description: 卸下耗时（游戏分钟）。
    *****/
    public const double UnequipGameMinutes = 5.0;

    /*****
    Date: 2026-09-26
    Name: _owner
    Description: 任务归属者（个人任务：只有本人可认领/加入）。
    *****/
    private readonly CharacterSim _owner;

    /*****
    Date: 2026-09-26
    Name: _slot
    Description: 要卸下的装备槽位。
    *****/
    private readonly EquipmentSlot _slot;

    /*****
    Date: 2026-09-26
    Name: _dropEquipped
    Description: 「把指定槽位的装备就地落地」回调（由模拟层注入；无可放置格返回 false）。
    *****/
    private readonly Func<CharacterSim, EquipmentSlot, bool>? _dropEquipped;

    /*****
    Date: 2026-09-26
    Name: _onOutcome
    Description: 结束回调（供表现层提示）；可选。
    *****/
    private readonly Action<UnequipOutcome>? _onOutcome;

    /*****
    Date: 2026-09-26
    Name: _elapsed
    Description: 已累计的游戏分钟。
    *****/
    private double _elapsed;

    /*****
    Date: 2026-09-26
    Name: UnequipTask
    Description: 构造函数；owner 为本人，slot 为要卸下的槽位，dropEquipped 为落地回调（可选），onOutcome 为结束回调，priority 默认 5 档。
    *****/
    public UnequipTask(CharacterSim owner, EquipmentSlot slot,
        Func<CharacterSim, EquipmentSlot, bool>? dropEquipped = null,
        Action<UnequipOutcome>? onOutcome = null, TaskPriority priority = TaskPriority.P5)
        : base(maxWorkers: 1, priority)
    {
        _owner = owner;
        _slot = slot;
        _dropEquipped = dropEquipped;
        _onOutcome = onOutcome;
    }

    /*****
    Date: 2026-09-26
    Name: Owner
    Description: 归属者（个人任务）。
    *****/
    public override CharacterSim? Owner => _owner;

    /*****
    Date: 2026-09-26
    Name: Slot
    Description: 要卸下的槽位（供表现层/测试读取）。
    *****/
    public EquipmentSlot Slot => _slot;

    /*****
    Date: 2026-09-26
    Name: PreferredWorkCell
    Description: 就地作业（角色当前格，模拟层不寻路）。
    *****/
    public override Vector2I? PreferredWorkCell(CharacterSim c) => c.Cell;

    /*****
    Date: 2026-09-26
    Name: IsStillValid
    Description: 该槽位仍有装备可卸。
    *****/
    public override bool IsStillValid() => _owner.Inventory.GetEquipped(_slot) != null;

    /*****
    Date: 2026-09-26
    Name: TotalGameMinutes
    Description: 基准总耗时（卸下耗时）。
    *****/
    public override double TotalGameMinutes => UnequipGameMinutes;

    /*****
    Date: 2026-09-26
    Name: RemainingGameMinutes
    Description: 剩余完成所需的游戏分钟数。
    *****/
    public override double RemainingGameMinutes => Math.Max(0.0, UnequipGameMinutes - _elapsed);

    /*****
    Date: 2026-09-26
    Name: ProgressFraction
    Description: 进度比例（0~1）。
    *****/
    public override double ProgressFraction
        => UnequipGameMinutes <= 0 ? 1.0 : Math.Clamp(_elapsed / UnequipGameMinutes, 0.0, 1.0);

    /*****
    Date: 2026-09-26
    Name: StartWork
    Description: 开始卸下（Assigned → InProgress）。
    *****/
    public override void StartWork()
    {
        if (State != TaskState.Assigned) return;
        SetState(TaskState.InProgress);
    }

    /*****
    Date: 2026-09-26
    Name: ProgressWork
    Description: 计时推进；到时卸下——先回背包；超重则就地落地；都不行保持装备并回调 `Blocked`。中途该槽位已空则取消。
    *****/
    public override bool ProgressWork(CharacterSim worker, double gameMinutes)
    {
        if (State != TaskState.InProgress) return false;
        if (!IsStillValid())
        {
            MarkCancelled();
            return false;
        }

        _elapsed += Math.Max(0, gameMinutes);
        if (_elapsed < UnequipGameMinutes) return false;

        if (_owner.Inventory.Unequip(_slot))
        {
            SetState(TaskState.Done);
            _onOutcome?.Invoke(UnequipOutcome.Stowed);
            return true;
        }

        if (_dropEquipped != null && _dropEquipped(_owner, _slot))
        {
            SetState(TaskState.Done);
            _onOutcome?.Invoke(UnequipOutcome.Dropped);
            return true;
        }

        MarkCancelled();
        _onOutcome?.Invoke(UnequipOutcome.Blocked);
        return false;
    }
}