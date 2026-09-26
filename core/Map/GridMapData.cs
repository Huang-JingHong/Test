using Godot;
using RelayStation.Core.Facilities;
using RelayStation.Core.Items;

namespace RelayStation.Core.Map;

/*****
Date: 2026-09-06
Name: GridMapData
Description: 地图数据资源（Godot Resource，.tres 持久化）；承载地图尺寸、格子类型一维数组、功能区归属一维数组、已放置设施列表与地面物品堆列表。供地图编辑器保存/加载地图与存档读写使用；启动时若存在 .tres 则优先加载为初始地图，否则生成环形基地并落盘。**默认地图只含设施布局、不含物品**（物品是玩法中的动态产物，仅随存档槽保存）。
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
    Date: 2026-09-26
    Name: Items
    Description: 地面物品堆列表（定义引用 + 所属格 + 格内像素偏移 + 数量）。默认地图恒为空数组（地图布局不含物品）；存档写入当前全部物品堆，读档由调用方恢复。
    *****/
    [Export] public ItemPlacement[] Items { get; set; } = Array.Empty<ItemPlacement>();

    /*****
    Date: 2026-09-06
    Name: FromGridMap
    Description: 从运行时 GridMap、设施放置列表与地面物品堆列表构建可序列化的 GridMapData；items 传 null 表示不含物品（保存默认地图布局时即如此）。
    *****/
    public static GridMapData FromGridMap(GridMap map, IReadOnlyList<FacilityPlacement> placements,
        IReadOnlyList<ItemPlacement>? items = null)
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
        data.Items = items?.ToArray() ?? Array.Empty<ItemPlacement>();
        return data;
    }

    /*****
    Date: 2026-09-25
    Name: ToGridMap
    Description: 将数据资源还原为运行时 GridMap（格子类型 + 功能区归属），不含设施（设施由调用方根据 Facilities 列表恢复）。旧数据兼容：CellKind.FacilitySlot（枚举值 4）已移除，历史 .tres（默认地图与旧存档槽）中残留的 4 在强转前清洗为 Floor——全项目唯一清洗点。
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
                if (index < Cells.Length)
                {
                    int raw = Cells[index];
                    if (raw == (int)LegacyFacilitySlot) raw = (int)CellKind.Floor; // 旧 FacilitySlot 清洗为地板
                    map.SetCell(cell, (CellKind)raw);
                }
                if (index < Zones.Length && Zones[index] >= 0)
                    map.SetZone(cell, (ZoneId)Zones[index]);
            }
        }
        return map;
    }

    /*****
    Date: 2026-09-25
    Name: LegacyFacilitySlot
    Description: 历史版本 CellKind.FacilitySlot 的枚举整数值（已从枚举移除，仅作旧数据清洗用）。
    *****/
    private const int LegacyFacilitySlot = 4;
}
