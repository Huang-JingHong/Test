using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Events;
using RelayStation.Core.Facilities;
using RelayStation.Core.Items;
using RelayStation.Core.Map;
using RelayStation.Core.Tasks;
using RelayStation.Core.Time;
using Xunit;
using GridMap = RelayStation.Core.Map.GridMap;

namespace RelayStation.Tests;

/*****
Date: 2026-09-26
Name: ItemScatterTests
Description: 开局散落零件的单元测试；覆盖「按组均分、余数并入最后一组、只落在空闲地板格（避开设施与既有物品）、可供格不足时少投、同种子可复现、非法数额空操作」。
*****/
public sealed class ItemScatterTests
{
    /*****
    Date: 2026-09-26
    Name: Parts
    Description: 备用零件定义替身（单元测试宿主无法构造 ItemDef Resource）。
    *****/
    private static IItemDef Parts() => new FakeItemDef("general_purpose_parts", 100, "通用零件");

    /*****
    Date: 2026-09-26
    Name: CreateFloorMap
    Description: 创建全地板地图（默认 Vacuum，逐格置 Floor）。
    *****/
    private static GridMap CreateFloorMap(int width, int height)
    {
        var map = new GridMap(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                map.SetCell(new Vector2I(x, y), CellKind.Floor);
            }
        }
        return map;
    }

    /*****
    Date: 2026-09-26
    Name: CreateSimulation
    Description: 在指定地图上装配模拟层。
    *****/
    private static Simulation CreateSimulation(GridMap map)
    {
        var blocked = new HashSet<Vector2I>();
        var eventBus = new EventBus();
        return new Simulation(
            new GameClock(), map, new GridPathfinding(map, blocked.Contains),
            new TaskBoard(eventBus), new NeedSystem(), eventBus, blocked);
    }

    /*****
    Date: 2026-09-26
    Name: ScatterItems_EvenGroupsOnDistinctFreeFloorCells
    Description: 100 个零件散成 5 组（每组 20），落在互不相同的空闲地板格上。
    *****/
    [Fact]
    public void ScatterItems_EvenGroupsOnDistinctFreeFloorCells()
    {
        GridMap map = CreateFloorMap(10, 10);
        Simulation simulation = CreateSimulation(map);

        IReadOnlyList<ItemStack> created = simulation.ScatterItems(Parts(), 100, 5, new Random(1234));

        Assert.Equal(5, created.Count);
        Assert.All(created, s => Assert.Equal(20, s.Count));
        Assert.Equal(5, created.Select(s => s.Cell).Distinct().Count());
        Assert.All(created, s =>
        {
            Assert.Equal(CellKind.Floor, map.GetCell(s.Cell));
            Assert.False(simulation.IsCellOccupied(s.Cell)); // 未落在设施占地格
            Assert.Single(simulation.Items.StacksAt(s.Cell)); // 每格恰好一组
        });
    }

    /*****
    Date: 2026-09-26
    Name: ScatterItems_RemainderGoesToLastGroup
    Description: 不能整除时余数并入最后一组（103 / 5 → 20,20,20,20,23），总量守恒。
    *****/
    [Fact]
    public void ScatterItems_RemainderGoesToLastGroup()
    {
        Simulation simulation = CreateSimulation(CreateFloorMap(10, 10));

        IReadOnlyList<ItemStack> created = simulation.ScatterItems(Parts(), 103, 5, new Random(7));

        Assert.Equal(new[] { 20, 20, 20, 20, 23 }, created.Select(s => s.Count).ToArray());
        Assert.Equal(103, created.Sum(s => s.Count));
    }

    /*****
    Date: 2026-09-26
    Name: ScatterItems_AvoidsOccupiedAndItemCells
    Description: 候选格为「地板 + 无设施 + 无物品」：3×3 地图被 2×2 设施占去 4 格、已有物品占去 1 格，仅剩 4 格，故请求 6 组时只投出 4 组，且不落在被占用的格子上。
    *****/
    [Fact]
    public void ScatterItems_AvoidsOccupiedAndItemCells()
    {
        GridMap map = CreateFloorMap(3, 3);
        Simulation simulation = CreateSimulation(map);
        simulation.AddFacility(new FacilitySim(null, new Vector2I(0, 0))); // 占地 (0,0)-(1,1)
        simulation.Items.Add(Parts(), 5, new Vector2I(2, 2));

        IReadOnlyList<ItemStack> created = simulation.ScatterItems(Parts(), 50, 6, new Random(99));

        Assert.Equal(4, created.Count);
        Assert.Equal(50, created.Sum(s => s.Count));
        foreach (ItemStack stack in created)
        {
            Assert.NotEqual(new Vector2I(2, 2), stack.Cell);
            Assert.False(simulation.IsCellOccupied(stack.Cell));
        }
    }

    /*****
    Date: 2026-09-26
    Name: ScatterItems_SameSeed_Reproducible
    Description: 同一随机种子散落结果一致（供测试与复现问题使用）。
    *****/
    [Fact]
    public void ScatterItems_SameSeed_Reproducible()
    {
        IReadOnlyList<ItemStack> first = CreateSimulation(CreateFloorMap(8, 8)).ScatterItems(Parts(), 100, 5, new Random(2026));
        IReadOnlyList<ItemStack> second = CreateSimulation(CreateFloorMap(8, 8)).ScatterItems(Parts(), 100, 5, new Random(2026));

        Assert.Equal(first.Select(s => s.Cell).ToArray(), second.Select(s => s.Cell).ToArray());
    }

    /*****
    Date: 2026-09-26
    Name: ScatterItems_NonPositiveTotal_NoOp
    Description: 非正数额或非法组数不做任何投放。
    *****/
    [Fact]
    public void ScatterItems_NonPositiveTotal_NoOp()
    {
        Simulation simulation = CreateSimulation(CreateFloorMap(5, 5));

        Assert.Empty(simulation.ScatterItems(Parts(), 0, 5, new Random(1)));
        Assert.Empty(simulation.ScatterItems(Parts(), -10, 5, new Random(1)));
        Assert.Empty(simulation.ScatterItems(Parts(), 100, 0, new Random(1)));
        Assert.Empty(simulation.Items.Stacks);
    }
}
