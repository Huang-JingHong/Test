namespace RelayStation.Core.Facilities;

/*****
Date: 2026-09-25
Name: FacilityCategory
Description: 设施分类枚举；用于建造面板按类过滤设施（家具/电力/氧气/通信/水利/建筑物/医疗/科研）。仅追加不改序，保证 .tres 中 Category 的整型值稳定；新增分类只需在末尾追加一项并在 FacilityDef 上标注即可，UI 与目录分组均数据驱动、零改框架。
*****/
public enum FacilityCategory
{
    /*****
    Date: 2026-09-25
    Name: Furniture
    Description: 家具（床/桌子/椅子/沙发等）。
    *****/
    Furniture = 0,

    /*****
    Date: 2026-09-25
    Name: Power
    Description: 电力（发电机等）。
    *****/
    Power = 1,

    /*****
    Date: 2026-09-25
    Name: Oxygen
    Description: 氧气（制氧机等）。
    *****/
    Oxygen = 2,

    /*****
    Date: 2026-09-25
    Name: Communication
    Description: 通信（通信阵列等）。
    *****/
    Communication = 3,

    /*****
    Date: 2026-09-25
    Name: Water
    Description: 水利（水回收系统等）。
    *****/
    Water = 4,

    /*****
    Date: 2026-09-25
    Name: Building
    Description: 建筑物（舱门等）。
    *****/
    Building = 5,

    /*****
    Date: 2026-09-25
    Name: Medical
    Description: 医疗（马桶/淋浴间/医疗床等）。
    *****/
    Medical = 6,

    /*****
    Date: 2026-09-25
    Name: Research
    Description: 科研（研究台等）。
    *****/
    Research = 7,
}
