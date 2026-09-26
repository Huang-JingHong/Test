using Godot;

namespace RelayStation.Core.Items;

/*****
Date: 2026-09-26
Name: ItemPlacement
Description: 地面物品堆的持久化条目（Godot Resource）；记录一组物品的静态定义引用、所属格、格内像素偏移与当前数量，作为地图/存档数据中物品列表的序列化单元（与设施放置条目 FacilityPlacement 对称）。运行时对应物为 ItemStack。
*****/
[GlobalClass]
public partial class ItemPlacement : Resource
{
    /*****
    Date: 2026-09-26
    Name: Def
    Description: 物品静态定义引用；可为 null（数据缺失时读档方跳过该条目）。
    *****/
    [Export] public ItemDef? Def { get; set; }

    /*****
    Date: 2026-09-26
    Name: Cell
    Description: 所属格。
    *****/
    [Export] public Vector2I Cell { get; set; }

    /*****
    Date: 2026-09-26
    Name: PixelOffset
    Description: 格内像素偏移（相对所属格左上角；视觉散落用）。
    *****/
    [Export] public Vector2 PixelOffset { get; set; }

    /*****
    Date: 2026-09-26
    Name: Count
    Description: 本组当前数量（应落在 [1, Def.MaxStack] 区间内）。
    *****/
    [Export] public int Count { get; set; }
}
