using Godot;
using RelayStation.Core.Items;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-26
Name: ItemPanel
Description: 材料信息栏；显示当前选中的地面物品。**按物品种类整合**——选中的多个堆先经 ItemSelectionSummary 合并成「种类汇总行」（名称 + 合计数量 + 堆数），再按堆列出明细（所在格 + 数量/单组上限），故框选十几堆零件时也能一眼看到总量。空选择时整栏隐藏。挂在右上角右侧栏（资源监测栏之下），与中间偏右的设施/人物面板互不遮挡。
*****/
public partial class ItemPanel : PanelContainer
{
    /*****
    Date: 2026-09-26
    Name: MaxStackRows
    Description: 明细最多列出的堆数；超出时以「…另有 N 堆」收尾，避免面板被拉得过长。
    *****/
    private const int MaxStackRows = 10;

    /*****
    Date: 2026-09-26
    Name: _summaryLabel
    Description: 汇总提示标签（已选堆数与合计数量）。
    *****/
    private Label? _summaryLabel;

    /*****
    Date: 2026-09-26
    Name: _groupBox
    Description: 种类汇总行容器（按需重建）。
    *****/
    private VBoxContainer? _groupBox;

    /*****
    Date: 2026-09-26
    Name: _stackBox
    Description: 逐堆明细容器（按需重建）。
    *****/
    private VBoxContainer? _stackBox;

    /*****
    Date: 2026-09-26
    Name: Setup
    Description: 构建面板控件（标题 + 汇总 + 种类汇总 + 逐堆明细）并初始隐藏。
    *****/
    public void Setup()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        AddChild(box);

        var title = new Label { Text = "材料" };
        title.AddThemeFontSizeOverride("font_size", 16);

        _summaryLabel = new Label { Text = "" };
        _summaryLabel.AddThemeFontSizeOverride("font_size", 13);
        _summaryLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));

        _groupBox = new VBoxContainer();
        _groupBox.AddThemeConstantOverride("separation", 4);
        _stackBox = new VBoxContainer();
        _stackBox.AddThemeConstantOverride("separation", 2);

        box.AddChild(title);
        box.AddChild(_summaryLabel);
        box.AddChild(_groupBox);
        box.AddChild(_stackBox);
        Visible = false;
    }

    /*****
    Date: 2026-09-26
    Name: ShowSelection
    Description: 显示选择集：按种类整合出汇总行 + 逐堆明细；空选择时隐藏整栏。
    *****/
    public void ShowSelection(IReadOnlyList<ItemStack> selection)
    {
        if (selection.Count == 0)
        {
            Visible = false;
            ClearChildren(_groupBox);
            ClearChildren(_stackBox);
            return;
        }

        Visible = true;

        if (_summaryLabel != null)
            _summaryLabel.Text = $"已选 {selection.Count} 堆（共 {selection.Sum(s => s.Count)} 个）";

        if (_groupBox != null)
        {
            ClearChildren(_groupBox);
            foreach (ItemGroupSummary group in ItemSelectionSummary.Aggregate(selection))
            {
                var label = new Label { Text = $"{group.DisplayName}　{group.TotalCount}（{group.StackCount} 堆 / 单组上限 {group.MaxStack}）" };
                label.AddThemeFontSizeOverride("font_size", 14);
                _groupBox.AddChild(label);
            }
        }

        if (_stackBox != null)
        {
            ClearChildren(_stackBox);
            foreach (ItemStack stack in selection.Take(MaxStackRows))
            {
                var label = new Label { Text = $"（{stack.Cell.X}, {stack.Cell.Y}）　{stack.Count}/{stack.MaxStack}" };
                label.AddThemeFontSizeOverride("font_size", 12);
                label.AddThemeColorOverride("font_color", new Color(0.78f, 0.81f, 0.85f));
                _stackBox.AddChild(label);
            }
            if (selection.Count > MaxStackRows)
            {
                var more = new Label { Text = $"…另有 {selection.Count - MaxStackRows} 堆" };
                more.AddThemeFontSizeOverride("font_size", 12);
                more.AddThemeColorOverride("font_color", new Color(0.78f, 0.81f, 0.85f));
                _stackBox.AddChild(more);
            }
        }
    }

    /*****
    Date: 2026-09-26
    Name: ClearChildren
    Description: 立即清空容器子节点（先移除再释放，保证本帧即刻从画面上消失，仅 QueueFree 会等到帧末）。
    *****/
    private static void ClearChildren(Node? parent)
    {
        if (parent == null) return;
        foreach (Node child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
