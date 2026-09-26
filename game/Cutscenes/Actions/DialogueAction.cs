using RelayStation.Game.Characters;
using RelayStation.Game.UI;

namespace RelayStation.Game.Cutscenes;

/*****
Date: 2026-09-25
Name: DialogueAction
Description: 对话动作；让指定角色（按 CharacterDef.Id 查找）在底部对话框说一句话（星露谷式：头像+姓名+打字机文本），头像按说话人定义与表情参数解析（缺省平静表情），等待玩家翻页完成后动作结束。说话人为队长时姓名后缀「（队长）」。
*****/
public sealed class DialogueAction : CutsceneAction
{
    /*****
    Date: 2026-09-25
    Name: _speakerId
    Description: 说话角色的定义 Id。
    *****/
    private readonly string _speakerId;

    /*****
    Date: 2026-09-25
    Name: _text
    Description: 台词文本。
    *****/
    private readonly string _text;

    /*****
    Date: 2026-09-25
    Name: _expression
    Description: 本句头像表情；表情贴图缺失时由头像库回退默认（平静）头像。
    *****/
    private readonly PortraitExpression _expression;

    /*****
    Date: 2026-09-25
    Name: _finished
    Description: 本句是否已翻页完成。
    *****/
    private bool _finished;

    /*****
    Date: 2026-09-25
    Name: DialogueAction
    Description: 构造函数；指定说话人 Id、台词与头像表情（默认平静）。
    *****/
    public DialogueAction(string speakerId, string text, PortraitExpression expression = PortraitExpression.Calm)
    {
        _speakerId = speakerId;
        _text = text;
        _expression = expression;
    }

    /*****
    Date: 2026-09-25
    Name: Start
    Description: 解析说话人显示名与表情头像并在对话框显示台词；订阅翻页完成事件（完成即退订）。
    *****/
    public override void Start(CutsceneContext context)
    {
        _finished = false;

        var speaker = context.FindCharacter(_speakerId);
        string name = speaker?.Def?.DisplayName ?? "？？？";
        if (speaker?.Def?.IsCaptain == true) name += "（队长）";

        DialogueBox dialogue = context.Dialogue;
        dialogue.LineFinished += OnLineFinished;
        dialogue.ShowLine(name, _text, CharacterPortraitLibrary.Get(speaker?.Def, _expression));

        /*****
        Date: 2026-09-25
        Name: OnLineFinished
        Description: 本地函数；台词翻页完成时置完成标记并退订事件。
        *****/
        void OnLineFinished()
        {
            dialogue.LineFinished -= OnLineFinished;
            _finished = true;
        }
    }

    /*****
    Date: 2026-09-25
    Name: Tick
    Description: 翻页完成后动作结束。
    *****/
    public override bool Tick(double delta) => _finished;
}
