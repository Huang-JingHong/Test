namespace RelayStation.Core.Items;

/*****
Date: 2026-09-26
Name: NoStackPolicy
Description: 默认自动堆叠策略——不做任何合并（恒返回 false）。合并条件尚未确定，故默认行为等于「维持现状」：Compact 调用成为无副作用空操作，仅把自动堆叠的框架预留出来，待条件明确后新增策略并显式注入即可。
*****/
public sealed class NoStackPolicy : IItemStackPolicy
{
    /*****
    Date: 2026-09-26
    Name: Instance
    Description: 无状态单例，供各处复用，免于重复分配。
    *****/
    public static NoStackPolicy Instance { get; } = new();

    /*****
    Date: 2026-09-26
    Name: CanStack
    Description: 恒不允许合并。
    *****/
    public bool CanStack(ItemStack target, ItemStack source) => false;
}
