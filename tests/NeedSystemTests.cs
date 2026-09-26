using Godot;
using RelayStation.Core.Characters;
using Xunit;

/*****
Date: 2026-09-26
Name: NeedSystemTests
Description: 需求系统单元测试；验证九项需求的初始最佳值、衰减方向与速率、两项增长型需求（排泄←饱食 / 排尿←饮水）的来源独立性（各来源为 0 时对应需求不增长）、上下限封顶、Set 覆盖与事件触发口径、快照数组序列化往返与旧档补齐，以及需求目录的方向口径。
*****/
namespace RelayStation.Tests;

public sealed class NeedSystemTests
{
    /*****
    Date: 2026-09-26
    Name: NewCharacter
    Description: 建一个引擎外测试用角色（无定义）。
    *****/
    private static CharacterSim NewCharacter() => new(null, new Vector2I(1, 1));

    /*****
    Date: 2026-09-26
    Name: Read_NoRecord_ReturnsFull
    Description: 未推进过的角色读取为最佳值：七项衰减型为 100、两项增长型（排泄/排尿）为 0。
    *****/
    [Fact]
    public void Read_NoRecord_ReturnsFull()
    {
        var system = new NeedSystem();
        NeedSnapshot needs = system.Read(NewCharacter());

        foreach (NeedId id in NeedCatalog.All)
        {
            float expected = NeedCatalog.Direction(id) == NeedDirection.Increasing ? 0f : NeedSnapshot.Max;
            Assert.Equal(expected, needs.Get(id), 3);
        }
    }

    /*****
    Date: 2026-09-26
    Name: Update_OneMinute_DecaysAtConfiguredRates
    Description: 推进 1 游戏分钟后，七项衰减型需求按各自速率下降。
    *****/
    [Fact]
    public void Update_OneMinute_DecaysAtConfiguredRates()
    {
        var system = new NeedSystem();
        CharacterSim c = NewCharacter();
        system.Update(c, 1.0);
        NeedSnapshot needs = system.Read(c);

        Assert.Equal(100f - NeedSystem.OxygenDecayPerMinute, needs.Oxygen, 3);
        Assert.Equal(100f - NeedSystem.FoodDecayPerMinute, needs.Food, 3);
        Assert.Equal(100f - NeedSystem.RestDecayPerMinute, needs.Rest, 3);
        Assert.Equal(100f - NeedSystem.WaterDecayPerMinute, needs.Water, 3);
        Assert.Equal(100f - NeedSystem.HygieneDecayPerMinute, needs.Hygiene, 3);
        Assert.Equal(100f - NeedSystem.SocialDecayPerMinute, needs.Social, 3);
        Assert.Equal(100f - NeedSystem.EntertainmentDecayPerMinute, needs.Entertainment, 3);
    }

    /*****
    Date: 2026-09-26
    Name: Update_WithBothSourcesFull_GrowsEachByItsOwnSource
    Description: 饱食与饮水均为满值时，100 游戏分钟：排泄增长 2 点（100×0.0002×100）、排尿增长 3 点（100×0.0003×100）——总量与拆分前的联合公式一致。
    *****/
    [Fact]
    public void Update_WithBothSourcesFull_GrowsEachByItsOwnSource()
    {
        var system = new NeedSystem();
        CharacterSim c = NewCharacter();
        system.Update(c, 100.0);

        Assert.Equal(2f, system.Read(c).Excretion, 3);
        Assert.Equal(3f, system.Read(c).Urination, 3);
    }

    /*****
    Date: 2026-09-26
    Name: Update_FoodOnly_GrowsDefecationButNotUrination
    Description: 「只吃不喝」：饱食满值、饮水为 0 时，排泄照常增长而排尿不增长（杜绝凭空产水）。
    *****/
    [Fact]
    public void Update_FoodOnly_GrowsDefecationButNotUrination()
    {
        var system = new NeedSystem();
        CharacterSim c = NewCharacter();
        system.Set(c, NeedSnapshot.Full.Set(NeedId.Water, 0f));

        system.Update(c, 100.0);

        Assert.Equal(2f, system.Read(c).Excretion, 3);
        Assert.Equal(0f, system.Read(c).Urination, 3);
    }

