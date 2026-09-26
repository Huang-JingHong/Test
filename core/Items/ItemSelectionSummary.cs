namespace RelayStation.Core.Items;

/*****
Date: 2026-09-26
Name: ItemSelectionSummary
Description: 材料选择汇总器（纯函数，无引擎依赖）；把选中的一堆物品堆按「物品种类」合并成若干汇总行，供材料信息栏展示「整合后的材料总览」。同种类的多个堆会被合并为一行（数量求和、堆数累加），不同种类各自成行，**保持首次出现顺序**（与选择/清单顺序一致，不因排序跳来跳去）。
*****/
public static class ItemSelectionSummary
{
    /*****
    Date: 2026-09-26
    Name: Aggregate
    Description: 汇总选中物品堆：按 Def.Id 分组求和；空输入返回空列表。
    *****/
    public static IReadOnlyList<ItemGroupSummary> Aggregate(IEnumerable<ItemStack> stacks)
    {
        var order = new List<string>();
        var totals = new Dictionary<string, int>();
        var counts = new Dictionary<string, int>();
        var names = new Dictionary<string, string>();
        var maxStacks = new Dictionary<string, int>();
        var icons = new Dictionary<string, string>();

        foreach (ItemStack stack in stacks)
        {
            string id = stack.Def.Id;
            if (!totals.ContainsKey(id))
            {
                order.Add(id);
                totals[id] = 0;
                counts[id] = 0;
                names[id] = stack.Def.DisplayName;
                maxStacks[id] = Math.Max(1, stack.Def.MaxStack);
                icons[id] = stack.Def.IconTexturePath ?? "";
            }
            totals[id] += stack.Count;
            counts[id]++;
        }

        var result = new List<ItemGroupSummary>(order.Count);
        foreach (string id in order)
        {
            result.Add(new ItemGroupSummary(id, names[id], totals[id], counts[id], maxStacks[id], icons[id]));
        }
        return result;
    }
}
