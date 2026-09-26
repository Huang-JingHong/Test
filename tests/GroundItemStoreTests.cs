using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Events;
using RelayStation.Core.Facilities;
using RelayStation.Core.Items;
using RelayStation.Core.Map;
using RelayStation.Core.Resources;
using RelayStation.Core.Tasks;
using RelayStation.Core.Time;
using Xunit;
using GridMap = RelayStation.Core.Map.GridMap;

namespace RelayStation.Tests;

/*****
Date: 2026-09-26
Name: GroundItemStoreTests
Description: 「地上零件就是库存」单元测试；覆盖余额=可达堆之和、不可达堆不计入、按堆扣除与空堆回收、余额不足时整笔不生效、返还投放到指定格、只认本定义（不吃别种物品）、以及物品变化向 UI 转发余额变化事件。
*****/
public sealed class GroundItemStoreTests
{
    /*****
    Date: 2026-09-26
    Name: Parts
    Description: 备用零件定义替身（单元测试宿主无法构造 ItemDef Resource）。
    *****/
    private static IItemDef Parts() => new FakeItemDef("general_purpose_parts", 100, "通用零件");

    /*****
    Date: 2026-09-26
    Name: CreateFloorMap
    Description: 创建全地板地图（默认 Vacuum，逐格置 Floor）。
    *****/
    private static GridMap CreateFloorMap(int width, int height)
    {
        var map = new GridMap(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                map.SetCell(new Vector2I(x, y), CellKind.Floor);
            }
        }
        return map;
    }

    /*****
    Date: 2026-09-26
    Name: CreateSimulation
    Description: 在指定地图上装配模拟层并把寻路用的阻塞集合返回（与 Simulation 共享同一实例）。
    *****/
    private static Simulation CreateSimulation(GridMap map)
    {
        var blocked = new HashSet<Vector2I>();
        var eventBus = new EventBus();
        return new Simulation(
            new GameClock(), map, new GridPathfinding(map, blocked.Contains),
            new TaskBoard(eventBus), new NeedSystem(), eventBus, blocked);
    }

    /*****
    Date: 2026-09-26
    Name: Balance_SumsReachableStacksOnly
    Description: 余额 = 可达堆之和：被墙隔开的孤岛上的零件不计入（角色到不了就拿不到）。
    *****/
    [Fact]
    public void Balance_SumsReachableStacksOnly()
    {
        GridMap map = CreateFloorMap(7, 7);
        for (int y = 0; y < 7; y++) map.SetCell(new Vector2I(3, y), CellKind.Wall); // x=3 整列封死
        Simulation simulation = CreateSimulation(map);
        simulation.AddCharacter(new CharacterSim(null, new Vector2I(0, 0)));

        simulation.Items.Add(Parts(), 30, new Vector2I(1, 1)); // 可达侧
        simulation.Items.Add(Parts(), 50, new Vector2I(5, 5)); // 孤岛侧
        var store = new GroundItemStore(simulation, Parts());

        Assert.Equal(30, store.Balance);

        // 无角色在场时视为「地上无任何可达资源」
        Simulation empty = CreateSimulation(CreateFloorMap(7, 7));
        empty.Items.Add(Parts(), 30, new Vector2I(1, 1));
        Assert.Equal(0, new GroundItemStore(empty, Parts()).Balance);
    }

    /*****
    Date: 2026-09-26
    Name: TrySpend_DeductsAcrossStacksAndRecyclesEmpty
    Description: 按堆扣除：跨两组扣足数量，扣空的组即时回收，余额随之减少。
    *****/
    [Fact]
    public void TrySpend_DeductsAcrossStacksAndRecyclesEmpty()
    {
        Simulation simulation = CreateSimulation(CreateFloorMap(6, 6));
        simulation.AddCharacter(new CharacterSim(null, new Vector2I(0, 0)));
        simulation.Items.Add(Parts(), 30, new Vector2I(1, 1));
        simulation.Items.Add(Parts(), 20, new Vector2I(2, 2));
        var store = new GroundItemStore(simulation, Parts());

        Assert.Equal(50, store.Balance);
        Assert.True(store.TrySpend(40));

        Assert.Equal(10, store.Balance);
        Assert.Single(simulation.Items.Stacks); // 30 那组被扣空回收
    }

    /*****
    Date: 2026-09-26
    Name: TrySpend_Insufficient_LeavesEverythingUntouched
    Description: 余额不足时整笔不生效（与旧库存池语义一致，保证「材料短缺 → 建造停滞」成立），且不改动任何物品堆。
    *****/
    [Fact]
    public void TrySpend_Insufficient_LeavesEverythingUntouched()
    {
        Simulation simulation = CreateSimulation(CreateFloorMap(6, 6));
        simulation.AddCharacter(new CharacterSim(null, new Vector2I(0, 0)));
        simulation.Items.Add(Parts(), 30, new Vector2I(1, 1));
        simulation.Items.Add(Parts(), 20, new Vector2I(2, 2));
        var store = new GroundItemStore(simulation, Parts());

        Assert.False(store.TrySpend(51));
        Assert.False(store.TrySpend(0));

        Assert.Equal(50, store.Balance);
        Assert.Equal(new[] { 30, 20 }, simulation.Items.Stacks.Select(s => s.Count).ToArray());
    }

    /*****
    Date: 2026-09-26
    Name: TrySpend_ConsidersOnlyReachableAndOwnDef
    Description: 只统计并消耗「可达 + 本定义」的堆：孤岛上的零件与别种物品都不算数。
    *****/
    [Fact]
    public void TrySpend_ConsidersOnlyReachableAndOwnDef()
    {
        GridMap map = CreateFloorMap(7, 7);
        for (int y = 0; y < 7; y++) map.SetCell(new Vector2I(3, y), CellKind.Wall);
        Simulation simulation = CreateSimulation(map);
        simulation.AddCharacter(new CharacterSim(null, new Vector2I(0, 0)));
        simulation.Items.Add(Parts(), 10, new Vector2I(1, 1));
        simulation.Items.Add(Parts(), 40, new Vector2I(5, 5));                                  // 孤岛：不计
        simulation.Items.Add(new FakeItemDef("scrap", 100), 60, new Vector2I(2, 2));            // 别种：不计

        var store = new GroundItemStore(simulation, Parts());

        Assert.Equal(10, store.Balance);
        Assert.False(store.TrySpend(11));
        Assert.True(store.TrySpend(10));
        Assert.Equal(0, store.Balance);
        // 未波及别种物品
        Assert.Equal(60, simulation.Items.Stacks.Single(s => s.Def.Id == "scrap").Count);
    }

    /*****
    Date: 2026-09-26
    Name: Add_DropsItemsAtGivenCell
    Description: 返还把零件投放到指定格（拆除原位），余额随之增加；抽象池实现忽略位置参数。
    *****/
    [Fact]
    public void Add_DropsItemsAtGivenCell()
    {
        Simulation simulation = CreateSimulation(CreateFloorMap(6, 6));
        simulation.AddCharacter(new CharacterSim(null, new Vector2I(0, 0)));
        var store = new GroundItemStore(simulation, Parts());

        store.Add(25, new Vector2I(4, 4));

        ItemStack stack = Assert.Single(simulation.Items.Stacks);
        Assert.Equal(new Vector2I(4, 4), stack.Cell);
        Assert.Equal(25, stack.Count);
        Assert.Equal(25, store.Balance);
    }

    /*****
    Date: 2026-09-26
    Name: Balance_BoundedByReachabilityAfterBlocking
    Description: 设施把格子封死会让原本可达的零件变为不可达，余额随之下降（可达性是动态判定的）。
    *****/
    [Fact]
    public void Balance_BoundedByReachabilityAfterBlocking()
    {
        GridMap map = CreateFloorMap(7, 2);
        Simulation simulation = CreateSimulation(map);
        simulation.AddCharacter(new CharacterSim(null, new Vector2I(0, 0)));
        simulation.Items.Add(Parts(), 40, new Vector2I(5, 0));
        var store = new GroundItemStore(simulation, Parts());

        Assert.Equal(40, store.Balance);

        // 2×2 阻挡设施正好堵死 7×2 走廊的 x=3..4，右侧零件随即变为不可达
        simulation.AddFacility(new FacilitySim(null, new Vector2I(3, 0), blocksMovement: true));

        Assert.Equal(0, store.Balance);
    }

    /*****
    Date: 2026-09-26
    Name: BalanceChanged_ForwardsItemChanges
    Description: 物品堆变化（外部增删或本店扣除）都会转发为余额变化事件，供 UI 刷新。
    *****/
    [Fact]
    public void BalanceChanged_ForwardsItemChanges()
    {
        Simulation simulation = CreateSimulation(CreateFloorMap(6, 6));
        simulation.AddCharacter(new CharacterSim(null, new Vector2I(0, 0)));
        var store = new GroundItemStore(simulation, Parts());
        int fired = 0;
        store.BalanceChanged += () => fired++;

        simulation.Items.Add(Parts(), 20, new Vector2I(1, 1)); // 外部投放
        Assert.Equal(1, fired);

        store.TrySpend(5);
        Assert.Equal(2, fired);
    }
}
