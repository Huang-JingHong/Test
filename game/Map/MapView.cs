using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Map;
using RelayStation.Game.Autoloads;

namespace RelayStation.Game.Map;

/*****
Date: 2026-09-06
Name: MapView
Description: 格子地图表现层；底层铺满宇宙背景图，其上以 Textures 材质构建 TileSet（真空格透明透出背景），用 TileMapLayer 渲染环形基地（地板按功能区贴图），提供格子坐标换算、悬停高亮与移动中角色的路径调试线。
*****/
public partial class MapView : Node2D
{
    /*****
    Date: 2026-09-06
    Name: TileSize
    Description: 单格像素尺寸。
    *****/
    public const int TileSize = 32;

    /*****
    Date: 2026-09-06
    Name: AtlasVacuum
    Description: 真空格图集坐标。
    *****/
    private static readonly Vector2I AtlasVacuum = new(0, 0);

    /*****
    Date: 2026-09-06
    Name: AtlasWall
    Description: 墙体图集坐标。
    *****/
    private static readonly Vector2I AtlasWall = new(1, 0);

    /*****
    Date: 2026-09-06
    Name: AtlasCorridor
    Description: 走廊地板图集坐标。
    *****/
    private static readonly Vector2I AtlasCorridor = new(2, 0);

    /*****
    Date: 2026-09-06
    Name: AtlasDoor
    Description: 门图集坐标。
    *****/
    private static readonly Vector2I AtlasDoor = new(3, 0);

    /*****
    Date: 2026-09-06
    Name: AtlasLiving
    Description: 生活区地板图集坐标。
    *****/
    private static readonly Vector2I AtlasLiving = new(0, 1);

    /*****
    Date: 2026-09-06
    Name: AtlasPower
    Description: 动力区地板图集坐标。
    *****/
    private static readonly Vector2I AtlasPower = new(1, 1);

    /*****
    Date: 2026-09-06
    Name: AtlasLifeSupport
    Description: 生保区地板图集坐标。
    *****/
    private static readonly Vector2I AtlasLifeSupport = new(2, 1);

    /*****
    Date: 2026-09-06
    Name: AtlasAirlockStorage
    Description: 气闸仓储区地板图集坐标。
    *****/
    private static readonly Vector2I AtlasAirlockStorage = new(3, 1);

    /*****
    Date: 2026-09-06
    Name: AtlasCommunication
    Description: 通信区地板图集坐标。
    *****/
    private static readonly Vector2I AtlasCommunication = new(0, 2);

    /*****
    Date: 2026-09-06
    Name: AtlasFacilitySlot
    Description: 设施占位格图集坐标。
    *****/
    private static readonly Vector2I AtlasFacilitySlot = new(1, 2);

    /*****
    Date: 2026-09-06
    Name: AtlasHighlight
    Description: 悬停高亮图集坐标（半透明白）。
    *****/
    private static readonly Vector2I AtlasHighlight = new(2, 2);

    /*****
    Date: 2026-09-06
    Name: _simulation
    Description: 模拟核心引用（只读）。
    *****/
    private Simulation? _simulation;

    /*****
    Date: 2026-09-06
    Name: _baseLayer
    Description: 基础地形渲染层。
    *****/
    private TileMapLayer? _baseLayer;

    /*****
    Date: 2026-09-06
    Name: _highlightLayer
    Description: 悬停高亮渲染层。
    *****/
    private TileMapLayer? _highlightLayer;

    /*****
    Date: 2026-09-06
    Name: _pathLines
    Description: 移动中角色到路径调试线的映射表。
    *****/
    private readonly Dictionary<CharacterSim, Line2D> _pathLines = new();

    /*****
    Date: 2026-09-06
    Name: _hoverCell
    Description: 当前悬停格子。
    *****/
    private Vector2I _hoverCell;

    /*****
    Date: 2026-09-06
    Name: _hoverValid
    Description: 当前悬停格子是否在地图内。
    *****/
    private bool _hoverValid;

    /*****
    Date: 2026-09-06
    Name: HoverCellChanged
    Description: 悬停格子变化事件（参数：新格子、是否在地图内）。
    *****/
    public event Action<Vector2I, bool>? HoverCellChanged;

    /*****
    Date: 2026-09-06
    Name: _Ready
    Description: 初始化：绑定模拟核心、构建渲染层并按格子数据填充 Tile。
    *****/
    public override void _Ready()
    {
        _simulation = GameRoot.Instance?.Simulation;
        BuildLayers();
        FillCells();
    }

    /*****
    Date: 2026-09-06
    Name: _Process
    Description: 每帧更新悬停高亮与路径调试线。
    *****/
    public override void _Process(double delta)
    {
        UpdateHover();
        UpdateDebugPathLines();
    }

    /*****
    Date: 2026-09-06
    Name: MapToLocal
    Description: 格子坐标 → 世界像素坐标（该格中心）。
    *****/
    public Vector2 MapToLocal(Vector2I cell)
        => _baseLayer?.MapToLocal(cell) ?? Vector2.Zero;

