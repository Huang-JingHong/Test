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
Name: ItemPlacementTests
Description: 物品与设施互斥规则的集成测试；覆盖「格上有物品即不可放设施」「设施占地格不可投物品」「非地板格两者皆不可放」「物品不阻挡角色通行」，以及物品被清空后该格重新允许放置设施。
*****/
public sealed class ItemPlacementTests
{
    /*****
    Date: 2026-09-26
    Name: Parts
    Description: 构造「通用零件」物品定义替身（单元测试宿主无法构造 ItemDef Resource）。
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
    Description: 在指定地图上装配模拟层，并把寻路用的阻塞集合一并返回（与 Simulation 共享同一实例）。
    *****/
    private static Simulation CreateSimulation(GridMap map, out HashSet<Vector2I> blocked, out GridPathfinding pathfinding)
    {
        blocked = new HashSet<Vector2I>();
        pathfinding = new GridPathfinding(map, blocked.Contains);
        var eventBus = new EventBus();
        return new Simulation(
            new GameClock(), map, pathfinding, new TaskBoard(eventBus), new NeedSystem(), eventBus, blocked);
    }

    /*****
    Date: 2026-09-26
    Name: Items_BlockFacilityPlacementOnSameCell
    Description: 格上有物品时不可放置设施（原因为「物品阻挡」）；物品不计入设施占地（二者是两套独立的判定，避免"物品也算占格"的语义混淆）。
    *****/
    [Fact]
    public void Items_BlockFacilityPlacementOnSameCell()
    {
        GridMap map = CreateFloorMap(4, 4);
        Simulation simulation = CreateSimulation(map, out _, out _);
        var cell = new Vector2I(1, 1);

        simulation.Items.Add(Parts(), 5, cell);

        Assert.Equal(PlacementObstacle.ItemsPresent, simulation.CheckFacilityCell(cell));
        Assert.Equal(PlacementObstacle.None, simulation.CheckFacilityCell(new Vector2I(1, 2)));
        Assert.False(simulation.IsCellOccupied(cell)); // 物品不进入设施占地集
    }

    /*****
    Date: 2026-09-26
    Name: Facility_BlocksItemPlacementOnOccupiedCells
    Description: 设施占地格不可投放物品（2×2 设施的全部四格均拒绝），占地之外的格子不受影响。
    *****/
    [Fact]
    public void Facility_BlocksItemPlacementOnOccupiedCells()
    {
        GridMap map = CreateFloorMap(5, 5);
        Simulation simulation = CreateSimulation(map, out _, out _);

        simulation.AddFacility(new FacilitySim(null, new Vector2I(2, 2))); // 占地 (2,2)-(3,3)

        Assert.False(simulation.CanPlaceItemsAt(new Vector2I(2, 2)));
        Assert.False(simulation.CanPlaceItemsAt(new Vector2I(2, 3)));
        Assert.False(simulation.CanPlaceItemsAt(new Vector2I(3, 2)));
        Assert.False(simulation.CanPlaceItemsAt(new Vector2I(3, 3)));
        Assert.True(simulation.CanPlaceItemsAt(new Vector2I(1, 1)));
    }

    /*****
    Date: 2026-09-26
    Name: NonFloorCells_RejectBoth
    Description: 非地板格（真空/墙）既不可放置设施也不可投放物品，设施侧原因为「不是地板」。
    *****/
    [Fact]
    public void NonFloorCells_RejectBoth()
    {
        GridMap map = CreateFloorMap(3, 3);
        map.SetCell(new Vector2I(0, 0), CellKind.Vacuum);
        map.SetCell(new Vector2I(0, 1), CellKind.Wall);
        Simulation simulation = CreateSimulation(map, out _, out _);

        Assert.False(simulation.CanPlaceItemsAt(new Vector2I(0, 0)));
        Assert.Equal(PlacementObstacle.NotFloor, simulation.CheckFacilityCell(new Vector2I(0, 0)));
        Assert.False(simulation.CanPlaceItemsAt(new Vector2I(0, 1)));
        Assert.Equal(PlacementObstacle.NotFloor, simulation.CheckFacilityCell(new Vector2I(0, 1)));
    }

