namespace RelayStation.Game.Cutscenes;

/*****
Date: 2026-09-25
Name: CutsceneScript
Description: 剧情动画脚本抽象基类；一段动画 = 有序的动作列表，由子类声明式覆写 Actions 提供。每个动画定义为独立小文件（如 IntroCutscene.cs），方便开发者随时修改。
*****/
public abstract class CutsceneScript
{
    /*****
    Date: 2026-09-25
    Name: Actions
    Description: 依序执行的动作列表。
    *****/
    public abstract IReadOnlyList<CutsceneAction> Actions { get; }
}
