using Godot;
using RelayStation.Core.Facilities;
using RelayStation.Core.Map;
using RelayStation.Game.Autoloads;
using RelayStation.Game.Map;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-06
Name: EditorPanel
Description: 地图编辑器 UI 面板；提供进入/退出编辑按钮、工具选择（地形画笔/功能区画笔/设施放置/拆除）、地形类型与功能区选择、设施下拉与保存按钮。根据当前工具动态显隐对应选项组；始终显示编辑切换按钮，编辑模式下展开工具区。
*****/
public partial class EditorPanel : PanelContainer
{
    /*****
    Date: 2026-09-06
    Name: _editor
    Description: 关联的地图编辑器。
    *****/
    private MapEditor? _editor;

    /*****
    Date: 2026-09-06
    Name: _facilityTemplates
    Description: 可放置的设施定义列表（供下拉选择）。
    *****/
    private IReadOnlyList<FacilityDef> _facilityTemplates = Array.Empty<FacilityDef>();

    /*****
    Date: 2026-09-06
    Name: _toggleButton
    Description: 进入/退出编辑切换按钮。
    *****/
    private Button? _toggleButton;

    /*****
    Date: 2026-09-06
    Name: _toolButtons
    Description: 工具按钮映射表（用于互斥选中态管理）。
    *****/
    private readonly Dictionary<MapEditor.Tool, Button> _toolButtons = new();

    /*****
    Date: 2026-09-06
    Name: _terrainButtons
    Description: 地形类型按钮映射表。
    *****/
    private readonly Dictionary<CellKind, Button> _terrainButtons = new();

    /*****
    Date: 2026-09-06
    Name: _zoneButtons
    Description: 功能区按钮映射表。
    *****/
    private readonly Dictionary<ZoneId, Button> _zoneButtons = new();

    /*****
    Date: 2026-09-06
    Name: _toolOptionsContainer
    Description: 工具选项区容器（编辑模式下可见）。
    *****/
    private VBoxContainer? _toolOptionsContainer;

    /*****
    Date: 2026-09-06
    Name: _terrainOptions
    Description: 地形选项组（仅地形画笔时可见）。
    *****/
    private VBoxContainer? _terrainOptions;

    /*****
    Date: 2026-09-06
    Name: _zoneOptions
    Description: 功能区选项组（仅功能区画笔时可见）。
    *****/
    private VBoxContainer? _zoneOptions;

    /*****
    Date: 2026-09-06
    Name: _facilityOptions
    Description: 设施选择组（仅设施放置时可见）。
    *****/
    private VBoxContainer? _facilityOptions;

    /*****
    Date: 2026-09-06
    Name: _facilitySelector
    Description: 设施下拉选择器。
    *****/
    private OptionButton? _facilitySelector;

    /*****
    Date: 2026-09-06
    Name: _saveButton
    Description: 保存地图按钮。
    *****/
    private Button? _saveButton;

    /*****
    Date: 2026-09-06
    Name: _undoButton
    Description: 撤销按钮；撤销最近一次编辑操作（等效 Ctrl+Z）。
    *****/
    private Button? _undoButton;

    /*****
    Date: 2026-09-06
    Name: Setup
    Description: 绑定编辑器与设施模板列表，构建控件并订阅编辑模式切换事件。
    *****/
    public void Setup(MapEditor editor, IReadOnlyList<FacilityDef> facilityTemplates)
    {
        _editor = editor;
        _facilityTemplates = facilityTemplates;
        BuildControls();
        _editor.EditModeChanged += OnEditModeChanged;
        UpdateVisibility();
    }

