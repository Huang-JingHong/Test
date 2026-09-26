using RelayStation.Core.Events;

namespace RelayStation.Game.Cutscenes;

/*****
Date: 2026-09-25
Name: GameEventTrigger
Description: 游戏事件触发器；事件总线上出现指定类型事件（如设施修复完成、任务状态变化）时触发所挂剧本。
*****/
public sealed class GameEventTrigger<TEvent> : CutsceneTrigger where TEvent : IGameEvent
{
    /*****
    Date: 2026-09-25
    Name: GameEventTrigger
    Description: 构造函数；指定剧本。
    *****/
    public GameEventTrigger(CutsceneScript script) : base(script)
    {
    }

    /*****
    Date: 2026-09-25
    Name: Attach
    Description: 订阅事件总线指定事件并登记退订。
    *****/
    public override void Attach(CutsceneTriggerRegistry registry)
        => registry.AddDetacher(registry.OnGameEvent<TEvent>(() => registry.Play(Script!)));
}
