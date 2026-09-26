using Godot;
using RelayStation.Core.Events;
using RelayStation.Game.Autoloads;

namespace RelayStation.Game.Cutscenes;

/*****
Date: 2026-09-25
Name: CutsceneTriggerRegistry
Description: 剧情动画触发器注册表；集中登记各类触发条件（新游戏/游戏时钟分钟/游戏事件）并分发到动画播放或独立播曲。提供统一的注销（UnregisterAll）供场景退出时清理订阅，防止悬空回调。
*****/
public sealed class CutsceneTriggerRegistry
{
    /*****
    Date: 2026-09-25
    Name: NewGame
    Description: 新游戏开始事件（由 Main 在新游戏首帧就绪后触发一次）。
    *****/
    public event Action? NewGame;

    /*****
    Date: 2026-09-25
    Name: _director
    Description: 关联的导演器。
    *****/
    private readonly CutsceneDirector _director;

    /*****
    Date: 2026-09-25
    Name: _detachers
    Description: 各触发器注册的退订回调列表。
    *****/
    private readonly List<Action> _detachers = new();

    /*****
    Date: 2026-09-25
    Name: CutsceneTriggerRegistry
    Description: 构造函数；绑定导演器。
    *****/
    public CutsceneTriggerRegistry(CutsceneDirector director)
    {
        _director = director;
    }

    /*****
    Date: 2026-09-25
    Name: Register
    Description: 登记一个触发器（挂接其触发条件）。
    *****/
    public void Register(CutsceneTrigger trigger) => trigger.Attach(this);

    /*****
    Date: 2026-09-25
    Name: OnNewGame
    Description: 订阅新游戏事件；返回退订回调。
    *****/
    public Action OnNewGame(Action handler)
    {
        NewGame += handler;
        return () => NewGame -= handler;
    }

    /*****
    Date: 2026-09-25
    Name: OnClockMinute
    Description: 订阅「累计游戏分钟达到指定值」条件（一次性触发）；返回退订回调。时钟不可用时返回空回调。
    *****/
    public Action OnClockMinute(double fireAtGameMinutes, Action handler)
    {
        var clock = GameRoot.Instance?.Simulation?.Clock;
        if (clock == null) return () => { };

        bool fired = false;
        void OnMinute(double _)
        {
            if (fired || clock.TotalGameMinutes < fireAtGameMinutes) return;
            fired = true;
            handler();
        }

        clock.GameMinuteElapsed += OnMinute;
        return () => clock.GameMinuteElapsed -= OnMinute;
    }

    /*****
    Date: 2026-09-25
    Name: OnGameEvent
    Description: 订阅指定类型的游戏事件（事件总线）；返回退订回调。总线不可用时返回空回调。
    *****/
    public Action OnGameEvent<TEvent>(Action handler) where TEvent : IGameEvent
    {
        var bus = GameRoot.Instance?.Simulation?.EventBus;
        if (bus == null) return () => { };

        void OnEvent(TEvent _) => handler();
        bus.Subscribe<TEvent>(OnEvent);
        return () => bus.Unsubscribe<TEvent>(OnEvent);
    }

    /*****
    Date: 2026-09-25
    Name: FireNewGame
    Description: 触发新游戏事件（供 Main 调用）。
    *****/
    public void FireNewGame() => NewGame?.Invoke();

    /*****
    Date: 2026-09-25
    Name: Play
    Description: 播放一段动画剧本。
    *****/
    public void Play(CutsceneScript script) => _director.Play(script);

    /*****
    Date: 2026-09-25
    Name: PlayMusic
    Description: 独立播放一首曲目（不经动画序列）。
    *****/
    public void PlayMusic(string trackId) => _director.PlayMusic(trackId);

    /*****
    Date: 2026-09-25
    Name: AddDetacher
    Description: 触发器登记退订回调（供 UnregisterAll 统一清理）。
    *****/
    internal void AddDetacher(Action detach) => _detachers.Add(detach);

    /*****
    Date: 2026-09-25
    Name: UnregisterAll
    Description: 注销全部触发条件（清空新游戏事件并执行所有退订回调）；场景退出时调用。
    *****/
    public void UnregisterAll()
    {
        NewGame = null;
        foreach (Action detach in _detachers) detach();
        _detachers.Clear();
    }
}
