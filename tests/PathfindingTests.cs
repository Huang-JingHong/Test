using Godot;
using RelayStation.Core.Map;
using Xunit;
using GridMap = RelayStation.Core.Map.GridMap;

namespace RelayStation.Tests;

/*****
Date: 2026-09-06
Name: PathfindingTests
Description: 寻路单元测试（自写 A*）；验证可达路径首末格正确、逐步四方向相邻、全程可通行且绕过墙体、不可达/不可通行目标返回空、同格返回单元素。
*****/
public sealed class PathfindingTests
{
    /*****
    Date: 2026-09-06
    Name: CreateMap
    Description: 创建 5×5 测试图：全地板，x=2 竖墙且在 y=2 留门口（左半区与右半区仅经 (2,2) 连通）。
    *****/
    private static GridMap CreateMap()
    {
        var map = new GridMap(5, 5);
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                map.SetCell(new Vector2I(x, y), CellKind.Floor);
            }
        }
        for (int y = 0; y < 5; y++)
        {
            if (y != 2) map.SetCell(new Vector2I(2, y), CellKind.Wall);
        }
        return map;
    }

    /*****
    Date: 2026-09-06
    Name: FindPath_StartToEnd_ReturnsConnectedWalkablePath
    Description: 跨区寻路：路径首末格正确、每步四方向相邻、全部可通行、且必经门口 (2,2)。
    *****/
    [Fact]
    public void FindPath_StartToEnd_ReturnsConnectedWalkablePath()
    {
        GridMap map = CreateMap();
        var pathfinding = new GridPathfinding(map);

        IReadOnlyList<Vector2I> path = pathfinding.FindPath(new Vector2I(0, 0), new Vector2I(4, 4));

        Assert.NotEmpty(path);
        Assert.Equal(new Vector2I(0, 0), path[0]);
        Assert.Equal(new Vector2I(4, 4), path[^1]);
        Assert.Contains(new Vector2I(2, 2), path);

        for (int i = 1; i < path.Count; i++)
        {
            int manhattan = Math.Abs(path[i].X - path[i - 1].X) + Math.Abs(path[i].Y - path[i - 1].Y);
            Assert.Equal(1, manhattan);
        }
        foreach (Vector2I cell in path)
        {
            Assert.True(map.IsWalkable(cell));
        }
    }

    /*****
    Date: 2026-09-06
    Name: FindPath_Unreachable_ReturnsEmpty
    Description: 目标被墙完全隔离时返回空列表。
    *****/
    [Fact]
    public void FindPath_Unreachable_ReturnsEmpty()
    {
        GridMap map = CreateMap();
        map.SetCell(new Vector2I(2, 2), CellKind.Wall); // 封死唯一门口
        var pathfinding = new GridPathfinding(map);

        IReadOnlyList<Vector2I> path = pathfinding.FindPath(new Vector2I(0, 0), new Vector2I(4, 4));

        Assert.Empty(path);
    }

    /*****
    Date: 2026-09-06
    Name: FindPath_SameCell_ReturnsSingleElement
    Description: 起终点相同返回单元素路径。
    *****/
    [Fact]
    public void FindPath_SameCell_ReturnsSingleElement()
    {
        var pathfinding = new GridPathfinding(CreateMap());

        IReadOnlyList<Vector2I> path = pathfinding.FindPath(new Vector2I(1, 1), new Vector2I(1, 1));

        Vector2I only = Assert.Single(path);
        Assert.Equal(new Vector2I(1, 1), only);
    }

    /*****
    Date: 2026-09-06
    Name: FindPath_UnwalkableTarget_ReturnsEmpty
    Description: 目标格不可通行（墙）时返回空列表。
    *****/
    [Fact]
    public void FindPath_UnwalkableTarget_ReturnsEmpty()
    {
        var pathfinding = new GridPathfinding(CreateMap());

        IReadOnlyList<Vector2I> path = pathfinding.FindPath(new Vector2I(0, 0), new Vector2I(2, 0));

        Assert.Empty(path);
    }
}
