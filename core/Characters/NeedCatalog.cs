namespace RelayStation.Core.Characters;

/*****
Date: 2026-09-26
Name: NeedCatalog
Description: 需求静态元数据表（纯 C#，不依赖引擎）；提供全量需求列表与各项需求的方向，使需求系统、UI 与测试共用同一权威口径，避免表现层各自猜测语义。需求的显示名属于表现层概念（由 UI 决定）。
*****/
public static class NeedCatalog
{
    /*****
    Date: 2026-09-26
    Name: All
    Description: 全部需求标识（按 NeedId 声明顺序，含追加在末尾的排尿）；供 UI 遍历渲染与测试逐项校验。
    *****/
    public static readonly NeedId[] All =
    {
        NeedId.Oxygen, NeedId.Food, NeedId.Rest, NeedId.Water,
        NeedId.Excretion, NeedId.Hygiene, NeedId.Social, NeedId.Entertainment,
        NeedId.Urination,
    };

    /*****
    Date: 2026-09-26
    Name: Direction
    Description: 取指定需求的方向；仅排泄与排尿为增长型（越小越好），其余为衰减型（越大越好）。
    *****/
    public static NeedDirection Direction(NeedId id)
        => id is NeedId.Excretion or NeedId.Urination ? NeedDirection.Increasing : NeedDirection.Decreasing;
}