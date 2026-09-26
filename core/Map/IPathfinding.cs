using Godot;

namespace RelayStation.Core.Map;

/*****
Date: 2026-09-06
Name: IPathfinding
Description: 寻路接口；基于格子地图计算两点之间的可通行路径。
*****/
public interface IPathfinding
{
    /*****
    Date: 2026-09-06
    Name: FindPath
    Description: 计算从起点到终点的格子路径（含起终点，四方向相邻步进）；不可达时返回空列表。
    *****/
    IReadOnlyList<Vector2I> FindPath(Vector2I from, Vector2I to);
}
