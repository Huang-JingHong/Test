namespace RelayStation.Core.Items;

/*****
Date: 2026-09-26
Name: ItemGroupSummary
Description: 一组同种物品的汇总行（材料信息栏用）；把选中的多个物品堆按「物品种类」合并后，给出总数量、堆数与单组上限，便于一眼看出「选了多少、还能装多少」。纯数据对象，不含任何引擎依赖。
*****/
public sealed class ItemGroupSummary
{
    /*****
    Date: 2026-09-26
    Name: ItemGroupSummary
    Description: 构造函数；由聚合方按种类汇总后创建（iconTexturePath 供信息栏展示图标，可为空串）。
    *****/
    public ItemGroupSummary(string itemId, string displayName, int totalCount, int stackCount, int maxStack,
        string iconTexturePath)
    {
        ItemId = itemId;
        DisplayName = displayName;
        TotalCount = totalCount;
        StackCount = stackCount;
        MaxStack = maxStack;
        IconTexturePath = iconTexturePath;
    }

    /*****
    Date: 2026-09-26
    Name: ItemId
    Description: 物品唯一标识（同 Id 视为同种）。
    *****/
    public string ItemId { get; }

    /*****
    Date: 2026-09-26
    Name: DisplayName
    Description: 物品显示名称。
    *****/
    public string DisplayName { get; }

    /*****
    Date: 2026-09-26
    Name: TotalCount
    Description: 该种类的合计数量。
    *****/
    public int TotalCount { get; }

    /*****
    Date: 2026-09-26
    Name: StackCount
    Description: 该种类被选中的堆数。
    *****/
    public int StackCount { get; }

    /*****
    Date: 2026-09-26
    Name: MaxStack
    Description: 该种类的单组上限（用于展示「还能装多少」）。
    *****/
    public int MaxStack { get; }

    /*****
    Date: 2026-09-26
    Name: IconTexturePath
    Description: 该种类的图标贴图路径（信息栏展示图标用）。
    *****/
    public string IconTexturePath { get; }
}
