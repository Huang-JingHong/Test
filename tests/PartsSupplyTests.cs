using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Events;
using RelayStation.Core.Facilities;
using RelayStation.Core.Items;
using RelayStation.Core.Map;
using RelayStation.Core.Tasks;
using RelayStation.Core.Time;
using Xunit;
using GridMap = RelayStation.Core.Map.GridMap;

namespace RelayStation.Tests;

/*****
Date: 2026-09-27
Name: PartsSupplyTests
Description: 设施作业「份额制备料」集成测试（对应反馈「一个建造任务被认领后要等前一个人取完料，第二个人才去取料，非常不符合直觉」）：
①**并行分份**——多人同时认领时按「未认领需求」自动分配份额（第一个按背包容量拿满、第二个拿剩下的、第三个没份额就不入列），各自并行去取料，不再有人排队等；
②**各扣各包**——谁推进就从谁的背包扣料，料尽即本人退场（不挂起任务、不拉别人垫）；
③**轮末自动续轮**——本轮没人了且还有人愿意接手时，设施自检未完成就自动重新发布下一轮（重新分配份额），循环往复直到完成；
④**无料直接挂起**——全基地无料可取时任务挂起（面板「零件不足」），不再空转，补料后点「继续」恢复。
*****/
public sealed class PartsSupplyTests
{
    /*****
    Date: 2026-09-27
    Name: CreateFloorMap
    Description: 创建全地板地图。
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
    Date: 2026-09-27
    Name: CreateSimulation
    Description: 创建带共享占地集与阻塞寻路的最小模拟（无角色/设施）。
    *****/
    private static Simulation CreateSimulation(GridMap map)
    {
        var bus = new EventBus();
        var blocked = new HashSet<Vector2I>();
        return new Simulation(
            new GameClock(), map, new GridPathfinding(map, blocked.Contains),
            new TaskBoard(bus), new NeedSystem(), bus, blocked);
    }

    /*****
    Date: 2026-09-27
    Name: RunToCompletion
    Description: 持续推进模拟直到任务完成（上限保护）；返回是否在限步内完成。
    *****/
    private static bool RunToCompletion(Simulation simulation, ITask task, int steps = 8000)
    {
        for (int i = 0; i < steps && task.State != TaskState.Done; i++) simulation.Update(0.1);
        return task.State == TaskState.Done;
    }

    /*****
    Date: 2026-09-27
    Name: ThreeWorkers_TakeTheirOwnSharesInParallel
    Description: **平均分份 + 并行取料**（对应反馈「第一次点维修/建造后只有一个人去拿所有材料」）：需求 40 件、三名角色都闲着 → 自动**按人均分份（14 / 13 / 13）**，三人**同时**出发取料、回来一起投入施工（总速度≈3 倍），而不是先来的一个人按容量把整份需求全领走。跑完恰好耗 40 件。
    *****/
    [Fact]
    public void ThreeWorkers_TakeTheirOwnSharesInParallel()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var facility = new FacilitySim(null, new Vector2I(6, 6));
        simulation.AddFacility(facility);
        var first = new CharacterSim(null, new Vector2I(0, 0));
        var second = new CharacterSim(null, new Vector2I(1, 0));
        var third = new CharacterSim(null, new Vector2I(2, 0));
        simulation.AddCharacter(first);
        simulation.AddCharacter(second);
        simulation.AddCharacter(third);

        var parts = new FakeItemDef("parts", 100, weightKg: 0.5f);
        ItemStack pile = simulation.Items.Add(parts, 40, new Vector2I(3, 0))[0];
        var task = new RepairTask(facility, 30, 40, parts, maxWorkers: 3);
        simulation.TaskBoard.Submit(task);

        // 提交后第一帧：三条备料任务同时存在（人均一份），三人都已出发
        simulation.Update(0.1);
        List<PickupTask> fetches = simulation.TaskBoard.Active.OfType<PickupTask>().ToList();
        Assert.Equal(3, fetches.Count);
        Assert.Equal(new[] { 13, 13, 14 }, fetches.Select(f => f.IntendedCount).OrderBy(n => n).ToArray());
        Assert.All(fetches, f => Assert.Same(task, f.ServedTask));
        Assert.All(fetches, f => Assert.Equal(CharacterState.Moving, f.Owner!.State));
        Assert.Equal(40, pile.Count); // 还在路上，地面未动

