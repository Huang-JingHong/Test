namespace RelayStation.Core.Events;

/*****
Date: 2026-09-06
Name: IEventBus
Description: 事件总线接口；提供事件的发布与订阅能力，解耦模拟层与表现层。
*****/
public interface IEventBus
{
    /*****
    Date: 2026-09-06
    Name: Publish
    Description: 发布一个游戏事件，通知所有已订阅该事件类型的处理器。
    *****/
    void Publish<TEvent>(TEvent e) where TEvent : IGameEvent;

    /*****
    Date: 2026-09-06
    Name: Subscribe
    Description: 订阅指定类型的游戏事件；事件发布时回调传入的处理器。
    *****/
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent;

    /*****
    Date: 2026-09-06
    Name: Unsubscribe
    Description: 取消订阅指定类型的游戏事件，移除对应的处理器。
    *****/
    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent;
}
