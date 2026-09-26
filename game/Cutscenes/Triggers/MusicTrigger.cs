namespace RelayStation.Game.Cutscenes;

/*****
Date: 2026-09-25
Name: MusicTrigger
Description: 独立音乐触发器（不播放剧本）；条件满足时按曲目 Id 直接播曲，音源经音库配置解耦（换曲只改 resources/music_library.tres）。支持两种构造：新游戏触发 / 游戏时钟分钟触发；游戏事件触发的播曲用 MusicEventTrigger<TEvent>。
*****/
public sealed class MusicTrigger : CutsceneTrigger
{
    /*****
    Date: 2026-09-25
    Name: _trackId
    Description: 音库曲目 Id。
    *****/
    private readonly string _trackId;

    /*****
    Date: 2026-09-25
    Name: _fireAtMinutes
    Description: 时钟触发的分钟阈值；null 表示新游戏触发。
    *****/
    private readonly double? _fireAtMinutes;

    /*****
    Date: 2026-09-25
    Name: MusicTrigger
    Description: 构造函数（新游戏触发）；指定曲目 Id。
    *****/
    public MusicTrigger(string trackId) : base(null)
    {
        _trackId = trackId;
    }

    /*****
    Date: 2026-09-25
    Name: MusicTrigger
    Description: 构造函数（时钟触发）；指定曲目 Id 与触发分钟阈值。
    *****/
    public MusicTrigger(string trackId, double fireAtGameMinutes) : base(null)
    {
        _trackId = trackId;
        _fireAtMinutes = fireAtGameMinutes;
    }

    /*****
    Date: 2026-09-25
    Name: Attach
    Description: 按构造条件挂接（新游戏或时钟分钟）并登记退订。
    *****/
    public override void Attach(CutsceneTriggerRegistry registry)
    {
        if (_fireAtMinutes is { } at)
            registry.AddDetacher(registry.OnClockMinute(at, () => registry.PlayMusic(_trackId)));
        else
            registry.AddDetacher(registry.OnNewGame(() => registry.PlayMusic(_trackId)));
    }
}
