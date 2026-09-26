using static RelayStation.Game.Characters.PortraitExpression;

namespace RelayStation.Game.Cutscenes.Scripts;

/*****
Date: 2026-09-25
Name: IntroCutscene
Description: 开场动画（每次「新的游戏」播放，读档不播）；镜头聚焦陈岩（队长，char_repair）→ 两句台词 → 播放音库占位曲目 intro_theme（循环作为 BGM，音频文件为占位素材，替换只需换文件不改本文件）→ 短暂停顿后恢复自由操作。修改台词/镜头/音乐只需调整本文件的声明式动作列表。
*****/
public sealed class IntroCutscene : CutsceneScript
{
    /*****
    Date: 2026-09-25
    Name: Actions
    Description: 开场动画动作序列（聚焦 → 对话 → 音乐 → 停顿）。
    *****/
    public override IReadOnlyList<CutsceneAction> Actions { get; } = new CutsceneAction[]
    {
        new PlayMusicAction("intro_theme"), // 音库占位曲目（bgm_intro_placeholder.mp3，循环）
        new CameraFocusAction("char_repair", 2.5f, 1.5f), // 镜头缓动聚焦陈岩（队长）
        new DialogueAction("char_repair", "咳咳，真倒霉，这里看起来已经废弃了。", Angry),
        new CameraFocusAction("char_operator", 2.5f, 0.5f), // 镜头缓动聚焦林晓
        new DialogueAction("char_operator", "这里似乎还剩下一些设备。", Hurt),
        new DialogueAction("char_operator", "我们应该修好这些设备。", Calm),
        new CameraFocusAction("char_field", 2.5f, 1.0f), // 镜头缓动聚焦赵雷）
        new DialogueAction("char_field", "那就赶快动手吧，朋友们。", Smile),
        new DelayAction(0.5f),
    };
}
