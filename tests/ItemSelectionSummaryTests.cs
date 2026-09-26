using Godot;
using RelayStation.Core.Items;
using Xunit;

namespace RelayStation.Tests;

/*****
Date: 2026-09-26
Name: ItemSelectionSummaryTests
Description: 材料选择汇总器单元测试；覆盖空选择、同种多堆合并（数量求和/堆数累加）、多物种各自成行、保持首次出现顺序、以及单堆与上限字段的正确性。
*****/
public sealed class ItemSelectionSummaryTests
{
    /*****
    Date: 2026-09-26
    Name: Def
    Description: 构造物品定义替身（单元测试宿主无法构造 ItemDef Resource）。
    *****/
    private static IItemDef Def(string id, int maxStack = 100) => new FakeItemDef(id, maxStack, id);

    /*****
    Date: 2026-09-26
    Name: Stack
    Description: 构造一个位于指定格的物品堆。
    *****/
    private static ItemStack Stack(IItemDef def, int count, int x = 0, int y = 0)
        => new(def, count, new Vector2I(x, y));

    /*****
    Date: 2026-09-26
    Name: Aggregate_EmptySelection_ReturnsEmpty
    Description: 空选择返回空列表（面板据此隐藏）。
    *****/
    [Fact]
    public void Aggregate_EmptySelection_ReturnsEmpty()
    {
        Assert.Empty(ItemSelectionSummary.Aggregate(Array.Empty<ItemStack>()));
    }

    /*****
    Date: 2026-09-26
    Name: Aggregate_MergesSameItemAcrossStacks
    Description: 同种的三个堆（20/20/20）合并成一行：总数 60、堆数 3。
    *****/
    [Fact]
    public void Aggregate_MergesSameItemAcrossStacks()
    {
        IItemDef parts = Def("parts", 100);

        IReadOnlyList<ItemGroupSummary> rows = ItemSelectionSummary.Aggregate(new[]
        {
            Stack(parts, 20, 1, 1),
            Stack(parts, 20, 2, 2),
            Stack(parts, 20, 3, 3),
        });

        ItemGroupSummary row = Assert.Single(rows);
        Assert.Equal("parts", row.ItemId);
        Assert.Equal(60, row.TotalCount);
        Assert.Equal(3, row.StackCount);
        Assert.Equal(100, row.MaxStack);
    }

    /*****
    Date: 2026-09-26
    Name: Aggregate_KeepsFirstAppearanceOrder
    Description: 多种物品各自成行，且行序 = 首次出现顺序（不按数量或名称重排）。
    *****/
    [Fact]
    public void Aggregate_KeepsFirstAppearanceOrder()
    {
        IItemDef scrap = Def("scrap", 50);
        IItemDef parts = Def("parts", 100);

        IReadOnlyList<ItemGroupSummary> rows = ItemSelectionSummary.Aggregate(new[]
        {
            Stack(parts, 10),
            Stack(scrap, 5),
            Stack(parts, 15),
            Stack(scrap, 7),
        });

        Assert.Equal(new[] { "parts", "scrap" }, rows.Select(r => r.ItemId).ToArray());
        Assert.Equal(25, rows[0].TotalCount);
        Assert.Equal(2, rows[0].StackCount);
        Assert.Equal(12, rows[1].TotalCount);
        Assert.Equal(2, rows[1].StackCount);
        Assert.Equal(50, rows[1].MaxStack);
    }

    /*****
    Date: 2026-09-26
    Name: Aggregate_KeepsDisplayNameAndIcon
    Description: 汇总行保留显示名与图标路径（信息栏展示用）；上限非法时按 1 处理。
    *****/
    [Fact]
    public void Aggregate_KeepsDisplayNameAndIcon()
    {
        var def = new FakeItemDef("parts", 0, "通用零件", "res://icon.png");

        IReadOnlyList<ItemGroupSummary> rows = ItemSelectionSummary.Aggregate(new[] { Stack(def, 3) });

        ItemGroupSummary row = Assert.Single(rows);
        Assert.Equal("通用零件", row.DisplayName);
        Assert.Equal("res://icon.png", row.IconTexturePath);
        Assert.Equal(1, row.MaxStack);
    }
}
