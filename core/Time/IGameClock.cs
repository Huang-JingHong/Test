namespace RelayStation.Core.Time;

/*****
Date: 2026-09-06
Name: IGameClock
Description: 游戏时钟接口；管理累计游戏分钟、暂停与倍速，驱动游戏时间的推进。
*****/
public interface IGameClock
{
    /*****
    Date: 2026-09-06
    Name: TotalGameMinutes
    Description: 已累计的游戏总分钟数。
    *****/
    double TotalGameMinutes { get; }

    /*****
    Date: 2026-09-06
    Name: IsPaused
    Description: 是否处于暂停状态。
    *****/
    bool IsPaused { get; }

    /*****
    Date: 2026-09-06
    Name: SpeedMultiplier
    Description: 当前时间倍速；取值范围 1 / 2 / 4。
    *****/
    int SpeedMultiplier { get; }

    /*****
    Date: 2026-09-06
    Name: SetPaused
    Description: 设置暂停状态。
    *****/
    void SetPaused(bool paused);

    /*****
    Date: 2026-09-06
    Name: SetSpeed
    Description: 设置时间倍速；仅允许 1 / 2 / 4，其他值抛出异常。
    *****/
    void SetSpeed(int multiplier);

    /*****
    Date: 2026-09-06
    Name: Advance
    Description: 按现实秒数推进游戏时间（由 GameRoot 驱动调用）；正常速度下 1 现实秒 = 1 游戏分钟。
    *****/
    void Advance(double realSeconds);

    /*****
    Date: 2026-09-06
    Name: GameMinuteElapsed
    Description: 每累计满 1 个游戏分钟时触发的事件；参数为该分钟消耗的现实秒数。
    *****/
    event Action<double>? GameMinuteElapsed;
}
