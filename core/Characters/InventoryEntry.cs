using RelayStation.Core.Items;

namespace RelayStation.Core.Characters;

/*****
Date: 2026-09-26
Name: InventoryEntry
Description: 背包内的一条物品记录（物品定义 + 数量）；纯记录类型，不参与 .tres 序列化（存档经由 InventorySaveEntry 转换）。同种物品在背包内合并为一条记录，不受 ItemDef.MaxStack 的拆堆约束——拆堆是「地面物品堆（一组一图）」的显示概念。
*****/
public sealed record InventoryEntry(IItemDef Def, int Count);