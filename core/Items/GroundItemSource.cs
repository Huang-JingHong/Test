using Godot;

namespace RelayStation.Core.Items;

/*****
Date: 2026-09-27
Name: GroundItemSource
Description: 地面物品堆的物品来源实现（IItemSource）；把 ItemStack（数量/位置）与 ItemRegistry（增删机制）组合成拾取任务的取货口径——取出即从该堆扣除（扣空由注册表即时回收），放回即在原格原位重新投放一组。物品堆本身仍由注册表维护，本类只是面向任务侧的一层取放视图。
*****/
public sealed class GroundItemSource : IItemSource
{
    /*****
    Date: 2026-09-27
    Name: _registry
    Description: 地面物品注册表（数量扣减与回投的唯一机制）。
    *****/
    private readonly ItemRegistry _registry;

    /*****
    Date: 2026-09-27
    Name: GroundItemSource
    Description: 构造函数；stack 为来源物品堆，registry 为维护该堆的注册表。
    *****/
    public GroundItemSource(ItemStack stack, ItemRegistry registry)
    {
        Stack = stack;
        _registry = registry;
    }

    /*****
    Date: 2026-09-27
    Name: Stack
    Description: 背后的物品堆实例；供表现层把任务进度条挂到该堆的视图上（ItemTarget）。
    *****/
    public ItemStack Stack { get; }

    /*****
    Date: 2026-09-27
    Name: Def
    Description: 来源内物品的静态定义。
    *****/
    public IItemDef Def => Stack.Def;

    /*****
    Date: 2026-09-27
    Name: Cell
    Description: 来源所在格（物品堆的所属格）。
    *****/
    public Vector2I Cell => Stack.Cell;

    /*****
    Date: 2026-09-27
    Name: AvailableCount
    Description: 此刻可取的件数（物品堆当前数量）。
    *****/
    public int AvailableCount => Stack.Count;

    /*****
    Date: 2026-09-27
    Name: TryTake
    Description: 从物品堆取出至多 count 件（经注册表扣减，扣空即回收该堆）；实际取出量经 taken 返回，失败时不产生任何改动。
    *****/
    public bool TryTake(int count, out int taken)
    {
        taken = Math.Min(Math.Max(0, count), Stack.Count);
        if (taken <= 0 || !_registry.Remove(Stack, taken))
        {
            taken = 0;
            return false;
        }
        return true;
    }

    /*****
    Date: 2026-09-27
    Name: Return
    Description: 把 count 件物品放回原格原位（重新投放一组，像素偏移沿用原堆）；供「取出后装入背包失败」的回退路径使用。
    *****/
    public void Return(int count)
    {
        if (count > 0) _registry.Add(Def, count, Cell, Stack.PixelOffset);
    }
}