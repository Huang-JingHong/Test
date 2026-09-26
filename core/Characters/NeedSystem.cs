namespace RelayStation.Core.Characters;

/*****
Date: 2026-09-26
Name: NeedSystem
Description: 需求系统（占位实现，仅数值）：按游戏分钟推进九项需求——七项衰减型（呼吸/进食/休息/饮水/卫生/社交/娱乐，随时间下降、下限 0），两项增长型且**增长来源彼此独立**：排泄按「饱食值×ExcretionFoodFactor」增长、排尿按「饮水值×UrinationWaterFactor」增长（各自来源为 0 时对应需求不增长，上限 100），以杜绝「只吃不喝却排尿」造成的资源循环失真。不致命、不影响工作效率、不驱动行为；每角色一份快照，读档经 Set 覆盖。
*****/
public sealed class NeedSystem : INeedSystem
{
    /*****
    Date: 2026-09-06
    Name: OxygenDecayPerMinute
    Description: 呼吸需求每游戏分钟衰减量。
    *****/
    public const float OxygenDecayPerMinute = 0.02f;

    /*****
    Date: 2026-09-06
    Name: FoodDecayPerMinute
    Description: 进食需求每游戏分钟衰减量。
    *****/
    public const float FoodDecayPerMinute = 0.015f;

    /*****
    Date: 2026-09-06
    Name: RestDecayPerMinute
    Description: 休息需求每游戏分钟衰减量。
    *****/
    public const float RestDecayPerMinute = 0.01f;

    /*****
    Date: 2026-09-26
    Name: WaterDecayPerMinute
    Description: 饮水需求每游戏分钟衰减量（越快越口渴）。
    *****/
    public const float WaterDecayPerMinute = 0.025f;

    /*****
    Date: 2026-09-26
    Name: HygieneDecayPerMinute
    Description: 卫生需求每游戏分钟衰减量（值越低越脏）。
    *****/
    public const float HygieneDecayPerMinute = 0.008f;

    /*****
    Date: 2026-09-26
    Name: SocialDecayPerMinute
    Description: 社交需求每游戏分钟衰减量（值越低越孤独）。
    *****/
    public const float SocialDecayPerMinute = 0.006f;

    /*****
    Date: 2026-09-26
    Name: EntertainmentDecayPerMinute
    Description: 娱乐需求每游戏分钟衰减量（值越低越想玩）。
    *****/
    public const float EntertainmentDecayPerMinute = 0.007f;

    /*****
    Date: 2026-09-26
    Name: ExcretionFoodFactor
    Description: 排泄增长系数：每游戏分钟按「当前饱食值 × 本系数」增长（饱食值 100 = 不饿；只影响排泄，不影响排尿）。
    *****/
    public const float ExcretionFoodFactor = 0.0002f;

    /*****
    Date: 2026-09-26
    Name: UrinationWaterFactor
    Description: 排尿增长系数：每游戏分钟按「当前饮水值 × 本系数」增长（饮水值 100 = 不渴；只影响排尿，不影响排泄——避免「只吃不喝却排尿」导致水回收系统凭空产水）。
    *****/
    public const float UrinationWaterFactor = 0.0003f;

    /*****
    Date: 2026-09-06
    Name: _needs
    Description: 角色到需求快照的映射表。
    *****/
    private readonly Dictionary<CharacterSim, NeedSnapshot> _needs = new();

    /*****
    Date: 2026-09-26
    Name: NeedsChanged
    Description: 需求推进后触发的事件（参数：角色本体、推进后的快照）；Set（读档恢复）不触发。
    *****/
    public event Action<CharacterSim, NeedSnapshot>? NeedsChanged;

    /*****
    Date: 2026-09-26
    Name: Update
    Description: 按游戏分钟推进指定角色的九项需求：七项衰减型取 max(0, 值 - 速率×增量)；排泄按**本次推进前**的饱食值、排尿按**本次推进前**的饮水值各自计算增长量并封顶 Max（来源为 0 则不增长）；推进后触发 NeedsChanged。
    *****/
    public void Update(CharacterSim c, double deltaGameMinutes)
    {
        NeedSnapshot current = Read(c);
        float delta = (float)deltaGameMinutes;

        // 两项增长型需求的来源彼此独立：排泄←饱食量，排尿←饮水量（各自来源为 0 时零增长）
        float excretion = current.Excretion + current.Food * ExcretionFoodFactor * delta;
        float urination = current.Urination + current.Water * UrinationWaterFactor * delta;

        NeedSnapshot next = new NeedSnapshot(
            Decay(current.Oxygen, OxygenDecayPerMinute, delta),
            Decay(current.Food, FoodDecayPerMinute, delta),
            Decay(current.Rest, RestDecayPerMinute, delta),
            Decay(current.Water, WaterDecayPerMinute, delta),
            MathF.Min(NeedSnapshot.Max, excretion),
            Decay(current.Hygiene, HygieneDecayPerMinute, delta),
            Decay(current.Social, SocialDecayPerMinute, delta),
            Decay(current.Entertainment, EntertainmentDecayPerMinute, delta),
            MathF.Min(NeedSnapshot.Max, urination));

        _needs[c] = next;
        NeedsChanged?.Invoke(c, next);
    }

    /*****
    Date: 2026-09-06
    Name: Read
    Description: 读取指定角色当前的需求快照；首次读取返回满值（各项 100）。
    *****/
    public NeedSnapshot Read(CharacterSim c)
        => _needs.TryGetValue(c, out NeedSnapshot? snapshot) ? snapshot : NeedSnapshot.Full;

    /*****
    Date: 2026-09-26
    Name: Set
    Description: 覆盖指定角色的需求快照（存档恢复用）；不触发 NeedsChanged。
    *****/
    public void Set(CharacterSim c, NeedSnapshot snapshot) => _needs[c] = snapshot;

    /*****
    Date: 2026-09-26
    Name: Restore
    Description: 提升指定角色的单项需求值（消耗物品的恢复路径）：以当前快照为基础加 amount 并夹在 0~Max，随后触发 NeedsChanged（供 UI 即时刷新）；amount ≤ 0 时无操作也不触发事件。
    *****/
    public void Restore(CharacterSim c, NeedId need, float amount)
    {
        if (amount <= 0f) return;
        NeedSnapshot current = Read(c);
        NeedSnapshot next = current.Set(need, current.Get(need) + amount);
        _needs[c] = next;
        NeedsChanged?.Invoke(c, next);
    }

    /*****
    Date: 2026-09-26
    Name: Decay
    Description: 衰减型需求的一次推进（下限 0）。
    *****/
    private static float Decay(float value, float ratePerMinute, float deltaGameMinutes)
        => MathF.Max(0f, value - ratePerMinute * deltaGameMinutes);
}