namespace RelayStation.Core.Items;

/*****
Date: 2026-09-26
Name: IItemStackPolicy
Description: 自动堆叠策略接口；只回答「这一对（目标组、来源组）能否合并」一个问题。合并的具体条件（同格？相邻？距离阈值？是否要求同一定义之外的其他约束？）尚未确定，故先以接口把判定点隔离出来——ItemRegistry.Compact 只负责搬运机制（满则溢出、空堆回收），策略替换即可改变合并行为，无需改动注册表。
*****/
public interface IItemStackPolicy
{
    /*****
    Date: 2026-09-26
    Name: CanStack
    Description: 判定来源组 source 的若干数量是否可以搬入目标组 target（是否合并由策略决定；实现方可读两组的 Def / Cell / PixelOffset 等只读状态）。
    *****/
    bool CanStack(ItemStack target, ItemStack source);
}
