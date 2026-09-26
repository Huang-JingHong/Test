using Godot;
using RelayStation.Game.Audio;
using RelayStation.Game.Autoloads;
using RelayStation.Game.Save;

namespace RelayStation.Game.TitleScreen;

/*****
Date: 2026-09-25
Name: TitleScreen
Description: 标题界面；提供「新的游戏」（默认地图开局）、「加载存档」（三槽选择）、「退出游戏」按钮与右上角「开发者模式」开关（会话级，决定是否解锁完整地图编辑与默认地图覆写）。背景为动态视频（res://game/Video/title_background.ogv，Ogg Theora，循环播放），缺失或加载失败时回退静态贴图（Export 可换美术素材）。背景音乐**独立于视频音轨**由音频引擎循环播放（视频循环重启时其内嵌音轨会在接缝处间歇爆音）。
*****/
public partial class TitleScreen : Control
{
    /*****
    Date: 2026-09-25
    Name: MainScenePath
    Description: 主场景资源路径。
    *****/
    private const string MainScenePath = "res://game/Main/Main.tscn";

    /*****
    Date: 2026-09-25
    Name: BackgroundVideoPath
    Description: 标题动态背景视频路径（Godot 原生仅支持 Ogg Theora .ogv）；文件缺失或加载失败时回退静态背景贴图。
    *****/
    private const string BackgroundVideoPath = "res://game/Video/title_background.ogv";

    /*****
    Date: 2026-09-25
    Name: MusicLibraryPath
    Description: 音库资源路径（与主场景共用一份）。
    *****/
    private const string MusicLibraryPath = "res://resources/music_library.tres";

    /*****
    Date: 2026-09-25
    Name: BackgroundTrackId
    Description: 标题背景音乐曲目 Id（音库中配置，指向从背景视频拆出的音轨）。
    *****/
    private const string BackgroundTrackId = "title_theme";

    /*****
    Date: 2026-09-25
    Name: MutedDb
    Description: 视频内嵌音轨的静音音量（dB）；仅在独立背景音乐成功起播后应用。
    *****/
    private const float MutedDb = -80f;

    /*****
    Date: 2026-09-25
    Name: BackgroundTexture
    Description: 标题背景贴图（占位 universe_background 副本；可在编辑器 Inspector 中替换正式美术）。
    *****/
    [Export] public Texture2D BackgroundTexture { get; set; } = default!;

    /*****
    Date: 2026-09-25
    Name: _backgroundVideo
    Description: 动态背景播放器；无视频时为 null（使用静态贴图）。
    *****/
    private VideoStreamPlayer? _backgroundVideo;

    /*****
    Date: 2026-09-25
    Name: _musicPlayer
    Description: 背景音乐播放器（独立于视频音轨）。
    *****/
    private AudioStreamPlayer? _musicPlayer;

    /*****
    Date: 2026-09-25
    Name: _lastMusicPosition
    Description: 上一帧的背景音乐播放位置（秒）；位置回绕即表示音乐完成一次循环，用于同步视频。
    *****/
    private float _lastMusicPosition;

    /*****
    Date: 2026-09-25
    Name: _devToggle
    Description: 开发者模式开关（右上角）。
    *****/
    private CheckButton? _devToggle;

    /*****
    Date: 2026-09-25
    Name: _loadDialog
    Description: 读档槽位选择对话框。
    *****/
    private SaveSlotDialog? _loadDialog;

    /*****
    Date: 2026-09-25
    Name: _Ready
    Description: 构建界面（背景优先动态视频，缺失时回退静态贴图）。
    *****/
    public override void _Ready()
    {
        BuildUi();
    }

