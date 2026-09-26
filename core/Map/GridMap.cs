using Godot;

namespace RelayStation.Core.Map;

/*****
Date: 2026-09-06
Name: GridMap
Description: 格子地图默认实现；以一维数组存储格子类型、字典存储功能区分区，提供查询/写入与可通行判定。纯 C# 数据结构，可脱离引擎单测。
*****/
public sealed class GridMap : IGridMap
{
    /*****
    Date: 2026-09-06
    Name: _cells
    Description: 格子类型一维数组（行优先，下标 = Y * Width + X）。
    *****/
    private readonly CellKind[] _cells;

    /*****
    Date: 2026-09-06
    Name: _zones
    Description: 功能区格点到功能区标识的映射表。
    *****/
    private readonly Dictionary<Vector2I, ZoneId> _zones = new();

    /*****
    Date: 2026-09-06
    Name: Width
    Description: 地图宽度（格子数）。
    *****/
    public int Width { get; }

    /*****
    Date: 2026-09-06
    Name: Height
    Description: 地图高度（格子数）。
    *****/
    public int Height { get; }

    /*****
    Date: 2026-09-06
    Name: GridMap
    Description: 构造函数；按宽高创建全 Vacuum 的地图。
    *****/
    public GridMap(int width, int height)
    {
        Width = width;
        Height = height;
        _cells = new CellKind[width * height];
    }

    /*****
    Date: 2026-09-06
    Name: GetCell
    Description: 获取指定格子的地形类型；越界格子视为 Vacuum。
    *****/
    public CellKind GetCell(Vector2I cell)
        => InBounds(cell) ? _cells[cell.Y * Width + cell.X] : CellKind.Vacuum;

    /*****
    Date: 2026-09-06
    Name: IsWalkable
    Description: 判断指定格子是否可通行（Floor / Door 可通行）。
    *****/
    public bool IsWalkable(Vector2I cell)
    {
        CellKind kind = GetCell(cell);
        return kind == CellKind.Floor || kind == CellKind.Door;
    }

    /*****
    Date: 2026-09-06
    Name: GetZone
    Description: 获取指定格子所属的功能区；不属于任何功能区时返回 null。
    *****/
    public ZoneId? GetZone(Vector2I cell)
        => _zones.TryGetValue(cell, out ZoneId zone) ? zone : null;

    /*****
    Date: 2026-09-06
    Name: SetCell
    Description: 设置指定格子的地形类型（供地图生成器构建时使用）；越界忽略。
    *****/
    public void SetCell(Vector2I cell, CellKind kind)
    {
        if (!InBounds(cell)) return;
        _cells[cell.Y * Width + cell.X] = kind;
    }

    /*****
    Date: 2026-09-06
    Name: SetZone
    Description: 将指定格子划入功能区（供地图生成器构建时使用）；越界忽略。
    *****/
    public void SetZone(Vector2I cell, ZoneId zone)
    {
        if (InBounds(cell)) _zones[cell] = zone;
    }

    /*****
    Date: 2026-09-06
    Name: ClearZone
    Description: 清除指定格子的功能区归属（供地图编辑器擦除功能区使用）；越界忽略。
    *****/
    public void ClearZone(Vector2I cell)
    {
        if (InBounds(cell)) _zones.Remove(cell);
    }

    /*****
    Date: 2026-09-06
    Name: InBounds
    Description: 判断格子是否在地图边界内。
    *****/
    public bool InBounds(Vector2I cell)
        => cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;
}
