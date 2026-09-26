namespace RelayStation.Game.Cutscenes;

/*****
Date: 2026-09-25
Name: CutsceneAction
Description: 剧情动画动作抽象基类；一个动作是动画序列中的一步（镜头聚焦/对话/播曲/延时等）。Start 在动作开始时调用一次，Tick 每帧驱动并返回动作是否完成。新增动作类型只需派生本类实现两个方法，满足「开发者随时修改/扩展」。
*****/
public abstract class CutsceneAction
{
    /*****
    Date: 2026-09-25
    Name: Start
    Description: 动作开始时调用一次；用于发起副作用（如显示对话框、开始播曲）。
    *****/
    public abstract void Start(CutsceneContext context);

    /*****
    Date: 2026-09-25
    Name: Tick
    Description: 每帧驱动动作；返回 true 表示本动作已完成，导演器进入下一步。
    *****/
    public abstract bool Tick(double delta);
}
