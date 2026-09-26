using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Facilities;
using RelayStation.Core.Items;
using RelayStation.Game.Items;
using RelayStation.Game.Map;

namespace RelayStation.Game.Input;

/*****
Date: 2026-09-25
Name: InputPicker
Description: 点击/框选拾取。左键点击按「角色 → 设施 → 地面物品」优先级命中：角色取光标半格像素阈值内的全部候选（按「距离升序 + 登场顺序」稳定排序，刻意不依赖渲染层级以免提升图层后循环顺序抖动），若当前主选在候选内则循环切到下一个，否则取第一个——从而可依次选中叠放在一起的角色，并由表现层把选中者提升到上层使其可见；未命中角色时按格子命中设施（**严格等于设施实际占地格**，无邻接容差）；再未命中时按格子命中地面物品（同格多堆按「距点击点最近 + 创建顺序」排序并同样支持循环切换）。按住左键拖动超过屏幕像素阈值进入框选，松开时提交矩形内的全部角色与物品（支持多选）。**三类选中互斥，事件顺序契约为「先清空未命中的各方，再发命中的那一方」**——角色命中：ItemPicked(null) → FacilityPicked(null) → CharacterPicked(角色)；设施命中：ItemPicked(null) → CharacterPicked(null) → FacilityPicked(设施)；物品命中：CharacterPicked(null) → FacilityPicked(null) → ItemPicked(物品)；点击空白则三类皆发 null（全部清空）。调用方按「后到达者生效」处理即可。
*****/
public partial class InputPicker : Node
{
    /*****
    Date: 2026-09-25
    Name: DragThresholdPx
    Description: 判定「拖动框选」而非「点击」的屏幕像素阈值（超过才进入框选，避免手抖误触）。
    *****/
    private const float DragThresholdPx = 6f;

    /*****
    Date: 2026-09-26
    Name: HoverTolerancePx
    Description: 悬停命中框相对物品**图标矩形**的外扩量（世界像素）：命中框 = 图标 24 + 2×2 = 28 见方，**小于整格 32**（比整格命中更精确，符合「判定框稍微小一点」）。
    *****/
    private const float HoverTolerancePx = 2f;

    /*****
    Date: 2026-09-06
    Name: _simulation
    Description: 模拟核心引用。
    *****/
    private Simulation? _simulation;

    /*****
    Date: 2026-09-06
    Name: _mapView
    Description: 地图视图（坐标换算）。
    *****/
    private MapView? _mapView;

    /*****
    Date: 2026-09-25
    Name: _leftPressed
    Description: 左键是否处于按下（拖拽判定中）状态。
    *****/
    private bool _leftPressed;

    /*****
    Date: 2026-09-25
    Name: _boxSelecting
    Description: 是否已进入框选（拖动距离超过阈值）。
    *****/
    private bool _boxSelecting;

    /*****
    Date: 2026-09-25
    Name: _pressScreen
    Description: 左键按下的屏幕坐标（用于拖动距离阈值判定）。
    *****/
    private Vector2 _pressScreen;

    /*****
    Date: 2026-09-25
    Name: _pressWorld
    Description: 左键按下的世界坐标（框选矩形起点）。
    *****/
    private Vector2 _pressWorld;

    /*****
    Date: 2026-09-06
    Name: FacilityPicked
    Description: 点击选中设施事件；参数为命中的设施（点击空处或命中角色时为 null）。
    *****/
    public event Action<FacilitySim?>? FacilityPicked;

    /*****
    Date: 2026-09-25
    Name: CharacterPicked
    Description: 点击选中角色事件；参数为命中的角色（点击空处或命中设施时为 null）。叠放时循环切换。
    *****/
    public event Action<CharacterSim?>? CharacterPicked;

    /*****
    Date: 2026-09-26
    Name: ItemPicked
    Description: 点击选中地面物品事件；参数为命中的物品堆（点击空处或命中角色/设施时为 null）。同格多堆时循环切换。
    *****/
    public event Action<ItemStack?>? ItemPicked;