        Assert.True(RunToCompletion(simulation, task), "三人应并行取料并一起完工");

        Assert.Equal(FacilityState.Operational, facility.State);
        Assert.Equal(40, task.PartsConsumed);          // 全程恰好耗成本
        Assert.Equal(0, pile.Count);                   // 40 件全被取走
        Assert.Equal(0, first.Inventory.CountOf(parts));
        Assert.Equal(0, second.Inventory.CountOf(parts));
        Assert.Equal(0, third.Inventory.CountOf(parts));
    }

    /*****
    Date: 2026-09-27
    Name: SingleWorker_RunsOutThenNextRoundFinishes
    Description: 轮末自动续轮：只有一名工人、背包一次装不下全程用量（需求 40、容量 30）→ 第一轮拿 30 件干到料尽**自行退场**（任务不挂起），设施自检未完成 → 自动重新发布下一轮 → 他再领 10 件收尾。全程恰好耗 40 件。
    *****/
    [Fact]
    public void SingleWorker_RunsOutThenNextRoundFinishes()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var facility = new FacilitySim(null, new Vector2I(6, 6));
        simulation.AddFacility(facility);
        var worker = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(worker);

        var parts = new FakeItemDef("parts", 100, weightKg: 0.5f);
        ItemStack pile = simulation.Items.Add(parts, 40, new Vector2I(3, 0))[0];
        var task = new RepairTask(facility, 30, 40, parts, maxWorkers: 1);
        simulation.TaskBoard.Submit(task);

        Assert.True(RunToCompletion(simulation, task), "料尽后应自动开下一轮并跑完");

        Assert.Equal(FacilityState.Operational, facility.State);
        Assert.Equal(40, task.PartsConsumed);
        Assert.Equal(0, pile.Count);
        Assert.Equal(0, worker.Inventory.CountOf(parts));
    }

    /*****
    Date: 2026-09-27
    Name: NoPartsAnywhere_SuspendsInsteadOfChurning
    Description: 无料直接挂起：全基地找不到任何可达的零件堆时，任务立即挂起并标明「零件不足」（不做「发布→取不到→退出→又发布」的空转）；玩家把料补到地上并点「继续」后，工人自行取料并跑完。
    *****/
    [Fact]
    public void NoPartsAnywhere_SuspendsInsteadOfChurning()
    {
        GridMap map = CreateFloorMap(12, 12);
        Simulation simulation = CreateSimulation(map);
        var facility = new FacilitySim(null, new Vector2I(6, 6));
        simulation.AddFacility(facility);
        var worker = new CharacterSim(null, new Vector2I(0, 0));
        simulation.AddCharacter(worker);

        var parts = new FakeItemDef("parts", 100, weightKg: 0.5f);
        var task = new RepairTask(facility, 30, 10, parts, maxWorkers: 1);
        simulation.TaskBoard.Submit(task);

        simulation.Update(0.1);
        Assert.Equal(TaskState.Suspended, task.State);
        Assert.True(task.SuspendedByPartsShortage);
        Assert.Empty(task.Workers);
        Assert.Null(worker.CurrentTask);
        Assert.Empty(simulation.TaskBoard.Active.OfType<PickupTask>()); // 没有空转出来的备料任务

        // 补料 + 「继续」→ 自动取料并跑完
        ItemStack pile = simulation.Items.Add(parts, 10, new Vector2I(2, 0))[0];
        simulation.TaskBoard.Resume(task);
        Assert.True(RunToCompletion(simulation, task));

        Assert.Equal(FacilityState.Operational, facility.State);
        Assert.Equal(10, task.PartsConsumed);
        Assert.Equal(0, pile.Count);
        Assert.False(task.SuspendedByPartsShortage); // 恢复后清除原因标记
    }
}