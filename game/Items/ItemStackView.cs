using Godot;
using RelayStation.Core.Items;
using RelayStation.Core.Tasks;
using RelayStation.Game.Autoloads;
using RelayStation.Game.Map;

namespace RelayStation.Game.Items;

/*****
Date: 2026-09-26
Name: ItemStackView
Description: 地面物品堆的表现层；以物品贴图在所属格内呈现（**一图一组**——同一物品的多组各有独立图标，如 110 个零件就是 100 与 10 两张图），并在图标下缘用小字标出本组数量（字号刻意压小，只给数量不给名称，避免遮挡地图）。层级固定 0：与设施同带但二者永不同格，且低于人物（1），故角色走过去会自然盖在物品之上。物品无损坏态，故不订阅状态事件；数量/格/偏移变化时由 Main 调 Refresh 同步。可像人物一样被点击/框选选中——选中时在图标外加**黄色**描边；**悬停**（选中了人物且鼠标落在命中框内）时在图标外画**蓝色**描边（比黄框更贴紧，即「偏小」）。**图标矩形口径由静态方法 IconRect 统一提供**（与像素偏移对齐），绘制、悬停命中与右键菜单共用同一口径，杜绝漂移。被逐件转移时在图标下缘显示细进度条。
*****/
public partial class ItemStackView : Node2D
{
    /*****
    Date: 2026-09-26
    Name: ZOrder
    Description: 物品层级（与设施同为 0 且二者互斥不同格；低于人物 1）。
    *****/
    public const int ZOrder = 0;

    /*****
    Date: 2026-09-26
    Name: DrawSize
    Description: 图标绘制边长（世界像素）；比一格（32）小一圈，符合「散落在地上」的观感。命中判定与右键菜单共用此口径。
    *****/
    public const float DrawSize = 24f;

    /*****
    Date: 2026-09-26
    Name: HoverOutlineGrow
    Description: 悬停蓝框相对图标矩形的外扩量（世界像素）；刻意小于选中黄框的 2px，使蓝框更贴紧图标（「轮廓偏小」）。
    *****/
    public const float HoverOutlineGrow = 1f;

    /*****
    Date: 2026-09-26
    Name: CountFontSize
    Description: 数量文字字号；压小以免遮挡地图。
    *****/
    private const int CountFontSize = 10;

    /*****
    Date: 2026-09-26
    Name: ProgressBarHeight
    Description: 搬运进度细条高度（世界像素）。
    *****/
    private const float ProgressBarHeight = 3f;

    /*****
    Date: 2026-09-26
    Name: SelectedOutlineColor
    Description: 选中描边颜色（与设施选中框同色系，保持「黄色=已选中」的统一语汇）。
    *****/
    private static readonly Color SelectedOutlineColor = new(1f, 1f, 0.4f);

    /*****
    Date: 2026-09-26
    Name: HoverOutlineColor
    Description: 悬停描边颜色（蓝色，表示「可对这里下拾取/穿戴指令」；与选中黄框并存时蓝在内、黄在外）。
    *****/
    private static readonly Color HoverOutlineColor = new(0.35f, 0.75f, 1f);

    /*****
    Date: 2026-09-26
    Name: ProgressColor
    Description: 搬运进度条颜色（与建造/维修进度条同一语汇）。
    *****/
    private static readonly Color ProgressColor = new(0.45f, 0.85f, 0.45f);

    /*****
    Date: 2026-09-26
    Name: _stack
    Description: 关联的物品堆（数据源）。
    *****/
    private ItemStack? _stack;

    /*****
    Date: 2026-09-26
    Name: _mapView
    Description: 地图视图（格子坐标换算）。
    *****/
    private MapView? _mapView;

    /*****
    Date: 2026-09-26
    Name: _texture
    Description: 物品贴图（未配置时为 null，绘制回退占位方块）。
    *****/
    private Texture2D? _texture;

    /*****
    Date: 2026-09-26
    Name: _countLabel
    Description: 数量标签（图标下缘的小字）。
    *****/
    private Label? _countLabel;

    /*****
    Date: 2026-09-26
    Name: _selected
    Description: 是否被选中。
    *****/
    private bool _selected;

    /*****
    Date: 2026-09-26
    Name: _hovered
    Description: 是否被悬停（由 Main 依 InputPicker 的悬停事件同步）。
    *****/
    private bool _hovered;

    /*****
    Date: 2026-09-26
    Name: _showProgress
    Description: 是否显示搬运进度条（本堆正被逐件转移）。
    *****/
    private bool _showProgress;

    /*****
    Date: 2026-09-26
    Name: _progress
    Description: 搬运进度比例（0~1，取自转移任务的 ProgressFraction）。
    *****/
    private double _progress;

    /*****
    Date: 2026-09-26
    Name: Stack
    Description: 关联的物品堆。
    *****/
    public ItemStack Stack => _stack!;

