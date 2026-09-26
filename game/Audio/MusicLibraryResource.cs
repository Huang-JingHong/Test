using Godot;

namespace RelayStation.Game.Audio;

/*****
Date: 2026-09-25
Name: MusicLibraryResource
Description: 音乐音库（Godot Resource，.tres 数据驱动）；持有曲目定义数组并提供按 Id 查询，音乐触发与动画动作只引用曲目 Id，实现音源与代码解耦。
*****/
[GlobalClass]
public partial class MusicLibraryResource : Resource
{
    /*****
    Date: 2026-09-25
    Name: Tracks
    Description: 曲目定义列表。
    *****/
    [Export] public MusicTrackDef[] Tracks { get; set; } = Array.Empty<MusicTrackDef>();

    /*****
    Date: 2026-09-25
    Name: Find
    Description: 按曲目 Id 查找定义；未找到时返回 null。
    *****/
    public MusicTrackDef? Find(string id)
    {
        foreach (MusicTrackDef track in Tracks)
        {
            if (track.Id == id) return track;
        }
        return null;
    }
}
