using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Events;
using RelayStation.Core.Facilities;
using RelayStation.Core.Map;
using RelayStation.Core.Tasks;
using RelayStation.Core.Time;
using Xunit;
using GridMap = RelayStation.Core.Map.GridMap;

namespace RelayStation.Tests;

/*****
Date: 2026-09-25
Name: FacilityBlockingTests
Description: 设施占地动态阻挡与旧数据清洗测试；覆盖阻塞委托使 Floor 格绕行/不可达、无委托默认回归、ToGridMap 清洗历史 FacilitySlot(4)、Simulation 增删设施维护占地集，以及作业格避让其他设施占地。
*****/
public sealed class FacilityBlockingTests
{
    /*****
    Date: 2026-09-25
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
    Date: 2026-09-25
    Name: BlockedColumn_PathUnreachableThenPassable
    Description: 阻塞委托使 Floor 格不可通行：整列阻塞时路径不可达，解除后恢复通行。
    *****/
    [Fact]
    public void BlockedColumn_PathUnreachableThenPassable()
    {
        GridMap map = CreateFloorMap(3, 3);
        var blocked = new HashSet<Vector2I> { new(1, 0), new(1, 1), new(1, 2) };
        var pathfinding = new GridPathfinding(map, blocked.Contains);

        Assert.Empty(pathfinding.FindPath(new Vector2I(0, 1), new Vector2I(2, 1)));

        blocked.Remove(new Vector2I(1, 1));
        IReadOnlyList<Vector2I> path = pathfinding.FindPath(new Vector2I(0, 1), new Vector2I(2, 1));
        Assert.NotEmpty(path);
        Assert.Contains(new Vector2I(1, 1), path);
    }

    /*****
    Date: 2026-09-25
    Name: NoBlockedDelegate_FloorRemainsPassable
    Description: 无阻塞委托时默认行为回归：Floor 地图直连可通行。
    *****/
    [Fact]
    public void NoBlockedDelegate_FloorRemainsPassable()
    {
        GridMap map = CreateFloorMap(3, 3);
        var pathfinding = new GridPathfinding(map);

        Assert.NotEmpty(pathfinding.FindPath(new Vector2I(0, 0), new Vector2I(2, 2)));
    }

    /*****
    Date: 2026-09-25
    Name: ToGridMap_CleansLegacyFacilitySlot
    Description: 旧数据清洗：Cells 中残留的历史 FacilitySlot 值（4）加载后变为 Floor。GridMapData 为 Godot Resource，引擎外测试进程无法构造（构造即崩，同 AStarGrid2D 教训），本用例跳过——清洗逻辑由无头冒烟验证（旧 base_map.tres 含 4 值加载运行无透明洞即通过）。
    *****/
    [Fact(Skip = "GridMapData(Resource) 需引擎运行时，由无头冒烟覆盖")]
    public void ToGridMap_CleansLegacyFacilitySlot()
    {
        var data = new GridMapData { Width = 2, Height = 1, Cells = new[] { 4, 1 } };

        GridMap map = data.ToGridMap();

        Assert.Equal(CellKind.Floor, map.GetCell(new Vector2I(0, 0)));
        Assert.Equal(CellKind.Floor, map.GetCell(new Vector2I(1, 0)));
    }

    /*****
    Date: 2026-09-25
    Name: AddRemoveFacility_MaintainsBlockedCells
    Description: Simulation 增删设施动态维护占地集：加入后占地格占用且寻路绕行，移除后恢复直线通行。
    *****/
    [Fact]
    public void AddRemoveFacility_MaintainsBlockedCells()
    {
        GridMap map = CreateFloorMap(5, 5);
        var blocked = new HashSet<Vector2I>();
        var pathfinding = new GridPathfinding(map, blocked.Contains);
        var simulation = new Simulation(
            new GameClock(), map, pathfinding, new TaskBoard(new EventBus()), new NeedSystem(), new EventBus(), blocked);

        var facility = new FacilitySim(null, new Vector2I(2, 2)); // 占地 (2,2)-(3,3)
        simulation.AddFacility(facility);

        Assert.True(simulation.IsCellOccupied(new Vector2I(2, 2)));
        Assert.True(simulation.IsCellOccupied(new Vector2I(3, 3)));
        IReadOnlyList<Vector2I> detour = pathfinding.FindPath(new Vector2I(2, 1), new Vector2I(2, 4));
        Assert.NotEmpty(detour); // 绕行可达
        foreach (Vector2I cell in detour)
        {
            Assert.False(simulation.IsCellOccupied(cell)); // 路径不穿占地格
        }

        simulation.RemoveFacility(facility);

        Assert.False(simulation.IsCellOccupied(new Vector2I(2, 2)));
        IReadOnlyList<Vector2I> direct = pathfinding.FindPath(new Vector2I(2, 1), new Vector2I(2, 4));
        Assert.Equal(4, direct.Count); // 直线 4 格：(2,1)(2,2)(2,3)(2,4)
    }