    /*****
    Date: 2026-09-26
    Name: Update_WaterOnly_GrowsUrinationButNotDefecation
    Description: 「只喝不吃」：饮水满值、饱食为 0 时，排尿照常增长而排泄不增长（两项来源彼此独立）。
    *****/
    [Fact]
    public void Update_WaterOnly_GrowsUrinationButNotDefecation()
    {
        var system = new NeedSystem();
        CharacterSim c = NewCharacter();
        system.Set(c, NeedSnapshot.Full.Set(NeedId.Food, 0f));

        system.Update(c, 100.0);

        Assert.Equal(3f, system.Read(c).Urination, 3);
        Assert.Equal(0f, system.Read(c).Excretion, 3);
    }

    /*****
    Date: 2026-09-26
    Name: Update_BothSourcesZero_GrowthNeedsDoNotGrow
    Description: 饱食与饮水都为 0（最饿且最渴）时，排泄与排尿均不增长。
    *****/
    [Fact]
    public void Update_BothSourcesZero_GrowthNeedsDoNotGrow()
    {
        var system = new NeedSystem();
        CharacterSim c = NewCharacter();
        system.Set(c, NeedSnapshot.Full
            .Set(NeedId.Food, 0f)
            .Set(NeedId.Water, 0f)
            .Set(NeedId.Excretion, 10f)
            .Set(NeedId.Urination, 20f));

        system.Update(c, 1000.0);

        Assert.Equal(10f, system.Read(c).Excretion, 3);
        Assert.Equal(20f, system.Read(c).Urination, 3);
    }

    /*****
    Date: 2026-09-26
    Name: Update_ClampsDecayAtZeroAndGrowthAtMax
    Description: 长时间推进后，衰减型需求下限为 0，排泄与排尿上限均为 100。
    *****/
    [Fact]
    public void Update_ClampsDecayAtZeroAndGrowthAtMax()
    {
        var system = new NeedSystem();
        CharacterSim c = NewCharacter();
        system.Set(c, NeedSnapshot.Full
            .Set(NeedId.Oxygen, 0.5f)
            .Set(NeedId.Excretion, 99.9f)
            .Set(NeedId.Urination, 99.9f));

        system.Update(c, 100.0);

        NeedSnapshot needs = system.Read(c);
        Assert.Equal(0f, needs.Oxygen, 3);
        Assert.Equal(NeedSnapshot.Max, needs.Excretion, 3);
        Assert.Equal(NeedSnapshot.Max, needs.Urination, 3);
    }

    /*****
    Date: 2026-09-26
    Name: Set_ReplacesSnapshotWithoutRaisingEvent
    Description: Set（读档恢复）覆盖快照且不触发 NeedsChanged；随后的 Update 才触发，并携带推进后的快照。
    *****/
    [Fact]
    public void Set_ReplacesSnapshotWithoutRaisingEvent()
    {
        var system = new NeedSystem();
        CharacterSim c = NewCharacter();
        int raised = 0;
        NeedSnapshot? last = null;
        system.NeedsChanged += (_, snapshot) => { raised++; last = snapshot; };

        var saved = new NeedSnapshot(70f, 60f, 50f, 40f, 30f, 20f, 10f, 5f, 3f);
        system.Set(c, saved);

        Assert.Equal(0, raised);
        Assert.Equal(70f, system.Read(c).Oxygen, 3);
        Assert.Equal(3f, system.Read(c).Urination, 3);

        system.Update(c, 1.0);

        Assert.Equal(1, raised);
        Assert.NotNull(last);
        Assert.Equal(70f - NeedSystem.OxygenDecayPerMinute, last!.Oxygen, 3);
    }

