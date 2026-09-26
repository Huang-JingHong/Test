namespace RelayStation.Core.Facilities;

/*****
Date: 2026-09-06
Name: FacilityState
Description: 设施状态枚举；描述设施的运行状态（Damaged=损坏、UnderRepair=维修中、Operational=运行、UnderConstruction=建造中、Demolishing=拆除中）。仅追加不改序，保证 .tres 与存档中既有整型值稳定（历史值 0/1/2 保持不变）。
*****/
public enum FacilityState
{
    /*****
    Date: 2026-09-06
    Name: Damaged
    Description: 损坏（可发起修复任务）。
    *****/
    Damaged = 0,

    /*****
    Date: 2026-09-06
    Name: UnderRepair
    Description: 维修中（存在未结的修复任务）。
    *****/
    UnderRepair = 1,

    /*****
    Date: 2026-09-06
    Name: Operational
    Description: 运行中（已建成且完好）。
    *****/
    Operational = 2,

    /*****
    Date: 2026-09-25
    Name: UnderConstruction
    Description: 建造中（设施占位已存在、阻挡寻路，但尚未建成；存在未结的建造任务）。
    *****/
    UnderConstruction = 3,

    /*****
    Date: 2026-09-25
    Name: Demolishing
    Description: 拆除中（存在未结的拆除任务，完成后设施从模拟中移除）。
    *****/
    Demolishing = 4,
}
