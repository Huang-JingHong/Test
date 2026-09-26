using Godot;
using RelayStation.Core.Common;
using RelayStation.Core.Items;

namespace RelayStation.Core.Resources;

/*****
Date: 2026-09-26
Name: GroundItemStore
Description: 「地上零件就是库存」的资源实现——真正消耗只在物品堆里发生。余额 = **本定义的、可达物品堆的数量之和**（可达 = 任一角色沿可通行格能走到该堆；惰性求和，仅在读取时计算，不逐帧）；TrySpend 从可达堆中扣除（整堆扣完再扣下一堆，余额不足则**整笔不生效**，与旧池语义一致，保证「材料短缺 → 建造进度停滞」成立）；Add（拆除返还）把零件重新投放到指定格。物品堆集合变化时自动转发 BalanceChanged，供 UI 刷新。角色搬运（拾取/携带）落地后，TrySpend 应改为按工人携带量校验——本类是那一层的过渡替身。
*****/
public sealed class GroundItemStore : IResourceStore
{
    /*****
    Date: 2026-09-26
    Name: _simulation
    Description: 模拟层引用（物品堆 + 可达性计算的来源）。
    *****/
    private readonly Simulation _simulation;

    /*****
    Date: 2026-09-26
    Name: _def
    Description: 本库存代表的物品种类（备用零件）；只统计与消耗该定义的物品堆。
    *****/
    private readonly IItemDef _def;

    /*****
    Date: 2026-09-26
    Name: BalanceChanged
    Description: 余额发生变化时触发的事件（供 UI 刷新显示）。
    *****/
    public event Action? BalanceChanged;

    /*****
    Date: 2026-09-26
    Name: GroundItemStore
    Description: 构造函数；绑定模拟层与零件定义，并订阅物品堆变化事件（增删/返还后自动通知 UI）。
    *****/
    public GroundItemStore(Simulation simulation, IItemDef def)
    {
        _simulation = simulation;
        _def = def;
        _simulation.Items.Changed += OnItemsChanged;
    }

    /*****
    Date: 2026-09-26
    Name: OnItemsChanged
    Description: 物品堆变化回调：转发为余额变化事件。
    *****/
    private void OnItemsChanged() => BalanceChanged?.Invoke();

    /*****
    Date: 2026-09-26
    Name: Balance
    Description: 当前余额 = 地上可达的零件堆数量之和（惰性计算）。
    *****/
    public int Balance
    {
        get
        {
            ISet<Vector2I> reachable = _simulation.ComputeReachableCells();
            int total = 0;
            foreach (ItemStack stack in _simulation.Items.Stacks)
            {
                if (Counts(stack, reachable)) total += stack.Count;
            }
            return total;
        }
    }

    /*****
    Date: 2026-09-26
    Name: TrySpend
    Description: 从可达零件堆中扣除指定数量：先求和校验，不足（或数量非法）时**不做任何改动**并返回 false；充足时逐堆扣除（扣空的堆由注册表即时回收），成功返回 true。
    *****/
    public bool TrySpend(int amount)
    {
        if (amount <= 0) return false;

        ISet<Vector2I> reachable = _simulation.ComputeReachableCells();
        int available = 0;
        foreach (ItemStack stack in _simulation.Items.Stacks)
        {
            if (Counts(stack, reachable)) available += stack.Count;
        }
        if (available < amount) return false;

        int remaining = amount;
        foreach (ItemStack stack in _simulation.Items.Stacks.ToArray())
        {
            if (remaining <= 0) break;
            if (!Counts(stack, reachable)) continue;
            int take = Math.Min(stack.Count, remaining);
            _simulation.Items.Remove(stack, take);
            remaining -= take;
        }
        return true;
    }

    /*****
    Date: 2026-09-26
    Name: Add
    Description: 把返还的零件投放到指定格（如拆掉的设施原位，调用方须保证该格此时已空出且是地板）。
    *****/
    public void Add(int amount, Vector2I cell)
    {
        if (amount <= 0) return;
        _simulation.Items.Add(_def, amount, cell);
    }

    /*****
    Date: 2026-09-26
    Name: Counts
    Description: 判断某物品堆是否计入本库存：种类为本定义，且所在格对角色可达。
    *****/
    private bool Counts(ItemStack stack, ISet<Vector2I> reachable)
        => stack.Def.Id == _def.Id && reachable.Contains(stack.Cell);
}
