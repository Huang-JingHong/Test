using Godot;
using RelayStation.Game.Map;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-26
Name: WorldHint
Description: 世界坐标短提示文本（跟随地图位置的单条 Label）；用于建造被拒时在点击处告知原因。两条约束：①**同一时刻全场只保留一条**——再次 ShowAt 直接覆盖文案并重置计时，玩家高频点击也只会刷新这一条而不会刷屏；②到期（3 秒）自行隐藏。文字挂 UI 层（而非世界层），字号不随相机缩放变化；每帧按相机变换把地图本地坐标投影成屏幕坐标，镜头平移/缩放时提示仍贴住原本的地图位置。
*****/
public partial class WorldHint : Label
{
    /*****
    Date: 2026-09-26
    Name: LifetimeSeconds
    Description: 单条提示的存在时长（秒）；到点自行消失。
    *****/
    public const double LifetimeSeconds = 3.0;

    /*****
    Date: 2026-09-26
    Name: HintWidth
    Description: 提示文本框宽度（固定宽度 + 居中排版 = 文本水平居中于点击位置，不依赖容器布局时机）。
    *****/
    private const float HintWidth = 460f;

    /*****
    Date: 2026-09-26
    Name: RiseOffsetPx
    Description: 提示相对点击位置的上移量（像素），避免文字压住光标所指的格子。
    *****/
    private const float RiseOffsetPx = 34f;

    /*****
    Date: 2026-09-26
    Name: _mapView
    Description: 地图视图（地图本地坐标 → 屏幕坐标的变换来源）。
    *****/
    private MapView? _mapView;

    /*****
    Date: 2026-09-26
    Name: _mapLocal
    Description: 提示锚定的地图本地坐标（相机移动时按此重新投影）。
    *****/
    private Vector2 _mapLocal;

    /*****
    Date: 2026-09-26
    Name: _remaining
    Description: 剩余存在时长（秒）。
    *****/
    private double _remaining;

    /*****
    Date: 2026-09-26
    Name: Setup
    Description: 绑定地图视图、设定固定宽度/字号/描边（保证压在地图贴图上仍可读）与鼠标穿透（不拦截点击），初始隐藏。
    *****/
    public void Setup(MapView mapView)
    {
        _mapView = mapView;
        CustomMinimumSize = new Vector2(HintWidth, 0f);
        HorizontalAlignment = HorizontalAlignment.Center;
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeFontSizeOverride("font_size", 16);
        AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
        AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f));
        AddThemeConstantOverride("outline_size", 6);
        Visible = false;
    }

    /*****
    Date: 2026-09-26
    Name: ShowAt
    Description: 在指定地图本地坐标处显示提示文本（覆盖既有文案并重置 3 秒计时）；立即完成一次定位，避免首帧停在上一处位置。
    *****/
    public void ShowAt(string text, Vector2 mapLocalPosition)
    {
        _mapLocal = mapLocalPosition;
        _remaining = LifetimeSeconds;
        Text = text;
        Visible = true;
        UpdatePosition();
    }

    /*****
    Date: 2026-09-26
    Name: _Process
    Description: 计时与跟随：到期隐藏；未到期时按当前相机变换刷新屏幕位置。
    *****/
    public override void _Process(double delta)
    {
        if (!Visible) return;
        _remaining -= delta;
        if (_remaining <= 0.0)
        {
            Visible = false;
            return;
        }
        UpdatePosition();
    }

    /*****
    Date: 2026-09-26
    Name: UpdatePosition
    Description: 把锚定的地图本地坐标经地图视图的「本地 → 视口」变换投影为屏幕坐标，并以点击位置为水平中心、上移 RiseOffsetPx 定位。
    *****/
    private void UpdatePosition()
    {
        if (_mapView == null) return;
        Vector2 screen = _mapView.GetGlobalTransformWithCanvas() * _mapLocal;
        Position = screen + new Vector2(-HintWidth / 2f, -RiseOffsetPx);
    }
}
