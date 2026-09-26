namespace RelayStation.Core.Facilities;

/*****
Date: 2026-09-25
Name: FacilityCatalog
Description: 设施目录（纯 C# 数据服务）；把一组设施定义按分类分组建索引，供建造面板「按分类过滤设施」使用。完全数据驱动——分类与设施均取自入参，新增设施/分类只需在 .tres 上标注 Category，本类与 UI 零改动。
*****/
public sealed class FacilityCatalog
{
    /*****
    Date: 2026-09-25
    Name: _all
    Description: 全部设施定义（保持入参顺序）。
    *****/
    private readonly IReadOnlyList<FacilityDef> _all;

    /*****
    Date: 2026-09-25
    Name: _categories
    Description: 实际出现过的分类（按 FacilityCategory 枚举序升序，无对应设施的分类不出现）。
    *****/
    private readonly IReadOnlyList<FacilityCategory> _categories;

    /*****
    Date: 2026-09-25
    Name: _byCategory
    Description: 分类 → 该分类下设施定义列表。
    *****/
    private readonly Dictionary<FacilityCategory, IReadOnlyList<FacilityDef>> _byCategory;

    /*****
    Date: 2026-09-25
    Name: FacilityCatalog
    Description: 构造函数；以设施定义集合建立分类索引，null 项被忽略。
    *****/
    public FacilityCatalog(IEnumerable<FacilityDef> facilities)
    {
        _all = facilities.Where(f => f != null).ToList();

        var grouped = new Dictionary<FacilityCategory, List<FacilityDef>>();
        foreach (FacilityDef def in _all)
        {
            if (!grouped.TryGetValue(def.Category, out List<FacilityDef>? list))
            {
                list = new List<FacilityDef>();
                grouped[def.Category] = list;
            }
            list.Add(def);
        }

        _categories = Enum.GetValues<FacilityCategory>()
            .Where(grouped.ContainsKey)
            .ToList();
        _byCategory = grouped.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<FacilityDef>)entry.Value);
    }

    /*****
    Date: 2026-09-25
    Name: All
    Description: 全部设施定义（只读）。
    *****/
    public IReadOnlyList<FacilityDef> All => _all;

    /*****
    Date: 2026-09-25
    Name: Categories
    Description: 实际有设施的分类列表（只读，按枚举序）。
    *****/
    public IReadOnlyList<FacilityCategory> Categories => _categories;

    /*****
    Date: 2026-09-25
    Name: GetByCategory
    Description: 取指定分类下的设施定义列表；该分类无设施时返回空列表（不返回 null）。
    *****/
    public IReadOnlyList<FacilityDef> GetByCategory(FacilityCategory category)
        => _byCategory.TryGetValue(category, out IReadOnlyList<FacilityDef>? list)
            ? list
            : Array.Empty<FacilityDef>();
}
