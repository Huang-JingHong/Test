using RelayStation.Core.Items;

namespace RelayStation.Core.Characters;

/*****
Date: 2026-09-26
Name: CharacterInventory
Description: 角色背包与装备（纯 C#，可引擎外测试）：口袋内容以物品条目列表承载，五个装备槽各最多一件；容量 = 基础负重 + 已装备容器（背包）提供的容量之和，按「单位重量 × 数量」的总重校验（**只管容量，不涉及体积与格子**）。
不变量：装备中的物品不出现在条目列表（卸下或替换时回到列表）；数量归零即移除条目；同种物品合并为一条记录。
本阶段为「数据 + UI + 负重校验」：无物品拾取/搬运/消耗流转，写入口为 Simulation 的装备/卸下/入包方法与存档恢复（Restore）。
*****/
public sealed class CharacterInventory
{
    /*****
    Date: 2026-09-26
    Name: Tolerance
    Description: 重量比较容差（浮点累加误差；不小于该差值即视为超重）。
    *****/
    private const float Tolerance = 1e-4f;

    /*****
    Date: 2026-09-26
    Name: _entries
    Description: 口袋内的物品条目（同种物品合并为一条）。
    *****/
    private readonly List<InventoryEntry> _entries = new();

    /*****
    Date: 2026-09-26
    Name: _equipped
    Description: 装备槽 → 已装备物品（每槽最多一件）。
    *****/
    private readonly Dictionary<EquipmentSlot, IItemDef> _equipped = new();

    /*****
    Date: 2026-09-26
    Name: CharacterInventory
    Description: 构造函数；以角色基础负重（kg）创建空背包（负数按 0 处理）。
    *****/
    public CharacterInventory(float baseCapacityKg) => BaseCapacityKg = MathF.Max(0f, baseCapacityKg);

    /*****
    Date: 2026-09-26
    Name: BaseCapacityKg
    Description: 角色自身基础负重（kg，来自 CharacterDef.BaseCarryCapacityKg）。
    *****/
    public float BaseCapacityKg { get; }

    /*****
    Date: 2026-09-26
    Name: Entries
    Description: 口袋内的物品条目（只读视图）。
    *****/
    public IReadOnlyList<InventoryEntry> Entries => _entries;

    /*****
    Date: 2026-09-26
    Name: Equipped
    Description: 各槽位已装备物品（只读视图）。
    *****/
    public IReadOnlyDictionary<EquipmentSlot, IItemDef> Equipped => _equipped;

    /*****
    Date: 2026-09-26
    Name: CapacityKg
    Description: 当前负重上限（kg）＝基础负重 + 已装备容器提供的容量之和。
    *****/
    public float CapacityKg
        => BaseCapacityKg + _equipped.Values.Sum(def => MathF.Max(0f, def.ContainerCapacityKg));

    /*****
    Date: 2026-09-26
    Name: TotalWeightKg
    Description: 当前携带总重（kg）＝Σ（单位重量 × 数量）；装备中的物品不计（穿戴在身上）。
    *****/
    public float TotalWeightKg => _entries.Sum(entry => WeightOf(entry.Def, entry.Count));

    /*****
    Date: 2026-09-26
    Name: WorkSpeedBonusPercent
    Description: 已装备物品提供的工作效率加成之和（0.1 = +10%）；供模拟层在推进作业时乘算（对全部作业生效，不限专长匹配）。
    *****/
    public float WorkSpeedBonusPercent
        => _equipped.Values.Sum(def => MathF.Max(0f, def.WorkSpeedBonusPercent));

    /*****
    Date: 2026-09-26
    Name: MoveSpeedBonusPercent
    Description: 已装备物品提供的移动速度加成之和（0.2 = +20%）；供模拟层在推进移动时乘算。
    *****/
    public float MoveSpeedBonusPercent
        => _equipped.Values.Sum(def => MathF.Max(0f, def.MoveSpeedBonusPercent));

    /*****
    Date: 2026-09-26
    Name: IsOverloaded
    Description: 是否超重（总重大于上限）；正常路径不会产生超重（写入均先校验），仅存档恢复可带回超重状态。
    *****/
    public bool IsOverloaded => TotalWeightKg > CapacityKg + Tolerance;

