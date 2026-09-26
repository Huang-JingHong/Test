namespace RelayStation.Core.Items;

/*****
Date: 2026-09-26
Name: EquipmentSlot
Description: 装备槽位枚举；可穿戴物品的佩戴位置——None 表示不可装备（普通物品），其余五槽对应 头部 / 身体 / 背部（背包） / 手部 / 脚部，每个槽位最多装备一件物品。
*****/
public enum EquipmentSlot { None, Head, Body, Back, Hands, Feet }