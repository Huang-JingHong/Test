namespace RelayStation.Game.Cutscenes;

/*****
Date: 2026-09-25
Name: PlayMusicAction
Description: 播放音乐动作；按曲目 Id 从音库查曲并开始播放（循环与否为音库配置项）。音源与代码解耦：换曲只改 resources/music_library.tres，不动动画代码。单曲播放为即时动作。
*****/
public sealed class PlayMusicAction : CutsceneAction
{
    /*****
    Date: 2026-09-25
    Name: _trackId
    Description: 音库曲目 Id（如 "intro_theme"）。
    *****/
    private readonly string _trackId;

    /*****
    Date: 2026-09-25
    Name: PlayMusicAction
    Description: 构造函数；指定曲目 Id。
    *****/
    public PlayMusicAction(string trackId)
    {
        _trackId = trackId;
    }

    /*****
    Date: 2026-09-25
    Name: Start
    Description: 经上下文从音库播放曲目。
    *****/
    public override void Start(CutsceneContext context) => context.PlayMusic(_trackId);

    /*****
    Date: 2026-09-25
    Name: Tick
    Description: 即时完成。
    *****/
    public override bool Tick(double delta) => true;
}