    /*****
    Date: 2026-09-06
    Name: LocalToMap
    Description: 世界像素坐标 → 格子坐标。
    *****/
    public Vector2I LocalToMap(Vector2 localPosition)
        => _baseLayer?.LocalToMap(localPosition) ?? Vector2I.Zero;

    /*****
    Date: 2026-09-06
    Name: BackgroundTexturePath
    Description: 背景图资源路径（universe_background.png，铺满整张地图世界尺寸）。
    *****/
    private const string BackgroundTexturePath = "res://game/Textures/universe_background.png";

    /*****
    Date: 2026-09-06
    Name: _backgroundLayer
    Description: 背景图层节点（最底层，铺满地图世界区域）。
    *****/
    private Sprite2D? _backgroundLayer;

    /*****
    Date: 2026-09-06
    Name: BuildLayers
    Description: 构建背景层、基础层与高亮层（背景层最先添加置于最底，基础/高亮层共用材质 TileSet）。
    *****/
    private void BuildLayers()
    {
        BuildBackgroundLayer();
        TileSet tileSet = BuildTileSet();
        _baseLayer = new TileMapLayer { Name = "BaseLayer", TileSet = tileSet };
        _highlightLayer = new TileMapLayer { Name = "HighlightLayer", TileSet = tileSet };
        AddChild(_baseLayer);
        AddChild(_highlightLayer);
    }

    /*****
    Date: 2026-09-06
    Name: BuildBackgroundLayer
    Description: 构建背景层：加载 universe_background.png 纹理，按地图世界尺寸（Width×TileSize × Height×TileSize）缩放 Sprite2D 使其铺满整张地图区域，左上角对齐地图原点，作为 MapView 第一个子节点置于最底层。
    *****/
    private void BuildBackgroundLayer()
    {
        if (_simulation == null) return;
        var texture = GD.Load<Texture2D>(BackgroundTexturePath);
        if (texture == null)
        {
            GD.PushError($"[MapView] 无法加载背景纹理：{BackgroundTexturePath}");
            return;
        }
        float worldWidth = _simulation.Map.Width * TileSize;
        float worldHeight = _simulation.Map.Height * TileSize;
        _backgroundLayer = new Sprite2D
        {
            Name = "BackgroundLayer",
            Texture = texture,
            Centered = false,
            Position = Vector2.Zero,
        };
        _backgroundLayer.Scale = new Vector2(
            worldWidth / texture.GetWidth(),
            worldHeight / texture.GetHeight());
        AddChild(_backgroundLayer);
    }

    /*****
    Date: 2026-09-06
    Name: BuildTileSet
    Description: 构建 TileSet：经 Godot 资源系统加载各材质 PNG（统一转换为 RGBA8 并缩放到 32×32 后拼贴，避免无 Alpha 通道的 RGB 材质因格式不匹配拼贴失败），真空格保持透明，共创建 11 种 Tile（真空/墙/走廊/门/五功能区/设施槽/高亮）；高亮为程序化半透明白色。
    *****/
    private static TileSet BuildTileSet()
    {
        var texturePaths = new (Vector2I Atlas, string Path)[]
        {
            (AtlasWall, "res://game/Textures/wall.png"),
            (AtlasCorridor, "res://game/Textures/corridor_floor.png"),
            (AtlasDoor, "res://game/Textures/door.png"),
            (AtlasLiving, "res://game/Textures/livingroom_floor.png"),
            (AtlasPower, "res://game/Textures/power_sector_floor.png"),
            (AtlasLifeSupport, "res://game/Textures/support_sector_floor.png"),
            (AtlasAirlockStorage, "res://game/Textures/airlock_and_storage_sector_floor.png"),
            (AtlasCommunication, "res://game/Textures/communication_sector_floor.png"),
            (AtlasFacilitySlot, "res://game/Textures/facility_placeholder.png"),
        };

        var atlas = Image.CreateEmpty(4 * TileSize, 3 * TileSize, false, Image.Format.Rgba8);
        var coords = new List<Vector2I> { AtlasVacuum };
        var tileSizeVec = new Vector2I(TileSize, TileSize);

        foreach ((Vector2I atlasCoord, string path) in texturePaths)
        {
            var texture = GD.Load<Texture2D>(path);
            Image? tileImage = texture?.GetImage();
            if (texture == null || tileImage == null)
            {
                GD.PushWarning($"[MapView] 无法加载材质：{path}");
                continue;
            }
            tileImage.Convert(Image.Format.Rgba8);
            if (tileImage.GetWidth() != TileSize || tileImage.GetHeight() != TileSize)
            {
                tileImage.Resize(TileSize, TileSize);
            }
            atlas.BlitRect(tileImage, new Rect2I(Vector2I.Zero, tileSizeVec), atlasCoord * TileSize);
            coords.Add(atlasCoord);
        }

        // 高亮格子：半透明白色
        var highlightImage = Image.CreateEmpty(TileSize, TileSize, false, Image.Format.Rgba8);
        highlightImage.Fill(new Color(1f, 1f, 1f, 0.45f));
        atlas.BlitRect(highlightImage, new Rect2I(Vector2I.Zero, tileSizeVec), AtlasHighlight * TileSize);
        coords.Add(AtlasHighlight);

        var source = new TileSetAtlasSource
        {
            Texture = ImageTexture.CreateFromImage(atlas),
            TextureRegionSize = tileSizeVec,
        };
        foreach (Vector2I coord in coords)
        {
            source.CreateTile(coord);
        }

        var tileSet = new TileSet { TileSize = tileSizeVec };
        tileSet.AddSource(source, 0);
        return tileSet;
    }

