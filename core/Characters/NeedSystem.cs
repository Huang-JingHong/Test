namespace RelayStation.Core.Characters;

/*****
Date: 2026-09-06
Name: NeedSystem
Description: 需求系统占位实现；按游戏分钟对三项需求（呼吸/进食/休息）做缓慢衰减，不致命、不生效死逻辑——仅冻结数据结构与接口，阶段二接入真实生存压力时不动架构。
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
    Date: 2026-09-06
    Name: _needs
    Description: 角色到需求快照的映射表。
    *****/
    private readonly Dictionary<CharacterSim, NeedSnapshot> _needs = new();

    /*****
    Date: 2026-09-06
    Name: Update
    Description: 按游戏分钟推进指定角色的需求衰减；各项数值下限为 0。
    *****/
    public void Update(CharacterSim c, double deltaGameMinutes)
    {
        NeedSnapshot current = Read(c);
        float delta = (float)deltaGameMinutes;
        _needs[c] = new NeedSnapshot(
            MathF.Max(0f, current.Oxygen - OxygenDecayPerMinute * delta),
            MathF.Max(0f, current.Food - FoodDecayPerMinute * delta),
            MathF.Max(0f, current.Rest - RestDecayPerMinute * delta));
    }

    /*****
    Date: 2026-09-06
    Name: Read
    Description: 读取指定角色当前的需求快照；首次读取返回满值 (100, 100, 100)。
    *****/
    public NeedSnapshot Read(CharacterSim c)
        => _needs.TryGetValue(c, out NeedSnapshot? snapshot) ? snapshot : new NeedSnapshot(100f, 100f, 100f);
}
