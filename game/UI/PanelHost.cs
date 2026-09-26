using Godot;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-25
Name: PanelHost
Description: 互斥面板宿主（HOI4 式单窗口面板区）；面板完全由注册驱动（禁止写死数量）：RegisterPanel 以惰性工厂登记（首次打开才实例化），Toggle 互斥单开（开新关旧、再点同项关闭），面板尺寸随内容 CustomMinimumSize 自适应。位置由装配方锚定（建议菜单栏下方）。
*****/
public partial class PanelHost : Control
{
    /*****
    Date: 2026-09-25
    Name: PanelVisibilityChanged
    Description: 面板可见性变化事件（参数：面板 Id、是否可见）；供菜单栏按钮状态同步。
    *****/
    public event Action<string, bool>? PanelVisibilityChanged;

    /*****
    Date: 2026-09-25
    Name: _factories
    Description: 面板 Id → 惰性创建工厂。
    *****/
    private readonly Dictionary<string, Func<Control>> _factories = new();

    /*****
    Date: 2026-09-25
    Name: _panels
    Description: 已实例化面板缓存（Id → 面板）。
    *****/
    private readonly Dictionary<string, Control> _panels = new();

    /*****
    Date: 2026-09-25
    Name: _activeId
    Description: 当前打开的面板 Id；关闭时为 null。
    *****/
    private string? _activeId;

    /*****
    Date: 2026-09-25
    Name: RegisterPanel
    Description: 注册面板（同 Id 覆盖工厂；未实例化的面板下次打开生效）。
    *****/
    public void RegisterPanel(string id, Func<Control> panelFactory)
    {
        _factories[id] = panelFactory;
    }

    /*****
    Date: 2026-09-25
    Name: Toggle
    Description: 互斥切换面板：当前已开则关闭；否则关闭旧面板并打开指定面板（首次打开惰性实例化）；未注册的 Id 仅关闭当前面板。
    *****/
    public void Toggle(string id)
    {
        if (_activeId == id)
        {
            CloseActive();
            return;
        }

        CloseActive();
        if (!_factories.TryGetValue(id, out Func<Control>? factory)) return;

        if (!_panels.TryGetValue(id, out Control? panel))
        {
            panel = factory();
            panel.Visible = false;
            AddChild(panel);
            _panels[id] = panel;
            // 让面板参与「滚轮只作用于所在窗口」的守卫（相机据此忽略窗口上的滚轮）
            UiWindowRegistry.Register(panel);
        }

        panel.Visible = true;
        _activeId = id;
        PanelVisibilityChanged?.Invoke(id, true);
    }

    /*****
    Date: 2026-09-25
    Name: Open
    Description: 幂等打开指定面板（已打开则不做任何事，不触发可见性事件）；供外部状态源同步使用（如 F12 进入建造模式时把「建造」面板一并打开），避免误触发 Toggle 的「再点关闭」语义。
    *****/
    public void Open(string id)
    {
        if (_activeId == id) return;
        Toggle(id);
    }

    /*****
    Date: 2026-09-25
    Name: CloseAll
    Description: 关闭当前打开的面板（若有）。
    *****/
    public void CloseAll() => CloseActive();

    /*****
    Date: 2026-09-25
    Name: CloseActive
    Description: 关闭当前面板并发出可见性事件。
    *****/
    private void CloseActive()
    {
        if (_activeId == null) return;
        if (_panels.TryGetValue(_activeId, out Control? panel))
        {
            panel.Visible = false;
        }
        PanelVisibilityChanged?.Invoke(_activeId, false);
        _activeId = null;
    }

    /*****
    Date: 2026-09-26
    Name: _ExitTree
    Description: 节点退出时把已实例化的面板从窗口注册表注销，避免悬空引用。
    *****/
    public override void _ExitTree()
    {
        foreach (Control panel in _panels.Values) UiWindowRegistry.Unregister(panel);
    }
}
