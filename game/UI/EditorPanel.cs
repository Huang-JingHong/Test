using Godot;
using RelayStation.Core.Facilities;
using RelayStation.Core.Map;
using RelayStation.Game.Autoloads;
using RelayStation.Game.Map;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-25
Name: EditorPanel
Description: 建造/地图编辑 UI 面板；由左上角菜单「建造」入口经 PanelHost 开合（不再常驻屏幕），面板可见即处于建造模式（可见性由 Main 协调 MapEditor 进出编辑模式）。工具为「建造 / 拆除」（开发者模式另有「地形 / 功能区」画笔）；建造工具下按分类过滤设施并以「占位图标 + 名称」的图标网格可视化选择（替换原下拉框），分类与设施均取自 FacilityCatalog，数据驱动、零硬编码。顶部显示备用零件余额（仅正常模式）。开发者模式展示全部工具与「保存地图 / 撤销」；正常模式隐藏地形/功能区画笔与撤销，保存入口在 ESC 暂停菜单。剧情动画期间按钮输入被忽略。
*****/
public partial class EditorPanel : PanelContainer
{
    /*****
    Date: 2026-09-25
    Name: _editor
    Description: 关联的地图编辑器。
    *****/
    private MapEditor? _editor;

    /*****
    Date: 2026-09-25
    Name: _catalog
    Description: 可建造设施目录（按分类分组的设施定义）。
    *****/
    private FacilityCatalog _catalog = new(Array.Empty<FacilityDef>());

    /*****
    Date: 2026-09-25
    Name: DevMode
    Description: 是否处于开发者模式（读 GameRoot 会话状态；无会话时视为开发者模式以保证独立可用）。
    *****/
    private static bool DevMode => GameRoot.Instance?.DevModeEnabled ?? true;

    /*****
    Date: 2026-09-25
    Name: _balanceLabel
    Description: 备用零件余额标签（正常模式显示，开发者模式隐藏）。
    *****/
    private Label? _balanceLabel;

    /*****
    Date: 2026-09-25
    Name: _toolButtons
    Description: 工具按钮映射表（用于互斥选中态管理）。
    *****/
    private readonly Dictionary<MapEditor.Tool, Button> _toolButtons = new();

    /*****
    Date: 2026-09-25
    Name: _terrainButtons
    Description: 地形类型按钮映射表。
    *****/
    private readonly Dictionary<CellKind, Button> _terrainButtons = new();

    /*****
    Date: 2026-09-25
    Name: _zoneButtons
    Description: 功能区按钮映射表。
    *****/
    private readonly Dictionary<ZoneId, Button> _zoneButtons = new();

    /*****
    Date: 2026-09-25
    Name: _categoryButtons
    Description: 设施分类按钮映射表。
    *****/
    private readonly Dictionary<FacilityCategory, Button> _categoryButtons = new();

    /*****
    Date: 2026-09-25
    Name: _facilityButtons
    Description: 当前分类下设施按钮映射表（随分类切换重建）。
    *****/
    private readonly Dictionary<FacilityDef, Button> _facilityButtons = new();

    /*****
    Date: 2026-09-25
    Name: _placeholderIcons
    Description: 分类占位图标纹理缓存（无贴图的设施以分类色块作为占位图）。
    *****/
    private readonly Dictionary<FacilityCategory, Texture2D> _placeholderIcons = new();

    /*****
    Date: 2026-09-25
    Name: _selectedCategory
    Description: 当前选中的设施分类；未选时为 null。
    *****/
    private FacilityCategory? _selectedCategory;

    /*****
    Date: 2026-09-25
    Name: _toolOptionsContainer
    Description: 工具选项区容器（工具行 + 各工具的选项组）。
    *****/
    private VBoxContainer? _toolOptionsContainer;

    /*****
    Date: 2026-09-25
    Name: _terrainOptions
    Description: 地形选项组（仅地形画笔时可见）。
    *****/
    private VBoxContainer? _terrainOptions;

