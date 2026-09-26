using Godot;
using RelayStation.Core.Common;
using RelayStation.Core.Items;
using RelayStation.Core.Resources;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-26
Name: ResourceMonitor
Description: 资源监测栏；常驻右上角（时间流速条正下方、贴住右边缘），可折叠（显示/收起）。展开时标题带关键数字（备用零件余额）并给出明细（地上可达零件、散落堆数），**折叠则彻底收起——只剩「资源」二字与展开箭头，不显示任何数字**。数值全部来自真实数据——余额取自库存（GroundItemStore：地上可达零件堆之和）、堆数取自物品注册表；余额或物品堆变化时自动刷新，不逐帧计算。新增资源只需在 Setup 里加一行。注意：与左侧菜单「资源」面板用途不同（后者另有他用、功能未实现），二者互不影响。
*****/
public partial class ResourceMonitor : PanelContainer
{
    /*****
    Date: 2026-09-26
    Name: _store
    Description: 备用零件库存（余额来源）。
    *****/
    private IResourceStore? _store;

    /*****
    Date: 2026-09-26
    Name: _simulation
    Description: 模拟层（散落堆数来源）。
    *****/
    private Simulation? _simulation;

    /*****
    Date: 2026-09-26
    Name: _partsItemId
    Description: 备用零件的物品 Id（只统计该种类的堆）；为 null 时统计全部地面物品堆。
    *****/
    private string? _partsItemId;

    /*****
    Date: 2026-09-26
    Name: _toggleButton
    Description: 折叠开关按钮（文本同时承载关键数字）。
    *****/
    private Button? _toggleButton;

    /*****
    Date: 2026-09-26
    Name: _details
    Description: 展开后的明细容器。
    *****/
    private VBoxContainer? _details;

    /*****
    Date: 2026-09-26
    Name: _balanceLabel
    Description: 地上可达备用零件标签。
    *****/
    private Label? _balanceLabel;

    /*****
    Date: 2026-09-26
    Name: _stacksLabel
    Description: 散落零件堆数标签。
    *****/
    private Label? _stacksLabel;

    /*****
    Date: 2026-09-26
    Name: _expanded
    Description: 当前是否展开。
    *****/
    private bool _expanded = true;

    /*****
    Date: 2026-09-26
    Name: Setup
    Description: 绑定库存与模拟层，构建「标题按钮 + 明细」并订阅余额变化；余额或物品堆变化时自动刷新。
    *****/
    public void Setup(IResourceStore store, Simulation simulation, string? partsItemId)
    {
        _store = store;
        _simulation = simulation;
        _partsItemId = partsItemId;

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        AddChild(box);

        _toggleButton = new Button { Name = "Toggle", Text = "资源", ToggleMode = false, Flat = false };
        _toggleButton.AddThemeFontSizeOverride("font_size", 13);
        _toggleButton.Pressed += ToggleExpanded;
        box.AddChild(_toggleButton);

        _details = new VBoxContainer { Name = "Details" };
        _details.AddThemeConstantOverride("separation", 2);
        box.AddChild(_details);

        _balanceLabel = new Label { Text = "" };
        _balanceLabel.AddThemeFontSizeOverride("font_size", 12);
        _details.AddChild(_balanceLabel);

        _stacksLabel = new Label { Text = "" };
        _stacksLabel.AddThemeFontSizeOverride("font_size", 12);
        _stacksLabel.AddThemeColorOverride("font_color", new Color(0.78f, 0.81f, 0.85f));
        _details.AddChild(_stacksLabel);

        var placeholder = new Label { Text = "（其余资源待扩展）" };
        placeholder.AddThemeFontSizeOverride("font_size", 12);
        placeholder.AddThemeColorOverride("font_color", new Color(0.62f, 0.65f, 0.70f));
        _details.AddChild(placeholder);

        _store.BalanceChanged += Refresh;
        _simulation.Items.Changed += Refresh;
        Refresh();
    }

    /*****
    Date: 2026-09-26
    Name: ToggleExpanded
    Description: 折叠/展开明细（标题按钮始终可见并携带关键数字）。
    *****/
    private void ToggleExpanded()
    {
        _expanded = !_expanded;
        if (_details != null) _details.Visible = _expanded;
        Refresh();
    }

    /*****
    Date: 2026-09-26
    Name: Refresh
    Description: 刷新显示：展开时标题带余额、明细给出地上可达备用零件与散落堆数；**折叠时彻底收起——标题只剩「资源」二字与展开箭头，不显示任何数字**。仅在余额/物品变化或折叠切换时调用，不做逐帧计算。
    *****/
    private void Refresh()
    {
        int balance = _store?.Balance ?? 0;
        int stacks = CountPartsStacks();

        if (_toggleButton != null)
            _toggleButton.Text = _expanded ? $"资源　备用零件 {balance}　▴" : "资源　▾";
        if (_balanceLabel != null)
            _balanceLabel.Text = $"地上可达备用零件：{balance}";
        if (_stacksLabel != null)
            _stacksLabel.Text = $"散落零件堆：{stacks}";
    }

    /*****
    Date: 2026-09-26
    Name: CountPartsStacks
    Description: 统计地上备用零件堆数（按零件 Id 过滤；未配置零件定义时统计全部地面物品堆）。
    *****/
    private int CountPartsStacks()
    {
        if (_simulation == null) return 0;
        if (_partsItemId == null) return _simulation.Items.Stacks.Count;

        int count = 0;
        foreach (ItemStack stack in _simulation.Items.Stacks)
        {
            if (stack.Def.Id == _partsItemId) count++;
        }
        return count;
    }

    /*****
    Date: 2026-09-26
    Name: _ExitTree
    Description: 节点退出时注销事件订阅，防止悬空回调。
    *****/
    public override void _ExitTree()
    {
        if (_store != null) _store.BalanceChanged -= Refresh;
        if (_simulation != null) _simulation.Items.Changed -= Refresh;
    }
}
