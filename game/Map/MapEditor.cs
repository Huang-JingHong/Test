using Godot;
using RelayStation.Core.Common;
using RelayStation.Core.Facilities;
using RelayStation.Core.Map;
using RelayStation.Game.Input;

namespace RelayStation.Game.Map;

/*****
Date: 2026-09-06
Name: MapEditor
Description: 游戏内地图编辑器；编辑模式下暂停时钟、禁用点击拾取，支持地形画笔、功能区画笔、设施放置与拆除。运行时直接修改模拟层 GridMap 与设施列表，即时刷新渲染；保存由 GameRoot.SaveMap() 持久化为 .tres。提供 FacilityPlaced/FacilityRemoved 事件供 Main 同步增删 FacilityView。
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

    private bool _isEditing;
    private bool _wasClockPaused;
    private Tool _currentTool = Tool.TerrainBrush;
    private CellKind _currentTerrain = CellKind.Floor;
    private ZoneId _currentZone = ZoneId.Living;
    private FacilityDef? _currentFacilityDef;
    private bool _painting;

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
    Date: 2026-09-06
    Name: EditModeChanged
    Description: 编辑模式切换事件（参数：是否进入编辑）。
    *****/
    public event Action<bool>? EditModeChanged;

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
    Date: 2026-09-06
    Name: Undo
    Description: 撤销最近一次编辑操作：恢复受影响格子的地形与功能区归属、逆置设施增删（撤销放置→移除设施、撤销拆除→重新加入设施），刷新渲染并通过事件同步设施视图。仅编辑模式下可用。
    *****/
    public void Undo()
    {
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
            foreach (Vector2I cell in action.RemovedFacility.OccupiedCells())
            {
                _simulation.Map.SetCell(cell, CellKind.FacilitySlot);
            }
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
        if (@event is InputEventKey { Pressed: true, Keycode: ToggleKey })
        {
            ToggleEdit();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_isEditing && @event is InputEventKey { Pressed: true, CtrlPressed: true, Keycode: Key.Z })
        {
            Undo();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!_isEditing || _simulation == null || _mapView == null) return;

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left or MouseButton.Right } mb:
                HandleMouseButton(mb);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseMotion when _painting:
                ApplyToolAtMouse();
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
    Description: 将当前工具应用到鼠标所在格子：地形画笔经 PaintTerrain 涂地形、功能区画笔经 PaintZone 涂功能区（均记录撤销）、设施放置/拆除按对应逻辑。
    *****/
    private void ApplyToolAtMouse()
    {
        if (_mapView == null || _simulation == null) return;
        Vector2I cell = _mapView.LocalToMap(_mapView.GetGlobalMousePosition());
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
        Vector2I cell = _mapView.LocalToMap(_mapView.GetGlobalMousePosition());
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
    Date: 2026-09-06
    Name: PlaceFacilityAt
    Description: 在指定原点放置当前选中设施：检查占地格全部为地板（越界视为非地板），通过后记录撤销、标记 FacilitySlot、加入模拟、刷新渲染并发出事件。
    *****/
    private void PlaceFacilityAt(Vector2I origin)
    {
        if (_currentFacilityDef == null || _simulation == null || _mapView == null) return;

        var facility = new FacilitySim(_currentFacilityDef, origin);
        foreach (Vector2I cell in facility.OccupiedCells())
        {
            if (_simulation.Map.GetCell(cell) != CellKind.Floor)
            {
                GD.PushWarning($"[Editor] 无法在 ({origin.X}, {origin.Y}) 放置设施「{_currentFacilityDef.DisplayName}」：占地格非地板。");
                return;
            }
        }

        EditorAction action = BeginAction();
        foreach (Vector2I cell in facility.OccupiedCells())
        {
            action.CaptureCell(_simulation.Map, cell);
            _simulation.Map.SetCell(cell, CellKind.FacilitySlot);
            _mapView.RefreshCell(cell);
        }
        _simulation.AddFacility(facility);
        action.AddedFacility = facility;
        FinalizeStroke();
        FacilityPlaced?.Invoke(facility);
    }

    /*****
    Date: 2026-09-06
    Name: RemoveFacilityAt
    Description: 拆除占地包含指定格子的设施：记录撤销、从模拟移除（占地格恢复地板）、刷新渲染并发出事件。
    *****/
    private void RemoveFacilityAt(Vector2I cell)
    {
        if (_simulation == null || _mapView == null) return;

        FacilitySim? target = null;
        foreach (FacilitySim f in _simulation.Facilities)
        {
            if (f.Occupies(cell)) { target = f; break; }
        }
        if (target == null) return;

        EditorAction action = BeginAction();
        foreach (Vector2I c in target.OccupiedCells())
        {
            action.CaptureCell(_simulation.Map, c);
        }
        _simulation.RemoveFacility(target);
        foreach (Vector2I c in target.OccupiedCells())
        {
            _mapView.RefreshCell(c);
        }
        action.RemovedFacility = target;
        FinalizeStroke();
        FacilityRemoved?.Invoke(target);
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
