using Godot;
using RelayStation.Core.Characters;

namespace RelayStation.Game.Save;

/*****
Date: 2026-09-25
Name: CharacterSaveEntry
Description: 存档中的角色条目；记录角色定义（.tres 引用）、所在格、生命值与八项需求数值，读档时据此重建为 Idle 状态（进行中任务不存档）。旧档缺新字段时按默认值恢复（生命值 100、需求满值）。
*****/
[GlobalClass]
public partial class CharacterSaveEntry : Resource
{
    /*****
    Date: 2026-09-25
    Name: Def
    Description: 角色静态定义引用。
    *****/
    [Export] public CharacterDef? Def { get; set; }

    /*****
    Date: 2026-09-25
    Name: Cell
    Description: 角色存档时刻所在格。
    *****/
    [Export] public Vector2I Cell { get; set; }

    /*****
    Date: 2026-09-26
    Name: Health
    Description: 存档时刻的生命值（0~100）；旧档缺字段时取默认满值。
    *****/
    [Export] public float Health { get; set; } = CharacterSim.MaxHealth;

    /*****
    Date: 2026-09-26
    Name: Needs
    Description: 存档时刻的八项需求数值（按 NeedId 顺序：呼吸/进食/休息/饮水/排泄/卫生/社交/娱乐）；空数组或长度不足时读档按满值恢复。
    *****/
    [Export] public float[] Needs { get; set; } = Array.Empty<float>();

    /*****
    Date: 2026-09-26
    Name: Equipment
    Description: 存档时刻的已装备物品（槽位 + 物品定义）；空数组（旧档）表示未装备任何物品。
    *****/
    [Export] public EquipmentSaveEntry[] Equipment { get; set; } = Array.Empty<EquipmentSaveEntry>();

    /*****
    Date: 2026-09-26
    Name: Inventory
    Description: 存档时刻的背包内物品条目（物品定义 + 数量）；空数组（旧档）表示空背包。
    *****/
    [Export] public InventorySaveEntry[] Inventory { get; set; } = Array.Empty<InventorySaveEntry>();
}
