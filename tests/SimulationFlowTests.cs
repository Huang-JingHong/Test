using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Events;
using RelayStation.Core.Facilities;
using RelayStation.Core.Map;
using RelayStation.Core.Tasks;
using RelayStation.Core.Time;
using Xunit;

namespace RelayStation.Tests;

/*****
Date: 2026-09-06
Name: SimulationFlowTests
Description: 模拟层闭环集成测试；基于真实环形基地验证「提交修复任务 → 角色认领 → 寻路移动 → 维修推进 → 设施运行」完整流转，以及五功能区锚点/出生格完整性与跨区连通性。
*****/
public sealed class SimulationFlowTests
{
    /*****
    Date: 2026-09-06
    Name: RingMap_AllZonesHaveAnchorsAndAreInterconnected
    Description: 环形基地生成校验：五个功能区均有设施锚点与出生格，且任意两功能区出生格之间可互相寻路抵达。
    *****/
    [Fact]
    public void RingMap_AllZonesHaveAnchorsAndAreInterconnected()
    {
        RingMapData ring = RingMapGenerator.Generate();
        var pathfinding = new GridPathfinding(ring.Map);

        foreach (ZoneId zone in Enum.GetValues<ZoneId>())
        {
            Assert.True(ring.Rooms[zone].FacilityAnchor.HasValue, $"功能区 {zone} 缺少设施锚点");
            Assert.NotEmpty(ring.Rooms[zone].SpawnCells);
        }

        ZoneId[] zones = Enum.GetValues<ZoneId>();
        for (int i = 0; i < zones.Length; i++)
        {
            for (int j = i + 1; j < zones.Length; j++)
            {
                Vector2I from = ring.Rooms[zones[i]].SpawnCells[0];
                Vector2I to = ring.Rooms[zones[j]].SpawnCells[0];
                Assert.True(pathfinding.FindPath(from, to).Count > 0, $"{zones[i]} → {zones[j]} 不可达");
            }
        }
    }

    /*****
    Date: 2026-09-06
    Name: RepairLoop_PickMoveWorkComplete
    Description: 修复闭环：提交任务后角色进入移动；持续推进模拟后抵达并完成维修——设施转为运行、任务完成、角色回到待机。
    *****/
    [Fact]
    public void RepairLoop_PickMoveWorkComplete()
    {
        RingMapData ring = RingMapGenerator.Generate();
        var clock = new GameClock();
        var eventBus = new EventBus();
        var simulation = new Simulation(
            clock,
            ring.Map,
            new GridPathfinding(ring.Map),
            new TaskBoard(eventBus),
            new NeedSystem(),
            eventBus);

        var facility = new FacilitySim(null, ring.Rooms[ZoneId.LifeSupport].FacilityAnchor!.Value);
        simulation.AddFacility(facility);

        var character = new CharacterSim(null, ring.Rooms[ZoneId.Living].SpawnCells[0]);
        simulation.AddCharacter(character);

        var task = new RepairTask(facility, 10);
        simulation.TaskBoard.Submit(task);

        // 第一帧：空闲角色认领任务并开始移动
        simulation.Update(1.0 / 60.0);
        Assert.Equal(CharacterState.Moving, character.State);
        Assert.Same(task, character.CurrentTask);

        // 持续推进 120 游戏分钟：足够走完跨区路径并完成 10 分钟维修（Def 为 null → 倍率 1）
        for (int i = 0; i < 1200; i++)
        {
            simulation.Update(0.1);
        }

        Assert.Equal(CharacterState.Idle, character.State);
        Assert.Null(character.CurrentTask);
        Assert.Equal(TaskState.Done, task.State);
        Assert.Equal(FacilityState.Operational, facility.State);
    }
}
