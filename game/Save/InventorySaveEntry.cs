using Godot;
using RelayStation.Core.Items;

namespace RelayStation.Game.Save;

/*****
Date: 2026-09-26
Name: InventorySaveEntry
Description: 存档中的「背包物品」条目；记录物品定义与数量，读档时据此恢复角色背包（重量的容量校验不参与恢复，超重状态原样带回）。
*****/
[GlobalClass]
public partial class InventorySaveEntry : Resource
{
    /*****
    Date: 2026-09-26
    Name: Def
    Description: 物品定义引用。
    *****/
    [Export] public ItemDef? Def { get; set; }

    /*****
    Date: 2026-09-26
    Name: Count
    Description: 该物品的数量（≤0 的条目读档时忽略）。
    *****/
    [Export] public int Count { get; set; }
}