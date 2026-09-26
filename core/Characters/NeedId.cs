namespace RelayStation.Core.Characters;

/*****
Date: 2026-09-26
Name: NeedId
Description: 角色需求标识枚举；九项需求——生理（呼吸 Oxygen、进食 Food、饮水 Water、休息 Rest、排泄 Excretion、排尿 Urination、卫生 Hygiene）与心理（社交 Social、娱乐 Entertainment）。**顺序冻结**：存档按此顺序序列化，只可追加、不可插入或重排。
*****/
public enum NeedId
{
    Oxygen,        // 呼吸
    Food,          // 进食
    Rest,          // 休息
    Water,         // 饮水
    Excretion,     // 排泄（增长型：由饱食量驱动，值越大越糟）
    Hygiene,       // 卫生
    Social,        // 社交
    Entertainment, // 娱乐
    Urination,     // 排尿（增长型：由饮水量驱动，值越大越糟）——追加在末尾以保持既有存档索引稳定
}