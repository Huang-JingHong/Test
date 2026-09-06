using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Events;
using RelayStation.Core.Facilities;
using RelayStation.Core.Map;
using RelayStation.Core.Tasks;
using RelayStation.Core.Time;
using GridMap = RelayStation.Core.Map.GridMap;

namespace RelayStation.Game.Autoloads;

/*****
Date: 2026-09-06
Name: GameRoot
Description: 组合根与唯一 Autoload；加载演示配置、装配模拟层各服务（时钟/地图/寻路/任务板/需求/事件总线）并放置设施与角色，每帧驱动 Simulation。启动时优先加载 base_map.tres 地图数据（含已放置设施），缺失时生成环形基地并落盘；编辑器通过 SaveMap() 持久化修改。表现层节点通过 Instance.Simulation 访问模拟服务。
*****/
public partial class GameRoot : Node
{
    /*****
    Date: 2026-09-06
    Name: DemoConfigPath
    Description: 演示配置资源的固定路径。
    *****/
    private const string DemoConfigPath = "res://resources/demo_config.tres";

    /*****
    Date: 2026-09-06
    Name: MapDataPath
    Description: 地图数据资源（.tres）的固定路径；启动时优先加载，编辑器保存时写入此路径。
    *****/
    private const string MapDataPath = "res://resources/maps/base_map.tres";

    /*****
    Date: 2026-09-06
    Name: Instance
    Description: GameRoot 的全局单例引用，供表现层节点访问模拟服务。
    *****/
    public static GameRoot Instance { get; private set; } = default!;

    /*****
    Date: 2026-09-06
    Name: Simulation
    Description: 模拟核心实例；持有时钟、地图、寻路、任务板、需求等服务的装配结果。
    *****/
    public Simulation Simulation { get; private set; } = default!;

    /*****
    Date: 2026-09-06
    Name: FacilityTemplates
    Description: 可放置的设施定义模板列表（取自演示配置）；供地图编辑器设施选择使用。
    *****/
    public IReadOnlyList<FacilityDef> FacilityTemplates { get; private set; } = Array.Empty<FacilityDef>();

    /*****
    Date: 2026-09-06
    Name: _Ready
    Description: 装配模拟层各服务（Autoload 先于主场景就绪）。
    *****/
    public override void _Ready()
    {
        Instance = this;
        Simulation = BuildSimulation();
    }

    /*****
    Date: 2026-09-06
    Name: _Process
    Description: 每帧推进模拟：时钟推进 + 角色行为状态机。
    *****/
    public override void _Process(double delta) => Simulation.Update(delta);

    /*****
    Date: 2026-09-06
    Name: BuildSimulation
    Description: 组装模拟核心：加载演示配置 → 加载或生成地图（含设施放置） → 创建时钟/寻路/任务板/需求 → 恢复设施到模拟 → 在生活区出生点创建角色。
    *****/
    private Simulation BuildSimulation()
    {
        var config = GD.Load<DemoConfigResource>(DemoConfigPath);
        if (config == null) GD.PushError($"无法加载演示配置：{DemoConfigPath}");

        FacilityTemplates = config?.Facilities ?? Array.Empty<FacilityDef>();

        var clock = new GameClock();
        var eventBus = new EventBus();

        (GridMap map, FacilityPlacement[] placements) = LoadOrGenerateMap(config);

        var simulation = new Simulation(
            clock,
            map,
            new GridPathfinding(map),
            new TaskBoard(eventBus),
            new NeedSystem(),
            eventBus);

        foreach (FacilityPlacement placement in placements)
        {
            RestoreFacility(simulation, placement);
        }

        IReadOnlyList<Vector2I> spawnCells = FindSpawnCells(map);
        int spawnIndex = 0;
        foreach (CharacterDef def in config?.Characters ?? Array.Empty<CharacterDef>())
        {
            simulation.AddCharacter(new CharacterSim(def, spawnCells[spawnIndex % spawnCells.Count]));
            spawnIndex++;
        }

        GD.Print($"[Demo] 模拟装配完成：角色 {simulation.Characters.Count} 名、设施 {simulation.Facilities.Count} 台（地图 {map.Width}×{map.Height}）。");
        return simulation;
    }

