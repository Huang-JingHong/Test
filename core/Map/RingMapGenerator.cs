using Godot;

namespace RelayStation.Core.Map;

/*****
Date: 2026-09-06
Name: ZoneRoomInfo
Description: 功能区舱室信息；记录该功能区在环上的设施放置锚点与可用出生格（由 RingMapGenerator 生成）。
*****/
public sealed class ZoneRoomInfo
{
    /*****
    Date: 2026-09-06
    Name: Zone
    Description: 功能区标识。
    *****/
    public ZoneId Zone { get; init; }

    /*****
    Date: 2026-09-06
    Name: FacilityAnchor
    Description: 2×2 设施放置原点格；该功能区放不下设施时为 null。
    *****/
    public Vector2I? FacilityAnchor { get; init; }

    /*****
    Date: 2026-09-06
    Name: SpawnCells
    Description: 可用作角色出生点的格子列表（内缘优先，最多 6 个）。
    *****/
    public IReadOnlyList<Vector2I> SpawnCells { get; init; } = Array.Empty<Vector2I>();
}

/*****
Date: 2026-09-06
Name: RingMapData
Description: 环形基地生成结果；包含格子地图与五个功能区的舱室信息。
*****/
public sealed class RingMapData
{
    /*****
    Date: 2026-09-06
    Name: Map
    Description: 生成的格子地图。
    *****/
    public GridMap Map { get; init; } = default!;

    /*****
    Date: 2026-09-06
    Name: Rooms
    Description: 功能区标识到舱室信息的映射表。
    *****/
    public IReadOnlyDictionary<ZoneId, ZoneRoomInfo> Rooms { get; init; }
        = new Dictionary<ZoneId, ZoneRoomInfo>();
}

/*****
Date: 2026-09-25
Name: RingMapGenerator
Description: 环形基地地图生成器（方案 A：环形格子近似）；在 MapSize×MapSize 网格上按「到中心距离」生成 3 格宽环状走廊，五个功能区沿环按 72° 均布、向外扩展为局部加宽舱室，并为每个功能区计算 2×2 设施锚点与出生格。空间站本体为固定基准尺寸（半径 16~24 格），不随地图边长变化——扩大地图只增加外围真空区域（可玩格子数变多），环外由生成器 else 分支自然填充为 Vacuum。
*****/
public static class RingMapGenerator
{
    /*****
    Date: 2026-09-25
    Name: MapSize
    Description: 地图边长（格）；即整张地图的格子范围（当前 72×72）。空间站本体的半径参数与该值无关，放大地图不会放大空间站。
    *****/
    public const int MapSize = 72;

    /*****
    Date: 2026-09-06
    Name: Center
    Description: 中心点坐标（浮点格坐标，位于地图几何中心）；空间站以此为中心，环外为真空。
    *****/
    public const float Center = (MapSize - 1) / 2f;

    /*****
    Date: 2026-09-25
    Name: CorridorInnerRadius
    Description: 走廊内缘半径（r < 此值为内墙或中心真空）；空间站基准尺寸，恒定 16 格。
    *****/
    public const float CorridorInnerRadius = 16f;

    /*****
    Date: 2026-09-25
    Name: CorridorOuterRadius
    Description: 走廊外缘半径（走廊带宽 [内缘, 外缘)，3 格宽）；恒定 19 格。
    *****/
    public const float CorridorOuterRadius = 19f;

    /*****
    Date: 2026-09-25
    Name: HullOuterRadius
    Description: 外壳墙外缘半径；恒定 20 格。
    *****/
    public const float HullOuterRadius = 20f;

    /*****
    Date: 2026-09-25
    Name: RoomOuterRadius
    Description: 舱室外缘半径（舱室自走廊向外加宽至此）；恒定 23 格。
    *****/
    public const float RoomOuterRadius = 23f;

    /*****
    Date: 2026-09-25
    Name: RoomWallOuterRadius
    Description: 舱室外墙外缘半径；恒定 24 格。超出此半径即为真空（扩大地图只增加真空区域）。
    *****/
    public const float RoomWallOuterRadius = 24f;

