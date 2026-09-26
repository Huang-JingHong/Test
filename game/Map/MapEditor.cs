using Godot;
using RelayStation.Core.Common;
using RelayStation.Core.Facilities;
using RelayStation.Core.Map;
using RelayStation.Game.Autoloads;
using RelayStation.Game.Facilities;
using RelayStation.Game.Input;

namespace RelayStation.Game.Map;

/*****
Date: 2026-09-25
Name: MapEditor
Description: 游戏内地图编辑/建造器；编辑模式下暂停时钟、禁用点击拾取。开发者模式为地图编辑沙盒：地形画笔、功能区画笔、设施即时放置与拆除（免费、可撤销）。正常模式为建造玩法：地形/功能区画笔禁用，设施放置以「建造中」占位入场并发起建造任务、拆除发起拆除任务（均由工人在时间与零件消耗下完成，见 Main 的任务装配），撤销在正常模式完全屏蔽。运行时直接修改模拟层 GridMap 与设施列表，即时刷新渲染；保存经 GameRoot 持久化（开发者模式写默认地图 base_map.tres，正常模式存档槽）。提供 FacilityPlaced/FacilityRemoved 事件供 Main 同步增删 FacilityView，FacilityBuildRequested/FacilityDemolishRequested 供 Main 创建建造/拆除任务。建造工具下每帧驱动注入的 FacilityPlacementPreview 显示落位幽灵（可放绿框 / 不可放红框，判定与真实放置同源），被拒点击经 PlacementRejected 事件交给表现层在点击处提示原因（编辑器本身不接触 UI）。
*****/
public partial class MapEditor : Node
{
    /*****
    Date: 2026-09-06
    Name: Tool
    Description: 编辑器工具枚举（地形画笔 / 功能区画笔 / 设施放置 / 设施拆除）。
    *****/
    public enum Tool { TerrainBrush, ZoneBrush, FacilityPlace, FacilityRemove }

    /*****
    Date: 2026-09-06
    Name: ToggleKey
    Description: 切换编辑模式的键盘快捷键（键位待定，当前占位 F12）。
    *****/
    private const Key ToggleKey = Key.F12;

    private Simulation? _simulation;
    private MapView? _mapView;
    private InputPicker? _inputPicker;

    /*****
    Date: 2026-09-26
    Name: _placementPreview
    Description: 建造预览幽灵（同实体层节点；由 Main 创建并经 SetPlacementPreview 注入，可缺省为 null）。
    *****/
    private FacilityPlacementPreview? _placementPreview;

    private bool _isEditing;
    private bool _wasClockPaused;
    private Tool _currentTool = Tool.TerrainBrush;
    private CellKind _currentTerrain = CellKind.Floor;
    private ZoneId _currentZone = ZoneId.Living;
    private FacilityDef? _currentFacilityDef;
    private bool _painting;

    /*****
    Date: 2026-09-26
    Name: _pointerViewportPosition
    Description: 最后一次鼠标事件自带的视口坐标（鼠标移动与左/右键事件都会刷新）；格子定位统一由它经「视口 → 地图本地」变换换算，与 InputPicker 的约定一致——不读全局鼠标，既便于事件驱动验证，也保证相机移动后换算始终基于当帧画布变换。未收到任何鼠标事件时为 null。
    *****/
    private Vector2? _pointerViewportPosition;

    /*****
    Date: 2026-09-06
    Name: MaxUndoActions
    Description: 撤销栈最大容量；超出时丢弃最旧记录。
    *****/
    private const int MaxUndoActions = 200;

    /*****
    Date: 2026-09-06
    Name: _undoStack
    Description: 撤销栈；每项对应一笔编辑操作（一次画笔笔画或一次设施放置/拆除）。
    *****/
    private readonly List<EditorAction> _undoStack = new();

    /*****
    Date: 2026-09-06
    Name: _currentAction
    Description: 正在记录的画笔笔画操作（鼠标按下开始、释放或单发操作完成时入栈）。
    *****/
    private EditorAction? _currentAction;

