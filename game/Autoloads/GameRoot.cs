using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Events;
using RelayStation.Core.Facilities;
using RelayStation.Core.Items;
using RelayStation.Core.Map;
using RelayStation.Core.Resources;
using RelayStation.Core.Tasks;
using RelayStation.Core.Time;
using RelayStation.Game.Save;
using GridMap = RelayStation.Core.Map.GridMap;

namespace RelayStation.Game.Autoloads;

/*****
Date: 2026-09-25
Name: GameRoot
Description: 组合根与唯一 Autoload；以会话流驱动游戏：标题界面调用 StartNewGame/LoadGame 构建模拟层（时钟/地图/寻路/任务板/需求/事件总线/备用零件库存）后切入主场景，_Process 每帧驱动 Simulation。新游戏优先加载 base_map.tres 默认地图（缺失则生成环形基地并落盘）；读档从存档槽恢复。开发者模式（标题界面开启）才允许覆写默认地图，正常模式的修改只能存入存档槽。
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
    Description: 默认地图数据资源（.tres）的固定路径；新游戏启动时优先加载，开发者模式保存时写入此路径。
    *****/
    private const string MapDataPath = "res://resources/maps/base_map.tres";

    /*****
    Date: 2026-09-25
    Name: FacilityCatalogPath
    Description: 可建造设施目录资源（.tres）的固定路径；与地图初始摆件解耦，供建造面板按分类选择。
    *****/
    private const string FacilityCatalogPath = "res://resources/facility_catalog.tres";

    /*****
    Date: 2026-09-26
    Name: PartsItemPath
    Description: 「通用零件」物品定义资源（.tres）的固定路径。
    *****/
    private const string PartsItemPath = "res://resources/items/general_purpose_parts.tres";

    /*****
    Date: 2026-09-26
    Name: FoodItemPath
    Description: 「压缩干粮」（食物）物品定义资源（.tres）的固定路径。
    *****/
    private const string FoodItemPath = "res://resources/items/food_ration.tres";

    /*****
    Date: 2026-09-26
    Name: WaterItemPath
    Description: 「饮用水」物品定义资源（.tres）的固定路径。
    *****/
    private const string WaterItemPath = "res://resources/items/water_bottle.tres";

    /*****
    Date: 2026-09-26
    Name: GroundPartsGroups
    Description: 开局把初始备用零件散落成的地面堆数（初始 100 个 → 5 组 × 20；不能整除时余数并入最后一组）。
    *****/
    private const int GroundPartsGroups = 5;

    /*****
    Date: 2026-09-26
    Name: GroundFoodGroups
    Description: 开局把初始食物散落成的地面堆数。
    *****/
    private const int GroundFoodGroups = 3;

    /*****
    Date: 2026-09-26
    Name: GroundWaterGroups
    Description: 开局把初始饮用水散落成的地面堆数。
    *****/
    private const int GroundWaterGroups = 2;

    /*****
    Date: 2026-09-25
    Name: SessionState
    Description: 会话状态枚举；标题阶段不构建模拟，进入游戏中态后才驱动模拟层。
    *****/
    private enum SessionState { Title, InGame }

    /*****
    Date: 2026-09-25
    Name: _state
    Description: 当前会话状态。
    *****/
    private SessionState _state = SessionState.Title;

    /*****
    Date: 2026-09-06
    Name: Instance
    Description: GameRoot 的全局单例引用，供表现层节点访问模拟服务。
    *****/
    public static GameRoot Instance { get; private set; } = default!;

    /*****
    Date: 2026-09-06
    Name: Simulation
    Description: 模拟核心实例；持有时钟、地图、寻路、任务板、需求等服务的装配结果。标题阶段为 null。
    *****/
    public Simulation Simulation { get; private set; } = default!;

    /*****
    Date: 2026-09-26
    Name: ResourceStore
    Description: 备用零件库存。正常模式下为**地上零件堆**（GroundItemStore：余额 = 可达堆之和，建造消耗与拆除返还都真实落在物品堆上）；物品定义缺失时退化为抽象池（ResourceStore）。开发者模式不注入库存（免费）。
    *****/
    public IResourceStore ResourceStore { get; private set; } = new ResourceStore();

    /*****
    Date: 2026-09-26
    Name: PartsItem
    Description: 备用零件的物品定义（res://resources/items/general_purpose_parts.tres）；加载失败为 null，此时库存退化为抽象池。装备库存、开局散落与拆除返还都依赖它。
    *****/
    public ItemDef? PartsItem { get; private set; }

    /*****
    Date: 2026-09-26
    Name: FoodItem
    Description: 食物（压缩干粮）的物品定义；需求驱动的自动取用依赖它（经 ConsumableCatalog 注册）。加载失败为 null。
    *****/
    public ItemDef? FoodItem { get; private set; }

    /*****
    Date: 2026-09-26
    Name: WaterItem
    Description: 饮用水（水）的物品定义；需求驱动的自动取用依赖它（经 ConsumableCatalog 注册）。加载失败为 null。
    *****/
    public ItemDef? WaterItem { get; private set; }

    /*****
    Date: 2026-09-25
    Name: DevModeEnabled
    Description: 是否处于开发者模式（标题界面开启，会话级）；开启后解锁完整地图编辑与默认地图覆写。
    *****/
    public bool DevModeEnabled { get; private set; }

    /*****
    Date: 2026-09-25
    Name: IntroPlayed
    Description: 本局开场动画是否已播放（新游戏置 false，读档取存档值；播过后存档记录 true）。
    *****/
    public bool IntroPlayed { get; set; }

    /*****
    Date: 2026-09-25
    Name: CutsceneActive
    Description: 是否有剧情动画正在播放；动画期间 UI 交互按钮应忽略输入。
    *****/
    public bool CutsceneActive { get; set; }

    /*****
    Date: 2026-09-25
    Name: FacilityCatalog
    Description: 可建造设施目录（按分类分组的只读视图）；供建造面板分类过滤与选择。取自 resources/facility_catalog.tres，与地图初始摆件（DemoConfigResource.Facilities）解耦。
    *****/
    public FacilityCatalog FacilityCatalog { get; private set; } = new(Array.Empty<FacilityDef>());

    /*****
    Date: 2026-09-25
    Name: _Ready
    Description: 仅注册单例引用；模拟层延迟到 StartNewGame/LoadGame 由标题界面驱动构建。
    *****/
    public override void _Ready()
    {
        Instance = this;
    }

    /*****
    Date: 2026-09-25
    Name: _Process
    Description: 每帧推进模拟（时钟 + 角色行为状态机）；仅在游戏中态且模拟已构建时驱动。
    *****/
    public override void _Process(double delta)
    {
        if (_state != SessionState.InGame || Simulation == null) return;
        Simulation.Update(delta);
    }

    /*****
    Date: 2026-09-26
    Name: StartNewGame
    Description: 开始新游戏：加载演示配置、设施目录与零件物品定义，加载（或生成）默认地图，按配置生成三名角色；初始备用零件**以散落的形式投放到随机地板格**（100 → 5 组 × 20）而不是放进抽象池，库存随之改为「地上可达零件堆」；开场动画标记为未播放。
    *****/
    public void StartNewGame(bool devMode)
    {
        DevModeEnabled = devMode;
        DemoConfigResource? config = LoadDemoConfig();
        LoadFacilityCatalog();
        LoadPartsItem();
        LoadConsumableItems();
        Simulation = BuildSimulation(config, save: null);
        ResourceStore = BuildResourceStore(config?.StartingParts ?? 0);
        IntroPlayed = false;
        _state = SessionState.InGame;
        GD.Print($"[Game] 新游戏开始：角色 {Simulation.Characters.Count} 名、设施 {Simulation.Facilities.Count} 台、地上零件 {Simulation.Items.Stacks.Count} 堆（余额 {ResourceStore.Balance}）（开发者模式：{(devMode ? "开" : "关")}）。");
    }

    /*****
    Date: 2026-09-25
    Name: LoadGame
    Description: 从指定存档槽读档：恢复地图/设施/地面物品/角色（重建为 Idle）/时钟分钟；库存同样为「地上可达零件堆」，故读档后余额即地上零件之和。开场动画按存档标记不再重播。失败返回 false。
    *****/
    public bool LoadGame(int slot)
    {
        SaveGameResource? save = SaveGameService.LoadFromSlot(slot);
        if (save == null)
        {
            GD.PushError($"[Game] 无法从存档槽 {slot} 加载（不存在或损坏）。");
            return false;
        }

        DevModeEnabled = false;
        DemoConfigResource? config = LoadDemoConfig();
        LoadFacilityCatalog();
        LoadPartsItem();
        LoadConsumableItems();
        Simulation = BuildSimulation(config, save);
        ResourceStore = BuildResourceStore(save.SpareParts);
        IntroPlayed = save.IntroPlayed;
        _state = SessionState.InGame;
        GD.Print($"[Game] 已从存档槽 {slot} 加载：角色 {Simulation.Characters.Count} 名、设施 {Simulation.Facilities.Count} 台、地上零件 {Simulation.Items.Stacks.Count} 堆（余额 {ResourceStore.Balance}）。");
        return true;
    }

    /*****
    Date: 2026-09-26
    Name: LoadPartsItem
    Description: 加载备用零件的物品定义；加载失败时报警并置 null（库存退化为抽象池，其它玩法不受影响）。
    *****/
    private void LoadPartsItem()
    {
        PartsItem = GD.Load<ItemDef>(PartsItemPath);
        if (PartsItem == null) GD.PushError($"无法加载物品定义：{PartsItemPath}");
    }

    /*****
    Date: 2026-09-26
    Name: LoadConsumableItems
    Description: 加载可消耗物品定义（食物/饮用水）；加载失败时报警并置 null（自动取用会静默不触发，其余玩法不受影响）。新游戏与读档都要加载——读档的地面物品按资源引用恢复，注册表则统一在此重建。
    *****/
    private void LoadConsumableItems()
    {
        FoodItem = GD.Load<ItemDef>(FoodItemPath);
        if (FoodItem == null) GD.PushError($"无法加载物品定义：{FoodItemPath}");
        WaterItem = GD.Load<ItemDef>(WaterItemPath);
        if (WaterItem == null) GD.PushError($"无法加载物品定义：{WaterItemPath}");
    }

    /*****
    Date: 2026-09-26
    Name: BuildResourceStore
    Description: 装配备件库存：物品定义就绪时用「地上零件堆」（余额 = 可达堆之和、消耗/返还都落在物品堆上），否则退化为抽象池并以 fallbackParts 作初始余额。
    *****/
    private IResourceStore BuildResourceStore(int fallbackParts)
        => PartsItem != null
            ? new GroundItemStore(Simulation, PartsItem)
            : new ResourceStore(fallbackParts);

    /*****
    Date: 2026-09-25
    Name: LoadDemoConfig
    Description: 加载演示配置（角色/初始摆件/初始零件）；加载失败时报警并返回 null。
    *****/
    private DemoConfigResource? LoadDemoConfig()
    {
        var config = GD.Load<DemoConfigResource>(DemoConfigPath);
        if (config == null) GD.PushError($"无法加载演示配置：{DemoConfigPath}");
        return config;
    }

    /*****
    Date: 2026-09-25
    Name: LoadFacilityCatalog
    Description: 加载可建造设施目录并构建分类索引（FacilityCatalog）；加载失败时退化为空目录并报警（建造面板将无可选设施，但不影响游戏运行）。
    *****/
    private void LoadFacilityCatalog()
    {
        var resource = GD.Load<FacilityCatalogResource>(FacilityCatalogPath);
        if (resource == null)
        {
            GD.PushError($"无法加载可建造设施目录：{FacilityCatalogPath}");
            FacilityCatalog = new FacilityCatalog(Array.Empty<FacilityDef>());
            return;
        }
        FacilityCatalog = new FacilityCatalog(resource.Facilities);
    }

    /*****
    Date: 2026-09-26
    Name: BuildSimulation
    Description: 组装模拟核心：新游戏走 LoadOrGenerateMap（默认地图，缺失生成并落盘），读档走存档地图并注入时钟分钟；随后恢复设施、**注册可消耗物品**（自动取用据此按种类反查）、恢复或散落地面物品、生成角色（新游戏按配置在生活区出生格，读档按存档条目原格重建）。初始零件/食物/饮用水只在新游戏时散落；读档按存档物品恢复，并对阶段 D 之前的旧存档（无物品数据但备件数额 > 0）按开局规则补散落，避免读档后库存归零。
    *****/
    private Simulation BuildSimulation(DemoConfigResource? config, SaveGameResource? save)
    {
        var clock = new GameClock();
        var eventBus = new EventBus();

        GridMap map;
        FacilityPlacement[] placements;
        if (save != null)
        {
            if (save.MapData == null) throw new InvalidOperationException("存档缺少地图数据，无法加载。");
            map = save.MapData.ToGridMap();
            placements = save.MapData.Facilities ?? Array.Empty<FacilityPlacement>();
            clock.SetTotalGameMinutes(save.ClockMinutes);
        }
        else
        {
            (map, placements) = LoadOrGenerateMap(config);
        }

        // 设施占地共享集：Simulation 维护（增删设施时更新），GridPathfinding 以其作阻塞判定
        var blockedCells = new HashSet<Vector2I>();

        var simulation = new Simulation(
            clock,
            map,
            new GridPathfinding(map, blockedCells.Contains),
            new TaskBoard(eventBus),
            new NeedSystem(),
            eventBus,
            blockedCells);

        foreach (FacilityPlacement placement in placements)
        {
            RestoreFacility(simulation, placement);
        }

        if (save != null)
        {
            foreach (CharacterSaveEntry entry in save.Characters ?? Array.Empty<CharacterSaveEntry>())
            {
                if (entry?.Def == null) continue;
                var character = new CharacterSim(entry.Def, entry.Cell) { Health = entry.Health };
                simulation.AddCharacter(character);
                // 需求：按存档数值恢复（旧档缺字段 → FromArray 回退满值）
                simulation.NeedSystem.Set(character, NeedSnapshot.FromArray(entry.Needs));
                // 背包与装备：按存档恢复（旧档缺字段 → 空背包；不做容量校验，超重状态原样带回）
                character.Inventory.Restore(
                    (entry.Equipment ?? Array.Empty<EquipmentSaveEntry>())
                        .Where(e => e?.Def != null)
                        .Select(e => (e.Slot, (IItemDef)e.Def!)),
                    (entry.Inventory ?? Array.Empty<InventorySaveEntry>())
                        .Where(e => e?.Def != null && e.Count > 0)
                        .Select(e => ((IItemDef)e.Def!, e.Count)));
            }
        }
        else
        {
            IReadOnlyList<Vector2I> spawnCells = FindSpawnCells(map);
            int spawnIndex = 0;
            foreach (CharacterDef def in config?.Characters ?? Array.Empty<CharacterDef>())
            {
                simulation.AddCharacter(new CharacterSim(def, spawnCells[spawnIndex % spawnCells.Count]));
                spawnIndex++;
            }
        }

        // 可消耗物品注册（食物/饮用水）：需求驱动的自动取用按种类反查；新游戏与读档都登记
        if (FoodItem != null) simulation.Consumables.Register(FoodItem);
        if (WaterItem != null) simulation.Consumables.Register(WaterItem);

        // 地面物品：新游戏按配置数额散落到随机地板格；读档按存档恢复（旧存档则迁移补散落）
        if (save == null)
        {
            if (PartsItem != null)
                simulation.ScatterItems(PartsItem, config?.StartingParts ?? 0, GroundPartsGroups, new Random());
            if (FoodItem != null)
                simulation.ScatterItems(FoodItem, config?.StartingFood ?? 0, GroundFoodGroups, new Random());
            if (WaterItem != null)
                simulation.ScatterItems(WaterItem, config?.StartingWater ?? 0, GroundWaterGroups, new Random());
        }
        else
        {
            ItemPlacement[] savedItems = save.MapData?.Items ?? Array.Empty<ItemPlacement>();
            foreach (ItemPlacement placement in savedItems)
            {
                RestoreItem(simulation, placement);
            }
            if (savedItems.Length == 0 && save.SpareParts > 0 && PartsItem != null)
            {
                GD.PushWarning($"[Game] 存档中的 {save.SpareParts} 个备用零件是阶段 D 之前的旧格式（无地面物品数据），已按开局规则补散落到地上。");
                simulation.ScatterItems(PartsItem, save.SpareParts, GroundPartsGroups, new Random());
            }
        }

        GD.Print($"[Demo] 模拟装配完成：角色 {simulation.Characters.Count} 名、设施 {simulation.Facilities.Count} 台、地面物品 {simulation.Items.Stacks.Count} 堆（地图 {map.Width}×{map.Height}）。");
        return simulation;
    }

    /*****
    Date: 2026-09-26
    Name: RestoreItem
    Description: 从物品条目恢复一组地面物品到模拟；定义缺失或数量非法时跳过（数量超过单组上限时由注册表按 MaxStack 自动拆堆）。
    *****/
    private static void RestoreItem(Simulation simulation, ItemPlacement placement)
    {
        if (placement.Def == null || placement.Count <= 0) return;
        simulation.Items.Add(placement.Def, placement.Count, placement.Cell, placement.PixelOffset);
    }

    /*****
    Date: 2026-09-25
    Name: LoadOrGenerateMap
    Description: 优先加载 base_map.tres（含格子与设施放置列表）；若 .tres 不存在、或尺寸与当前生成器（RingMapGenerator.MapSize）不符（地图尺寸变更后旧存档），则生成环形基地、按配置放置初始设施并落盘。
    *****/
    private (GridMap Map, FacilityPlacement[] Placements) LoadOrGenerateMap(DemoConfigResource? config)
    {
        if (ResourceLoader.Exists(MapDataPath) && GD.Load<GridMapData>(MapDataPath) is { } data)
        {
            if (data.Width == RingMapGenerator.MapSize && data.Height == RingMapGenerator.MapSize)
            {
                GD.Print($"[Demo] 从 {MapDataPath} 加载地图数据。");
                return (data.ToGridMap(), data.Facilities ?? Array.Empty<FacilityPlacement>());
            }
            GD.PushWarning($"[Demo] 默认地图尺寸（{data.Width}×{data.Height}）与当前生成器（{RingMapGenerator.MapSize}×{RingMapGenerator.MapSize}）不符，忽略旧数据并重新生成。");
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
    Date: 2026-09-25
    Name: RestoreFacility
    Description: 从放置条目恢复设施到模拟：创建 FacilitySim、按归一化后的稳定状态恢复并加入模拟（占地阻挡由 Simulation 动态维护，不修改格子地形）。任务不参与存档，故作业态（建造中/拆除中/维修中）在读档时归一（见 NormalizeState）。
    *****/
    private static void RestoreFacility(Simulation simulation, FacilityPlacement placement)
    {
        if (placement.Def == null) return;
        FacilityState? state = NormalizeState(placement.State);
        if (state is not { } stable) return; // 建造中：未建成，不恢复
        var facility = new FacilitySim(placement.Def, placement.Origin);
        facility.SetState(stable);
        simulation.AddFacility(facility);
    }

    /*****
    Date: 2026-09-25
    Name: NormalizeState
    Description: 读档时把不可持续的作业态归一为稳定态：建造中→不恢复（null，未建成即视为未建）、拆除中→运行、维修中→损坏（可再次修复）；其余状态原样返回。
    *****/
    private static FacilityState? NormalizeState(FacilityState state) => state switch
    {
        FacilityState.UnderConstruction => null,
        FacilityState.Demolishing => FacilityState.Operational,
        FacilityState.UnderRepair => FacilityState.Damaged,
        _ => state,
    };

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
    Date: 2026-09-25
    Name: ReturnToTitle
    Description: 退出至标题界面：重置会话状态（模拟清空、开发者模式与动画标记复位），_Process 停止驱动；由暂停菜单「退出游戏至主菜单」调用，随后切换到标题场景。
    *****/
    public void ReturnToTitle()
    {
        _state = SessionState.Title;
        Simulation = null!;
        DevModeEnabled = false;
        CutsceneActive = false;
        IntroPlayed = false;
    }

    /*****
    Date: 2026-09-25
    Name: SaveMap
    Description: 将当前地图与设施列表保存为默认地图 base_map.tres；仅开发者模式允许调用（正常模式修改只能存入存档槽）。
    *****/
    public void SaveMap()
    {
        if (!DevModeEnabled)
        {
            GD.PushWarning("[Game] 正常模式不允许覆写默认地图；请经存档槽保存（开发者模式方可覆写默认地图）。");
            return;
        }
        if (Simulation == null) return;

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

    /*****
    Date: 2026-09-26
    Name: SaveGame
    Description: 将当前局面快照（地图/设施/地面物品/角色位置/时钟/备用零件余额/开场标记）写入指定存档槽（user://saves/slot_n.tres）。SpareParts 记录保存当刻的余额（= 地上可达零件之和），读档时库存由地面物品重建，该字段仅作参考与旧档迁移依据。
    *****/
    public void SaveGame(int slot)
    {
        if (Simulation == null) return;

        var map = (GridMap)Simulation.Map;
        var placements = Simulation.Facilities
            .Select(f => new FacilityPlacement { Def = f.Def, Origin = f.OriginCell, State = f.State })
            .ToArray();
        var entries = Simulation.Characters
            .Select(c => new CharacterSaveEntry
            {
                Def = c.Def,
                Cell = c.Cell,
                Health = c.Health,
                Needs = Simulation.NeedSystem.Read(c).ToArray(),
                Equipment = c.Inventory.Equipped
                    .Where(pair => pair.Value is ItemDef)
                    .Select(pair => new EquipmentSaveEntry { Slot = pair.Key, Def = (ItemDef)pair.Value })
                    .ToArray(),
                Inventory = c.Inventory.Entries
                    .Where(e => e.Def is ItemDef)
                    .Select(e => new InventorySaveEntry { Def = (ItemDef)e.Def, Count = e.Count })
                    .ToArray(),
            })
            .ToArray();
        // 地面物品（非 ItemDef 实现仅存在于引擎外测试，存档跳过）
        var itemPlacements = Simulation.Items.Stacks
            .Where(s => s.Def is ItemDef)
            .Select(s => new ItemPlacement
            {
                Def = (ItemDef)s.Def,
                Cell = s.Cell,
                PixelOffset = s.PixelOffset,
                Count = s.Count,
            })
            .ToArray();

        var save = new SaveGameResource
        {
            MapData = GridMapData.FromGridMap(map, placements, itemPlacements),
            ClockMinutes = Simulation.Clock.TotalGameMinutes,
            Characters = entries,
            SpareParts = ResourceStore.Balance,
            IntroPlayed = IntroPlayed,
            SavedAtIso = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        };
        SaveGameService.SaveToSlot(slot, save);
    }
}