    /*****
    Date: 2026-09-06
    Name: BuildControls
    Description: 构建面板控件：切换按钮 + 工具行 + 地形/功能区/设施选项组 + 保存按钮。
    *****/
    private void BuildControls()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 4);
        AddChild(root);

        _toggleButton = new Button { Text = "进入编辑", CustomMinimumSize = new Vector2(120, 32) };
        _toggleButton.Pressed += OnTogglePressed;
        root.AddChild(_toggleButton);

        _toolOptionsContainer = new VBoxContainer();
        _toolOptionsContainer.AddThemeConstantOverride("separation", 4);
        root.AddChild(_toolOptionsContainer);

        // 工具选择行
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

        // 地形选项
        _terrainOptions = new VBoxContainer();
        _terrainOptions.AddThemeConstantOverride("separation", 2);
        _terrainOptions.AddChild(new Label { Text = "地形" });
        var terrainRow = new HBoxContainer();
        terrainRow.AddThemeConstantOverride("separation", 4);
        foreach (CellKind kind in new[] { CellKind.Floor, CellKind.Wall, CellKind.Door, CellKind.Vacuum })
        {
            var btn = new Button { Text = TerrainName(kind), ToggleMode = true, CustomMinimumSize = new Vector2(48, 28) };
            btn.Pressed += () => OnTerrainPressed(kind);
            _terrainButtons[kind] = btn;
            terrainRow.AddChild(btn);
        }
        _terrainOptions.AddChild(terrainRow);
        _toolOptionsContainer.AddChild(_terrainOptions);

        // 功能区选项
        _zoneOptions = new VBoxContainer();
        _zoneOptions.AddThemeConstantOverride("separation", 2);
        _zoneOptions.AddChild(new Label { Text = "功能区" });
        var zoneRow = new HBoxContainer();
        zoneRow.AddThemeConstantOverride("separation", 4);
        foreach (ZoneId zone in Enum.GetValues<ZoneId>())
        {
            var btn = new Button { Text = ZoneName(zone), ToggleMode = true, CustomMinimumSize = new Vector2(48, 28) };
            btn.Pressed += () => OnZonePressed(zone);
            _zoneButtons[zone] = btn;
            zoneRow.AddChild(btn);
        }
        _zoneOptions.AddChild(zoneRow);
        _toolOptionsContainer.AddChild(_zoneOptions);

        // 设施选择
        _facilityOptions = new VBoxContainer();
        _facilityOptions.AddThemeConstantOverride("separation", 2);
        _facilityOptions.AddChild(new Label { Text = "设施" });
        _facilitySelector = new OptionButton { CustomMinimumSize = new Vector2(160, 28) };
        foreach (FacilityDef def in _facilityTemplates)
        {
            _facilitySelector.AddItem(def.DisplayName);
        }
        if (_facilitySelector.ItemCount > 0)
        {
            _facilitySelector.Selected = 0;
            if (_editor != null) _editor.CurrentFacilityDef = _facilityTemplates[0];
        }
        _facilitySelector.ItemSelected += OnFacilitySelected;
        _facilityOptions.AddChild(_facilitySelector);
        _toolOptionsContainer.AddChild(_facilityOptions);

        // 保存按钮
        _saveButton = new Button { Text = "保存地图", CustomMinimumSize = new Vector2(120, 32) };
        _saveButton.Pressed += OnSavePressed;
        _toolOptionsContainer.AddChild(_saveButton);

        _undoButton = new Button { Text = "撤销 (Ctrl+Z)", CustomMinimumSize = new Vector2(120, 32) };
        _undoButton.Pressed += OnUndoPressed;
        _toolOptionsContainer.AddChild(_undoButton);

        // 初始选中态
        SelectTool(MapEditor.Tool.TerrainBrush);
        SelectTerrain(CellKind.Floor);
        SelectZone(ZoneId.Living);
    }

    /*****
    Date: 2026-09-06
    Name: OnTogglePressed
    Description: 切换编辑模式。
    *****/
    private void OnTogglePressed() => _editor?.ToggleEdit();

    /*****
    Date: 2026-09-06
    Name: OnEditModeChanged
    Description: 编辑模式切换时更新按钮文字与选项区可见性。
    *****/
    private void OnEditModeChanged(bool editing)
    {
        if (_toggleButton != null) _toggleButton.Text = editing ? "退出编辑" : "进入编辑";
        UpdateVisibility();
    }

    /*****
    Date: 2026-09-06
    Name: UpdateVisibility
    Description: 更新选项区可见性：非编辑时隐藏工具区，编辑时按当前工具显隐对应选项组。
    *****/
    private void UpdateVisibility()
    {
        bool editing = _editor?.IsEditing ?? false;
        if (_toolOptionsContainer != null) _toolOptionsContainer.Visible = editing;
        if (!editing) return;

        MapEditor.Tool tool = _editor?.CurrentTool ?? MapEditor.Tool.TerrainBrush;
        if (_terrainOptions != null) _terrainOptions.Visible = tool == MapEditor.Tool.TerrainBrush;
        if (_zoneOptions != null) _zoneOptions.Visible = tool == MapEditor.Tool.ZoneBrush;
        if (_facilityOptions != null) _facilityOptions.Visible = tool == MapEditor.Tool.FacilityPlace;
    }

    /*****
    Date: 2026-09-06
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
    Date: 2026-09-06
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
    Date: 2026-09-06
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
    Date: 2026-09-06
    Name: OnFacilitySelected
    Description: 下拉选择设施定义。
    *****/
    private void OnFacilitySelected(long index)
    {
        if (_editor == null || index < 0 || index >= _facilityTemplates.Count) return;
        _editor.CurrentFacilityDef = _facilityTemplates[(int)index];
    }

    /*****
    Date: 2026-09-06
    Name: OnSavePressed
    Description: 保存当前地图到 .tres。
    *****/
    private void OnSavePressed() => GameRoot.Instance?.SaveMap();

    /*****
    Date: 2026-09-06
    Name: OnUndoPressed
    Description: 撤销最近一次编辑操作。
    *****/
    private void OnUndoPressed() => _editor?.Undo();

    /*****
    Date: 2026-09-06
    Name: SelectTool
    Description: 设置工具按钮互斥选中态。
    *****/
    private void SelectTool(MapEditor.Tool tool)
    {
        foreach ((MapEditor.Tool t, Button btn) in _toolButtons)
            btn.ButtonPressed = (t == tool);
    }

    /*****
    Date: 2026-09-06
    Name: SelectTerrain
    Description: 设置地形按钮互斥选中态。
    *****/
    private void SelectTerrain(CellKind kind)
    {
        foreach ((CellKind k, Button btn) in _terrainButtons)
            btn.ButtonPressed = (k == kind);
    }

    /*****
    Date: 2026-09-06
    Name: SelectZone
    Description: 设置功能区按钮互斥选中态。
    *****/
    private void SelectZone(ZoneId zone)
    {
        foreach ((ZoneId z, Button btn) in _zoneButtons)
            btn.ButtonPressed = (z == zone);
    }

    /*****
    Date: 2026-09-06
    Name: ToolName
    Description: 工具显示名称。
    *****/
    private static string ToolName(MapEditor.Tool tool) => tool switch
    {
        MapEditor.Tool.TerrainBrush => "地形",
        MapEditor.Tool.ZoneBrush => "功能区",
        MapEditor.Tool.FacilityPlace => "放设施",
        MapEditor.Tool.FacilityRemove => "拆设施",
        _ => tool.ToString(),
    };

    /*****
    Date: 2026-09-06
    Name: TerrainName
    Description: 地形类型显示名称。
    *****/
    private static string TerrainName(CellKind kind) => kind switch
    {
        CellKind.Floor => "地板",
        CellKind.Wall => "墙",
        CellKind.Door => "门",
        CellKind.Vacuum => "真空",
        CellKind.FacilitySlot => "设施",
        _ => kind.ToString(),
    };

    /*****
    Date: 2026-09-06
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
    Date: 2026-09-06
    Name: _ExitTree
    Description: 节点退出时注销事件订阅，防止悬空回调。
    *****/
    public override void _ExitTree()
    {
        if (_editor != null) _editor.EditModeChanged -= OnEditModeChanged;
    }
}
