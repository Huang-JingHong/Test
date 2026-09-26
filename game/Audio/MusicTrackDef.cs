using Godot;

namespace RelayStation.Game.Audio;

/*****
Date: 2026-09-25
Name: MusicTrackDef
Description: 单曲目定义（Godot Resource，.tres 数据驱动）；以逻辑 Id 关联音频文件路径与循环开关，换音源只改 .tres 不动代码。
*****/
[GlobalClass]
public partial class MusicTrackDef : Resource
{
    /*****
    Date: 2026-09-25
    Name: Id
    Description: 曲目逻辑标识（动画/触发器以此引用，如 "intro_theme"）。
    *****/
    [Export] public string Id { get; set; } = "";

    /*****
    Date: 2026-09-25
    Name: StreamPath
    Description: 音频文件资源路径（res:// 协议）。
    *****/
    [Export] public string StreamPath { get; set; } = "";

    /*****
    Date: 2026-09-25
    Name: Loop
    Description: 是否循环播放。
    *****/
    [Export] public bool Loop { get; set; } = true;
}
