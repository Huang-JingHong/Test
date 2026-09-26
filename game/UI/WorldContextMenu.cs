using Godot;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-26
Name: WorldContextAction
Description: 世界右键菜单的一条动作（注册式条目）：`Id` 仅用于内部编号与调试显示，`Label` 为按钮文本，`Enabled=false` 时置灰，`Tooltip` 说明置灰原因（如「背包已满」），`OnSelected` 为点击回调。
*****/
public sealed record WorldContextAction(string Id, string Label, bool Enabled, string Tooltip, Action OnSelected);

/*****
Date: 2026-09-26
Name: WorldContextMenu
Description: 世界右键上下文菜单（基于 Godot 内置 PopupMenu：点击外部/ESC 自动关闭、条目可置灰并带提示，零自绘成本）。由装配方（Main）在「有选中人物 + 鼠标悬停命中物品」时调用 `Open` 弹出；动作清单为**注册式**——新增按钮只需在 Main 的清单里追加一项，零改本类。
窗口为**嵌入式子窗口**（不是 Control，故不能登记 UiWindowRegistry）：依赖子窗口自身消费鼠标事件（滚轮不会穿透为地图缩放，已由探针验证）。
*****/
public partial class WorldContextMenu : PopupMenu
{
    /*****
    Date: 2026-09-26
    Name: _handlers
    Description: 当前弹出的条目回调（与条目编号一一对应；每次 Open 重建）。
    *****/
    private readonly List<Action> _handlers = new();

    /*****
    Date: 2026-09-26
    Name: Setup
    Description: 订阅条目点击信号（只订阅一次）。
    *****/
    public void Setup()
    {
        IdPressed += OnIdPressed;
    }

    /*****
    Date: 2026-09-26
    Name: Open
    Description: 在指定视口坐标弹出菜单：重建条目（文本/置灰/提示）与回调，随后 `Popup(bounds)` 显示。动作清单为空时不弹出（避免空菜单）。
    *****/
    public void Open(Vector2 viewportPosition, IReadOnlyList<WorldContextAction> actions)
    {
        Clear();
        _handlers.Clear();

        int index = 0;
        foreach (WorldContextAction action in actions)
        {
            int id = index;
            AddItem(action.Label, id);
            if (!action.Enabled)
            {
                SetItemDisabled(id, true);
                if (!string.IsNullOrEmpty(action.Tooltip)) SetItemTooltip(id, action.Tooltip);
            }
            _handlers.Add(action.OnSelected);
            index++;
        }

        if (_handlers.Count == 0) return;

        Popup(new Rect2I((Vector2I)viewportPosition, Vector2I.Zero));
    }

    /*****
    Date: 2026-09-26
    Name: OnIdPressed
    Description: 条目点击回调：按编号取出注册的回调执行（编号越界忽略）。
    *****/
    private void OnIdPressed(long id)
    {
        int index = (int)id;
        if (index < 0 || index >= _handlers.Count) return;
        _handlers[index]();
    }

    /*****
    Date: 2026-09-26
    Name: IsOpen
    Description: 菜单当前是否可见；供装配方在右键前判断是否需要先关闭旧菜单。
    *****/
    public bool IsOpen => Visible;

    /*****
    Date: 2026-09-26
    Name: _ExitTree
    Description: 退出树时退订信号，防止悬空回调。
    *****/
    public override void _ExitTree()
    {
        IdPressed -= OnIdPressed;
    }
}