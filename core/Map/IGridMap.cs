using Godot;

namespace RelayStation.Core.Map;

/*****
Date: 2026-09-06
Name: IGridMap
Description: 格子地图接口；提供格子查询、可通行判定与功能区归属查询。
*****/
public interface IGridMap
{
    /*****
    Date: 2026-09-06
    Name: Width
    Description: 地图宽度（格子数）。
    *****/
    int Width { get; }

    /*****
    Date: 2026-09-06
    Name: Height
    Description: 地图高度（格子数）。
    *****/
    int Height { get; }

    /*****
    Date: 2026-09-06
    Name: GetCell
    Description: 获取指定格子的地形类型；越界格子视为 Vacuum。
    *****/
    CellKind GetCell(Vector2I cell);

    /*****
    Date: 2026-09-06
    Name: IsWalkable
    Description: 判断指定格子是否可通行（Floor / Door 可通行）。
    *****/
    bool IsWalkable(Vector2I cell);

    /*****
    Date: 2026-09-06
    Name: GetZone
    Description: 获取指定格子所属的功能区；不属于任何功能区时返回 null。
    *****/
    ZoneId? GetZone(Vector2I cell);

    /*****
    Date: 2026-09-06
    Name: SetCell
    Description: 设置指定格子的地形类型（供地图生成器与地图编辑器写入使用）；越界忽略。
    *****/
    void SetCell(Vector2I cell, CellKind kind);

    /*****
    Date: 2026-09-06
    Name: SetZone
    Description: 将指定格子划入功能区（供地图生成器与地图编辑器写入使用）；越界忽略。
    *****/
    void SetZone(Vector2I cell, ZoneId zone);

    /*****
    Date: 2026-09-06
    Name: ClearZone
    Description: 清除指定格子的功能区归属（供地图编辑器擦除功能区使用）；越界忽略。
    *****/
    void ClearZone(Vector2I cell);
}
