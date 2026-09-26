namespace RelayStation.Core.Events;

/*****
Date: 2026-09-06
Name: EventBus
Description: 事件总线默认实现；按事件类型维护处理器列表，提供发布/订阅/取消订阅能力。非线程安全，约定仅在主线程（或测试线程）使用。
*****/
public sealed class EventBus : IEventBus
{
    /*****
    Date: 2026-09-06
    Name: _handlers
    Description: 事件类型到处理器列表的映射表。
    *****/
    private readonly Dictionary<Type, List<object>> _handlers = new();

    /*****
    Date: 2026-09-06
    Name: Publish
    Description: 发布一个游戏事件；按订阅顺序同步调用该事件类型的所有处理器（遍历快照，允许处理器内增删订阅）。
    *****/
    public void Publish<TEvent>(TEvent e) where TEvent : IGameEvent
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var list)) return;
        var snapshot = list.ToArray();
        foreach (var handler in snapshot)
        {
            if (handler is Action<TEvent> action) action(e);
        }
    }

    /*****
    Date: 2026-09-06
    Name: Subscribe
    Description: 订阅指定类型的游戏事件；同一处理器重复订阅时只保留一份。
    *****/
    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var list))
        {
            list = new List<object>();
            _handlers[typeof(TEvent)] = list;
        }
        if (!list.Contains(handler)) list.Add(handler);
    }

    /*****
    Date: 2026-09-06
    Name: Unsubscribe
    Description: 取消订阅指定类型的游戏事件；处理器不存在时静默忽略。
    *****/
    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent
    {
        if (_handlers.TryGetValue(typeof(TEvent), out var list)) list.Remove(handler);
    }
}
