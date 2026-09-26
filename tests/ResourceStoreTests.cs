using Godot;
using RelayStation.Core.Resources;
using Xunit;

/*****
Date: 2026-09-25
Name: ResourceStoreTests
Description: 占位资源系统单元测试；验证初始余额、足额扣减、不足拒绝（余额不变）、返还增加、非法数量忽略与余额变化事件。
*****/
namespace RelayStation.Tests;

public sealed class ResourceStoreTests
{
    /*****
    Date: 2026-09-25
    Name: Constructor_NegativeInitial_ClampsToZero
    Description: 负数初始余额钳制为 0。
    *****/
    [Fact]
    public void Constructor_NegativeInitial_ClampsToZero()
    {
        var store = new ResourceStore(-10);
        Assert.Equal(0, store.Balance);
    }

    /*****
    Date: 2026-09-25
    Name: TrySpend_Sufficient_DeductsAndReturnsTrue
    Description: 余额充足时扣减成功且余额正确更新。
    *****/
    [Fact]
    public void TrySpend_Sufficient_DeductsAndReturnsTrue()
    {
        var store = new ResourceStore(100);
        bool spent = store.TrySpend(30);
        Assert.True(spent);
        Assert.Equal(70, store.Balance);
    }

    /*****
    Date: 2026-09-25
    Name: TrySpend_Insufficient_ReturnsFalseAndKeepsBalance
    Description: 余额不足时返回 false 且余额不变。
    *****/
    [Fact]
    public void TrySpend_Insufficient_ReturnsFalseAndKeepsBalance()
    {
        var store = new ResourceStore(20);
        bool spent = store.TrySpend(30);
        Assert.False(spent);
        Assert.Equal(20, store.Balance);
    }

    /*****
    Date: 2026-09-25
    Name: TrySpend_NonPositiveAmount_ReturnsFalse
    Description: 非正数扣除请求返回 false 且余额不变。
    *****/
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void TrySpend_NonPositiveAmount_ReturnsFalse(int amount)
    {
        var store = new ResourceStore(50);
        bool spent = store.TrySpend(amount);
        Assert.False(spent);
        Assert.Equal(50, store.Balance);
    }

    /*****
    Date: 2026-09-25
    Name: Add_Positive_IncreasesBalance
    Description: 返还增加余额（对应拆除返还场景，返还值由调用方按 50% 向下取整计算后传入）。
    *****/
    [Fact]
    public void Add_Positive_IncreasesBalance()
    {
        var store = new ResourceStore(10);
        store.Add(15, Vector2I.Zero);
        Assert.Equal(25, store.Balance);
    }

    /*****
    Date: 2026-09-25
    Name: Add_NonPositive_Ignored
    Description: 非正数返还被忽略。
    *****/
    [Fact]
    public void Add_NonPositive_Ignored()
    {
        var store = new ResourceStore(10);
        store.Add(0, Vector2I.Zero);
        store.Add(-3, Vector2I.Zero);
        Assert.Equal(10, store.Balance);
    }

    /*****
    Date: 2026-09-25
    Name: BalanceChanged_FiresOnSpendAndAdd
    Description: 扣减与增加成功时触发余额变化事件，失败操作不触发。
    *****/
    [Fact]
    public void BalanceChanged_FiresOnSpendAndAdd()
    {
        var store = new ResourceStore(10);
        int fired = 0;
        store.BalanceChanged += () => fired++;

        store.TrySpend(5);
        Assert.Equal(1, fired);

        store.TrySpend(100);
        Assert.Equal(1, fired);

        store.Add(5, Vector2I.Zero);
        Assert.Equal(2, fired);
    }
}
