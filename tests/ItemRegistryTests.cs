using Godot;
using RelayStation.Core.Items;
using Xunit;

namespace RelayStation.Tests;

/*****
Date: 2026-09-26
Name: ItemRegistryTests
Description: 地面物品堆注册表单元测试；覆盖按 MaxStack 自动拆堆、非法入参保护、扣减与空堆回收、按格查询，以及自动堆叠框架 Compact 的策略解耦（默认不合并、可合并时的搬运与溢出、策略按格/按定义受限、总量守恒与单组上限不变量）。
*****/
public sealed class ItemRegistryTests
{
    /*****
    Date: 2026-09-26
    Name: Parts
    Description: 构造「通用零件」物品定义替身（默认一组 100）。
    *****/
    private static IItemDef Parts(int maxStack = 100)
        => new FakeItemDef("general_purpose_parts", maxStack, "通用零件");

    /*****
    Date: 2026-09-26
    Name: SameDefPolicy
    Description: 测试用宽松策略：同一物品定义（同 Id）即可合并（仅用于验证搬运机制本身，正式合并条件未定）。
    *****/
    private sealed class SameDefPolicy : IItemStackPolicy
    {
        /*****
        Date: 2026-09-26
        Name: CanStack
        Description: 两组属于同一物品定义时允许合并。
        *****/
        public bool CanStack(ItemStack target, ItemStack source) => target.Def.Id == source.Def.Id;
    }

    /*****
    Date: 2026-09-26
    Name: SameCellPolicy
    Description: 测试用受限策略：定义相同且所属格相同才允许合并（验证「条件由策略决定」）。
    *****/
    private sealed class SameCellPolicy : IItemStackPolicy
    {
        /*****
        Date: 2026-09-26
        Name: CanStack
        Description: 同定义且同格时允许合并。
        *****/
        public bool CanStack(ItemStack target, ItemStack source)
            => target.Def.Id == source.Def.Id && target.Cell == source.Cell;
    }

    /*****
    Date: 2026-09-26
    Name: Add_SplitsByMaxStack
    Description: 数量超过单组上限时按 MaxStack 自动拆堆：110 个（上限 100）→ 100 与 10 两组，同格同偏移。
    *****/
    [Fact]
    public void Add_SplitsByMaxStack()
    {
        var registry = new ItemRegistry();
        var cell = new Vector2I(3, 4);

        IReadOnlyList<ItemStack> created = registry.Add(Parts(100), 110, cell, new Vector2(6, 7));

        Assert.Equal(2, created.Count);
        Assert.Equal(100, created[0].Count);
        Assert.Equal(10, created[1].Count);
        Assert.Equal(2, registry.StacksAt(cell).Count);
        Assert.True(registry.HasItemAt(cell));
        Assert.False(registry.HasItemAt(new Vector2I(3, 5)));
        Assert.Equal(new Vector2(6, 7), created[0].PixelOffset);
        Assert.Equal(new Vector2(6, 7), created[1].PixelOffset);
    }

    /*****
    Date: 2026-09-26
    Name: Add_ChunksByMaxStack
    Description: 100 个通用零件按一组 20 投放 → 5 组、每组 20（开局散落的口径）。
    *****/
    [Fact]
    public void Add_ChunksByMaxStack()
    {
        var registry = new ItemRegistry();
        var cell = new Vector2I(1, 1);

        IReadOnlyList<ItemStack> created = registry.Add(Parts(20), 100, cell);

        Assert.Equal(5, created.Count);
        Assert.All(created, s => Assert.Equal(20, s.Count));
        Assert.Equal(5, registry.StacksAt(cell).Count);
    }

    /*****
    Date: 2026-09-26
    Name: Add_NonPositiveCount_NoOp
    Description: 非正数投放不做任何改动。
    *****/
    [Fact]
    public void Add_NonPositiveCount_NoOp()
    {
        var registry = new ItemRegistry();

        Assert.Empty(registry.Add(Parts(), 0, new Vector2I(1, 1)));
        Assert.Empty(registry.Add(Parts(), -5, new Vector2I(1, 1)));
        Assert.Empty(registry.Stacks);
    }

    /*****
    Date: 2026-09-26
    Name: Add_InvalidMaxStack_TreatedAsOne
    Description: 定义中单组上限非法（0）时按 1 处理：3 个物品拆成 3 组。
    *****/
    [Fact]
    public void Add_InvalidMaxStack_TreatedAsOne()
    {
        var registry = new ItemRegistry();

        IReadOnlyList<ItemStack> created = registry.Add(Parts(0), 3, new Vector2I(1, 1));

        Assert.Equal(3, created.Count);
        Assert.All(created, s => Assert.Equal(1, s.Count));
    }

    /*****
    Date: 2026-09-26
    Name: Remove_ReducesThenRecycles
    Description: 扣减按数量生效，扣空后空堆即时回收（离开 Stacks 并按格查询不再命中）。
    *****/
    [Fact]
    public void Remove_ReducesThenRecycles()
    {
        var registry = new ItemRegistry();
        var cell = new Vector2I(2, 2);
        ItemStack stack = registry.Add(Parts(100), 100, cell)[0];

        Assert.True(registry.Remove(stack, 40));
        Assert.Equal(60, stack.Count);
        Assert.True(registry.HasItemAt(cell));

        Assert.True(registry.Remove(stack, 60));
        Assert.Empty(registry.Stacks);
        Assert.False(registry.HasItemAt(cell));
    }

