using Godot;
using RelayStation.Core.Common;
using RelayStation.Game.Map;

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
    Date: 2026-09-06
    Name: MinZoom
    Description: 缩放下限（越小视野越广）。
    *****/
    private const float MinZoom = 0.2f;

    /*****
    Date: 2026-09-06
    Name: MaxZoom
    Description: 缩放上限（越大越贴近细节）。
    *****/
    private const float MaxZoom = 4f;

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
    *****/
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_camera == null) return;

        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                ZoomAt(ZoomStep);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                ZoomAt(1f / ZoomStep);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Middle }:
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
        float newZoom = Mathf.Clamp(oldZoom * factor, MinZoom, MaxZoom);
        if (Mathf.IsEqualApprox(newZoom, oldZoom)) return;

        Vector2 anchor = _camera.GetGlobalMousePosition();
        Vector2 offset = anchor - _camera.Position;
        _camera.Zoom = new Vector2(newZoom, newZoom);
        _camera.Position = anchor - offset * (oldZoom / newZoom);
        ClampPosition();
    }

    /*****
    Date: 2026-09-06
    Name: ClampPosition
    Description: 将相机中心限制在地图世界区域内（0..宽 × 0..高），防止平移/缩放出界。
    *****/
    private void ClampPosition()
    {
        if (_camera == null || _simulation == null) return;
        float worldWidth = _simulation.Map.Width * MapView.TileSize;
        float worldHeight = _simulation.Map.Height * MapView.TileSize;
        _camera.Position = new Vector2(
            Mathf.Clamp(_camera.Position.X, 0f, worldWidth),
            Mathf.Clamp(_camera.Position.Y, 0f, worldHeight));
    }
}