    /*****
    Date: 2026-09-26
    Name: Snapshot_ArrayRoundTrip_AndOldSaveFill
    Description: 快照按 NeedId 顺序往返数组；旧档（较短数组）缺项按最佳值补齐（旧 8 项 → 排尿 0）；越界数值读档时被夹在 0~Max。
    *****/
    [Fact]
    public void Snapshot_ArrayRoundTrip_AndOldSaveFill()
    {
        var snapshot = new NeedSnapshot(1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f);
        Assert.Equal(new[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f }, snapshot.ToArray());
        Assert.Equal(snapshot, NeedSnapshot.FromArray(snapshot.ToArray()));

        // 旧档只有八项：前八项照读，追加的排尿按最佳值（0）补齐
        NeedSnapshot migrated = NeedSnapshot.FromArray(new[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f });
        Assert.Equal(1f, migrated.Oxygen, 3);
        Assert.Equal(8f, migrated.Entertainment, 3);
        Assert.Equal(0f, migrated.Urination, 3);

        Assert.Equal(NeedSnapshot.Full, NeedSnapshot.FromArray(null));
        Assert.Equal(NeedSnapshot.Full, NeedSnapshot.FromArray(Array.Empty<float>()));

        NeedSnapshot partial = NeedSnapshot.FromArray(new[] { 200f, -5f, 3f, 4f });
        Assert.Equal(NeedSnapshot.Max, partial.Oxygen, 3);
        Assert.Equal(0f, partial.Food, 3);
        Assert.Equal(3f, partial.Rest, 3);
        Assert.Equal(4f, partial.Water, 3);
        Assert.Equal(100f, partial.Hygiene, 3); // 缺项按最佳值补齐（衰减型满值）
        Assert.Equal(0f, partial.Urination, 3); // 增长型缺项为 0
    }

    /*****
    Date: 2026-09-26
    Name: Catalog_AllContainsEachIdOnce_AndOnlyGrowthNeedsIncrease
    Description: 需求目录含九项且不重复；仅排泄与排尿为增长型（越小越好），其余为衰减型。
    *****/
    [Fact]
    public void Catalog_AllContainsEachIdOnce_AndOnlyGrowthNeedsIncrease()
    {
        Assert.Equal(9, NeedCatalog.All.Length);
        Assert.Equal(9, NeedCatalog.All.Distinct().Count());

        foreach (NeedId id in NeedCatalog.All)
        {
            bool growth = id is NeedId.Excretion or NeedId.Urination;
            Assert.Equal(growth ? NeedDirection.Increasing : NeedDirection.Decreasing, NeedCatalog.Direction(id));
        }
    }
}

/*****
Date: 2026-09-26
Name: CharacterHealthTests
Description: 角色生命值单元测试；验证初始满值与增减钳制（0~100）。
*****/
public sealed class CharacterHealthTests
{
    /*****
    Date: 2026-09-26
    Name: Health_DefaultsToMax
    Description: 新建角色生命值为上限（100）。
    *****/
    [Fact]
    public void Health_DefaultsToMax()
    {
        var c = new CharacterSim(null, new Vector2I(2, 3));
        Assert.Equal(CharacterSim.MaxHealth, c.Health, 3);
    }

    /*****
    Date: 2026-09-26
    Name: ApplyHealthDelta_ClampsAtBothEnds
    Description: 生命值增减被夹在 0~上限之间（治疗不会超过上限，伤害不会低于 0）。
    *****/
    [Fact]
    public void ApplyHealthDelta_ClampsAtBothEnds()
    {
        var c = new CharacterSim(null, new Vector2I(2, 3));

        c.ApplyHealthDelta(-30f);
        Assert.Equal(70f, c.Health, 3);

        c.ApplyHealthDelta(100f);
        Assert.Equal(CharacterSim.MaxHealth, c.Health, 3);

        c.ApplyHealthDelta(-999f);
        Assert.Equal(0f, c.Health, 3);
    }
}