    /*****
    Date: 2026-09-25
    Name: _zoneOptions
    Description: 功能区选项组（仅功能区画笔时可见）。
    *****/
    private VBoxContainer? _zoneOptions;

    /*****
    Date: 2026-09-25
    Name: _buildOptions
    Description: 建造选项组（分类行 + 设施图标网格；仅建造工具时可见）。
    *****/
    private VBoxContainer? _buildOptions;

    /*****
    Date: 2026-09-25
    Name: _demolishOptions
    Description: 拆除选项组（操作提示；仅拆除工具时可见）。
    *****/
    private VBoxContainer? _demolishOptions;

    /*****
    Date: 2026-09-25
    Name: _facilityGrid
    Description: 设施图标网格容器（随分类切换重建）。
    *****/
    private GridContainer? _facilityGrid;

    /*****
    Date: 2026-09-25
    Name: _saveButton
    Description: 保存地图按钮（仅开发者模式显示）。
    *****/
    private Button? _saveButton;

    /*****
    Date: 2026-09-25
    Name: _undoButton
    Description: 撤销按钮（仅开发者模式显示；正常模式完全屏蔽）。
    *****/
    private Button? _undoButton;

    /*****
    Date: 2026-09-25
    Name: Setup
    Description: 绑定编辑器与设施目录，构建控件并订阅余额变化事件（面板开合与编辑模式由 Main 经 PanelHost 协调，本面板不自行切换）。
    *****/
    public void Setup(MapEditor editor, FacilityCatalog catalog)
    {
        _editor = editor;
        _catalog = catalog;
        BuildControls();
        if (GameRoot.Instance?.ResourceStore is { } store)
        {
            UpdateBalanceLabel();
            store.BalanceChanged += OnBalanceChanged;
        }
        UpdateVisibility();
    }

