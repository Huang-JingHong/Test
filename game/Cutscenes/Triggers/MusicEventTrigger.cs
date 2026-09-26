using RelayStation.Core.Events;

namespace RelayStation.Game.Cutscenes;

/*****
Date: 2026-09-25
Name: MusicEventTrigger
Description: 游戏事件音乐触发器；事件总线上出现指定类型事件时按曲目 Id 直接播曲（不播放剧本），音源经音库配置解耦。
*****/
public sealed class MusicEventTrigger<TEvent> : CutsceneTrigger where TEvent : IGameEvent
{
    /*****
    Date: 2026-09-25
    Name: _trackId
    Description: 音库曲目 Id。
    *****/
    private readonly string _trackId;

    /*****
    Date: 2026-09-25
    Name: MusicEventTrigger
    Description: 构造函数；指定曲目 Id。
    *****/
    public MusicEventTrigger(string trackId) : base(null)
    {
        _trackId = trackId;
    }

    /*****
    Date: 2026-09-25
    Name: Attach
    Description: 订阅事件总线指定事件并登记退订。
    *****/
    public override void Attach(CutsceneTriggerRegistry registry)
        => registry.AddDetacher(registry.OnGameEvent<TEvent>(() => registry.PlayMusic(_trackId)));
}
