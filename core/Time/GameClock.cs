namespace RelayStation.Core.Time;

/*****
Date: 2026-09-06
Name: GameClock
Description: 游戏时钟默认实现；按「1 现实秒 = 1 游戏分钟 × 倍速」推进累计游戏分钟，并在每累计满 1 整分钟时触发 GameMinuteElapsed 事件。
*****/
public sealed class GameClock : IGameClock
{
    /*****
    Date: 2026-09-06
    Name: _totalGameMinutes
    Description: 累计游戏分钟（含小数）。
    *****/
    private double _totalGameMinutes;

    /*****
    Date: 2026-09-06
    Name: TotalGameMinutes
    Description: 已累计的游戏总分钟数。
    *****/
    public double TotalGameMinutes => _totalGameMinutes;

    /*****
    Date: 2026-09-06
    Name: IsPaused
    Description: 是否处于暂停状态。
    *****/
    public bool IsPaused { get; private set; }

    /*****
    Date: 2026-09-06
    Name: SpeedMultiplier
    Description: 当前时间倍速（1 / 2 / 4）。
    *****/
    public int SpeedMultiplier { get; private set; } = 1;

    /*****
    Date: 2026-09-06
    Name: GameMinuteElapsed
    Description: 每累计满 1 个游戏分钟时触发的事件；参数为该分钟消耗的现实秒数（= 本次现实秒数 / 本次游戏分钟数）。
    *****/
    public event Action<double>? GameMinuteElapsed;

    /*****
    Date: 2026-09-06
    Name: SetPaused
    Description: 设置暂停状态；暂停时 Advance 不推进时间也不触发分钟事件。
    *****/
    public void SetPaused(bool paused) => IsPaused = paused;

    /*****
    Date: 2026-09-06
    Name: SetSpeed
    Description: 设置时间倍速；仅允许 1 / 2 / 4，其他值抛出 ArgumentOutOfRangeException。
    *****/
    public void SetSpeed(int multiplier)
    {
        if (multiplier is not (1 or 2 or 4))
            throw new ArgumentOutOfRangeException(nameof(multiplier), "倍速仅允许 1 / 2 / 4。");
        SpeedMultiplier = multiplier;
    }

    /*****
    Date: 2026-09-06
    Name: Advance
    Description: 按现实秒数推进游戏时间：游戏分钟 += 现实秒数 × 倍速；每跨过 1 个整分钟边界触发一次 GameMinuteElapsed。暂停或非正数输入时不推进。
    *****/
    public void Advance(double realSeconds)
    {
        if (IsPaused || realSeconds <= 0) return;

        double deltaGameMinutes = realSeconds * SpeedMultiplier;
        long wholeBefore = (long)Math.Floor(_totalGameMinutes);
        _totalGameMinutes += deltaGameMinutes;
        long wholeAfter = (long)Math.Floor(_totalGameMinutes);
        long elapsedMinutes = wholeAfter - wholeBefore;

        if (elapsedMinutes > 0 && GameMinuteElapsed != null)
        {
            double realSecondsPerMinute = realSeconds / deltaGameMinutes;
            for (long i = 0; i < elapsedMinutes; i++) GameMinuteElapsed.Invoke(realSecondsPerMinute);
        }
    }
}