    /*****
    Date: 2026-09-26
    Name: Items_DoNotBlockPathfinding
    Description: 物品不阻挡角色通行：格上有物品时寻路仍可穿越该格（物品只与设施互斥）。
    *****/
    [Fact]
    public void Items_DoNotBlockPathfinding()
    {
        GridMap map = CreateFloorMap(3, 3);
        Simulation simulation = CreateSimulation(map, out _, out GridPathfinding pathfinding);
        var cell = new Vector2I(1, 1);

        simulation.Items.Add(Parts(), 5, cell);

        IReadOnlyList<Vector2I> path = pathfinding.FindPath(new Vector2I(0, 1), new Vector2I(2, 1));
        Assert.Contains(cell, path);
    }

    /*****
    Date: 2026-09-26
    Name: RemovingItems_ReopensCellForFacility
    Description: 物品被搬空后该格重新允许放置设施（互斥判定实时反映物品现状，不残留）。
    *****/
    [Fact]
    public void RemovingItems_ReopensCellForFacility()
    {
        GridMap map = CreateFloorMap(4, 4);
        Simulation simulation = CreateSimulation(map, out _, out _);
        var cell = new Vector2I(2, 2);
        ItemStack stack = simulation.Items.Add(Parts(), 5, cell)[0];

        Assert.Equal(PlacementObstacle.ItemsPresent, simulation.CheckFacilityCell(cell));

        Assert.True(simulation.Items.Remove(stack, 5));

        Assert.Equal(PlacementObstacle.None, simulation.CheckFacilityCell(cell));
    }

    /*****
    Date: 2026-09-26
    Name: CheckFacilityCell_ReportsReasonPerObstacle
    Description: 三类硬性阻挡各自给出对应原因（非地板 / 设施占用 / 物品阻挡），合法地板格返回 None——建造预览的绿红框与点击提示文案都取自此结果。
    *****/
    [Fact]
    public void CheckFacilityCell_ReportsReasonPerObstacle()
    {
        GridMap map = CreateFloorMap(4, 4);
        map.SetCell(new Vector2I(0, 0), CellKind.Wall);
        Simulation simulation = CreateSimulation(map, out _, out _);
        simulation.AddFacility(new FacilitySim(null, new Vector2I(2, 2))); // 占地 (2,2)-(3,3)
        simulation.Items.Add(Parts(), 3, new Vector2I(1, 1));

        Assert.Equal(PlacementObstacle.NotFloor, simulation.CheckFacilityCell(new Vector2I(0, 0)));
        Assert.Equal(PlacementObstacle.FacilityOccupied, simulation.CheckFacilityCell(new Vector2I(2, 2)));
        Assert.Equal(PlacementObstacle.ItemsPresent, simulation.CheckFacilityCell(new Vector2I(1, 1)));
        Assert.Equal(PlacementObstacle.None, simulation.CheckFacilityCell(new Vector2I(1, 2)));
    }

    /*****
    Date: 2026-09-26
    Name: MultiCellPlacement_ChecksWholeFootprint
    Description: 多格设施整块校验：2×2 占地中任一格被物品占用即整体不可放置（报物品阻挡），并在该格清理后放行；占地越界到图外时按「非地板」拒绝。
    *****/
    [Fact]
    public void MultiCellPlacement_ChecksWholeFootprint()
    {
        GridMap map = CreateFloorMap(5, 5);
        Simulation simulation = CreateSimulation(map, out _, out _);
        var origin = new Vector2I(2, 2);
        var size = new Vector2I(2, 2);
        var blockedCorner = new Vector2I(3, 3);
        ItemStack stack = simulation.Items.Add(Parts(), 5, blockedCorner)[0];

        Assert.Equal(PlacementObstacle.ItemsPresent, simulation.CheckFacilityPlacement(origin, size));
        Assert.Equal(PlacementObstacle.None, simulation.CheckFacilityPlacement(new Vector2I(0, 0), size));

        Assert.True(simulation.Items.Remove(stack, 5));
        Assert.Equal(PlacementObstacle.None, simulation.CheckFacilityPlacement(origin, size));

        // 越界（4,4 起 2×2 会探到图外的 (5,5)）→ 越界格视为真空 → 非地板
        Assert.Equal(PlacementObstacle.NotFloor, simulation.CheckFacilityPlacement(new Vector2I(4, 4), size));
    }
}