    /*****
    Date: 2026-09-26
    Name: CountOf
    Description: 背包内指定物品的数量（不含已装备的那件）。
    *****/
    public int CountOf(IItemDef def)
    {
        int total = 0;
        foreach (InventoryEntry entry in _entries)
        {
            if (ReferenceEquals(entry.Def, def)) total += entry.Count;
        }
        return total;
    }

    /*****
    Date: 2026-09-26
    Name: GetEquipped
    Description: 取指定槽位已装备的物品；空槽返回 null。
    *****/
    public IItemDef? GetEquipped(EquipmentSlot slot)
        => _equipped.TryGetValue(slot, out IItemDef? def) ? def : null;

    /*****
    Date: 2026-09-26
    Name: CanAdd
    Description: 是否可以再装入指定数量的物品（数量为正且装入后不超重）。
    *****/
    public bool CanAdd(IItemDef def, int count)
        => count > 0 && TotalWeightKg + WeightOf(def, count) <= CapacityKg + Tolerance;

    /*****
    Date: 2026-09-26
    Name: TryAdd
    Description: 装入指定数量的物品（超重则整笔拒绝并返回 false）；成功返回 true。
    *****/
    public bool TryAdd(IItemDef def, int count)
    {
        if (!CanAdd(def, count)) return false;
        AddToEntries(def, count);
        return true;
    }

    /*****
    Date: 2026-09-26
    Name: Remove
    Description: 取出指定数量的物品，返回实际取出的数量（不足时取出全部，空条目即时移除）。
    *****/
    public int Remove(IItemDef def, int count)
    {
        if (count <= 0) return 0;
        int remaining = count;
        int removed = 0;
        for (int i = _entries.Count - 1; i >= 0 && remaining > 0; i--)
        {
            InventoryEntry entry = _entries[i];
            if (!ReferenceEquals(entry.Def, def)) continue;
            int take = Math.Min(entry.Count, remaining);
            remaining -= take;
            removed += take;
            if (take == entry.Count) _entries.RemoveAt(i);
            else _entries[i] = entry with { Count = entry.Count - take };
        }
        return removed;
    }

    /*****
    Date: 2026-09-26
    Name: MaxAddable
    Description: 在**不再超重**的前提下还能再装入的该物品最大个数（按剩余容量 ÷ 单位重量向下取整；单位重量为 0 的物品容量不构成限制，返回 int.MaxValue）。供拾取任务的「逐件转移」判定每件是否装得下与容量预检共用。
    *****/
    public int MaxAddable(IItemDef def)
    {
        float unit = MathF.Max(0f, def.WeightKg);
        if (unit <= Tolerance) return int.MaxValue;
        float free = CapacityKg + Tolerance - TotalWeightKg;
        if (free <= 0f) return 0;
        return (int)MathF.Floor(free / unit);
    }

    /*****
    Date: 2026-09-26
    Name: TryEquip
    Description: 装备背包内的一件可穿戴物品：物品须在背包内且装备槽合法；同槽已有装备时旧装备回到背包。若换装后（容量变化 + 旧装备回包）超重则整笔拒绝。
    *****/
    public bool TryEquip(IItemDef def)
    {
        EquipmentSlot slot = def.EquipSlot;
        if (slot == EquipmentSlot.None) return false;
        if (CountOf(def) <= 0) return false;

        IItemDef? old = GetEquipped(slot);
        if (ReferenceEquals(old, def)) return false;

        // 试算：容量按「除本槽外全部装备 + 待装备物品」重算；重量按「取出一件待装备 + 旧装备回包」重算
        float capacity = CapacityWithout(slot) + MathF.Max(0f, def.ContainerCapacityKg);
        float weight = TotalWeightKg - WeightOf(def, 1) + (old != null ? MathF.Max(0f, old.WeightKg) : 0f);
        if (weight > capacity + Tolerance) return false;

        Remove(def, 1);
        if (old != null) AddToEntries(old, 1);
        _equipped[slot] = def;
        return true;
    }