    /*****
    Date: 2026-09-06
    Name: RoomHalfAngleRad
    Description: 舱室半张角（24°，五舱均布 72° 间隔，舱间留 24° 外壳墙）。
    *****/
    public const float RoomHalfAngleRad = 24f * MathF.PI / 180f;

    /*****
    Date: 2026-09-06
    Name: RoomWallMarginRad
    Description: 舱室外墙半张角在舱室张角基础上的加宽余量（4°）。
    *****/
    public const float RoomWallMarginRad = 4f * MathF.PI / 180f;

    /*****
    Date: 2026-09-06
    Name: ZoneAngles
    Description: 五个功能区的中心角度（弧度，屏幕 Y 轴向下：-90° 为正上方，五区顺时针均布）。
    *****/
    private static readonly (ZoneId Zone, float AngleRad)[] ZoneAngles =
    {
        (ZoneId.Living, -MathF.PI / 2f),
        (ZoneId.Power, -MathF.PI / 10f),
        (ZoneId.LifeSupport, 3f * MathF.PI / 10f),
        (ZoneId.AirlockStorage, 7f * MathF.PI / 10f),
        (ZoneId.Communication, 11f * MathF.PI / 10f),
    };

    /*****
    Date: 2026-09-06
    Name: Generate
    Description: 生成环形基地：单次遍历按半径/角度分类格子（舱室地板、舱室外墙、走廊、内外壳墙、真空），随后计算各功能区设施锚点（标记 FacilitySlot）与出生格。
    *****/
    public static RingMapData Generate()
    {
        var map = new GridMap(MapSize, MapSize);

        for (int y = 0; y < MapSize; y++)
        {
            for (int x = 0; x < MapSize; x++)
            {
                float dx = x - Center;
                float dy = y - Center;
                float r = MathF.Sqrt(dx * dx + dy * dy);
                float theta = MathF.Atan2(dy, dx);
                (ZoneId Zone, float AngDist)? nearest = NearestZone(theta);
                float angDist = nearest?.AngDist ?? float.MaxValue;
                var cell = new Vector2I(x, y);

                CellKind kind;
                ZoneId? zone = null;

                if (angDist <= RoomHalfAngleRad && r >= CorridorInnerRadius && r < RoomOuterRadius)
                {
                    // 功能区舱室地板：覆盖走廊段并向外飘出加宽
                    kind = CellKind.Floor;
                    zone = nearest!.Value.Zone;
                }
                else if (angDist <= RoomHalfAngleRad + RoomWallMarginRad && r >= RoomOuterRadius && r < RoomWallOuterRadius)
                {
                    // 舱室外墙
                    kind = CellKind.Wall;
                }
                else if (r >= CorridorInnerRadius && r < CorridorOuterRadius)
                {
                    // 环状走廊
                    kind = CellKind.Floor;
                }
                else if (r >= CorridorOuterRadius && r < HullOuterRadius)
                {
                    // 外壳墙
                    kind = CellKind.Wall;
                }
                else if (r >= CorridorInnerRadius - 1f && r < CorridorInnerRadius)
                {
                    // 内环墙
                    kind = CellKind.Wall;
                }
                else
                {
                    // 中心真空区 / 舱外真空
                    kind = CellKind.Vacuum;
                }

                map.SetCell(cell, kind);
                if (zone != null) map.SetZone(cell, zone.Value);
            }
        }

        // 先为所有功能区计算设施锚点，再收集房间信息（锚点仅记录坐标；设备放置后由 Simulation 动态维护占地阻挡）
        var anchors = new Dictionary<ZoneId, Vector2I>();
        foreach ((ZoneId zone, _) in ZoneAngles)
        {
            Vector2I? anchor = FindFacilityAnchor(map, zone);
            if (anchor is { } a)
            {
                anchors[zone] = a;
            }
        }

        var rooms = new Dictionary<ZoneId, ZoneRoomInfo>();
        foreach ((ZoneId zone, _) in ZoneAngles)
        {
            rooms[zone] = new ZoneRoomInfo
            {
                Zone = zone,
                FacilityAnchor = anchors.TryGetValue(zone, out Vector2I anchor) ? anchor : null,
                SpawnCells = FindSpawnCells(map, zone),
            };
        }

        return new RingMapData { Map = map, Rooms = rooms };
    }