    /*****
    Date: 2026-09-25
    Name: Enabled
    Description: 输入启用开关；禁用时（如剧情动画期间）忽略 F12 与编辑鼠标操作。
    *****/
    public bool Enabled { get; set; } = true;

    /*****
    Date: 2026-09-25
    Name: DevMode
    Description: 是否处于开发者模式（读 GameRoot 会话状态；无会话时视为开发者模式以保证独立可用）。
    *****/
    private static bool DevMode => GameRoot.Instance?.DevModeEnabled ?? true;

    /*****
    Date: 2026-09-06
    Name: IsEditing
    Description: 是否处于编辑模式。
    *****/
    public bool IsEditing => _isEditing;

    /*****
    Date: 2026-09-06
    Name: CurrentTool
    Description: 当前编辑工具。
    *****/
    public Tool CurrentTool { get => _currentTool; set => _currentTool = value; }

    /*****
    Date: 2026-09-06
    Name: CurrentTerrain
    Description: 地形画笔当前选中的地形类型。
    *****/
    public CellKind CurrentTerrain { get => _currentTerrain; set => _currentTerrain = value; }

    /*****
    Date: 2026-09-06
    Name: CurrentZone
    Description: 功能区画笔当前选中的功能区。
    *****/
    public ZoneId CurrentZone { get => _currentZone; set => _currentZone = value; }

    /*****
    Date: 2026-09-06
    Name: CurrentFacilityDef
    Description: 设施放置当前选中的设施定义。
    *****/
    public FacilityDef? CurrentFacilityDef { get => _currentFacilityDef; set => _currentFacilityDef = value; }

    /*****
    Date: 2026-09-06
    Name: FacilityPlaced
    Description: 设施放置成功事件（参数：新创建的设施模拟对象）。
    *****/
    public event Action<FacilitySim>? FacilityPlaced;

    /*****
    Date: 2026-09-06
    Name: FacilityRemoved
    Description: 设施拆除事件（参数：被移除的设施模拟对象）。
    *****/
    public event Action<FacilitySim>? FacilityRemoved;

    /*****
    Date: 2026-09-25
    Name: FacilityBuildRequested
    Description: 建造请求事件（参数：已入场的「建造中」占位设施）；由 Main 创建并提交建造任务（仅正常模式发出，开发者模式为即时放置）。
    *****/
    public event Action<FacilitySim>? FacilityBuildRequested;

    /*****
    Date: 2026-09-25
    Name: FacilityDemolishRequested
    Description: 拆除请求事件（参数：待拆除的设施）；由 Main 创建并提交拆除任务（仅正常模式发出，开发者模式为即时移除）。
    *****/
    public event Action<FacilitySim>? FacilityDemolishRequested;

    /*****
    Date: 2026-09-26
    Name: PlacementRejected
    Description: 建造被拒事件（参数：设施定义、占地原点、受阻原因）；玩家在不可放置处按下左键时发出，由表现层在点击位置弹出提示（同一时刻全场仅一条）。编辑器只负责判定与上报，文案由表现层组织。
    *****/
    public event Action<FacilityDef, Vector2I, PlacementObstacle>? PlacementRejected;

    /*****
    Date: 2026-09-06
    Name: EditModeChanged
    Description: 编辑模式切换事件（参数：是否进入编辑）。
    *****/
    public event Action<bool>? EditModeChanged;

    /*****
    Date: 2026-09-26
    Name: SetPlacementPreview
    Description: 注入建造预览节点（由 Main 创建并挂在实体层，保证其坐标与设施视图同一坐标系）；未注入时建造预览功能整体降级为无操作。
    *****/
    public void SetPlacementPreview(FacilityPlacementPreview preview) => _placementPreview = preview;

    /*****
    Date: 2026-09-26
    Name: _Process
    Description: 每帧刷新建造预览（编辑状态 / 工具 / 选中设施 / 鼠标位置都可能变化，事件驱动不足以覆盖）。
    *****/
    public override void _Process(double delta) => UpdatePlacementPreview();