    /*****
    Date: 2026-09-26
    Name: Remove_InvalidArgs_ReturnsFalse
    Description: 非法数量、超量扣减、以及不在册的堆一律拒绝且不改动现状。
    *****/
    [Fact]
    public void Remove_InvalidArgs_ReturnsFalse()
    {
        var registry = new ItemRegistry();
        var cell = new Vector2I(2, 2);
        ItemStack stack = registry.Add(Parts(100), 50, cell)[0];

        Assert.False(registry.Remove(stack, 0));
        Assert.False(registry.Remove(stack, 51));
        Assert.False(registry.Remove(new ItemStack(Parts(), 1, cell), 1));
        Assert.Equal(50, stack.Count);
    }

    /*****
    Date: 2026-09-26
    Name: Compact_DefaultPolicy_IsNoOp
    Description: 默认策略（NoStackPolicy）下自动堆叠不发生任何改动：三组 70/50/90 原样保留。
    *****/
    [Fact]
    public void Compact_DefaultPolicy_IsNoOp()
    {
        var registry = new ItemRegistry();
        registry.Add(Parts(100), 70, new Vector2I(1, 1));
        registry.Add(Parts(100), 50, new Vector2I(1, 2));
        registry.Add(Parts(100), 90, new Vector2I(1, 3));

        int recycled = registry.Compact(NoStackPolicy.Instance);

        Assert.Equal(0, recycled);
        Assert.Equal(new[] { 70, 50, 90 }, registry.Stacks.Select(s => s.Count).ToArray());
    }

    /*****
    Date: 2026-09-26
    Name: Compact_MergingPolicy_ConsolidatesAndConservesTotal
    Description: 允许合并时两组（30 + 40）合并为一组 70，返回被回收的组数 1，总量不变。
    *****/
    [Fact]
    public void Compact_MergingPolicy_ConsolidatesAndConservesTotal()
    {
        var registry = new ItemRegistry();
        registry.Add(Parts(100), 30, new Vector2I(1, 1));
        registry.Add(Parts(100), 40, new Vector2I(1, 2));

        int recycled = registry.Compact(new SameDefPolicy());

        Assert.Equal(1, recycled);
        ItemStack merged = Assert.Single(registry.Stacks);
        Assert.Equal(70, merged.Count);
    }

    /*****
    Date: 2026-09-26
    Name: Compact_OverflowStaysAsSpillover
    Description: 目标组装满后余量溢出留在原堆：两组各 60（上限 100）→ 100 与 20 两组，总量不变。
    *****/
    [Fact]
    public void Compact_OverflowStaysAsSpillover()
    {
        var registry = new ItemRegistry();
        registry.Add(Parts(100), 60, new Vector2I(1, 1));
        registry.Add(Parts(100), 60, new Vector2I(1, 2));

        Assert.Equal(0, registry.Compact(new SameDefPolicy()));

        Assert.Equal(new[] { 20, 100 }, registry.Stacks.Select(s => s.Count).OrderBy(c => c).ToArray());
    }

    /*****
    Date: 2026-09-26
    Name: Compact_KeepsInvariantsOnMultiGroupCase
    Description: 三组 70/50/90（上限 100）合并后：总量守恒为 210、无组超上限、无空组，且组数收敛到理论上限 3（210 / 100 上取整）。
    *****/
    [Fact]
    public void Compact_KeepsInvariantsOnMultiGroupCase()
    {
        var registry = new ItemRegistry();
        registry.Add(Parts(100), 70, new Vector2I(1, 1));
        registry.Add(Parts(100), 50, new Vector2I(1, 2));
        registry.Add(Parts(100), 90, new Vector2I(1, 3));
        int total = registry.Stacks.Sum(s => s.Count);

        registry.Compact(new SameDefPolicy());

        Assert.Equal(total, registry.Stacks.Sum(s => s.Count));
        Assert.Equal(3, registry.Stacks.Count);
        Assert.All(registry.Stacks, s =>
        {
            Assert.True(s.Count > 0 && s.Count <= s.MaxStack);
        });
    }

    /*****
    Date: 2026-09-26
    Name: Compact_MergingIsConditionDriven
    Description: 合并与否完全由策略决定：按格受限的策略既不跨格合并零件、也不合并同格不同定义的组；换成按定义受限的策略后，跨格的两组零件可合并，而同格不同定义的组依旧不合并。
    *****/
    [Fact]
    public void Compact_MergingIsConditionDriven()
    {
        var registry = new ItemRegistry();
        registry.Add(Parts(100), 30, new Vector2I(1, 1));
        registry.Add(Parts(100), 40, new Vector2I(1, 2));
        registry.Add(new FakeItemDef("scrap", 100), 25, new Vector2I(1, 1));

        Assert.Equal(0, registry.Compact(new SameCellPolicy()));
        Assert.Equal(3, registry.Stacks.Count);

        Assert.Equal(1, registry.Compact(new SameDefPolicy()));
        Assert.Equal(2, registry.Stacks.Count);
        Assert.Equal(25, registry.Stacks.Single(s => s.Def.Id == "scrap").Count);
    }

    /*****
    Date: 2026-09-26
    Name: Compact_EmptyRegistry_ReturnsZero
    Description: 空注册表上调用自动堆叠是安全空操作。
    *****/
    [Fact]
    public void Compact_EmptyRegistry_ReturnsZero()
    {
        var registry = new ItemRegistry();

        Assert.Equal(0, registry.Compact(new SameDefPolicy()));
        Assert.Empty(registry.Stacks);
    }
}