    /*****
    Date: 2026-09-06
    Name: NearestZone
    Description: 求与指定角度最近的功能区及其角距；用于判定格子是否落在某舱室张角内。
    *****/
    private static (ZoneId Zone, float AngDist)? NearestZone(float theta)
    {
        (ZoneId Zone, float AngDist) best = default;
        bool found = false;
        foreach ((ZoneId zone, float angle) in ZoneAngles)
        {
            float dist = MathF.Abs(AngularDistance(theta, angle));
            if (!found || dist < best.AngDist)
            {
                best = (zone, dist);
                found = true;
            }
        }
        return found ? best : null;
    }

    /*****
    Date: 2026-09-06
    Name: AngularDistance
    Description: 计算两角之差并归一化到 [-π, π]。
    *****/
    private static float AngularDistance(float a, float b)
    {
        float d = (a - b) % (2f * MathF.PI);
        if (d > MathF.PI) d -= 2f * MathF.PI;
        else if (d < -MathF.PI) d += 2f * MathF.PI;
        return d;
    }

    /*****
    Date: 2026-09-06
    Name: FindFacilityAnchor
    Description: 在指定功能区舱室内寻找 2×2 设施放置原点：候选格按「靠外缘优先、近舱室中心角优先」排序，返回第一个四格均为该功能区地板的原点；找不到返回 null。
    *****/
    private static Vector2I? FindFacilityAnchor(GridMap map, ZoneId zone)
    {
        var candidates = new List<(Vector2I Cell, float R, float AngDist)>();
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var cell = new Vector2I(x, y);
                if (map.GetCell(cell) != CellKind.Floor || map.GetZone(cell) != zone) continue;
                float dx = x - Center;
                float dy = y - Center;
                float theta = MathF.Atan2(dy, dx);
                candidates.Add((cell, MathF.Sqrt(dx * dx + dy * dy), MathF.Abs(AngularDistance(theta, ZoneAngleOf(zone)))));
            }
        }

        foreach ((Vector2I cell, _, _) in candidates.OrderByDescending(c => c.R).ThenBy(c => c.AngDist))
        {
            if (IsTwoByTwoZoneFloor(map, zone, cell)) return cell;
        }
        return null;
    }

    /*****
    Date: 2026-09-06
    Name: IsTwoByTwoZoneFloor
    Description: 判断以 origin 为原点的 2×2 区块是否全部为指定功能区的地板。
    *****/
    private static bool IsTwoByTwoZoneFloor(GridMap map, ZoneId zone, Vector2I origin)
    {
        for (int dy = 0; dy < 2; dy++)
        {
            for (int dx = 0; dx < 2; dx++)
            {
                var cell = new Vector2I(origin.X + dx, origin.Y + dy);
                if (map.GetCell(cell) != CellKind.Floor || map.GetZone(cell) != zone) return false;
            }
        }
        return true;
    }

    /*****
    Date: 2026-09-06
    Name: FindSpawnCells
    Description: 收集指定功能区的角色出生格：地板格按半径升序（内缘优先）取前 6 个。
    *****/
    private static IReadOnlyList<Vector2I> FindSpawnCells(GridMap map, ZoneId zone)
    {
        var cells = new List<(Vector2I Cell, float R)>();
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var cell = new Vector2I(x, y);
                if (map.GetCell(cell) != CellKind.Floor || map.GetZone(cell) != zone) continue;
                float dx = x - Center;
                float dy = y - Center;
                cells.Add((cell, MathF.Sqrt(dx * dx + dy * dy)));
            }
        }
        return cells.OrderBy(c => c.R).Take(6).Select(c => c.Cell).ToList();
    }

    /*****
    Date: 2026-09-06
    Name: ZoneAngleOf
    Description: 获取指定功能区的中心角度（弧度）。
    *****/
    private static float ZoneAngleOf(ZoneId zone)
    {
        foreach ((ZoneId z, float angle) in ZoneAngles)
        {
            if (z == zone) return angle;
        }
        return 0f;
    }
}