    /*****
    Date: 2026-09-26
    Name: UpdatePlacementPreview
    Description: 建造预览刷新：仅在「输入启用 && 编辑中 && 建造工具 && 已选中设施 && 鼠标在地图内」时显示落位幽灵（合法性由预览节点向 Simulation 询问，绿框可放 / 红框不可放），其余情形一律隐藏（退出编辑、切到拆除工具、取消选中、鼠标移出图外、剧情动画期间均会即时消失）。
    *****/
    private void UpdatePlacementPreview()
    {
        if (_placementPreview == null) return;

        if (!Enabled || !_isEditing || _currentTool != Tool.FacilityPlace || _currentFacilityDef == null || _mapView == null)
        {
            _placementPreview.Clear();
            return;
        }

        Vector2I cell = PointerCell();
        if (!CanEditCell(cell))
        {
            _placementPreview.Clear();
            return;
        }
        _placementPreview.ShowPlacement(_currentFacilityDef, cell);
    }

    /*****
    Date: 2026-09-26
    Name: PointerCell
    Description: 指针当前所在格子：取最后一次鼠标事件的视口坐标，经地图视图的「视口 → 地图本地」逆变换换算出地图本地坐标后再取格。未收到任何鼠标事件时返回 (-1,-1)（下游 CanEditCell 会据此判为不可编辑）。换算每帧实时进行，故相机平移/缩放后指针所指格子仍正确。
    *****/
    private Vector2I PointerCell()
    {
        if (_mapView == null || _pointerViewportPosition is not { } viewportPosition)
            return new Vector2I(-1, -1);
        Vector2 localPosition = _mapView.GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition;
        return _mapView.LocalToMap(localPosition);
    }

    /*****
    Date: 2026-09-06
    Name: Setup
    Description: 绑定模拟核心、地图视图与输入拾取器。
    *****/
    public void Setup(Simulation simulation, MapView mapView, InputPicker inputPicker)
    {
        _simulation = simulation;
        _mapView = mapView;
        _inputPicker = inputPicker;
    }

    /*****
    Date: 2026-09-06
    Name: EnterEdit
    Description: 进入编辑模式：暂停时钟（记录原暂停状态）、禁用点击拾取、发出事件。
    *****/
    public void EnterEdit()
    {
        if (_isEditing || _simulation == null) return;
        _isEditing = true;
        // 正常模式仅建造玩法：若当前工具为地形/功能区画笔则回落到设施放置
        if (!DevMode && _currentTool is Tool.TerrainBrush or Tool.ZoneBrush)
            _currentTool = Tool.FacilityPlace;
        _wasClockPaused = _simulation.Clock.IsPaused;
        _simulation.Clock.SetPaused(true);
        if (_inputPicker != null) _inputPicker.Enabled = false;
        EditModeChanged?.Invoke(true);
    }

    /*****
    Date: 2026-09-06
    Name: ExitEdit
    Description: 退出编辑模式：恢复时钟暂停状态、恢复点击拾取、发出事件。
    *****/
    public void ExitEdit()
    {
        if (!_isEditing || _simulation == null) return;
        _isEditing = false;
        _simulation.Clock.SetPaused(_wasClockPaused);
        if (_inputPicker != null) _inputPicker.Enabled = true;
        _painting = false;
        _pointerViewportPosition = null; // 丢弃旧指针位置：重进建造模式后需先移动鼠标才显示预览幽灵
        FinalizeStroke();
        EditModeChanged?.Invoke(false);
    }

    /*****
    Date: 2026-09-06
    Name: ToggleEdit
    Description: 切换编辑模式。
    *****/
    public void ToggleEdit()
    {
        if (_isEditing) ExitEdit();
        else EnterEdit();
    }