    /*****
    Date: 2026-09-26
    Name: Selected
    Description: 是否被选中（选中时在图标外加黄色描边）；变化时触发重绘。
    *****/
    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            QueueRedraw();
        }
    }

    /*****
    Date: 2026-09-26
    Name: Hovered
    Description: 是否被悬停（选中了人物且鼠标落在命中框内时置位）；变化时触发重绘以显示/隐藏蓝色描边。
    *****/
    public bool Hovered
    {
        get => _hovered;
        set
        {
            if (_hovered == value) return;
            _hovered = value;
            QueueRedraw();
        }
    }

    /*****
    Date: 2026-09-26
    Name: IconRect
    Description: **图标世界像素矩形**（唯一口径）：中心 = 格中心 + 格内像素偏移，边长 `DrawSize`。绘制、悬停命中（InputPicker）与右键菜单命中共用，保证与像素偏移严格对齐。
    *****/
    public static Rect2 IconRect(ItemStack stack, MapView mapView)
    {
        float margin = (MapView.TileSize - DrawSize) / 2f;
        Vector2 topLeft = mapView.MapToLocal(stack.Cell)
                          - new Vector2(MapView.TileSize, MapView.TileSize) / 2f
                          + stack.PixelOffset
                          + new Vector2(margin, margin);
        return new Rect2(topLeft, new Vector2(DrawSize, DrawSize));
    }

    /*****
    Date: 2026-09-26
    Name: Setup
    Description: 绑定物品堆与地图视图：加载贴图、按「格左上角 + 格内像素偏移」定位并创建数量标签。
    *****/
    public void Setup(ItemStack stack, MapView mapView)
    {
        _stack = stack;
        _mapView = mapView;
        ZIndex = ZOrder;

        if (!string.IsNullOrEmpty(stack.Def.IconTexturePath))
            _texture = GD.Load<Texture2D>(stack.Def.IconTexturePath);

        _countLabel = new Label
        {
            Name = "Count",
            CustomMinimumSize = new Vector2(MapView.TileSize, 0f),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _countLabel.AddThemeFontSizeOverride("font_size", CountFontSize);
        _countLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
        _countLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f));
        _countLabel.AddThemeConstantOverride("outline_size", 4);
        _countLabel.Position = new Vector2(0f, MapView.TileSize - 12f);
        AddChild(_countLabel);

        Refresh();
    }

    /*****
    Date: 2026-09-26
    Name: Refresh
    Description: 同步数据变化：刷新数量文字，并按当前格与像素偏移重定位（物品被搬运/数量变化后由 Main 调用）。
    *****/
    public void Refresh()
    {
        if (_stack == null || _mapView == null) return;
        if (_countLabel != null) _countLabel.Text = _stack.Count.ToString();

        var tileSize = new Vector2(MapView.TileSize, MapView.TileSize);
        Position = _mapView.MapToLocal(_stack.Cell) - tileSize / 2f + _stack.PixelOffset;
        QueueRedraw();
    }

    /*****
    Date: 2026-09-26
    Name: _Process
    Description: 每帧同步搬运进度（仅本堆正被逐件转移时需要）：在未结任务里找 `ItemTarget` 为本堆的转移任务，取 `ProgressFraction`；变化时才请求重绘（无转移任务时不重绘，与 FacilityView 的进度同步同一模式）。
    *****/
    public override void _Process(double delta)
    {
        if (_stack == null) return;

        bool show = false;
        double progress = 0.0;
        foreach (ITask task in GameRoot.Instance?.Simulation?.TaskBoard.Active ?? Array.Empty<ITask>())
        {
            if (!ReferenceEquals(task.ItemTarget, _stack)) continue;
            if (task.State is not (TaskState.Assigned or TaskState.InProgress or TaskState.Suspended)) continue;
            show = true;
            progress = task.ProgressFraction;
            break;
        }

        if (show == _showProgress && Math.Abs(progress - _progress) < 0.0001) return;
        _showProgress = show;
        _progress = progress;
        QueueRedraw();
    }

    /*****
    Date: 2026-09-26
    Name: _Draw
    Description: 在格内居中绘制物品贴图（未配置贴图时回退半透明占位方块）；被选中时在图标外加黄色描边（外扩 2px），被悬停时加蓝色描边（外扩 1px，蓝在内、黄在外）；被逐件转移时在图标下缘画细进度条。
    *****/
    public override void _Draw()
    {
        float margin = (MapView.TileSize - DrawSize) / 2f;
        var rect = new Rect2(new Vector2(margin, margin), new Vector2(DrawSize, DrawSize));
        if (_texture != null)
            DrawTextureRect(_texture, rect, false);
        else
            DrawRect(rect, new Color(0.80f, 0.82f, 0.88f, 0.9f));

        if (_hovered)
            DrawRect(rect.Grow(HoverOutlineGrow), HoverOutlineColor, false, 1.5f);
        if (_selected)
            DrawRect(rect.Grow(2f), SelectedOutlineColor, false, 2f);

        if (_showProgress)
        {
            float barWidth = DrawSize;
            float x = margin;
            float y = margin + DrawSize + 2f;
            DrawRect(new Rect2(new Vector2(x, y), new Vector2(barWidth, ProgressBarHeight)), new Color(0f, 0f, 0f, 0.55f));
            DrawRect(new Rect2(new Vector2(x, y), new Vector2(barWidth * (float)_progress, ProgressBarHeight)), ProgressColor);
        }
    }
}