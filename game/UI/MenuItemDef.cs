namespace RelayStation.Game.UI;

/*****
Date: 2026-09-25
Name: MenuItemDef
Description: 菜单项定义（数据类）；菜单结构完全数据驱动——新增菜单入口只需提供定义并注册到 MenuBar，零改动框架。IconPath 为占位字段（空串渲染文字按钮，配置路径后可切换图标按钮，未来换图标零改框架）。
*****/
public sealed class MenuItemDef
{
    /*****
    Date: 2026-09-25
    Name: Id
    Description: 菜单项唯一标识（同时作为对应面板注册键）。
    *****/
    public string Id { get; }

    /*****
    Date: 2026-09-25
    Name: DisplayName
    Description: 按钮显示文字。
    *****/
    public string DisplayName { get; }

    /*****
    Date: 2026-09-25
    Name: SortOrder
    Description: 排序权重（升序排列；同序按注册先后）。
    *****/
    public int SortOrder { get; }

    /*****
    Date: 2026-09-25
    Name: IconPath
    Description: 图标资源路径（空串 = 文字按钮；路径不可加载时同样回退文字按钮）。
    *****/
    public string IconPath { get; }

    /*****
    Date: 2026-09-25
    Name: MenuItemDef
    Description: 构造函数。
    *****/
    public MenuItemDef(string id, string displayName, int sortOrder = 0, string iconPath = "")
    {
        Id = id;
        DisplayName = displayName;
        SortOrder = sortOrder;
        IconPath = iconPath;
    }
}