    /*****
    Date: 2026-09-25
    Name: Undo
    Description: 撤销最近一次编辑操作：恢复受影响格子的地形与功能区归属、逆置设施增删（撤销放置→移除设施、撤销拆除→重新加入设施），刷新渲染并通过事件同步设施视图。仅开发者模式下可用（正常模式的建造/拆除走任务流程、不提供撤销，UI 亦不显示）。
    *****/
    public void Undo()
    {
        if (!DevMode) return;
        if (!_isEditing || _simulation == null || _mapView == null) return;
        FinalizeStroke();
        if (_undoStack.Count == 0) return;

        EditorAction action = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);

        if (action.AddedFacility != null)
        {
            _simulation.RemoveFacility(action.AddedFacility);
            FacilityRemoved?.Invoke(action.AddedFacility);
        }
        if (action.RemovedFacility != null)
        {
            _simulation.AddFacility(action.RemovedFacility);
            FacilityPlaced?.Invoke(action.RemovedFacility);
        }

        foreach (KeyValuePair<Vector2I, (CellKind Kind, ZoneId? Zone)> entry in action.CellChanges)
        {
            Vector2I cell = entry.Key;
            _simulation.Map.SetCell(cell, entry.Value.Kind);
            if (entry.Value.Zone is { } z) _simulation.Map.SetZone(cell, z);
            else _simulation.Map.ClearZone(cell);
            _mapView.RefreshCell(cell);
        }
    }

    /*****
    Date: 2026-09-06
    Name: BeginAction
    Description: 获取当前正在记录的操作记录（画笔笔画复用同一记录；无进行中记录时新建）。
    *****/
    private EditorAction BeginAction() => _currentAction ??= new EditorAction();

    /*****
    Date: 2026-09-06
    Name: FinalizeStroke
    Description: 结束当前操作记录：有实际变更时压入撤销栈（超容量丢弃最旧），并清空进行中记录。
    *****/
    private void FinalizeStroke()
    {
        if (_currentAction == null) return;
        if (_currentAction.CellChanges.Count > 0
            || _currentAction.AddedFacility != null
            || _currentAction.RemovedFacility != null)
        {
            _undoStack.Add(_currentAction);
            while (_undoStack.Count > MaxUndoActions) _undoStack.RemoveAt(0);
        }
        _currentAction = null;
    }

    /*****
    Date: 2026-09-06
    Name: _UnhandledInput
    Description: 处理编辑输入：F12 切换编辑模式；编辑模式下 Ctrl+Z 撤销、左键涂抹/放置（拖拽连续涂抹）、右键擦除/拆除；仅消费鼠标左/右键，滚轮与中键放行给相机控制。
    *****/
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Enabled) return;

        if (@event is InputEventKey { Pressed: true, Keycode: ToggleKey })
        {
            ToggleEdit();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (DevMode && _isEditing && @event is InputEventKey { Pressed: true, CtrlPressed: true, Keycode: Key.Z })
        {
            Undo();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!_isEditing || _simulation == null || _mapView == null) return;

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left or MouseButton.Right } mb:
                _pointerViewportPosition = mb.Position;
                HandleMouseButton(mb);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseMotion mm:
                _pointerViewportPosition = mm.Position;
                if (_painting) ApplyToolAtMouse();
                break;
        }
    }

    /*****
    Date: 2026-09-06
    Name: HandleMouseButton
    Description: 处理鼠标按键：左键按下开始涂抹、释放停止；右键按下擦除。
    *****/
    private void HandleMouseButton(InputEventMouseButton mb)
    {
        if (mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                _painting = true;
                ApplyToolAtMouse();
            }
            else
            {
                _painting = false;
                FinalizeStroke();
            }
        }
        else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            ApplyEraseAtMouse();
            FinalizeStroke();
        }
    }

    /*****
    Date: 2026-09-06
    Name: ApplyToolAtMouse
    Description: 将当前工具应用到指针所在格子：地形画笔经 PaintTerrain 涂地形、功能区画笔经 PaintZone 涂功能区（均记录撤销）、设施放置/拆除按对应逻辑。
    *****/
    private void ApplyToolAtMouse()
    {
        if (_mapView == null || _simulation == null) return;
        // 正常模式禁用地形/功能区画笔（防御性；UI 已隐藏对应按钮）
        if (!DevMode && _currentTool is Tool.TerrainBrush or Tool.ZoneBrush) return;
        Vector2I cell = PointerCell();
        if (!CanEditCell(cell)) return;

        switch (_currentTool)
        {
            case Tool.TerrainBrush:
                PaintTerrain(cell, _currentTerrain);
                break;
            case Tool.ZoneBrush:
                PaintZone(cell, _currentZone);
                break;
            case Tool.FacilityPlace:
                PlaceFacilityAt(cell);
                break;
            case Tool.FacilityRemove:
                RemoveFacilityAt(cell);
                break;
        }
    }

    /*****
    Date: 2026-09-06
    Name: ApplyEraseAtMouse
    Description: 右键擦除：地形画笔经 PaintTerrain 涂真空、功能区画笔经 PaintZone 清除归属（均记录撤销）、设施工具拆除命中设施。
    *****/
    private void ApplyEraseAtMouse()
    {
        if (_mapView == null || _simulation == null) return;
        // 正常模式禁用地形/功能区画笔（防御性；UI 已隐藏对应按钮）
        if (!DevMode && _currentTool is Tool.TerrainBrush or Tool.ZoneBrush) return;
        Vector2I cell = PointerCell();
        if (!CanEditCell(cell)) return;

        switch (_currentTool)
        {
            case Tool.TerrainBrush:
                PaintTerrain(cell, CellKind.Vacuum);
                break;
            case Tool.ZoneBrush:
                PaintZone(cell, null);
                break;
            case Tool.FacilityPlace:
            case Tool.FacilityRemove:
                RemoveFacilityAt(cell);
                break;
        }
    }

    /*****
    Date: 2026-09-06
    Name: PaintTerrain
    Description: 涂抹单格地形并记录撤销（值未变化时不记录）；供左键涂抹与右键擦除共用。
    *****/
    private void PaintTerrain(Vector2I cell, CellKind kind)
    {
        if (_simulation == null || _mapView == null) return;
        if (_simulation.Map.GetCell(cell) == kind) return;
        BeginAction().CaptureCell(_simulation.Map, cell);
        _simulation.Map.SetCell(cell, kind);
        _mapView.RefreshCell(cell);
    }

    /*****
    Date: 2026-09-06
    Name: PaintZone
    Description: 涂抹单格功能区归属（null 表示擦除归属）并记录撤销；值未变化时不记录。
    *****/
    private void PaintZone(Vector2I cell, ZoneId? zone)
    {
        if (_simulation == null || _mapView == null) return;
        if (_simulation.Map.GetZone(cell) == zone) return;
        BeginAction().CaptureCell(_simulation.Map, cell);
        if (zone is { } z) _simulation.Map.SetZone(cell, z);
        else _simulation.Map.ClearZone(cell);
        _mapView.RefreshCell(cell);
    }

    /*****
    Date: 2026-09-25
    Name: PlaceFacilityAt
    Description: 在指定原点放置当前选中设施：先经 Simulation.CheckFacilityPlacement 整块校验（地板 + 未被设施占用 + 格上无物品；材料不足不阻止放置），受阻时记日志并发出 PlacementRejected 事件交由表现层在点击处提示原因；通过后记录撤销、加入模拟（占地阻挡由 Simulation 动态维护，不修改格子地形）并发出事件。开发者模式为即时放置（免费）；正常模式设施以「建造中（UnderConstruction）」占位入场（立刻占格阻挡并可被选中），并发出建造请求由 Main 创建建造任务——工人随时间推进、零件随进度实时扣除，建成后转 Operational。
    *****/
    private void PlaceFacilityAt(Vector2I origin)
    {
        if (_currentFacilityDef == null || _simulation == null) return;

        PlacementObstacle obstacle = _simulation.CheckFacilityPlacement(origin, _currentFacilityDef.Size);
        if (obstacle != PlacementObstacle.None)
        {
            GD.PushWarning($"[Editor] 无法在 ({origin.X}, {origin.Y}) 放置设施「{_currentFacilityDef.DisplayName}」：{obstacle}。");
            PlacementRejected?.Invoke(_currentFacilityDef, origin, obstacle);
            return;
        }

        var facility = new FacilitySim(_currentFacilityDef, origin);

        EditorAction action = BeginAction();
        // 正常模式：以建造中占位入场，实际建成由建造任务（工人+时间+实时扣件）完成
        if (!DevMode) facility.SetState(FacilityState.UnderConstruction);
        _simulation.AddFacility(facility);
        action.AddedFacility = facility;
        FinalizeStroke();
        FacilityPlaced?.Invoke(facility);
        if (!DevMode) FacilityBuildRequested?.Invoke(facility);
    }

    /*****
    Date: 2026-09-25
    Name: RemoveFacilityAt
    Description: 拆除占地包含指定格子的设施。开发者模式为即时移除（免费、可撤销）；正常模式改为发出拆除请求，由 Main 创建拆除任务（工人+时间；完成后连同 50% 建造零件返还一并移除），并对正在建造/拆除中的设施拒绝重复下达指令。
    *****/
    private void RemoveFacilityAt(Vector2I cell)
    {
        if (_simulation == null) return;

        FacilitySim? target = null;
        foreach (FacilitySim f in _simulation.Facilities)
        {
            if (f.Occupies(cell)) { target = f; break; }
        }
        if (target == null) return;

        if (DevMode)
        {
            EditorAction action = BeginAction();
            _simulation.RemoveFacility(target);
            action.RemovedFacility = target;
            FinalizeStroke();
            FacilityRemoved?.Invoke(target);
            return;
        }

        // 正常模式：发起拆除任务；建造中/拆除中的设施无法重复下达指令（任务板同目标去重为最终兜底）
        if (target.State is FacilityState.UnderConstruction or FacilityState.Demolishing)
        {
            GD.PushWarning($"[Editor] 「{target.Def?.DisplayName ?? "设施"}」正在建造/拆除中，无法再次下达拆除指令。");
            return;
        }
        FacilityDemolishRequested?.Invoke(target);
    }

    /*****
    Date: 2026-09-06
    Name: CanEditCell
    Description: 判断格子是否在地图边界内。
    *****/
    private bool CanEditCell(Vector2I cell)
    {
        if (_simulation == null) return false;
        return cell.X >= 0 && cell.X < _simulation.Map.Width
            && cell.Y >= 0 && cell.Y < _simulation.Map.Height;
    }

    /*****
    Date: 2026-09-06
    Name: EditorAction
    Description: 编辑操作撤销记录单元；保存操作影响的格子先前状态（地形 + 功能区归属）与设施增删信息，供 Undo 恢复。
    *****/
    private sealed class EditorAction
    {
        /*****
        Date: 2026-09-06
        Name: CellChanges
        Description: 受影响格子到其先前状态的映射（同一笔画内同格多次修改只保留最早状态）。
        *****/
        public Dictionary<Vector2I, (CellKind Kind, ZoneId? Zone)> CellChanges { get; } = new();

        /*****
        Date: 2026-09-06
        Name: AddedFacility
        Description: 本操作放置的设施；撤销时从模拟移除。
        *****/
        public FacilitySim? AddedFacility { get; set; }

        /*****
        Date: 2026-09-06
        Name: RemovedFacility
        Description: 本操作拆除的设施；撤销时重新加入模拟。
        *****/
        public FacilitySim? RemovedFacility { get; set; }

        /*****
        Date: 2026-09-06
        Name: CaptureCell
        Description: 记录指定格子的当前状态作为撤销依据；已记录过的格子跳过。
        *****/
        public void CaptureCell(IGridMap map, Vector2I cell)
        {
            if (CellChanges.ContainsKey(cell)) return;
            CellChanges[cell] = (map.GetCell(cell), map.GetZone(cell));
        }
    }
}
