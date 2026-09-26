using Godot;

namespace RelayStation.Core.Items;

/*****
Date: 2026-09-26
Name: ItemStack
Description: 地面物品堆的运行时实例（普通 C# 类，不参与 .tres 序列化）；承载「物品定义 + 当前数量 + 所属格 + 格内像素偏移」。逻辑上绑定一个格子（用于与设施互斥、堆叠判定），视觉上经像素偏移在格内自由散落（不对齐 tile）。数量仅由 ItemRegistry 变更（internal setter），使「不超过 MaxStack、空堆即时回收」等不变量集中在一处维护。
*****/
public sealed class ItemStack
{
    /*****
    Date: 2026-09-26
    Name: ItemStack
    Description: 构造函数；count 由调用方（ItemRegistry）保证在 [1, MaxStack] 区间内。
    *****/
    public ItemStack(IItemDef def, int count, Vector2I cell, Vector2 pixelOffset = default)
    {
        Def = def;
        Count = count;
        Cell = cell;
        PixelOffset = pixelOffset;
    }

    /*****
    Date: 2026-09-26
    Name: Def
    Description: 物品静态定义（只读抽象，生产运行为 ItemDef）。
    *****/
    public IItemDef Def { get; }

    /*****
    Date: 2026-09-26
    Name: Count
    Description: 当前数量（1 ~ MaxStack）；归零即由 ItemRegistry 回收，外部只读。
    *****/
    public int Count { get; internal set; }

    /*****
    Date: 2026-09-26
    Name: Cell
    Description: 所属格（逻辑归属；物品不阻挡通行，仅与设施互斥）。
    *****/
    public Vector2I Cell { get; set; }

    /*****
    Date: 2026-09-26
    Name: PixelOffset
    Description: 格内像素偏移（相对所属格左上角）；视觉散落用，不对齐 tile。
    *****/
    public Vector2 PixelOffset { get; set; }

    /*****
    Date: 2026-09-26
    Name: MaxStack
    Description: 单组最大数量（取自定义；定义值非法（≤0）时按 1）。
    *****/
    public int MaxStack => Math.Max(1, Def.MaxStack);

    /*****
    Date: 2026-09-26
    Name: FreeSpace
    Description: 本组还能再容纳的数量（MaxStack - Count）；已满为 0。
    *****/
    public int FreeSpace => Math.Max(0, MaxStack - Count);

    /*****
    Date: 2026-09-26
    Name: FillRatio
    Description: 装满比例（0~1）；供表现层显示「70/100」一类的数量与可能的填充效果。
    *****/
    public float FillRatio => (float)Count / MaxStack;
}
