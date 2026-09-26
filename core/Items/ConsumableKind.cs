namespace RelayStation.Core.Items;

/*****
Date: 2026-09-26
Name: ConsumableKind
Description: 可消耗物品种类；标明该物品被「消耗」时满足哪一类需求（None = 不可消耗，如零件与装备）。取用任务据此把「消耗」映射到对应需求（Food → NeedId.Food、Water → NeedId.Water），使需求层不反向依赖物品层。
*****/
public enum ConsumableKind
{
    /*****
    Date: 2026-09-26
    Name: None
    Description: 不可消耗（普通物品与装备）。
    *****/
    None,

    /*****
    Date: 2026-09-26
    Name: Food
    Description: 食物；消耗后恢复「进食」需求。
    *****/
    Food,

    /*****
    Date: 2026-09-26
    Name: Water
    Description: 饮水；消耗后恢复「饮水」需求。
    *****/
    Water,
}