    /*****
    Date: 2026-09-25
    Name: BuildUi
    Description: 纯代码构建界面：全屏背景（动态视频优先，缺失回退静态贴图）+ 暗化遮罩 + 居中标题与三按钮 + 右上角开发者模式开关 + 读档对话框。
    *****/
    private void BuildUi()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _backgroundVideo = BuildBackgroundVideo();
        if (_backgroundVideo == null)
        {
            // 未提供 .ogv 动态背景时回退静态贴图（未在 Inspector 覆盖时加载占位图）
            Texture2D? texture = BackgroundTexture;
            if (texture == null)
                texture = GD.Load<Texture2D>("res://game/Textures/background/title_background.png");

            var background = new TextureRect
            {
                Name = "Background",
                Texture = texture,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, // 最小尺寸不跟随贴图原尺寸，交由全屏锚点控制
            };
            AddChild(background);
            background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); // 先入树（父级已铺满）再设铺满锚点
        }

        // 背景音乐独立于视频音轨播放（见 BuildBackgroundMusic）；仅在音乐成功起播后静音视频内嵌音轨，
        // 否则保留视频自带音轨发声，避免标题界面变成无声
        if (BuildBackgroundMusic() && _backgroundVideo != null)
            _backgroundVideo.VolumeDb = MutedDb;

        var overlay = new ColorRect { Name = "Overlay", Color = new Color(0f, 0f, 0f, 0.45f) };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        // 标题与主按钮（全屏居中容器包竖排盒子；CenterContainer 保证任意内容尺寸下精确居中，
        // 不用 Center 预设——预设按调用时尺寸算偏移，子节点后加入会导致盒子右下偏移）
        var centerWrapper = new CenterContainer { Name = "CenterBox" };
        centerWrapper.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(centerWrapper);

        var centerBox = new VBoxContainer();
        centerBox.AddThemeConstantOverride("separation", 16);
        centerWrapper.AddChild(centerBox);

        var titleLabel = new Label
        {
            Text = "中继站",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 64);
        centerBox.AddChild(titleLabel);

        var subtitle = new Label
        {
            Text = "Relay Station",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        subtitle.AddThemeFontSizeOverride("font_size", 18);
        subtitle.AddThemeColorOverride("font_color", new Color(0.8f, 0.85f, 0.9f));
        centerBox.AddChild(subtitle);

        var spacer = new Control { CustomMinimumSize = new Vector2(0, 24) };
        centerBox.AddChild(spacer);

        var newGameButton = new Button { Text = "新的游戏", CustomMinimumSize = new Vector2(220, 44) };
        newGameButton.Pressed += OnNewGamePressed;
        centerBox.AddChild(newGameButton);

        var loadButton = new Button { Text = "加载存档", CustomMinimumSize = new Vector2(220, 44) };
        loadButton.Pressed += OnLoadGamePressed;
        centerBox.AddChild(loadButton);

        var quitButton = new Button { Text = "退出游戏", CustomMinimumSize = new Vector2(220, 44) };
        quitButton.Pressed += OnQuitPressed;
        centerBox.AddChild(quitButton);

        // 右上角开发者模式开关
        _devToggle = new CheckButton { Name = "DevModeToggle", Text = "开发者模式" };
        _devToggle.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, LayoutPresetMode.Minsize, 16);
        AddChild(_devToggle);

        // 读档对话框
        _loadDialog = new SaveSlotDialog { Name = "LoadDialog" };
        _loadDialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_loadDialog);
        _loadDialog.SlotConfirmed += OnLoadSlotConfirmed;
    }

    /*****
    Date: 2026-09-25
    Name: BuildBackgroundVideo
    Description: 构建动态背景层：加载 .ogv 视频流并循环播放、铺满全屏（Godot 原生仅支持 Ogg Theora）；文件缺失或流加载失败时返回 null，由调用方回退静态背景贴图。视频内嵌音轨的处理见 BuildBackgroundMusic。
    *****/
    private VideoStreamPlayer? BuildBackgroundVideo()
    {
        if (!ResourceLoader.Exists(BackgroundVideoPath)) return null;

        var stream = GD.Load<VideoStream>(BackgroundVideoPath);
        if (stream == null)
        {
            GD.PushWarning($"[TitleScreen] 无法加载背景视频：{BackgroundVideoPath}");
            return null;
        }

        var video = new VideoStreamPlayer
        {
            Name = "BackgroundVideo",
            Stream = stream,
            Loop = true,
            Expand = true,
        };
        AddChild(video);
        video.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); // 先入树（父级已铺满）再设铺满锚点
        video.Play();
        return video;
    }

    /*****
    Date: 2026-09-25
    Name: BuildBackgroundMusic
    Description: 构建标题背景音乐播放器：从音库取 BackgroundTrackId 曲目并按曲目配置循环播放。**刻意与视频音轨分离**——VideoStreamPlayer 循环重启视频时其内嵌音轨会在接缝处间歇爆音（听感像接触不良），改由音频引擎独立循环同一段音源（从背景视频无损拆出的 Ogg Vorbis，精确循环无接缝）；曲目缺失或音源加载失败返回 false，此时保留视频自带音轨发声。更换 .ogv 后须同步重新拆轨更新该音频文件（音源路径见音库）。
    *****/
    private bool BuildBackgroundMusic()
    {
        MusicTrackDef? track = GD.Load<MusicLibraryResource>(MusicLibraryPath)?.Find(BackgroundTrackId);
        if (track == null)
        {
            GD.PushWarning($"[TitleScreen] 音库中不存在背景曲目「{BackgroundTrackId}」，沿用视频自带音轨。");
            return false;
        }

        var stream = GD.Load<AudioStream>(track.StreamPath);
        if (stream == null)
        {
            GD.PushWarning($"[TitleScreen] 无法加载背景音乐（尚未导入或路径错误）：{track.StreamPath}");
            return false;
        }

        stream.Set("loop", track.Loop); // 音频流类型各异（Ogg/MP3/Wav），统一按属性名设置循环
        _musicPlayer = new AudioStreamPlayer { Name = "TitleMusic", Stream = stream };
        AddChild(_musicPlayer);
        _musicPlayer.Play();
        return true;
    }

    /*****
    Date: 2026-09-25
    Name: _Process
    Description: 以背景音乐为时间基准同步视频：音乐播放位置回绕（完成一次循环）即把视频位置归零。两者各自循环且时长略有差异（音频 5.108s、视频约 5.02s），不锁基准会逐圈累积漂移；视频画面为静音循环背景，提前/滞后几十毫秒归零无观感影响。
    *****/
    public override void _Process(double delta)
    {
        if (_musicPlayer == null || _backgroundVideo == null) return;
        if (!_musicPlayer.IsPlaying()) return;

        float position = (float)_musicPlayer.GetPlaybackPosition();
        if (position < _lastMusicPosition) _backgroundVideo.StreamPosition = 0;
        _lastMusicPosition = position;
    }

    /*****
    Date: 2026-09-25
    Name: OnNewGamePressed
    Description: 「新的游戏」：按开发者模式开关状态开始新游戏并切换主场景。
    *****/
    private void OnNewGamePressed()
    {
        GameRoot.Instance.StartNewGame(_devToggle?.ButtonPressed ?? false);
        GetTree().ChangeSceneToFile(MainScenePath);
    }

    /*****
    Date: 2026-09-25
    Name: OnLoadGamePressed
    Description: 「加载存档」：打开读档对话框。
    *****/
    private void OnLoadGamePressed() => _loadDialog?.Open(SaveSlotDialog.DialogMode.Load);

    /*****
    Date: 2026-09-25
    Name: OnLoadSlotConfirmed
    Description: 读档对话框确认槽位：加载成功则切换主场景（失败留在标题）。
    *****/
    private void OnLoadSlotConfirmed(int slot)
    {
        if (GameRoot.Instance.LoadGame(slot))
            GetTree().ChangeSceneToFile(MainScenePath);
    }

    /*****
    Date: 2026-09-25
    Name: OnQuitPressed
    Description: 「退出游戏」：结束进程。
    *****/
    private void OnQuitPressed() => GetTree().Quit();
}
