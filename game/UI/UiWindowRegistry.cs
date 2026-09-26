using Godot;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-26
Name: UiWindowRegistry
Description: UI 窗口注册表（表现层）；登记「浮在地图之上的窗口控件」，供全局输入消费者（当前为相机滚轮缩放/中键平移）判定鼠标是否落在窗口上——落在窗口上的滚轮只作用于该窗口（面板内部滚动、或无可滚动时的无操作），**不得外泄为地图缩放**。
为什么需要：Godot 中滚轮事件即便经过 MouseFilter=Stop 的面板也不会被标记为已处理（滚动容器到达顶/底边界时同样不消费），事件会继续落到 _UnhandledInput；故由相机侧统一守卫，而非逐个面板拦截。
注册约定：窗口在装配处注册（Main 的 BuildUi/SetupMenus、PanelHost 的惰性工厂），节点退出时注销；**鼠标穿透型元素（世界提示 WorldHint 等）不注册**。
*****/
public static class UiWindowRegistry
{
    /*****
    Date: 2026-09-26
    Name: _windows
    Description: 已注册窗口列表（引用存活期间有效；节点释放后由 IsInstanceValid 兜底跳过）。
    *****/
    private static readonly List<Control> _windows = new();

    /*****
    Date: 2026-09-26
    Name: Register
    Description: 注册一个窗口控件（重复注册忽略）。
    *****/
    public static void Register(Control window)
    {
        if (_windows.Contains(window)) return;
        _windows.Add(window);
    }

    /*****
    Date: 2026-09-26
    Name: Unregister
    Description: 注销窗口控件（节点退出树时调用）。
    *****/
    public static void Unregister(Control window) => _windows.Remove(window);

    /*****
    Date: 2026-09-26
    Name: IsOverWindow
    Description: 判定指定视口坐标是否落在任一「有效、可见且非零尺寸」的已注册窗口内；供相机在缩放/平移前调用。
    *****/
    public static bool IsOverWindow(Vector2 viewportPosition)
    {
        foreach (Control window in _windows)
        {
            if (!GodotObject.IsInstanceValid(window) || !window.IsInsideTree()) continue;
            if (!window.IsVisibleInTree()) continue;
            if (window.Size.X <= 0f || window.Size.Y <= 0f) continue;
            if (window.GetGlobalRect().HasPoint(viewportPosition)) return true;
        }
        return false;
    }
}