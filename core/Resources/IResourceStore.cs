using Godot;

namespace RelayStation.Core.Resources;

/*****
Date: 2026-09-25
Name: IResourceStore
Description: 资源库存接口（开发计划 §12 预留挂点的最简落地）；以「备用零件」计数的占位资源系统，提供建造/拆除等玩法的消耗与返还校验，阶段二可替换为真实多资源实现。生产运行时由 GroundItemStore 实现——库存即「地上可达的零件堆」，消耗与返还都真实落在物品堆上；ResourceStore（抽象池）仅留给引擎外测试与开发者占位。
*****/
public interface IResourceStore
{
    /*****
    Date: 2026-09-25
    Name: Balance
    Description: 当前备用零件余额。
    *****/
    int Balance { get; }

    /*****
    Date: 2026-09-25
    Name: TrySpend
    Description: 尝试扣除指定数量的备用零件；余额充足时扣除并返回 true，不足（或数量非法）时余额不变并返回 false。
    *****/
    bool TrySpend(int amount);

    /*****
    Date: 2026-09-26
    Name: Add
    Description: 增加备用零件数量（如拆除返还）；非正数忽略。**cell 为零件落地的格子**——地上物品必须有物理落点（如拆掉的设施原位），抽象库存实现可忽略该参数。
    *****/
    void Add(int amount, Vector2I cell);

    /*****
    Date: 2026-09-25
    Name: BalanceChanged
    Description: 余额发生变化时触发的事件（供 UI 刷新显示）。
    *****/
    event Action? BalanceChanged;
}