    /*****
    Date: 2026-09-25
    Name: BoxSelectChanged
    Description: 框选矩形变化事件（世界坐标）；参数宽高为 0 表示框选结束/取消，供表现层绘制或清除选择框。
    *****/
    public event Action<Rect2>? BoxSelectChanged;

    /*****
    Date: 2026-09-26
    Name: BoxSelectionCommitted
    Description: 框选提交事件（松开左键时触发）；参数为矩形内的全部角色与全部地面物品堆（各自按模拟层顺序，可能为空表示框内无对应对象）。
    *****/
    public event Action<IReadOnlyList<CharacterSim>, IReadOnlyList<ItemStack>>? BoxSelectionCommitted;

    /*****
    Date: 2026-09-26
    Name: RightClicked
    Description: 右键点击事件（世界坐标）；由装配方（Main）决定语义——鼠标悬停命中物品且有选中人物时弹右键菜单，否则维持「走到该位置」。编辑模式下不发出（本拾取器整体禁用，右键留给地图编辑器擦除）。
    *****/
    public event Action<Vector2>? RightClicked;

    /*****
    Date: 2026-09-26
    Name: ItemHovered
    Description: 物品悬停变化事件；参数为当前悬停的地面物品堆（离开/无选中人物时为 null）。**仅在有主选角色时才做悬停**（悬停用于「对物品下拾取/穿戴指令」，没选人时无意义），供表现层给该物品加蓝色描边。
    *****/
    public event Action<ItemStack?>? ItemHovered;

    /*****
    Date: 2026-09-26
    Name: HoveredItem
    Description: 当前悬停的地面物品堆（无则 null）；供装配方在右键时判定「是否针对物品弹菜单」。
    *****/
    public ItemStack? HoveredItem { get; private set; }

    /*****
    Date: 2026-09-26
    Name: PointerViewportPosition
    Description: 最近一次鼠标事件自带的视口坐标；供装配方把右键菜单弹在该处。刻意使用事件自带坐标而非全局鼠标位置，便于无头注入事件验证。
    *****/
    public Vector2 PointerViewportPosition { get; private set; }

    /*****
    Date: 2026-09-25
    Name: SelectedCharacter
    Description: 当前主选角色（由选择方在选中变化时写入）；作为叠放候选循环切换的起点。
    *****/
    public CharacterSim? SelectedCharacter { get; set; }

    /*****
    Date: 2026-09-26
    Name: SelectedItem
    Description: 当前主选物品堆（由选择方在选中变化时写入）；作为同格多堆循环切换的起点。
    *****/
    public ItemStack? SelectedItem { get; set; }

    /*****
    Date: 2026-09-06
    Name: Enabled
    Description: 输入拾取是否启用；编辑模式下设为 false 以禁用点击拾取，避免与画笔冲突。
    *****/
    public bool Enabled { get; set; } = true;

    /*****
    Date: 2026-09-06
    Name: Setup
    Description: 绑定模拟核心与地图视图。
    *****/
    public void Setup(Simulation simulation, MapView mapView)
    {
        _simulation = simulation;
        _mapView = mapView;
    }

