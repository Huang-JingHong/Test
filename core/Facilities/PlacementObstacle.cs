namespace RelayStation.Core.Facilities;

/*****
Date: 2026-09-26
Name: PlacementObstacle
Description: 设施放置受阻原因枚举（放置校验的返回结果）；None 表示可放置，其余取值一一对应「不是地板 / 已被设施占用 / 格上有物品」三类硬性阻挡。建造预览的绿红框与「强行点击」时的提示文案**共用同一次校验结果**，故预览颜色与实际放置结果必然一致，不会出现「显示能放、点了却失败」。特别注意：材料（备用零件）不足**不在此枚举内**——材料短缺只让建造任务的进度停滞、补料后自动继续，不阻止放置。
*****/
public enum PlacementObstacle
{
    /*****
    Date: 2026-09-26
    Name: None
    Description: 无阻挡，可放置。
    *****/
    None = 0,

    /*****
    Date: 2026-09-26
    Name: NotFloor
    Description: 目标格不是地板（墙 / 真空 / 门），或压根不在图内（越界格一律视为真空）。
    *****/
    NotFloor = 1,

    /*****
    Date: 2026-09-26
    Name: FacilityOccupied
    Description: 目标格已被其他设施占地（含不阻挡通行的家具——不阻挡通行不等于可以叠放）。
    *****/
    FacilityOccupied = 2,

    /*****
    Date: 2026-09-26
    Name: ItemsPresent
    Description: 目标格上有物品阻挡（物品与设施不可同格共存）。
    *****/
    ItemsPresent = 3,
}
