namespace RelayStation.Core.Characters;

/*****
Date: 2026-09-26
Name: NeedSnapshot
Description: 需求快照；记录角色九项需求（呼吸/进食/休息/饮水/排泄/卫生/社交/娱乐/排尿）的当前数值（0~Max）。字段顺序与 NeedId 声明顺序**严格一致**（存档按此顺序序列化），排泄与排尿为增长型（值越大越糟，分别由饱食量与饮水量驱动），其余为衰减型（值越大越好）。
*****/
public sealed record NeedSnapshot(
    float Oxygen, float Food, float Rest, float Water,
    float Excretion, float Hygiene, float Social, float Entertainment, float Urination)
{
    /*****
    Date: 2026-09-26
    Name: Max
    Description: 单项需求的数值上限（0~100）。
    *****/
    public const float Max = 100f;

    /*****
    Date: 2026-09-26
    Name: Full
    Description: 满值快照（各需求均处于最佳状态：七项衰减型为 100，两项增长型——排泄与排尿——为 0）；新建角色与旧档缺需求数据时的默认值。
    *****/
    public static NeedSnapshot Full { get; } = new(Max, Max, Max, Max, 0f, Max, Max, Max, 0f);

    /*****
    Date: 2026-09-26
    Name: Get
    Description: 按需求标识取值；供系统逐项运算与表现层遍历渲染使用。
    *****/
    public float Get(NeedId id) => id switch
    {
        NeedId.Oxygen => Oxygen,
        NeedId.Food => Food,
        NeedId.Rest => Rest,
        NeedId.Water => Water,
        NeedId.Excretion => Excretion,
        NeedId.Hygiene => Hygiene,
        NeedId.Social => Social,
        NeedId.Entertainment => Entertainment,
        _ => Urination,
    };

    /*****
    Date: 2026-09-26
    Name: Set
    Description: 返回把指定需求改为 value（自动夹在 0~Max）后的新快照（record 不可变）。
    *****/
    public NeedSnapshot Set(NeedId id, float value)
    {
        float v = Math.Clamp(value, 0f, Max);
        return id switch
        {
            NeedId.Oxygen => this with { Oxygen = v },
            NeedId.Food => this with { Food = v },
            NeedId.Rest => this with { Rest = v },
            NeedId.Water => this with { Water = v },
            NeedId.Excretion => this with { Excretion = v },
            NeedId.Hygiene => this with { Hygiene = v },
            NeedId.Social => this with { Social = v },
            NeedId.Entertainment => this with { Entertainment = v },
            _ => this with { Urination = v },
        };
    }

    /*****
    Date: 2026-09-26
    Name: ToArray
    Description: 按 NeedId 顺序导出九项数值；供存档序列化（.tres 的 float[] 字段）使用。
    *****/
    public float[] ToArray()
        => new[] { Oxygen, Food, Rest, Water, Excretion, Hygiene, Social, Entertainment, Urination };

    /*****
    Date: 2026-09-26
    Name: FromArray
    Description: 按 NeedId 顺序由数组还原快照：逐项覆盖（每项夹在 0~Max），**数组缺失或较短的旧档按「最佳状态」补齐**（衰减型满值、增长型 0，即旧 8 项存档读入后排尿为 0）；数组为 null 时返回满值。
    *****/
    public static NeedSnapshot FromArray(float[]? values)
    {
        if (values == null) return Full;
        NeedSnapshot snapshot = Full;
        int count = Math.Min(values.Length, 9);
        for (int i = 0; i < count; i++)
        {
            snapshot = snapshot.Set((NeedId)i, values[i]);
        }
        return snapshot;
    }
}