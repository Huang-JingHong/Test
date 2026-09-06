namespace RelayStation.Core.Characters;

/*****
Date: 2026-09-06
Name: INeedSystem
Description: 需求系统接口；负责角色的需求数值衰减与读取。阶段一仅占位冻结接口，不接入生存死逻辑。
*****/
public interface INeedSystem
{
    /*****
    Date: 2026-09-06
    Name: Update
    Description: 按游戏分钟推进指定角色的需求衰减。
    *****/
    void Update(CharacterSim c, double deltaGameMinutes);

    /*****
    Date: 2026-09-06
    Name: Read
    Description: 读取指定角色当前的需求快照。
    *****/
    NeedSnapshot Read(CharacterSim c);
}
