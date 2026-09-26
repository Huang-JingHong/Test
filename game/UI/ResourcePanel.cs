using Godot;
using RelayStation.Game.Autoloads;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-25
Name: ResourcePanel
Description: 资源面板（占位框架 + 真实数据）；显示备用零件余额并订阅变化实时刷新，结构可扩展后续资源条目。
*****/
public partial class ResourcePanel : PanelContainer
{
    /*****
    Date: 2026-09-25
    Name: _partsLabel
    Description: 备用零件余额标签。
    *****/
    private Label? _partsLabel;

    /*****
    Date: 2026-09-25
    Name: Setup
    Description: 构建标题与余额行，订阅库存变化实时刷新。
    *****/
    public void Setup()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        AddChild(box);

        var titleLabel = new Label { Text = "资源" };
        titleLabel.AddThemeFontSizeOverride("font_size", 18);
        box.AddChild(titleLabel);

        _partsLabel = new Label { Text = "备用零件：0" };
        box.AddChild(_partsLabel);
        box.AddChild(new Label { Text = "（其余资源待扩展）", CustomMinimumSize = new Vector2(180, 0) });

        if (GameRoot.Instance?.ResourceStore is { } store)
        {
            RefreshParts();
            store.BalanceChanged += OnBalanceChanged;
        }
    }

    /*****
    Date: 2026-09-25
    Name: OnBalanceChanged
    Description: 余额变化时刷新显示。
    *****/
    private void OnBalanceChanged() => RefreshParts();

    /*****
    Date: 2026-09-25
    Name: RefreshParts
    Description: 刷新备用零件余额文本。
    *****/
    private void RefreshParts()
    {
        if (_partsLabel != null)
        {
            _partsLabel.Text = $"备用零件（地上可达）：{GameRoot.Instance?.ResourceStore?.Balance ?? 0}";
        }
    }

    /*****
    Date: 2026-09-25
    Name: _ExitTree
    Description: 节点退出时注销事件订阅，防止悬空回调。
    *****/
    public override void _ExitTree()
    {
        if (GameRoot.Instance?.ResourceStore is { } store)
        {
            store.BalanceChanged -= OnBalanceChanged;
        }
    }
}
