namespace RelayStation.Game.Cutscenes;

/*****
Date: 2026-09-25
Name: NewGameTrigger
Description: 新游戏触发器；每次「新的游戏」开局（Main 场景首帧就绪后）触发一次所挂剧本。
*****/
public sealed class NewGameTrigger : CutsceneTrigger
{
    /*****
    Date: 2026-09-25
    Name: NewGameTrigger
    Description: 构造函数；指定触发的剧本。
    *****/
    public NewGameTrigger(CutsceneScript script) : base(script)
    {
    }

    /*****
    Date: 2026-09-25
    Name: Attach
    Description: 订阅注册表的新游戏事件并登记退订。
    *****/
    public override void Attach(CutsceneTriggerRegistry registry)
        => registry.AddDetacher(registry.OnNewGame(() => registry.Play(Script!)));
}
