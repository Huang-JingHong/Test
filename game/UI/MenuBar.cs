using Godot;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-25
Name: MenuBar
Description: 注册制菜单栏（HOI4 式顶栏入口）；按钮数量与内容完全由注册项驱动（禁止写死数量）：RegisterItem 动态生成 ToggleMode 按钮、按 SortOrder 排列；菜单项配置 IconPath 时渲染图标按钮（显示名作悬停提示，图标以主题常量 icon_max_width 限幅），未配置或加载失败回退文字按钮。支持注销与外部状态同步（SetPressed，如建造钮跟随编辑模式切换）。零注册时整栏隐藏。宽度溢出策略（折叠「≡ 更多」）为预留钩子，本期不实现。
*****/
public partial class MenuBar : HBoxContainer
{
    /*****
    Date: 2026-09-25
    Name: ItemEntry
    Description: 单个菜单项的运行时登记（定义 + 切换回调 + 按钮）。
    *****/
    private sealed class ItemEntry
    {
        /*****
        Date: 2026-09-25
        Name: Def
        Description: 菜单项定义。
        *****/
        public MenuItemDef Def = default!;

        /*****
        Date: 2026-09-25
        Name: OnToggle
        Description: 按钮切换回调（参数为按下状态；由装配方决定开面板或执行动作）。
        *****/
        public Action<bool> OnToggle = default!;

        /*****
        Date: 2026-09-25
        Name: Button
        Description: 对应的 ToggleMode 按钮。
        *****/
        public Button Button = default!;
    }

    /*****
    Date: 2026-09-25
    Name: _items
    Description: 已注册菜单项列表。
    *****/
    private readonly List<ItemEntry> _items = new();

    /*****
    Date: 2026-09-25
    Name: MenuBar
    Description: 构造函数；设置按钮间距。
    *****/
    public MenuBar()
    {
        AddThemeConstantOverride("separation", 6);
    }

    /*****
    Date: 2026-09-25
    Name: RegisterItem
    Description: 注册一个菜单项（重复 Id 忽略）并按 SortOrder 重建按钮栏。
    *****/
    public void RegisterItem(MenuItemDef def, Action<bool> onToggle)
    {
        if (_items.Exists(i => i.Def.Id == def.Id)) return;

        _items.Add(new ItemEntry { Def = def, OnToggle = onToggle });
        RebuildButtons();
    }

    /*****
    Date: 2026-09-25
    Name: UnregisterItem
    Description: 注销指定菜单项并重建按钮栏。
    *****/
    public void UnregisterItem(string id)
    {
        if (_items.RemoveAll(i => i.Def.Id == id) > 0)
        {
            RebuildButtons();
        }
    }

    /*****
    Date: 2026-09-25
    Name: SetPressed
    Description: 外部同步某菜单项按钮的按下状态（不触发回调；供编辑模式等外部状态源同步）。
    *****/
    public void SetPressed(string id, bool pressed)
    {
        ItemEntry? entry = _items.Find(i => i.Def.Id == id);
        if (entry != null) entry.Button.ButtonPressed = pressed;
    }

    /*****
    Date: 2026-09-25
    Name: IconMaxWidth
    Description: 图标边长上限（像素）；以主题常量 icon_max_width 约束，超限贴图按比例缩小。
    *****/
    private const int IconMaxWidth = 28;

    /*****
    Date: 2026-09-25
    Name: RebuildButtons
    Description: 按 SortOrder 升序（同序按注册先后）全量重建按钮；配置了 IconPath 且贴图可加载时渲染图标按钮（显示名转为悬停提示），否则渲染文字按钮；零注册时整栏隐藏。
    *****/
    private void RebuildButtons()
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        List<ItemEntry> sorted = _items.OrderBy(i => i.Def.SortOrder).ToList();
        foreach (ItemEntry entry in sorted)
        {
            var button = new Button
            {
                ToggleMode = true,
                CustomMinimumSize = new Vector2(64, 32),
            };

            Texture2D? icon = LoadIcon(entry.Def.IconPath);
            if (icon != null)
            {
                button.Icon = icon;
                button.AddThemeConstantOverride("icon_max_width", IconMaxWidth);
                button.TooltipText = entry.Def.DisplayName;
            }
            else
            {
                button.Text = entry.Def.DisplayName;
            }

            button.Pressed += () => entry.OnToggle(button.ButtonPressed);
            entry.Button = button;
            AddChild(button);
        }

        Visible = _items.Count > 0;
    }

    /*****
    Date: 2026-09-25
    Name: LoadIcon
    Description: 加载菜单图标；路径为空或贴图加载失败返回 null（调用方回退文字按钮）。
    *****/
    private static Texture2D? LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath)) return null;

        var icon = GD.Load<Texture2D>(iconPath);
        if (icon == null) GD.PushWarning($"[MenuBar] 无法加载菜单图标：{iconPath}");
        return icon;
    }
}
