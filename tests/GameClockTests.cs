using RelayStation.Core.Time;
using Xunit;

namespace RelayStation.Tests;

/*****
Date: 2026-09-06
Name: GameClockTests
Description: 游戏时钟单元测试；验证「1 现实秒 = 1 游戏分钟」换算、倍速加倍、暂停不推进、分钟事件按整分钟触发与非法倍速报错。
*****/
public sealed class GameClockTests
{
    /*****
    Date: 2026-09-06
    Name: Advance_At1x_ConvertsRealSecondsToGameMinutes
    Description: 1× 倍速下现实秒数原值换算为游戏分钟。
    *****/
    [Fact]
    public void Advance_At1x_ConvertsRealSecondsToGameMinutes()
    {
        var clock = new GameClock();
        clock.Advance(1.0);
        Assert.Equal(1.0, clock.TotalGameMinutes, 5);
        clock.Advance(0.5);
        Assert.Equal(1.5, clock.TotalGameMinutes, 5);
    }

    /*****
    Date: 2026-09-06
    Name: Advance_At2x_DoublesGameMinutes
    Description: 2× 倍速下游戏分钟按倍速加倍。
    *****/
    [Fact]
    public void Advance_At2x_DoublesGameMinutes()
    {
        var clock = new GameClock();
        clock.SetSpeed(2);
        clock.Advance(1.0);
        Assert.Equal(2.0, clock.TotalGameMinutes, 5);
    }

    /*****
    Date: 2026-09-06
    Name: Advance_At4x_QuadruplesGameMinutes
    Description: 4× 倍速下游戏分钟按倍速四倍。
    *****/
    [Fact]
    public void Advance_At4x_QuadruplesGameMinutes()
    {
        var clock = new GameClock();
        clock.SetSpeed(4);
        clock.Advance(1.0);
        Assert.Equal(4.0, clock.TotalGameMinutes, 5);
    }

    /*****
    Date: 2026-09-06
    Name: Advance_Paused_DoesNotAdvance
    Description: 暂停时时间不推进。
    *****/
    [Fact]
    public void Advance_Paused_DoesNotAdvance()
    {
        var clock = new GameClock();
        clock.SetPaused(true);
        clock.Advance(10.0);
        Assert.Equal(0.0, clock.TotalGameMinutes, 5);
        Assert.True(clock.IsPaused);
    }

    /*****
    Date: 2026-09-06
    Name: Advance_FiresEventForEachWholeMinute
    Description: 分钟事件按跨过的整分钟数触发：1 秒(1×)=1 次；0.5 秒不触发；再 0.5 秒跨过整分再触发 1 次。
    *****/
    [Fact]
    public void Advance_FiresEventForEachWholeMinute()
    {
        var clock = new GameClock();
        int fired = 0;
        clock.GameMinuteElapsed += _ => fired++;

        clock.Advance(1.0);
        clock.Advance(0.5);
        clock.Advance(0.5);

        Assert.Equal(2, fired);
    }

    /*****
    Date: 2026-09-06
    Name: Advance_At4x_FiresFourMinuteEvents
    Description: 4× 倍速下 1 现实秒 = 4 游戏分钟，触发 4 次分钟事件。
    *****/
    [Fact]
    public void Advance_At4x_FiresFourMinuteEvents()
    {
        var clock = new GameClock();
        clock.SetSpeed(4);
        int fired = 0;
        clock.GameMinuteElapsed += _ => fired++;

        clock.Advance(1.0);

        Assert.Equal(4, fired);
    }

    /*****
    Date: 2026-09-06
    Name: SetSpeed_InvalidValue_Throws
    Description: 非 1/2/4 的倍速值抛出 ArgumentOutOfRangeException。
    *****/
    [Fact]
    public void SetSpeed_InvalidValue_Throws()
    {
        var clock = new GameClock();
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetSpeed(3));
    }
}
