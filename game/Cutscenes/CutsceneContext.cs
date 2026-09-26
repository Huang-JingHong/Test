using Godot;
using RelayStation.Core.Characters;
using RelayStation.Game.Audio;
using RelayStation.Game.Map;
using RelayStation.Game.UI;

namespace RelayStation.Game.Cutscenes;

/*****
Date: 2026-09-25
Name: CutsceneContext
Description: 剧情动画上下文；聚合导演器与各动作共用的表现层引用（相机/地图视图/对话框/音乐播放器/音库）与按 Id 查找角色的委托，动作经此访问外部能力，保持动作类自身无全局依赖。
*****/
public sealed class CutsceneContext
{
    /*****
    Date: 2026-09-25
    Name: Camera
    Description: 主相机。
    *****/
    public Camera2D Camera { get; }

    /*****
    Date: 2026-09-25
    Name: MapView
    Description: 格子地图视图（格子与世界坐标换算）。
    *****/
    public MapView MapView { get; }

    /*****
    Date: 2026-09-25
    Name: Dialogue
    Description: 底部对话框。
    *****/
    public DialogueBox Dialogue { get; }

    /*****
    Date: 2026-09-25
    Name: MusicPlayer
    Description: 背景音乐播放器。
    *****/
    public AudioStreamPlayer MusicPlayer { get; }

    /*****
    Date: 2026-09-25
    Name: MusicLibrary
    Description: 音乐音库（曲目 Id → 音频路径配置）。
    *****/
    public MusicLibraryResource MusicLibrary { get; }

    /*****
    Date: 2026-09-25
    Name: FindCharacter
    Description: 按角色定义 Id（如 "char_repair"）查找运行时角色；未找到返回 null。
    *****/
    public Func<string, CharacterSim?> FindCharacter { get; }

    /*****
    Date: 2026-09-25
    Name: CutsceneContext
    Description: 构造函数；注入全部共用引用。
    *****/
    public CutsceneContext(Camera2D camera, MapView mapView, DialogueBox dialogue,
        AudioStreamPlayer musicPlayer, MusicLibraryResource musicLibrary,
        Func<string, CharacterSim?> findCharacter)
    {
        Camera = camera;
        MapView = mapView;
        Dialogue = dialogue;
        MusicPlayer = musicPlayer;
        MusicLibrary = musicLibrary;
        FindCharacter = findCharacter;
    }

    /*****
    Date: 2026-09-26
    Name: PlayMusic
    Description: 从音库按 Id 查曲并播放（音源与代码解耦：换曲只改音库 .tres 所指文件，不动本类与剧情脚本）；未找到曲目或音频加载失败时仅警告不中断动画。音频流按基类 AudioStream 加载（不写死 mp3，否则音轨换成 .ogg 时会静默加载失败），循环经属性名统一设置。
    *****/
    public void PlayMusic(string trackId)
    {
        MusicTrackDef? track = MusicLibrary.Find(trackId);
        if (track == null)
        {
            GD.PushWarning($"[Cutscene] 音库中不存在曲目「{trackId}」。");
            return;
        }
        if (GD.Load<AudioStream>(track.StreamPath) is not { } stream)
        {
            GD.PushWarning($"[Cutscene] 无法加载音频（尚未导入或路径错误）：{track.StreamPath}");
            return;
        }
        stream.Set("loop", track.Loop); // 音频流类型各异（MP3/Ogg/Wav），统一按属性名设置循环
        MusicPlayer.Stream = stream;
        MusicPlayer.Play();
    }
}
