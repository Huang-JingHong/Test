namespace RelayStation.Game.Cutscenes;

/*****
Date: 2026-09-25
Name: CutsceneTrigger
Description: 剧情动画触发器抽象基类；每段动画必须挂在某个触发条件上（新游戏/游戏时钟/游戏事件/音乐播曲等）。Attach 由注册表调用，具体触发器在实现中挂接条件并在满足时经注册表播放剧本；音乐类触发器不携带剧本（Script 为 null）。
*****/
public abstract class CutsceneTrigger
{
    /*****
    Date: 2026-09-25
    Name: Script
    Description: 触发后要播放的动画剧本；音乐触发器为 null。
    *****/
    public CutsceneScript? Script { get; }

    /*****
    Date: 2026-09-25
    Name: CutsceneTrigger
    Description: 构造函数；传入触发的剧本（音乐触发器传 null）。
    *****/
    protected CutsceneTrigger(CutsceneScript? script)
    {
        Script = script;
    }

    /*****
    Date: 2026-09-25
    Name: Attach
    Description: 挂接触发条件到注册表；条件满足时调用 registry.Play(Script) 或 registry.PlayMusic(...)。
    *****/
    public abstract void Attach(CutsceneTriggerRegistry registry);
}