    /*****
    Date: 2026-09-26
    Name: TryWearDirect
    Description: 直接穿戴一件**不在背包内**的物品（地面拾取后直接穿上的路径）：槽位须合法且不与该槽现有装备同物；同槽已有装备时旧装备回背包，**旧装备回包即超重则整笔拒绝**（容量按「除本槽外全部装备 + 待穿物品」重算，重量按「旧装备回包」重算，待穿物品不从包里出）。
    *****/
    public bool TryWearDirect(IItemDef def)
    {
        EquipmentSlot slot = def.EquipSlot;
        if (slot == EquipmentSlot.None) return false;

        IItemDef? old = GetEquipped(slot);
        if (ReferenceEquals(old, def)) return false;

        float capacity = CapacityWithout(slot) + MathF.Max(0f, def.ContainerCapacityKg);
        float weight = TotalWeightKg + (old != null ? MathF.Max(0f, old.WeightKg) : 0f);
        if (weight > capacity + Tolerance) return false;

        if (old != null) AddToEntries(old, 1);
        _equipped[slot] = def;
        return true;
    }

    /*****
    Date: 2026-09-26
    Name: Unequip
    Description: 卸下指定槽位的装备（回到背包）；卸下后超重则拒绝（槽位与背包均不变）。
    *****/
    public bool Unequip(EquipmentSlot slot)
    {
        if (!_equipped.TryGetValue(slot, out IItemDef? def)) return false;

        float capacity = CapacityWithout(slot);
        float weight = TotalWeightKg + MathF.Max(0f, def.WeightKg);
        if (weight > capacity + Tolerance) return false;

        _equipped.Remove(slot);
        AddToEntries(def, 1);
        return true;
    }

    /*****
    Date: 2026-09-26
    Name: TakeEquipped
    Description: 脱下指定槽位的装备并返回该物品（**不做容量校验**）；空槽返回 null。供模拟层在「卸下后背包放不下 → 就地落地」路径使用——物品既不入包也不留在槽位，由调用方负责落位。
    *****/
    internal IItemDef? TakeEquipped(EquipmentSlot slot)
    {
        if (!_equipped.TryGetValue(slot, out IItemDef? def)) return null;
        _equipped.Remove(slot);
        return def;
    }

    /*****
    Date: 2026-09-26
    Name: Restore
    Description: 存档恢复：覆盖装备槽与背包条目；**不做容量校验**（超重状态原样恢复，界面以红字提示）。
    *****/
    internal void Restore(
        IEnumerable<(EquipmentSlot Slot, IItemDef Def)> equipped,
        IEnumerable<(IItemDef Def, int Count)> entries)
    {
        _equipped.Clear();
        _entries.Clear();
        foreach ((EquipmentSlot slot, IItemDef def) in equipped)
        {
            if (slot != EquipmentSlot.None) _equipped[slot] = def;
        }
        foreach ((IItemDef def, int count) in entries)
        {
            if (count > 0) AddToEntries(def, count);
        }
    }

    /*****
    Date: 2026-09-26
    Name: CapacityWithout
    Description: 计算「除指定槽位外」的容量（基础负重 + 其余已装备容器的容量之和）；装备/卸下时用于试算容量变化。internal：同程序集的穿戴任务（EquipTask）需在「旧装备改走落地」时预检同一口径。
    *****/
    internal float CapacityWithout(EquipmentSlot slot)
    {
        float capacity = BaseCapacityKg;
        foreach (KeyValuePair<EquipmentSlot, IItemDef> pair in _equipped)
        {
            if (pair.Key == slot) continue;
            capacity += MathF.Max(0f, pair.Value.ContainerCapacityKg);
        }
        return capacity;
    }

    /*****
    Date: 2026-09-26
    Name: AddToEntries
    Description: 把物品并入条目列表（同种物品合并计数；负重量按 0 处理）。
    *****/
    private void AddToEntries(IItemDef def, int count)
    {
        if (count <= 0) return;
        for (int i = 0; i < _entries.Count; i++)
        {
            if (!ReferenceEquals(_entries[i].Def, def)) continue;
            _entries[i] = _entries[i] with { Count = _entries[i].Count + count };
            return;
        }
        _entries.Add(new InventoryEntry(def, count));
    }

    /*****
    Date: 2026-09-26
    Name: WeightOf
    Description: 一组物品的重量（单位重量 × 数量；负的单位重量按 0 处理）。
    *****/
    private static float WeightOf(IItemDef def, int count) => MathF.Max(0f, def.WeightKg) * count;
}