    /*****
    Date: 2026-09-25
    Name: WorkCell_AvoidsOccupiedNeighbors
    Description: 设施旁的作业格避让其他设施占地：相邻两设施，角色维修后者时站位不落在前者占地内。
    *****/
    [Fact]
    public void WorkCell_AvoidsOccupiedNeighbors()
    {
        GridMap map = CreateFloorMap(7, 3);
        var blocked = new HashSet<Vector2I>();
        var pathfinding = new GridPathfinding(map, blocked.Contains);
        var eventBus = new EventBus();
        var simulation = new Simulation(
            new GameClock(), map, pathfinding, new TaskBoard(eventBus), new NeedSystem(), eventBus, blocked);

        var facilityA = new FacilitySim(null, new Vector2I(0, 0)); // 占地 (0,0)-(1,1)
        var facilityB = new FacilitySim(null, new Vector2I(3, 0)); // 占地 (3,0)-(4,1)，左邻 (2,0)(2,1) 被 A 遮挡路径但非 A 占地
        var facilityC = new FacilitySim(null, new Vector2I(1, 0)); // 与 B 左侧紧邻：占地 (1,0)-(2,1)
        simulation.AddFacility(facilityA);
        simulation.AddFacility(facilityB);
        simulation.AddFacility(facilityC);

        var character = new CharacterSim(null, new Vector2I(0, 2));
        simulation.AddCharacter(character);

        var task = new RepairTask(facilityB, 5);
        simulation.TaskBoard.Submit(task);

        // 推进到角色开始作业
        int frames = 0;
        while (character.State != CharacterState.Working && frames < 600)
        {
            simulation.Update(0.1);
            frames++;
        }

        Assert.Equal(CharacterState.Working, character.State);
        // B 的可达邻格只有下侧 (3,2)(4,2)（左邻 (2,0)(2,1) 是 C 占地，上侧越界）
        Assert.Contains(character.Cell, new[] { new Vector2I(3, 2), new Vector2I(4, 2) });
        Assert.False(simulation.IsCellOccupied(character.Cell));
    }

    /*****
    Date: 2026-09-26
    Name: NonBlockingFacility_OccupiesCellButDoesNotBlockPath
    Description: 不阻挡通行的设施（家具类）行为：占地格仍计入「已占用」（禁止两设施叠放），但不进入寻路阻塞集合，角色可直穿其格；移除后占格释放。
    *****/
    [Fact]
    public void NonBlockingFacility_OccupiesCellButDoesNotBlockPath()
    {
        GridMap map = CreateFloorMap(3, 3);
        var blocked = new HashSet<Vector2I>();
        var pathfinding = new GridPathfinding(map, blocked.Contains);
        var simulation = new Simulation(
            new GameClock(), map, pathfinding, new TaskBoard(new EventBus()), new NeedSystem(), new EventBus(), blocked);

        var sofa = new FacilitySim(null, new Vector2I(1, 1), blocksMovement: false); // 占地 (1,1)-(2,2)
        simulation.AddFacility(sofa);

        Assert.True(simulation.IsCellOccupied(new Vector2I(1, 1)));   // 仍占格：禁止叠放
        Assert.DoesNotContain(new Vector2I(1, 1), blocked);           // 不阻挡寻路
        Assert.Equal(3, pathfinding.FindPath(new Vector2I(0, 1), new Vector2I(2, 1)).Count); // 直穿 3 格

        simulation.RemoveFacility(sofa);

        Assert.False(simulation.IsCellOccupied(new Vector2I(1, 1)));
        Assert.Empty(blocked);
    }
}
