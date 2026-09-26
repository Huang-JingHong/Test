namespace RelayStation.Game.Cutscenes;

/*****
Date: 2026-09-25
Name: ClockMinuteTrigger
Description: 游戏时钟触发器；累计游戏分钟达到指定值时一次性触发所挂剧本（如开局第 60 分钟的剧情）。
*****/
public sealed class ClockMinuteTrigger : CutsceneTrigger
{
    /*****
    Date: 2026-09-25
    Name: _fireAtMinutes
    Description: 触发的累计游戏分钟数阈值。
    *****/
    private readonly double _fireAtMinutes;

    /*****
    Date: 2026-09-25
    Name: ClockMinuteTrigger
    Description: 构造函数；指定剧本与触发分钟阈值。
    *****/
    public ClockMinuteTrigger(CutsceneScript script, double fireAtGameMinutes) : base(script)
    {
        _fireAtMinutes = fireAtGameMinutes;
    }

    /*****
    Date: 2026-09-25
    Name: Attach
    Description: 订阅时钟分钟条件并登记退订。
    *****/
    public override void Attach(CutsceneTriggerRegistry registry)
        => registry.AddDetacher(registry.OnClockMinute(_fireAtMinutes, () => registry.Play(Script!)));
}
