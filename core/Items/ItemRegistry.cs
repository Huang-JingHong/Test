using Godot;

namespace RelayStation.Core.Items;

/*****
Date: 2026-09-26
Name: ItemRegistry
Description: 地面物品堆注册表（纯 C#，不引用任何 Godot 节点）；统一维护场上全部 ItemStack 的增删与查询，并保证两条不变量：①任一组的数量落在 [1, MaxStack]（Add 按 MaxStack 自动拆堆）；②数量归零的组即时回收。对外提供「按格查询」（供「设施与物品不可同格共存」的互斥校验）与自动堆叠框架 Compact(IItemStackPolicy)——能否合并交由策略判定，注册表只负责搬运机制（满则溢出、空堆回收），后续明确合并条件时零改注册表。
*****/
public sealed class ItemRegistry
{
    /*****
    Date: 2026-09-26
    Name: _stacks
    Description: 场上全部物品堆（顺序即创建顺序，供表现层稳定渲染）。
    *****/
    private readonly List<ItemStack> _stacks = new();

    /*****
    Date: 2026-09-26
    Name: Changed
    Description: 物品堆集合发生变化（增删/搬运）时触发的事件；供表现层重建视图与资源统计（地上可达零件数）刷新，免于逐帧轮询。
    *****/
    public event Action? Changed;

    /*****
    Date: 2026-09-26
    Name: Stacks
    Description: 场上全部物品堆（只读视图）。
    *****/
    public IReadOnlyList<ItemStack> Stacks => _stacks;

    /*****
    Date: 2026-09-26
    Name: Add
    Description: 在指定格投放指定数量的物品（可带格内像素偏移）；按 def.MaxStack 自动拆堆（如 MaxStack=100、数量 110 → 100 与 10 两组），返回本次新建的全部堆（供表现层生成图标）。不做场地合法性校验（由 Simulation.CanPlaceItemsAt 负责），也不与既有堆自动合并（合并是 Compact 的职责）。
    *****/
    public IReadOnlyList<ItemStack> Add(IItemDef def, int count, Vector2I cell, Vector2 pixelOffset = default)
    {
        var created = new List<ItemStack>();
        if (count <= 0) return created;

        int maxStack = Math.Max(1, def.MaxStack);
        int remaining = count;
        while (remaining > 0)
        {
            int chunk = Math.Min(maxStack, remaining);
            var stack = new ItemStack(def, chunk, cell, pixelOffset);
            _stacks.Add(stack);
            created.Add(stack);
            remaining -= chunk;
        }
        if (created.Count > 0) Changed?.Invoke();
        return created;
    }

    /*****
    Date: 2026-09-26
    Name: Remove
    Description: 从指定堆扣除指定数量；堆不存在、数量非法或超过现有数量时不做任何改动并返回 false。扣空后该堆即时回收（离开 Stacks）。
    *****/
    public bool Remove(ItemStack stack, int count)
    {
        if (count <= 0 || !_stacks.Contains(stack) || count > stack.Count) return false;
        stack.Count -= count;
        if (stack.Count <= 0) _stacks.Remove(stack);
        Changed?.Invoke();
        return true;
    }

    /*****
    Date: 2026-09-26
    Name: StacksAt
    Description: 返回所属格为指定格的全部物品堆（可能多于一组，如 110 个零件拆成的两组）。
    *****/
    public IReadOnlyList<ItemStack> StacksAt(Vector2I cell)
    {
        var result = new List<ItemStack>();
        foreach (ItemStack stack in _stacks)
        {
            if (stack.Cell == cell) result.Add(stack);
        }
        return result;
    }

    /*****
    Date: 2026-09-26
    Name: HasItemAt
    Description: 指定格上是否存在物品（含多组中的任意一组）；供设施放置校验实现「地板有物品即不可放设施」的互斥规则。
    *****/
    public bool HasItemAt(Vector2I cell)
    {
        foreach (ItemStack stack in _stacks)
        {
            if (stack.Cell == cell) return true;
        }
        return false;
    }

    /*****
    Date: 2026-09-26
    Name: Compact
    Description: 自动堆叠框架：按策略把「来源组」的数量搬进有空位的「目标组」，满则溢出（余量留在原堆），搬运后为空的组即时回收；返回本次被回收的组数（即"省下了几张图"，0 表示未发生任何合并）。机制与条件解耦——能否合并由 IItemStackPolicy 判定，默认 NoStackPolicy 恒不允许，故当前调用等价于空操作；待合并条件明确后只需注入新策略。遍历按创建顺序对所有有序「目标-来源」对判定一次，同一轮内已清空的组不再被回填。
    *****/
    public int Compact(IItemStackPolicy policy)
    {
        ItemStack[] snapshot = _stacks.ToArray();
        foreach (ItemStack source in snapshot)
        {
            if (source.Count <= 0) continue;
            foreach (ItemStack target in snapshot)
            {
                if (ReferenceEquals(target, source) || target.Count <= 0) continue;
                if (!policy.CanStack(target, source)) continue;

                int amount = Math.Min(target.FreeSpace, source.Count);
                if (amount <= 0) continue;

                target.Count += amount;
                source.Count -= amount;
                if (source.Count <= 0) break;
            }
        }

        int before = _stacks.Count;
        _stacks.RemoveAll(s => s.Count <= 0);
        int recycled = before - _stacks.Count;
        if (recycled > 0) Changed?.Invoke();
        return recycled;
    }

    /*****
    Date: 2026-09-26
    Name: Clear
    Description: 清空全部物品堆（供开局重建、读档重置等场景使用）。
    *****/
    public void Clear() => _stacks.Clear();
}