    /*****
    Date: 2026-09-25
    Name: BuildControls
    Description: 构建面板控件：余额 + 工具行 + 地形/功能区/建造/拆除选项组 + 保存/撤销（后两者仅开发者模式）。
    *****/
    private void BuildControls()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 4);
        AddChild(root);

        _balanceLabel = new Label { Text = "备用零件：0" };
        _balanceLabel.Visible = !DevMode;
        root.AddChild(_balanceLabel);

        _toolOptionsContainer = new VBoxContainer();
        _toolOptionsContainer.AddThemeConstantOverride("separation", 6);
        root.AddChild(_toolOptionsContainer);

        var toolRow = new HBoxContainer();
        toolRow.AddThemeConstantOverride("separation", 4);
        _toolOptionsContainer.AddChild(toolRow);
        foreach (MapEditor.Tool tool in Enum.GetValues<MapEditor.Tool>())
        {
            var btn = new Button { Text = ToolName(tool), ToggleMode = true, CustomMinimumSize = new Vector2(70, 28) };
            btn.Pressed += () => OnToolPressed(tool);
            _toolButtons[tool] = btn;
            toolRow.AddChild(btn);
        }
        // 正常模式仅建造玩法：隐藏地形/功能区画笔按钮
        if (!DevMode)
        {
            _toolButtons[MapEditor.Tool.TerrainBrush].Visible = false;
            _toolButtons[MapEditor.Tool.ZoneBrush].Visible = false;
        }

        _terrainOptions = BuildTerrainOptions();
        _toolOptionsContainer.AddChild(_terrainOptions);

        _zoneOptions = BuildZoneOptions();
        _toolOptionsContainer.AddChild(_zoneOptions);

        _buildOptions = BuildBuildOptions();
        _toolOptionsContainer.AddChild(_buildOptions);

        _demolishOptions = BuildDemolishOptions();
        _toolOptionsContainer.AddChild(_demolishOptions);

        // 保存地图按钮（仅开发者模式显示：覆写默认地图；正常模式的保存入口在 ESC 暂停菜单）
        _saveButton = new Button { Text = "保存地图", CustomMinimumSize = new Vector2(120, 32) };
        _saveButton.Visible = DevMode;
        _saveButton.Pressed += OnSavePressed;
        _toolOptionsContainer.AddChild(_saveButton);

        // 撤销按钮（仅开发者模式显示：正常模式完全屏蔽撤销）
        _undoButton = new Button { Text = "撤销 (Ctrl+Z)", CustomMinimumSize = new Vector2(120, 32) };
        _undoButton.Visible = DevMode;
        _undoButton.Pressed += OnUndoPressed;
        _toolOptionsContainer.AddChild(_undoButton);

        // 初始选中态（正常模式默认建造工具：设施放置）
        MapEditor.Tool initialTool = DevMode ? MapEditor.Tool.TerrainBrush : MapEditor.Tool.FacilityPlace;
        if (_editor != null) _editor.CurrentTool = initialTool;
        SelectTool(initialTool);
        SelectTerrain(CellKind.Floor);
        SelectZone(ZoneId.Living);
        SelectCategory(_catalog.Categories.Count > 0 ? _catalog.Categories[0] : null);
    }

    /*****
    Date: 2026-09-25
    Name: BuildTerrainOptions
    Description: 构建地形选项组（标题 + 地形类型按钮行）。
    *****/
    private VBoxContainer BuildTerrainOptions()
    {
        var options = new VBoxContainer();
        options.AddThemeConstantOverride("separation", 2);
        options.AddChild(new Label { Text = "地形" });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        foreach (CellKind kind in new[] { CellKind.Floor, CellKind.Wall, CellKind.Door, CellKind.Vacuum })
        {
            var btn = new Button { Text = TerrainName(kind), ToggleMode = true, CustomMinimumSize = new Vector2(48, 28) };
            btn.Pressed += () => OnTerrainPressed(kind);
            _terrainButtons[kind] = btn;
            row.AddChild(btn);
        }
        options.AddChild(row);
        return options;
    }

    /*****
    Date: 2026-09-25
    Name: BuildZoneOptions
    Description: 构建功能区选项组（标题 + 功能区按钮行）。
    *****/
    private VBoxContainer BuildZoneOptions()
    {
        var options = new VBoxContainer();
        options.AddThemeConstantOverride("separation", 2);
        options.AddChild(new Label { Text = "功能区" });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        foreach (ZoneId zone in Enum.GetValues<ZoneId>())
        {
            var btn = new Button { Text = ZoneName(zone), ToggleMode = true, CustomMinimumSize = new Vector2(48, 28) };
            btn.Pressed += () => OnZonePressed(zone);
            _zoneButtons[zone] = btn;
            row.AddChild(btn);
        }
        options.AddChild(row);
        return options;
    }

    /*****
    Date: 2026-09-25
    Name: BuildBuildOptions
    Description: 构建建造选项组：分类按钮行（自动换行）+ 设施图标网格（滚动容器内）。分类与设施完全取自目录，无硬编码。
    *****/
    private VBoxContainer BuildBuildOptions()
    {
        var options = new VBoxContainer();
        options.AddThemeConstantOverride("separation", 4);

        options.AddChild(new Label { Text = "分类" });
        var categoryRow = new HFlowContainer();
        categoryRow.AddThemeConstantOverride("h_separation", 4);
        categoryRow.AddThemeConstantOverride("v_separation", 4);
        options.AddChild(categoryRow);
        foreach (FacilityCategory category in _catalog.Categories)
        {
            var btn = new Button { Text = CategoryName(category), ToggleMode = true, CustomMinimumSize = new Vector2(56, 26) };
            btn.Pressed += () => OnCategoryPressed(category);
            _categoryButtons[category] = btn;
            categoryRow.AddChild(btn);
        }

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(184, 200),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        options.AddChild(scroll);
        _facilityGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _facilityGrid.AddThemeConstantOverride("h_separation", 4);
        _facilityGrid.AddThemeConstantOverride("v_separation", 4);
        scroll.AddChild(_facilityGrid);
        return options;
    }

    /*****
    Date: 2026-09-25
    Name: BuildDemolishOptions
    Description: 构建拆除选项组（操作提示文案）。
    *****/
    private static VBoxContainer BuildDemolishOptions()
    {
        var options = new VBoxContainer();
        options.AddThemeConstantOverride("separation", 2);
        options.AddChild(new Label { Text = "拆除" });
        var hint = new Label
        {
            Text = "点击地图上的设施以下达拆除指令（工人在时间与零件返还下完成）。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(184, 0),
        };
        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.AddThemeColorOverride("font_color", new Color(0.75f, 0.78f, 0.82f));
        options.AddChild(hint);
        return options;
    }

    /*****
    Date: 2026-09-25
    Name: OnBalanceChanged
    Description: 备用零件余额变化时刷新标签。
    *****/
    private void OnBalanceChanged() => UpdateBalanceLabel();

    /*****
    Date: 2026-09-25
    Name: UpdateBalanceLabel
    Description: 刷新备用零件余额标签文本。
    *****/
    private void UpdateBalanceLabel()
    {
        if (_balanceLabel != null)
            _balanceLabel.Text = $"备用零件（地上可达）：{GameRoot.Instance?.ResourceStore?.Balance ?? 0}";
    }

    /*****
    Date: 2026-09-25
    Name: UpdateVisibility
    Description: 按当前工具显隐对应选项组（地形/功能区/建造/拆除）；面板可见即建造模式，故不再按编辑模式整体隐藏。
    *****/
    private void UpdateVisibility()
    {
        MapEditor.Tool tool = _editor?.CurrentTool ?? MapEditor.Tool.TerrainBrush;
        if (_terrainOptions != null) _terrainOptions.Visible = tool == MapEditor.Tool.TerrainBrush;
        if (_zoneOptions != null) _zoneOptions.Visible = tool == MapEditor.Tool.ZoneBrush;
        if (_buildOptions != null) _buildOptions.Visible = tool == MapEditor.Tool.FacilityPlace;
        if (_demolishOptions != null) _demolishOptions.Visible = tool == MapEditor.Tool.FacilityRemove;
    }

    /*****
    Date: 2026-09-25
    Name: OnToolPressed
    Description: 选择编辑工具并更新互斥选中态与选项区显隐。
    *****/
    private void OnToolPressed(MapEditor.Tool tool)
    {
        if (_editor == null) return;
        _editor.CurrentTool = tool;
        SelectTool(tool);
        UpdateVisibility();
    }

    /*****
    Date: 2026-09-25
    Name: OnCategoryPressed
    Description: 切换设施分类：重建设施图标网格并默认选中该分类首个设施。
    *****/
    private void OnCategoryPressed(FacilityCategory category) => SelectCategory(category);

    /*****
    Date: 2026-09-25
    Name: SelectCategory
    Description: 应用分类选中态（null 表示无分类）：更新分类按钮互斥态并重建设施网格。
    *****/
    private void SelectCategory(FacilityCategory? category)
    {
        _selectedCategory = category;
        foreach ((FacilityCategory c, Button btn) in _categoryButtons)
            btn.ButtonPressed = (c == category);
        RebuildFacilityGrid();
    }

    /*****
    Date: 2026-09-25
    Name: RebuildFacilityGrid
    Description: 重建当前分类下的设施图标网格：每项为「占位图标 + 名称」的切换按钮（有贴图用贴图，无贴图用分类色块占位）；重建后默认选中首项并回写编辑器。
    *****/
    private void RebuildFacilityGrid()
    {
        if (_facilityGrid == null) return;

        foreach (Node child in _facilityGrid.GetChildren())
        {
            _facilityGrid.RemoveChild(child);
            child.QueueFree();
        }
        _facilityButtons.Clear();

        IReadOnlyList<FacilityDef> defs = _selectedCategory is { } category
            ? _catalog.GetByCategory(category)
            : Array.Empty<FacilityDef>();

        foreach (FacilityDef def in defs)
        {
            var btn = new Button
            {
                ToggleMode = true,
                CustomMinimumSize = new Vector2(88, 76),
                Icon = ResolveIcon(def),
                Text = def.DisplayName,
                IconAlignment = HorizontalAlignment.Center,
                VerticalIconAlignment = VerticalAlignment.Top,
                Alignment = HorizontalAlignment.Center,
            };
            btn.AddThemeConstantOverride("icon_max_width", 40);
            btn.Pressed += () => OnFacilityPressed(def);
            _facilityButtons[def] = btn;
            _facilityGrid.AddChild(btn);
        }

        FacilityDef? first = defs.Count > 0 ? defs[0] : null;
        SelectFacility(first);
    }

    /*****
    Date: 2026-09-25
    Name: OnFacilityPressed
    Description: 选择设施定义并更新网格选中态。
    *****/
    private void OnFacilityPressed(FacilityDef def) => SelectFacility(def);

    /*****
    Date: 2026-09-25
    Name: SelectFacility
    Description: 应用设施选中态：更新设施按钮互斥态并回写编辑器 CurrentFacilityDef。
    *****/
    private void SelectFacility(FacilityDef? def)
    {
        foreach ((FacilityDef d, Button btn) in _facilityButtons)
            btn.ButtonPressed = ReferenceEquals(d, def);
        if (_editor != null) _editor.CurrentFacilityDef = def;
    }

    /*****
    Date: 2026-09-25
    Name: ResolveIcon
    Description: 解析设施图标：配置了 IconTexturePath 时加载贴图（失败回退占位），否则用该分类的占位色块纹理。
    *****/
    private Texture2D? ResolveIcon(FacilityDef def)
    {
        if (!string.IsNullOrEmpty(def.IconTexturePath))
        {
            var texture = GD.Load<Texture2D>(def.IconTexturePath);
            if (texture != null) return texture;
        }
        return PlaceholderIcon(def.Category);
    }

    /*****
    Date: 2026-09-25
    Name: PlaceholderIcon
    Description: 取指定分类的占位图标（首次生成后缓存）：纯色方块纹理，作为无贴图设施的占位图片。
    *****/
    private Texture2D PlaceholderIcon(FacilityCategory category)
    {
        if (_placeholderIcons.TryGetValue(category, out Texture2D? cached)) return cached;

        var image = Image.CreateEmpty(48, 48, false, Image.Format.Rgba8);
        image.Fill(CategoryColor(category));
        var texture = ImageTexture.CreateFromImage(image);
        _placeholderIcons[category] = texture;
        return texture;
    }

    /*****
    Date: 2026-09-25
    Name: OnTerrainPressed
    Description: 选择地形类型并更新互斥选中态。
    *****/
    private void OnTerrainPressed(CellKind kind)
    {
        if (_editor == null) return;
        _editor.CurrentTerrain = kind;
        SelectTerrain(kind);
    }

    /*****
    Date: 2026-09-25
    Name: OnZonePressed
    Description: 选择功能区并更新互斥选中态。
    *****/
    private void OnZonePressed(ZoneId zone)
    {
        if (_editor == null) return;
        _editor.CurrentZone = zone;
        SelectZone(zone);
    }

    /*****
    Date: 2026-09-25
    Name: OnSavePressed
    Description: 保存按钮（仅开发者模式可见）：覆写默认地图 base_map.tres；正常模式的保存入口在 ESC 暂停菜单。
    *****/
    private void OnSavePressed()
    {
        if (GameRoot.Instance?.CutsceneActive == true) return;
        GameRoot.Instance?.SaveMap();
    }

    /*****
    Date: 2026-09-25
    Name: OnUndoPressed
    Description: 撤销按钮（仅开发者模式可见）：撤销最近一次编辑操作。
    *****/
    private void OnUndoPressed()
    {
        if (GameRoot.Instance?.CutsceneActive == true) return;
        _editor?.Undo();
    }

    /*****
    Date: 2026-09-25
    Name: SelectTool
    Description: 设置工具按钮互斥选中态。
    *****/
    private void SelectTool(MapEditor.Tool tool)
    {
        foreach ((MapEditor.Tool t, Button btn) in _toolButtons)
            btn.ButtonPressed = (t == tool);
    }

    /*****
    Date: 2026-09-25
    Name: SelectTerrain
    Description: 设置地形按钮互斥选中态。
    *****/
    private void SelectTerrain(CellKind kind)
    {
        foreach ((CellKind k, Button btn) in _terrainButtons)
            btn.ButtonPressed = (k == kind);
    }

    /*****
    Date: 2026-09-25
    Name: SelectZone
    Description: 设置功能区按钮互斥选中态。
    *****/
    private void SelectZone(ZoneId zone)
    {
        foreach ((ZoneId z, Button btn) in _zoneButtons)
            btn.ButtonPressed = (z == zone);
    }

    /*****
    Date: 2026-09-25
    Name: ToolName
    Description: 工具显示名称。
    *****/
    private static string ToolName(MapEditor.Tool tool) => tool switch
    {
        MapEditor.Tool.TerrainBrush => "地形",
        MapEditor.Tool.ZoneBrush => "功能区",
        MapEditor.Tool.FacilityPlace => "建造",
        MapEditor.Tool.FacilityRemove => "拆除",
        _ => tool.ToString(),
    };

    /*****
    Date: 2026-09-25
    Name: CategoryName
    Description: 设施分类显示名称；新增分类只需在此追加一行（未列出时回退枚举名）。
    *****/
    private static string CategoryName(FacilityCategory category) => category switch
    {
        FacilityCategory.Furniture => "家具",
        FacilityCategory.Power => "电力",
        FacilityCategory.Oxygen => "氧气",
        FacilityCategory.Communication => "通信",
        FacilityCategory.Water => "水利",
        FacilityCategory.Building => "建筑物",
        FacilityCategory.Medical => "医疗",
        FacilityCategory.Research => "科研",
        _ => category.ToString(),
    };

    /*****
    Date: 2026-09-25
    Name: CategoryColor
    Description: 设施分类占位色（无贴图设施以该色块作占位图标）；新增分类只需在此追加一行。
    *****/
    private static Color CategoryColor(FacilityCategory category) => category switch
    {
        FacilityCategory.Furniture => new Color(0.72f, 0.55f, 0.35f),
        FacilityCategory.Power => new Color(0.90f, 0.78f, 0.25f),
        FacilityCategory.Oxygen => new Color(0.35f, 0.75f, 0.85f),
        FacilityCategory.Communication => new Color(0.65f, 0.45f, 0.85f),
        FacilityCategory.Water => new Color(0.30f, 0.55f, 0.90f),
        FacilityCategory.Building => new Color(0.60f, 0.62f, 0.66f),
        FacilityCategory.Medical => new Color(0.85f, 0.40f, 0.45f),
        FacilityCategory.Research => new Color(0.45f, 0.78f, 0.50f),
        _ => new Color(0.55f, 0.55f, 0.55f),
    };

    /*****
    Date: 2026-09-25
    Name: TerrainName
    Description: 地形类型显示名称。
    *****/
    private static string TerrainName(CellKind kind) => kind switch
    {
        CellKind.Floor => "地板",
        CellKind.Wall => "墙",
        CellKind.Door => "门",
        CellKind.Vacuum => "真空",
        _ => kind.ToString(),
    };

    /*****
    Date: 2026-09-25
    Name: ZoneName
    Description: 功能区显示名称。
    *****/
    private static string ZoneName(ZoneId zone) => zone switch
    {
        ZoneId.Living => "生活",
        ZoneId.Power => "动力",
        ZoneId.LifeSupport => "生保",
        ZoneId.AirlockStorage => "气闸",
        ZoneId.Communication => "通信",
        _ => zone.ToString(),
    };

    /*****
    Date: 2026-09-25
    Name: _ExitTree
    Description: 节点退出时注销事件订阅，防止悬空回调。
    *****/
    public override void _ExitTree()
    {
        if (GameRoot.Instance?.ResourceStore is { } store)
            store.BalanceChanged -= OnBalanceChanged;
    }
}
