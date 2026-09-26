namespace RelayStation.Game.Cutscenes;

/*****
Date: 2026-09-25
Name: DelayAction
Description: 延时动作；等待指定秒数后完成（用于动作间留白）。
*****/
public sealed class DelayAction : CutsceneAction
{
    /*****
    Date: 2026-09-25
    Name: _seconds
    Description: 延时时长（秒）。
    *****/
    private readonly float _seconds;

    /*****
    Date: 2026-09-25
    Name: _elapsed
    Description: 已等待秒数。
    *****/
    private float _elapsed;

    /*****
    Date: 2026-09-25
    Name: DelayAction
    Description: 构造函数；指定延时时长。
    *****/
    public DelayAction(float seconds)
    {
        _seconds = seconds;
    }

    /*****
    Date: 2026-09-25
    Name: Start
    Description: 重置计时。
    *****/
    public override void Start(CutsceneContext context) => _elapsed = 0f;

    /*****
    Date: 2026-09-25
    Name: Tick
    Description: 累计时间，达到时长后返回完成。
    *****/
    public override bool Tick(double delta)
    {
        _elapsed += (float)delta;
        return _elapsed >= _seconds;
    }
}
