using Godot;
using RelayStation.Core.Common;
using RelayStation.Game.Map;
using RelayStation.Game.UI;

namespace RelayStation.Game.Input;

/*****
Date: 2026-09-06
Name: CameraController
Description: 相机控制；为绑定的 Camera2D 提供鼠标滚轮缩放（朝光标所在世界点锚定缩放）与鼠标中键拖拽平移，缩放限制在 [MinZoom, MaxZoom]，相机中心限制在地图世界区域内。
*****/
public partial class CameraController : Node
{
    /*****
    Date: 2026-09-06
    Name: ZoomStep
    Description: 单次滚轮缩放倍率（滚轮向上放大、向下缩小）。
    *****/
    private const float ZoomStep = 1.1f;

    /*****
    Date: 2026-09-25
    Name: MinZoomFloor
    Description: 缩放下限的兜底值；实际下限为「正好铺满地图」的缩放（见 ComputeMinZoom），此值仅在地图尺寸不可用时兜底。
    *****/
    private const float MinZoomFloor = 0.2f;

    /*****
    Date: 2026-09-26
    Name: MaxZoom
    Description: 缩放上限（越大越贴近细节）。2026-09-26 由 4 提到 6：原上限下看设施/角色仍偏小，提高后可更清楚地观察设施贴图、施工警戒线与角色动作；选 6 的理由——设施贴图为 128 px/格，放大 6 倍时约 1.5 倍上采样仍算清晰，而角色立绘 910 px 高在该倍率下仍是降采样故很锐利。注意：**地形贴图在图集中被硬性压到 32×32，放大到 6 倍会明显发软**（源图分辨率再高也无用），要改清晰需另改图集尺寸（MapView.TileSize），本次未动。
    *****/
    private const float MaxZoom = 6f;

    /*****
    Date: 2026-09-06
    Name: _camera
    Description: 受控相机。
    *****/
    private Camera2D? _camera;

    /*****
    Date: 2026-09-06
    Name: _simulation
    Description: 模拟核心（获取地图尺寸以限制平移范围）。
    *****/
    private Simulation? _simulation;

    /*****
    Date: 2026-09-25
    Name: Enabled
    Description: 输入启用开关；禁用时（如剧情动画期间）忽略一切相机输入。
    *****/
    public bool Enabled { get; set; } = true;

    /*****
    Date: 2026-09-06
    Name: _panning
    Description: 是否正在中键拖拽平移。
    *****/
    private bool _panning;

    /*****
    Date: 2026-09-06
    Name: Setup
    Description: 绑定受控相机与模拟核心。
    *****/
    public void Setup(Camera2D camera, Simulation simulation)
    {
        _camera = camera;
        _simulation = simulation;
    }

    /*****
    Date: 2026-09-06
    Name: _UnhandledInput
    Description: 处理相机输入：滚轮上/下缩放（朝光标世界点锚定）、中键按下/释放开始/结束平移、平移中按鼠标屏幕位移换算为世界位移反向移动相机。
    **窗口守卫**：鼠标落在任一已注册 UI 窗口（面板/对话框/信息栏等）之上时忽略滚轮与中键按下——事件即便经面板（MouseFilter=Stop）也可能落到本阶段（滚动容器到达边界时不消费滚轮），故在此统一拦截，保证滚轮只作用于所在窗口。
    *****/
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Enabled || _camera == null) return;

        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp } wheel
                when !UiWindowRegistry.IsOverWindow(wheel.Position):
                ZoomAt(ZoomStep);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown } wheel
                when !UiWindowRegistry.IsOverWindow(wheel.Position):
                ZoomAt(1f / ZoomStep);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Middle } middle
                when !UiWindowRegistry.IsOverWindow(middle.Position):
                _panning = true;
                break;
            case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Middle }:
                _panning = false;
                break;
            case InputEventMouseMotion motion when _panning:
                _camera.Position -= motion.Relative / _camera.Zoom;
                ClampPosition();
                break;
        }
    }

    /*****
    Date: 2026-09-06
    Name: ZoomAt
    Description: 以光标当前所指世界点为锚缩放相机（缩放后该点仍位于光标下方），并夹紧缩放倍率与相机位置。
    *****/
    private void ZoomAt(float factor)
    {
        if (_camera == null) return;
        float oldZoom = _camera.Zoom.X;
        float newZoom = Mathf.Clamp(oldZoom * factor, ComputeMinZoom(), MaxZoom);
        if (Mathf.IsEqualApprox(newZoom, oldZoom)) return;

        Vector2 anchor = _camera.GetGlobalMousePosition();
        Vector2 offset = anchor - _camera.Position;
        _camera.Zoom = new Vector2(newZoom, newZoom);
        _camera.Position = anchor - offset * (oldZoom / newZoom);
        ClampPosition();
    }

    /*****
    Date: 2026-09-25
    Name: ComputeMinZoom
    Description: 动态最小缩放 = 两轴「视口 ÷ 地图世界尺寸」之比取较大者（下限兜底 MinZoomFloor），使视野矩形在任何缩放级别下都不大于地图世界区域——即缩到最远也只能看到整张地图（背景图铺满范围），不会露出地图外。地图尺寸不可用时返回兜底值。
    *****/
    private float ComputeMinZoom()
    {
        if (_camera == null || _simulation == null) return MinZoomFloor;
        float worldWidth = _simulation.Map.Width * MapView.TileSize;
        float worldHeight = _simulation.Map.Height * MapView.TileSize;
        if (worldWidth <= 0f || worldHeight <= 0f) return MinZoomFloor;

        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        float needed = MathF.Max(viewport.X / worldWidth, viewport.Y / worldHeight);
        return MathF.Max(MinZoomFloor, needed);
    }

    /*****
    Date: 2026-09-25
    Name: ClampPosition
    Description: 将相机视野矩形（视口尺寸 ÷ 缩放，世界单位）完全限制在地图世界区域（= 背景图铺满范围）内，使视角不越过背景图边缘：先校正缩放下限（视口/窗口尺寸变化后仍保证视野不超出世界），再沿各轴夹紧到 [半视野, 世界尺寸-半视野]；世界小于视野时该轴居中。
    *****/
    private void ClampPosition()
    {
        if (_camera == null || _simulation == null) return;

        // 视口尺寸变化（如窗口缩放）后校正缩放下限，保证视野始终不大于地图世界区域
        float minZoom = ComputeMinZoom();
        if (_camera.Zoom.X < minZoom) _camera.Zoom = new Vector2(minZoom, minZoom);

        float worldWidth = _simulation.Map.Width * MapView.TileSize;
        float worldHeight = _simulation.Map.Height * MapView.TileSize;
        Vector2 halfView = GetViewport().GetVisibleRect().Size * 0.5f / _camera.Zoom;
        _camera.Position = new Vector2(
            ClampAxis(_camera.Position.X, halfView.X, worldWidth),
            ClampAxis(_camera.Position.Y, halfView.Y, worldHeight));
    }

    /*****
    Date: 2026-09-25
    Name: ClampAxis
    Description: 单轴夹紧：视野比世界大时返回世界中心（居中显示），否则把相机中心限制在 [半视野, 世界尺寸 - 半视野]。
    *****/
    private static float ClampAxis(float value, float halfView, float worldSize)
        => halfView * 2f >= worldSize
            ? worldSize * 0.5f
            : Mathf.Clamp(value, halfView, worldSize - halfView);
}
