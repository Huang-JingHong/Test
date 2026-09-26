using Godot;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-25
Name: DialogueBox
Description: 底部对话框（星露谷式）；头像（占位贴图，Export 可换）+ 说话人姓名 + 打字机逐字显示的文本。点击/空格/回车推进：未显示完时先补全文本，已显示完时结束本行并触发 LineFinished。仅在有台词时可见并响应输入。
*****/
public partial class DialogueBox : PanelContainer
{
    /*****
    Date: 2026-09-25
    Name: CharsPerSecond
    Description: 打字机速度（每秒字符数）。
    *****/
    private const float CharsPerSecond = 30f;

    /*****
    Date: 2026-09-25
    Name: AvatarTexture
    Description: 默认头像贴图（未在 Inspector 覆盖时为占位图 astronaut1_placeholder.png）；ShowLine 未传 portrait 时使用，传入时按说话人/表情替换。
    *****/
    [Export] public Texture2D AvatarTexture { get; set; } = default!;

    /*****
    Date: 2026-09-25
    Name: LineFinished
    Description: 一行台词翻页完成时触发（供对话动作推进动画序列）。
    *****/
    public event Action? LineFinished;

    /*****
    Date: 2026-09-25
    Name: _nameLabel
    Description: 说话人姓名标签。
    *****/
    private Label? _nameLabel;

    /*****
    Date: 2026-09-25
    Name: _textLabel
    Description: 台词文本标签。
    *****/
    private RichTextLabel? _textLabel;

    /*****
    Date: 2026-09-25
    Name: _avatarRect
    Description: 头像显示区。
    *****/
    private TextureRect? _avatarRect;

    /*****
    Date: 2026-09-25
    Name: _fullText
    Description: 当前台词全文。
    *****/
    private string _fullText = "";

    /*****
    Date: 2026-09-25
    Name: _visibleChars
    Description: 已显示的字符数。
    *****/
    private int _visibleChars;

    /*****
    Date: 2026-09-25
    Name: _charTimer
    Description: 打字机累计计时（以字符为单位）。
    *****/
    private float _charTimer;

    /*****
    Date: 2026-09-25
    Name: _lineActive
    Description: 是否有台词正在显示。
    *****/
    private bool _lineActive;

    /*****
    Date: 2026-09-25
    Name: DialogueBox
    Description: 构造函数；纯代码构建布局（头像 + 姓名 + 文本），初始隐藏。
    *****/
    public DialogueBox()
    {
        Visible = false;
        CustomMinimumSize = new Vector2(560, 140);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        AddChild(row);

        _avatarRect = new TextureRect
        {
            CustomMinimumSize = new Vector2(96, 96),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        };
        row.AddChild(_avatarRect);

        var textColumn = new VBoxContainer();
        textColumn.AddThemeConstantOverride("separation", 4);
        // 水平方向必须 ExpandFill：否则 HBox 只按最小宽分配，RichTextLabel 宽≈0，
        // 中文逐字换行成竖排「一列」，看起来像宽度不够。
        textColumn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        textColumn.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        row.AddChild(textColumn);

        _nameLabel = new Label { Text = "" };
        _nameLabel.AddThemeFontSizeOverride("font_size", 18);
        textColumn.AddChild(_nameLabel);

        _textLabel = new RichTextLabel
        {
            FitContent = false,
            ScrollActive = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _textLabel.AddThemeFontSizeOverride("normal_font_size", 16);
        textColumn.AddChild(_textLabel);
    }

    /*****
    Date: 2026-09-25
    Name: Setup
    Description: 初始化头像贴图（未在 Inspector 覆盖时加载占位图）。
    *****/
    public void Setup()
    {
        if (AvatarTexture == null)
            AvatarTexture = GD.Load<Texture2D>("res://game/Textures/characters/astronaut1_placeholder.png");
        if (_avatarRect != null) _avatarRect.Texture = AvatarTexture;
    }

    /*****
    Date: 2026-09-25
    Name: ShowLine
    Description: 显示一行台词（说话人姓名 + 头像 + 文本），从头开始打字机显示；portrait 为 null 时沿用默认头像（AvatarTexture，即占位图）。
    *****/
    public void ShowLine(string speakerName, string text, Texture2D? portrait = null)
    {
        if (_nameLabel != null) _nameLabel.Text = speakerName;
        if (_avatarRect != null) _avatarRect.Texture = portrait ?? AvatarTexture;
        _fullText = text;
        _visibleChars = 0;
        _charTimer = 0f;
        _lineActive = true;
        Visible = true;
        UpdateDisplay();
    }

    /*****
    Date: 2026-09-25
    Name: _Process
    Description: 打字机推进：按速度逐字显示。
    *****/
    public override void _Process(double delta)
    {
        if (!_lineActive || _visibleChars >= _fullText.Length) return;

        _charTimer += (float)delta * CharsPerSecond;
        bool changed = false;
        while (_charTimer >= 1f && _visibleChars < _fullText.Length)
        {
            _visibleChars++;
            _charTimer -= 1f;
            changed = true;
        }
        if (changed) UpdateDisplay();
    }

    /*****
    Date: 2026-09-25
    Name: _UnhandledInput
    Description: 推进输入：点击/空格/回车——未显示完先补全，已显示完结束本行（触发 LineFinished 并隐藏）。
    *****/
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_lineActive) return;

        bool advance = @event switch
        {
            InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } => true,
            InputEventKey { Pressed: true, Keycode: Key.Space or Key.Enter or Key.KpEnter } => true,
            _ => false,
        };
        if (!advance) return;

        GetViewport().SetInputAsHandled();
        Advance();
    }

    /*****
    Date: 2026-09-25
    Name: Advance
    Description: 推进一行台词：未显示完时补全文本；已显示完时结束本行（隐藏并触发 LineFinished）。
    *****/
    private void Advance()
    {
        if (!_lineActive) return;
        if (_visibleChars < _fullText.Length)
        {
            _visibleChars = _fullText.Length;
            UpdateDisplay();
            return;
        }

        _lineActive = false;
        Visible = false;
        LineFinished?.Invoke();
    }

    /*****
    Date: 2026-09-25
    Name: UpdateDisplay
    Description: 按已显示字符数刷新文本。
    *****/
    private void UpdateDisplay()
    {
        int count = Math.Clamp(_visibleChars, 0, _fullText.Length);
        _textLabel?.SetText(count >= _fullText.Length ? _fullText : _fullText.Substring(0, count));
    }
}
