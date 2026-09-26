using Godot;
using RelayStation.Core.Map;

namespace RelayStation.Game.Save;

/*****
Date: 2026-09-25
Name: SaveGameResource
Description: 存档数据载体（Godot Resource）；聚合地图数据（含设施放置列表）、时钟分钟、角色条目、备用零件余额、开场动画已播标记与保存时间戳，经 SaveGameService 序列化到 user://saves/slot_n.tres。
*****/
[GlobalClass]
public partial class SaveGameResource : Resource
{
    /*****
    Date: 2026-09-25
    Name: MapData
    Description: 地图数据（格子 + 功能区 + 设施放置列表）。
    *****/
    [Export] public GridMapData? MapData { get; set; }

    /*****
    Date: 2026-09-25
    Name: ClockMinutes
    Description: 存档时刻的累计游戏分钟数。
    *****/
    [Export] public double ClockMinutes { get; set; }

    /*****
    Date: 2026-09-25
    Name: Characters
    Description: 角色存档条目列表（定义引用 + 所在格）。
    *****/
    [Export] public CharacterSaveEntry[]? Characters { get; set; }

    /*****
    Date: 2026-09-25
    Name: SpareParts
    Description: 存档时刻的备用零件余额。
    *****/
    [Export] public int SpareParts { get; set; }

    /*****
    Date: 2026-09-25
    Name: IntroPlayed
    Description: 开场动画是否已播放（读档后不再重播）。
    *****/
    [Export] public bool IntroPlayed { get; set; }

    /*****
    Date: 2026-09-25
    Name: SavedAtIso
    Description: 保存时间戳文本（供存档槽列表显示）。
    *****/
    [Export] public string SavedAtIso { get; set; } = "";
}
