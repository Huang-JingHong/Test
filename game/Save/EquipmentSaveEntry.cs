using Godot;
using RelayStation.Core.Items;

namespace RelayStation.Game.Save;

/*****
Date: 2026-09-26
Name: EquipmentSaveEntry
Description: 存档中的「已装备物品」条目；记录槽位与被装备的物品定义，读档时据此恢复角色的装备槽。
*****/
[GlobalClass]
public partial class EquipmentSaveEntry : Resource
{
    /*****
    Date: 2026-09-26
    Name: Slot
    Description: 装备槽位（None 表示无效槽，读档时忽略）。
    *****/
    [Export] public EquipmentSlot Slot { get; set; } = EquipmentSlot.None;

    /*****
    Date: 2026-09-26
    Name: Def
    Description: 被装备的物品定义引用。
    *****/
    [Export] public ItemDef? Def { get; set; }
}