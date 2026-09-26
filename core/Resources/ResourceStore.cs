using Godot;

namespace RelayStation.Core.Resources;

/*****
Date: 2026-09-25
Name: ResourceStore
Description: 「备用零件」占位资源系统的最简实现；单一 int 余额，线程外单次操作。**仅供引擎外单元测试与开发者占位使用**——生产运行时的库存是地上零件堆（见 GroundItemStore），本实现没有任何物理落点，故会忽略 Add 的位置参数。
*****/
public sealed class ResourceStore : IResourceStore
{
    /*****
    Date: 2026-09-25
    Name: _balance
    Description: 当前备用零件余额。
    *****/
    private int _balance;

    /*****
    Date: 2026-09-25
    Name: BalanceChanged
    Description: 余额发生变化时触发的事件（供 UI 刷新显示）。
    *****/
    public event Action? BalanceChanged;

    /*****
    Date: 2026-09-25
    Name: ResourceStore
    Description: 构造函数；以指定初始余额创建库存（默认 0）。
    *****/
    public ResourceStore(int initialBalance = 0)
    {
        _balance = initialBalance < 0 ? 0 : initialBalance;
    }

    /*****
    Date: 2026-09-25
    Name: Balance
    Description: 当前备用零件余额。
    *****/
    public int Balance => _balance;

    /*****
    Date: 2026-09-25
    Name: TrySpend
    Description: 尝试扣除指定数量的备用零件；数量非法（≤0）或余额不足时返回 false 且余额不变，成功时扣除并触发 BalanceChanged。
    *****/
    public bool TrySpend(int amount)
    {
        if (amount <= 0 || _balance < amount) return false;
        _balance -= amount;
        BalanceChanged?.Invoke();
        return true;
    }

    /*****
    Date: 2026-09-26
    Name: Add
    Description: 增加备用零件数量（如拆除返还）；非正数忽略，成功时触发 BalanceChanged。cell 参数（零件落点）对本抽象库存无意义，予以忽略。
    *****/
    public void Add(int amount, Vector2I cell)
    {
        if (amount <= 0) return;
        _balance += amount;
        BalanceChanged?.Invoke();
    }
}
