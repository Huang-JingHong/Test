using Godot;
using RelayStation.Core.Facilities;

namespace RelayStation.Core.Map;

/*****
Date: 2026-09-06
Name: GridMapData
Description: 地图数据资源（Godot Resource，.tres 持久化）；承载地图尺寸、格子类型一维数组、功能区归属一维数组与已放置设施列表。供地图编辑器保存/加载地图使用；启动时若存在 .tres 则优先加载为初始地图，否则生成环形基地并落盘。
*****/
[GlobalClass]
public partial class GridMapData : Resource
{
    /*****
    Date: 2026-09-06
    Name: Width
    Description: 地图宽度（格子数）。
    *****/
    [Export] public int Width { get; set; }

    /*****
    Date: 2026-09-06
    Name: Height
    Description: 地图高度（格子数）。
    *****/
    [Export] public int Height { get; set; }

    /*****
    Date: 2026-09-06
    Name: Cells
    Description: 格子类型一维数组（行优先，下标 = Y * Width + X；值为 CellKind 枚举整数）。
    *****/
    [Export] public int[] Cells { get; set; } = Array.Empty<int>();

    /*****
    Date: 2026-09-06
    Name: Zones
    Description: 功能区归属一维数组（行优先；值为 ZoneId 枚举整数，-1 表示不属于任何功能区）。
    *****/
    [Export] public int[] Zones { get; set; } = Array.Empty<int>();

    /*****
    Date: 2026-09-06
    Name: Facilities
    Description: 已放置设施列表（占地原点 + 定义引用 + 状态）。
    *****/
    [Export] public FacilityPlacement[] Facilities { get; set; } = Array.Empty<FacilityPlacement>();

    /*****
    Date: 2026-09-06
    Name: FromGridMap
    Description: 从运行时 GridMap 与设施放置列表构建可序列化的 GridMapData。
    *****/
    public static GridMapData FromGridMap(GridMap map, IReadOnlyList<FacilityPlacement> placements)
    {
        var data = new GridMapData { Width = map.Width, Height = map.Height };
        data.Cells = new int[map.Width * map.Height];
        data.Zones = new int[map.Width * map.Height];
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var cell = new Vector2I(x, y);
                int index = y * map.Width + x;
                data.Cells[index] = (int)map.GetCell(cell);
                data.Zones[index] = map.GetZone(cell) is { } zone ? (int)zone : -1;
            }
        }
        data.Facilities = placements.ToArray();
        return data;
    }

    /*****
    Date: 2026-09-06
    Name: ToGridMap
    Description: 将数据资源还原为运行时 GridMap（格子类型 + 功能区归属），不含设施（设施由调用方根据 Facilities 列表恢复）。
    *****/
    public GridMap ToGridMap()
    {
        var map = new GridMap(Width, Height);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int index = y * Width + x;
                var cell = new Vector2I(x, y);
                if (index < Cells.Length) map.SetCell(cell, (CellKind)Cells[index]);
                if (index < Zones.Length && Zones[index] >= 0)
                    map.SetZone(cell, (ZoneId)Zones[index]);
            }
        }
        return map;
    }
}
