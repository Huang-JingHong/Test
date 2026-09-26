namespace RelayStation.Core.Items;

/*****
Date: 2026-09-26
Name: ConsumableCatalog
Description: 可消耗物品注册表（纯 C#，不引用任何 Godot 节点）；登记本局全部「可消耗物品定义」，供模拟层按种类反查「饿了吃什么、渴了喝什么」。由 GameRoot 在装配时按固定路径注册（新游戏与读档共用同一条路径），需求驱动的自动取用据此挑选目标物品；未注册时可消耗物品则自动取用静默不触发。
*****/
public sealed class ConsumableCatalog
{
    /*****
    Date: 2026-09-26
    Name: _items
    Description: 已登记的可消耗物品定义（按注册顺序；同一定义只登记一次）。
    *****/
    private readonly List<IItemDef> _items = new();

    /*****
    Date: 2026-09-26
    Name: All
    Description: 全部已登记的可消耗物品（只读）。
    *****/
    public IReadOnlyList<IItemDef> All => _items;

    /*****
    Date: 2026-09-26
    Name: Register
    Description: 登记一个可消耗物品定义；`ConsumableKind` 为 None 的定义（普通物品/装备）与重复登记一律忽略。
    *****/
    public void Register(IItemDef def)
    {
        if (def.ConsumableKind == ConsumableKind.None) return;
        foreach (IItemDef existing in _items)
        {
            if (ReferenceEquals(existing, def)) return;
        }
        _items.Add(def);
    }

    /*****
    Date: 2026-09-26
    Name: Find
    Description: 按可消耗种类取**首个**已登记的物品定义（同种类登记多个时取注册顺序在前者）；未登记返回 null。
    *****/
    public IItemDef? Find(ConsumableKind kind)
    {
        foreach (IItemDef def in _items)
        {
            if (def.ConsumableKind == kind) return def;
        }
        return null;
    }
}