    /*****
    Date: 2026-09-06
    Name: FillCells
    Description: 按模拟层格子数据填充基础层 Tile：Floor 按功能区着色，其余按地形着色。
    *****/
    private void FillCells()
    {
        if (_simulation == null || _baseLayer == null) return;
        IGridMap map = _simulation.Map;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var cell = new Vector2I(x, y);
                _baseLayer.SetCell(cell, 0, AtlasFor(map, cell));
            }
        }
    }

    /*****
    Date: 2026-09-06
    Name: AtlasFor
    Description: 地形/功能区 → 灰盒图集坐标。
    *****/
    private static Vector2I AtlasFor(IGridMap map, Vector2I cell)
    {
        return map.GetCell(cell) switch
        {
            CellKind.Wall => AtlasWall,
            CellKind.Door => AtlasDoor,
            CellKind.FacilitySlot => AtlasFacilitySlot,
            CellKind.Floor => map.GetZone(cell) switch
            {
                ZoneId.Living => AtlasLiving,
                ZoneId.Power => AtlasPower,
                ZoneId.LifeSupport => AtlasLifeSupport,
                ZoneId.AirlockStorage => AtlasAirlockStorage,
                ZoneId.Communication => AtlasCommunication,
                _ => AtlasCorridor,
            },
            _ => AtlasVacuum,
        };
    }

    /*****
    Date: 2026-09-06
    Name: RefreshCell
    Description: 刷新单个格子的渲染（供地图编辑器涂抹后调用）。
    *****/
    public void RefreshCell(Vector2I cell)
    {
        if (_simulation == null || _baseLayer == null) return;
        _baseLayer.SetCell(cell, 0, AtlasFor(_simulation.Map, cell));
    }

    /*****
    Date: 2026-09-06
    Name: RefreshAll
    Description: 清空并重新填充整张地图渲染（供地图编辑器批量修改后调用）。
    *****/
    public void RefreshAll()
    {
        if (_simulation == null || _baseLayer == null) return;
        _baseLayer.Clear();
        FillCells();
    }

    /*****
    Date: 2026-09-06
    Name: UpdateHover
    Description: 将鼠标世界坐标换算为格子：在地图内时于高亮层放置半透明 Tile，并触发 HoverCellChanged 事件。
    *****/
    private void UpdateHover()
    {
        if (_simulation == null || _highlightLayer == null) return;

        Vector2I cell = LocalToMap(GetGlobalMousePosition());
        bool valid = cell.X >= 0 && cell.X < _simulation.Map.Width
                  && cell.Y >= 0 && cell.Y < _simulation.Map.Height;

        if (cell == _hoverCell && valid == _hoverValid) return;

        _hoverCell = cell;
        _hoverValid = valid;
        _highlightLayer.Clear();
        if (valid) _highlightLayer.SetCell(cell, 0, AtlasHighlight);
        HoverCellChanged?.Invoke(cell, valid);
    }

    /*****
    Date: 2026-09-06
    Name: UpdateDebugPathLines
    Description: 为每个移动中的角色维护一条青色路径调试线（当前格 + 剩余路径）；停止移动时移除对应线（开发期可视化，验收后可隐藏）。
    *****/
    private void UpdateDebugPathLines()
    {
        if (_simulation == null) return;

        List<CharacterSim> moving = _simulation.Characters
            .Where(c => c.State == CharacterState.Moving && c.Path != null)
            .ToList();

        foreach ((CharacterSim character, Line2D line) in _pathLines.Where(kv => !moving.Contains(kv.Key)).ToArray())
        {
            line.QueueFree();
            _pathLines.Remove(character);
        }

        foreach (CharacterSim character in moving)
        {
            if (!_pathLines.TryGetValue(character, out Line2D? line))
            {
                line = new Line2D { Width = 3f, DefaultColor = new Color(0.25f, 0.85f, 1f, 0.8f) };
                AddChild(line);
                _pathLines[character] = line;
            }

            line.ClearPoints();
            line.AddPoint(MapToLocal(character.Cell));
            for (int i = character.PathIndex; i < character.Path!.Count; i++)
            {
                line.AddPoint(MapToLocal(character.Path[i]));
            }
        }
    }
}
