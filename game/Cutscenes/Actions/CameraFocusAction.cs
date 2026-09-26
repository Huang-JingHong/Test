using Godot;
using RelayStation.Game.Map;

namespace RelayStation.Game.Cutscenes;

/*****
Date: 2026-09-25
Name: CameraFocusAction
Description: 镜头聚焦动作；将相机平滑移动并缩放到目标角色（按 CharacterDef.Id 查找）所在格中心，使用平滑插值（ease-in-out）在指定时长内完成。目标不存在时立即完成。
*****/
public sealed class CameraFocusAction : CutsceneAction
{
    /*****
    Date: 2026-09-25
    Name: _targetId
    Description: 目标角色定义 Id。
    *****/
    private readonly string _targetId;

    /*****
    Date: 2026-09-25
    Name: _zoom
    Description: 聚焦后的相机缩放（越大越近）。
    *****/
    private readonly float _zoom;

    /*****
    Date: 2026-09-25
    Name: _duration
    Description: 聚焦时长（秒）。
    *****/
    private readonly float _duration;

    /*****
    Date: 2026-09-25
    Name: _camera
    Description: 受控相机。
    *****/
    private Camera2D? _camera;

    /*****
    Date: 2026-09-25
    Name: _skip
    Description: 目标不可解析时跳过本动作。
    *****/
    private bool _skip;

    /*****
    Date: 2026-09-25
    Name: _elapsed
    Description: 已进行秒数。
    *****/
    private float _elapsed;

    /*****
    Date: 2026-09-25
    Name: _startPos
    Description: 起始相机位置。
    *****/
    private Vector2 _startPos;

    /*****
    Date: 2026-09-25
    Name: _endPos
    Description: 目标相机位置（角色所在格中心）。
    *****/
    private Vector2 _endPos;

    /*****
    Date: 2026-09-25
    Name: _startZoom
    Description: 起始缩放。
    *****/
    private Vector2 _startZoom;

    /*****
    Date: 2026-09-25
    Name: _endZoom
    Description: 目标缩放。
    *****/
    private Vector2 _endZoom;

    /*****
    Date: 2026-09-25
    Name: CameraFocusAction
    Description: 构造函数；指定目标角色 Id、目标缩放与时长。
    *****/
    public CameraFocusAction(string targetCharacterId, float zoom = 2.5f, float duration = 1.5f)
    {
        _targetId = targetCharacterId;
        _zoom = zoom;
        _duration = duration;
    }

    /*****
    Date: 2026-09-25
    Name: Start
    Description: 解析目标角色并记录相机起点/终点；目标或相机缺失时置跳过。
    *****/
    public override void Start(CutsceneContext context)
    {
        _elapsed = 0f;
        _skip = false;
        _camera = context.Camera;

        var target = context.FindCharacter(_targetId);
        if (target == null || _camera == null)
        {
            _skip = true;
            return;
        }

        _startPos = _camera.Position;
        _startZoom = _camera.Zoom;
        _endPos = context.MapView.MapToLocal(target.Cell);
        _endZoom = new Vector2(_zoom, _zoom);
    }

    /*****
    Date: 2026-09-25
    Name: Tick
    Description: 按平滑插值推进相机位置与缩放；到达时长后完成。
    *****/
    public override bool Tick(double delta)
    {
        if (_skip || _camera == null) return true;

        _elapsed += (float)delta;
        float t = _duration <= 0f ? 1f : Mathf.Clamp(_elapsed / _duration, 0f, 1f);
        float eased = t * t * (3f - 2f * t); // smoothstep 缓入缓出
        _camera.Position = _startPos.Lerp(_endPos, eased);
        _camera.Zoom = _startZoom.Lerp(_endZoom, eased);
        return t >= 1f;
    }
}
