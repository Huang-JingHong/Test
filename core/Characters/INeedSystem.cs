namespace RelayStation.Core.Characters;

/*****
Date: 2026-09-06
Name: INeedSystem
Description: 需求系统接口；负责角色九项需求的推进（衰减/增长）、读取与存档恢复。当前为「仅数值」骨架：数值不致命、不影响工作效率、不驱动行为（需求惩罚与自主满足行为后续接入）。
*****/
public interface INeedSystem
{
    /*****
    Date: 2026-09-06
    Name: Update
    Description: 按游戏分钟推进指定角色的需求（衰减型下降、排泄增长）。
    *****/
    void Update(CharacterSim c, double deltaGameMinutes);

    /*****
    Date: 2026-09-06
    Name: Read
    Description: 读取指定角色当前的需求快照；无记录时返回满值。
    *****/
    NeedSnapshot Read(CharacterSim c);

    /*****
    Date: 2026-09-26
    Name: Set
    Description: 覆盖指定角色的需求快照（存档恢复用）；**不触发** NeedsChanged，避免读档瞬间刷屏。
    *****/
    void Set(CharacterSim c, NeedSnapshot snapshot);

    /*****
    Date: 2026-09-26
    Name: Restore
    Description: 提升指定角色的**单项**需求值（消耗物品后的恢复路径，如吃饭补水）：结果夹在 0~Max，**触发** NeedsChanged（经 Simulation 转发事件总线，面板自动刷新）；amount ≤ 0 时无操作、不触发事件。
    *****/
    void Restore(CharacterSim c, NeedId need, float amount);

    /*****
    Date: 2026-09-26
    Name: NeedsChanged
    Description: 需求推进后触发的事件（参数：角色本体、推进后的快照）；供 UI 刷新数值显示。
    *****/
    event Action<CharacterSim, NeedSnapshot>? NeedsChanged;
}