    /*****
    Date: 2026-09-06
    Name: LoadOrGenerateMap
    Description: 优先加载 base_map.tres（含格子与设施放置列表）；若 .tres 不存在则生成环形基地、按配置放置初始设施并落盘。
    *****/
    private (GridMap Map, FacilityPlacement[] Placements) LoadOrGenerateMap(DemoConfigResource? config)
    {
        if (ResourceLoader.Exists(MapDataPath) && GD.Load<GridMapData>(MapDataPath) is { } data)
        {
            GD.Print($"[Demo] 从 {MapDataPath} 加载地图数据。");
            return (data.ToGridMap(), data.Facilities);
        }

        RingMapData ringData = RingMapGenerator.Generate();
        var placements = new List<FacilityPlacement>();
        foreach (FacilityDef def in config?.Facilities ?? Array.Empty<FacilityDef>())
        {
            if (ringData.Rooms.TryGetValue(def.Zone, out ZoneRoomInfo? room) && room.FacilityAnchor is { } anchor)
            {
                placements.Add(new FacilityPlacement { Def = def, Origin = anchor, State = def.InitialState });
            }
            else
            {
                GD.PushWarning($"设施「{def.DisplayName}」所在功能区 {def.Zone} 无可用放置锚点，已跳过。");
            }
        }
        SaveMap(ringData.Map, placements);
        return (ringData.Map, placements.ToArray());
    }

    /*****
    Date: 2026-09-06
    Name: RestoreFacility
    Description: 从放置条目恢复设施到模拟：创建 FacilitySim、占地格标记为 FacilitySlot、恢复状态并加入模拟。
    *****/
    private static void RestoreFacility(Simulation simulation, FacilityPlacement placement)
    {
        if (placement.Def == null) return;
        var facility = new FacilitySim(placement.Def, placement.Origin);
        foreach (Vector2I cell in facility.OccupiedCells())
        {
            simulation.Map.SetCell(cell, CellKind.FacilitySlot);
        }
        facility.SetState(placement.State);
        simulation.AddFacility(facility);
    }

    /*****
    Date: 2026-09-06
    Name: FindSpawnCells
    Description: 在生活区收集角色出生格（Floor 且 Zone=Living），按到中心距离升序取前 6 个；无生活区地板时回退到地图中心。
    *****/
    private static IReadOnlyList<Vector2I> FindSpawnCells(GridMap map)
    {
        float center = (map.Width - 1) / 2f;
        var cells = new List<(Vector2I Cell, float R)>();
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var cell = new Vector2I(x, y);
                if (map.GetCell(cell) == CellKind.Floor && map.GetZone(cell) == ZoneId.Living)
                {
                    float dx = x - center;
                    float dy = y - center;
                    cells.Add((cell, MathF.Sqrt(dx * dx + dy * dy)));
                }
            }
        }
        if (cells.Count == 0)
            return new[] { new Vector2I(map.Width / 2, map.Height / 2) };
        return cells.OrderBy(c => c.R).Take(6).Select(c => c.Cell).ToList();
    }

    /*****
    Date: 2026-09-06
    Name: SaveMap
    Description: 将当前模拟层的地图与设施列表保存为 base_map.tres（供地图编辑器调用）；自动创建目标目录。
    *****/
    public void SaveMap()
    {
        var map = (GridMap)Simulation.Map;
        var placements = Simulation.Facilities
            .Select(f => new FacilityPlacement { Def = f.Def, Origin = f.OriginCell, State = f.State })
            .ToArray();
        SaveMap(map, placements);
    }

    /*****
    Date: 2026-09-06
    Name: SaveMap
    Description: 内部保存方法；将 GridMap 与设施放置列表序列化为 GridMapData 并写入 .tres。
    *****/
    private static void SaveMap(GridMap map, IReadOnlyList<FacilityPlacement> placements)
    {
        DirAccess.MakeDirRecursiveAbsolute("res://resources/maps");
        GridMapData data = GridMapData.FromGridMap(map, placements);
        Error err = ResourceSaver.Save(data, MapDataPath);
        if (err != Error.Ok)
            GD.PushError($"保存地图数据失败：{err}");
        else
            GD.Print($"[Demo] 地图已保存至 {MapDataPath}（设施 {placements.Count} 台）。");
    }
}
