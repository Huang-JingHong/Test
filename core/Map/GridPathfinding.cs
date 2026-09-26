using Godot;

namespace RelayStation.Core.Map;

/*****
Date: 2026-09-06
Name: GridPathfinding
Description: 寻路默认实现（自写 A*，四方向移动、曼哈顿启发式、惰性删除式优先队列）。因 Godot AStarGrid2D 无法脱离引擎构造（引擎外进程实测崩溃，2026-09-06 确认改用纯 C# 实现），本类型保持模拟层引擎无关、可真实单测。
*****/
public sealed class GridPathfinding : IPathfinding
{
    /*****
    Date: 2026-09-06
    Name: NeighborOffsets
    Description: 四方向相邻偏移（上下左右）。
    *****/
    private static readonly Vector2I[] NeighborOffsets =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
    };

    /*****
    Date: 2026-09-06
    Name: _map
    Description: 寻路所依据的格子地图。
    *****/
    private readonly IGridMap _map;

    /*****
    Date: 2026-09-25
    Name: _blocked
    Description: 额外阻塞判定（如设施占地动态阻挡，设施移除 CellKind.FacilitySlot 后由 Simulation 维护占地集注入）；null 表示无额外阻挡（引擎外测试兼容）。
    *****/
    private readonly Func<Vector2I, bool>? _blocked;

    /*****
    Date: 2026-09-25
    Name: GridPathfinding
    Description: 构造函数；基于指定格子地图创建寻路器，可选注入额外阻塞判定（通行 = 地形可通行且未被阻塞）。
    *****/
    public GridPathfinding(IGridMap map, Func<Vector2I, bool>? blocked = null)
    {
        _map = map;
        _blocked = blocked;
    }

    /*****
    Date: 2026-09-25
    Name: IsPassable
    Description: 综合通行判定：地形可通行且未被额外阻塞。
    *****/
    private bool IsPassable(Vector2I cell)
        => _map.IsWalkable(cell) && (_blocked == null || !_blocked(cell));

    /*****
    Date: 2026-09-06
    Name: FindPath
    Description: 计算 from 到 to 的格子路径（含起终点）；起点或终点不可通行返回空列表；起终点相同返回单元素列表；不可达返回空列表。
    *****/
    public IReadOnlyList<Vector2I> FindPath(Vector2I from, Vector2I to)
    {
        if (!IsPassable(from) || !IsPassable(to)) return Array.Empty<Vector2I>();
        if (from == to) return new[] { from };

        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var gScore = new Dictionary<Vector2I, double> { [from] = 0 };
        var openSet = new HashSet<Vector2I> { from };
        var queue = new PriorityQueue<Vector2I, double>();
        queue.Enqueue(from, Heuristic(from, to));

        while (queue.Count > 0)
        {
            Vector2I current = queue.Dequeue();
            if (!openSet.Remove(current)) continue; // 惰性删除：跳过已关闭的过期条目
            if (current == to) return ReconstructPath(cameFrom, current);

            foreach (Vector2I offset in NeighborOffsets)
            {
                Vector2I next = current + offset;
                if (!IsPassable(next)) continue;

                double tentative = gScore[current] + 1;
                if (tentative < gScore.GetValueOrDefault(next, double.PositiveInfinity))
                {
                    cameFrom[next] = current;
                    gScore[next] = tentative;
                    openSet.Add(next);
                    queue.Enqueue(next, tentative + Heuristic(next, to));
                }
            }
        }
        return Array.Empty<Vector2I>();
    }

    /*****
    Date: 2026-09-06
    Name: Heuristic
    Description: A* 启发函数；曼哈顿距离（四方向移动的合法低估）。
    *****/
    private static double Heuristic(Vector2I a, Vector2I b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    /*****
    Date: 2026-09-06
    Name: ReconstructPath
    Description: 沿 cameFrom 回溯重建从起点到终点的路径。
    *****/
    private static List<Vector2I> ReconstructPath(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I current)
    {
        var path = new List<Vector2I> { current };
        while (cameFrom.TryGetValue(current, out Vector2I previous))
        {
            current = previous;
            path.Add(current);
        }
        path.Reverse();
        return path;
    }
}