    /*****
    Date: 2026-09-25
    Name: _UnhandledInput
    Description: 处理鼠标：左键按下开始拖拽判定、移动超阈值转框选（持续上报矩形）、松开时按「框选提交」或「点击拾取」二选一处理；右键按下发出 RightClicked（语义由 Main 决定：悬停物品时弹菜单，否则走到该处）；鼠标移动且未拖拽时更新物品悬停；禁用（编辑模式等）时取消进行中的拖拽并清空悬停。
    *****/
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Enabled)
        {
            if (_leftPressed) CancelDrag();
            SetHovered(null);
            return;
        }
        if (_simulation == null || _mapView == null) return;

        switch (@event)
        {
            case InputEventMouseMotion motion:
                PointerViewportPosition = motion.Position;
                if (_leftPressed) UpdateDrag(motion.Position);
                else UpdateHover(motion.Position);
                break;
            case InputEventMouseButton mb:
                PointerViewportPosition = mb.Position;
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    if (mb.Pressed) BeginDrag(mb.Position);
                    else EndDrag(mb.Position);
                }
                else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
                {
                    RightClicked?.Invoke(ToWorldPosition(mb.Position));
                }
                break;
        }
    }

    /*****
    Date: 2026-09-26
    Name: UpdateHover
    Description: 鼠标移动时的悬停更新：仅当存在主选角色（`SelectedCharacter`）时才做命中——悬停是「对物品下拾取/穿戴指令」的前置提示，没选人时不予显示；命中框见 FindItemAtPointer。
    *****/
    private void UpdateHover(Vector2 viewportPosition)
    {
        if (SelectedCharacter == null)
        {
            SetHovered(null);
            return;
        }
        SetHovered(FindItemAtPointer(ToWorldPosition(viewportPosition)));
    }

    /*****
    Date: 2026-09-26
    Name: SetHovered
    Description: 写入当前悬停对象，仅在发生变化时发 ItemHovered（供表现层同步蓝色描边）。
    *****/
    private void SetHovered(ItemStack? stack)
    {
        if (ReferenceEquals(HoveredItem, stack)) return;
        HoveredItem = stack;
        ItemHovered?.Invoke(stack);
    }

    /*****
    Date: 2026-09-26
    Name: ClearHover
    Description: 清空当前悬停并发出 ItemHovered(null)；供装配方在选择集变化或物品堆增删后调用，避免蓝框停留在已失效的对象上。
    *****/
    public void ClearHover() => SetHovered(null);

    /*****
    Date: 2026-09-26
    Name: FindItemAtPointer
    Description: 按「图标矩形（`ItemStackView.IconRect`）+ 外扩 `HoverTolerancePx`」的命中框找光标下的物品堆（多堆命中时取框中心离光标最近者，并列取创建顺序在前者）；未命中返回 null。命中框刻意小于整格（32），避免光标落在物品旁边的格子也触发拾取提示；与像素偏移严格对齐由 IconRect 保证。
    *****/
    private ItemStack? FindItemAtPointer(Vector2 worldPosition)
    {
        ItemStack? best = null;
        float bestDistance = float.MaxValue;
        foreach (ItemStack stack in _simulation!.Items.Stacks)
        {
            Rect2 box = ItemStackView.IconRect(stack, _mapView!).Grow(HoverTolerancePx);
            if (!box.HasPoint(worldPosition)) continue;

            float distance = box.GetCenter().DistanceTo(worldPosition);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = stack;
        }
        return best;
    }

    /*****
    Date: 2026-09-25
    Name: BeginDrag
    Description: 记录左键按下位置，开始拖拽判定。
    *****/
    private void BeginDrag(Vector2 screenPosition)
    {
        _leftPressed = true;
        _boxSelecting = false;
        _pressScreen = screenPosition;
        _pressWorld = ToWorldPosition(screenPosition);
    }

    /*****
    Date: 2026-09-25
    Name: UpdateDrag
    Description: 拖动中：距离超过阈值后进入框选并持续上报世界坐标矩形。
    *****/
    private void UpdateDrag(Vector2 screenPosition)
    {
        if (!_boxSelecting && screenPosition.DistanceTo(_pressScreen) > DragThresholdPx)
        {
            _boxSelecting = true;
        }
        if (_boxSelecting)
        {
            BoxSelectChanged?.Invoke(MakeWorldRect(_pressWorld, ToWorldPosition(screenPosition)));
        }
    }

    /*****
    Date: 2026-09-25
    Name: EndDrag
    Description: 松开左键：框选中则清除选择框并提交矩形内角色；否则按普通点击处理。
    *****/
    private void EndDrag(Vector2 screenPosition)
    {
        if (!_leftPressed) return;
        _leftPressed = false;

        if (_boxSelecting)
        {
            _boxSelecting = false;
            BoxSelectChanged?.Invoke(new Rect2()); // 清除选择框
            Rect2 rect = MakeWorldRect(_pressWorld, ToWorldPosition(screenPosition));
            BoxSelectionCommitted?.Invoke(FindCharactersIn(rect), FindItemsIn(rect));
            return;
        }

        HandleClick(ToWorldPosition(screenPosition));
    }

    /*****
    Date: 2026-09-25
    Name: CancelDrag
    Description: 取消进行中的拖拽（如中途被禁用）；若正在框选则一并清除选择框。
    *****/
    private void CancelDrag()
    {
        _leftPressed = false;
        if (_boxSelecting)
        {
            _boxSelecting = false;
            BoxSelectChanged?.Invoke(new Rect2());
        }
    }

    /*****
    Date: 2026-09-26
    Name: HandleClick
    Description: 普通点击：按「角色 → 设施 → 物品」优先级命中，并按事件顺序契约（先清空未命中的各方、再发命中的一方）通知调用方；角色与物品各自支持叠放/同格循环选中。
    *****/
    private void HandleClick(Vector2 worldPosition)
    {
        Vector2I cell = _mapView!.LocalToMap(worldPosition);
        IReadOnlyList<CharacterSim> characters = FindCharactersAt(worldPosition);
        if (characters.Count > 0)
        {
            ItemPicked?.Invoke(null);
            FacilityPicked?.Invoke(null);
            CharacterPicked?.Invoke(NextCharacterPick(characters));
            return;
        }

        FacilitySim? facility = FindFacilityAt(cell);
        if (facility != null)
        {
            ItemPicked?.Invoke(null);
            CharacterPicked?.Invoke(null);
            FacilityPicked?.Invoke(facility);
            return;
        }

        IReadOnlyList<ItemStack> items = FindItemsAt(worldPosition, cell);
        CharacterPicked?.Invoke(null);
        FacilityPicked?.Invoke(null);
        ItemPicked?.Invoke(items.Count > 0 ? NextItemPick(items) : null);
    }

    /*****
    Date: 2026-09-25
    Name: NextCharacterPick
    Description: 叠放角色候选的循环选取：候选仅一人时直接返回；否则若当前主选在候选内则取下一个（末尾绕回首个），不在候选内则取首个。
    *****/
    private CharacterSim NextCharacterPick(IReadOnlyList<CharacterSim> candidates)
    {
        if (candidates.Count == 1) return candidates[0];

        int currentIndex = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (ReferenceEquals(candidates[i], SelectedCharacter))
            {
                currentIndex = i;
                break;
            }
        }
        return candidates[(currentIndex + 1) % candidates.Count];
    }

    /*****
    Date: 2026-09-26
    Name: NextItemPick
    Description: 同格多堆物品的循环选取：仅一堆时直接返回；否则若当前主选在候选内则取下一个（末尾绕回首个），不在候选内则取首个。
    *****/
    private ItemStack NextItemPick(IReadOnlyList<ItemStack> candidates)
    {
        if (candidates.Count == 1) return candidates[0];

        int currentIndex = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (ReferenceEquals(candidates[i], SelectedItem))
            {
                currentIndex = i;
                break;
            }
        }
        return candidates[(currentIndex + 1) % candidates.Count];
    }

    /*****
    Date: 2026-09-25
    Name: FindCharactersAt
    Description: 查找光标半格像素阈值内的全部角色（按「距离升序 + 登场顺序」稳定排序）；无命中返回空列表。排序不依赖渲染层级，保证叠放时循环切换顺序稳定。
    *****/
    private IReadOnlyList<CharacterSim> FindCharactersAt(Vector2 worldPosition)
    {
        float radius = MapView.TileSize * 0.5f;
        var hits = new List<(CharacterSim Character, float Distance, int Order)>();

        int order = 0;
        foreach (CharacterSim character in _simulation!.Characters)
        {
            float distance = _mapView!.MapToLocal(character.Cell).DistanceTo(worldPosition);
            if (distance <= radius) hits.Add((character, distance, order));
            order++;
        }

        return hits
            .OrderBy(h => h.Distance)
            .ThenBy(h => h.Order)
            .Select(h => h.Character)
            .ToList();
    }

    /*****
    Date: 2026-09-25
    Name: FindCharactersIn
    Description: 查找世界坐标矩形内的全部角色（按登场顺序，即模拟层角色列表顺序）；供框选提交使用。
    *****/
    private IReadOnlyList<CharacterSim> FindCharactersIn(Rect2 worldRect)
    {
        var hits = new List<CharacterSim>();
        foreach (CharacterSim character in _simulation!.Characters)
        {
            if (worldRect.HasPoint(_mapView!.MapToLocal(character.Cell))) hits.Add(character);
        }
        return hits;
    }

    /*****
    Date: 2026-09-26
    Name: FindItemsAt
    Description: 查找指定格上的全部地面物品堆（按「距点击点最近 + 创建顺序」稳定排序）；不在该格的堆不参与。物品画在本格内，故用整格命中比像素半径更宽容；同格多堆时由调用方循环选中。
    *****/
    private IReadOnlyList<ItemStack> FindItemsAt(Vector2 worldPosition, Vector2I cell)
    {
        var hits = new List<(ItemStack Stack, float Distance, int Order)>();
        int order = 0;
        foreach (ItemStack stack in _simulation!.Items.Stacks)
        {
            int stackOrder = order++;
            if (stack.Cell != cell) continue;
            // 图标中心 = 格中心 + 格内像素偏移（与 ItemStackView 的绘制口径一致）
            Vector2 center = _mapView!.MapToLocal(stack.Cell) + stack.PixelOffset;
            hits.Add((stack, center.DistanceTo(worldPosition), stackOrder));
        }

        return hits
            .OrderBy(h => h.Distance)
            .ThenBy(h => h.Order)
            .Select(h => h.Stack)
            .ToList();
    }

    /*****
    Date: 2026-09-26
    Name: FindItemsIn
    Description: 查找世界坐标矩形内的全部地面物品堆（按模拟层顺序）；供框选提交使用。
    *****/
    private IReadOnlyList<ItemStack> FindItemsIn(Rect2 worldRect)
    {
        var hits = new List<ItemStack>();
        foreach (ItemStack stack in _simulation!.Items.Stacks)
        {
            if (worldRect.HasPoint(_mapView!.MapToLocal(stack.Cell))) hits.Add(stack);
        }
        return hits;
    }

    /*****
    Date: 2026-09-26
    Name: FindFacilityAt
    Description: 查找占格命中的设施：**严格按设施实际占地格精确匹配**——点击设施占地之外的相邻格一律不命中（曾有四邻容差，导致点制氧机旁边的格子也会选中它，判定框比设施大一圈，已移除）；无命中返回 null。
    *****/
    private FacilitySim? FindFacilityAt(Vector2I cell)
    {
        foreach (FacilitySim facility in _simulation!.Facilities)
        {
            if (facility.Occupies(cell)) return facility;
        }
        return null;
    }

    /*****
    Date: 2026-09-25
    Name: ToWorldPosition
    Description: 视口坐标 → 世界坐标（经地图视图画布变换的逆变换）；用输入事件自带坐标而非全局鼠标位置，便于无头注入事件与保持一致。
    *****/
    private Vector2 ToWorldPosition(Vector2 viewportPosition)
        => _mapView == null ? Vector2.Zero : _mapView.GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition;

    /*****
    Date: 2026-09-25
    Name: MakeWorldRect
    Description: 由两个世界坐标点构造矩形（自动取较小坐标为原点、取绝对差为尺寸）。
    *****/
    private static Rect2 MakeWorldRect(Vector2 a, Vector2 b)
    {
        var origin = new Vector2(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y));
        var size = new Vector2(MathF.Abs(b.X - a.X), MathF.Abs(b.Y - a.Y));
        return new Rect2(origin, size);
    }
}
