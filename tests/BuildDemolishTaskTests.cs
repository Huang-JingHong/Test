using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Events;
using RelayStation.Core.Facilities;
using RelayStation.Core.Map;
using RelayStation.Core.Resources;
using RelayStation.Core.Tasks;
using RelayStation.Core.Time;
using Xunit;

namespace RelayStation.Tests;

/*****
Date: 2026-09-25
Name: BuildDemolishTaskTests
Description: 建造/拆除任务与设施分类单元测试；覆盖建造实时零件消耗与完成转运行、建造取消回调移除设施、拆除开始转拆除中并在完成时返还零件 + 回调移除、拆除取消回滚原状态、Simulation 建造闭环，以及新增枚举值/分类值的稳定性（保护既有 .tres 与存档整型值）。
*****/
public sealed class BuildDemolishTaskTests
{
    /*****
    Date: 2026-09-25
    Name: CreateFacility
    Description: 创建无定义设施（引擎外可构造）。
    *****/
    private static FacilitySim CreateFacility() => new(null, new Vector2I(10, 10));

    /*****
    Date: 2026-09-25
    Name: CreateCharacter
    Description: 创建无定义空闲角色。
    *****/
    private static CharacterSim CreateCharacter() => new(null, new Vector2I(9, 10));

    /*****
    Date: 2026-09-25
    Name: BuildTask_ConsumesPartsAndTurnsOperational
    Description: 建造任务随进度实时扣件、完成后设施转运行；进度比例与剩余量按比例推进。
    *****/
    [Fact]
    public void BuildTask_ConsumesPartsAndTurnsOperational()
    {
        FacilitySim facility = CreateFacility();
        facility.SetState(FacilityState.UnderConstruction);
        var parts = new FakeItemDef("parts", 100, weightKg: 0.5f);
        var worker = CreateCharacter();
        Assert.True(worker.Inventory.TryAdd(parts, 10)); // 零件随工携带（从主工背包扣）
        var task = new BuildTask(facility, 30, 10, parts, maxWorkers: 1);
        task.MarkAssigned(worker);
        task.StartWork();

        Assert.False(task.ProgressWork(worker, 15)); // 半程扣 5 件
        Assert.Equal(5, task.PartsConsumed);
        Assert.Equal(5, worker.Inventory.CountOf(parts));
        Assert.Equal(0.5, task.ProgressFraction, 5);
        Assert.Equal(FacilityState.UnderConstruction, facility.State);

        Assert.True(task.ProgressWork(worker, 15));
        Assert.Equal(10, task.PartsConsumed);
        Assert.Equal(0, worker.Inventory.CountOf(parts));
        Assert.Equal(TaskState.Done, task.State);
        Assert.Equal(FacilityState.Operational, facility.State);
    }

    /*****
    Date: 2026-09-25
    Name: BuildTask_Cancel_InvokesAbandoned
    Description: 建造任务被取消时经 onAbandoned 回调上报未完成设施（供装配方移除）。
    *****/
    [Fact]
    public void BuildTask_Cancel_InvokesAbandoned()
    {
        FacilitySim facility = CreateFacility();
        facility.SetState(FacilityState.UnderConstruction);
        FacilitySim? abandoned = null;
        var task = new BuildTask(facility, 30, 0, null, 1, TaskPriority.P5, f => abandoned = f);

        task.MarkAssigned(CreateCharacter());
        task.StartWork();
        task.MarkCancelled();

        Assert.Equal(TaskState.Cancelled, task.State);
        Assert.Same(facility, abandoned);
    }

    /*****
    Date: 2026-09-25
    Name: DemolishTask_RemovesAndRefundsOnComplete
    Description: 拆除任务开始转拆除中、不消耗零件，完成时返还零件并经 onRemoved 回调上报设施。
    *****/
    [Fact]
    public void DemolishTask_RemovesAndRefundsOnComplete()
    {
        FacilitySim facility = CreateFacility();
        facility.SetState(FacilityState.Operational);
        var store = new ResourceStore(0);
        FacilitySim? removed = null;
        var task = new DemolishTask(facility, 20, 10, store, 1, TaskPriority.P5, f => removed = f);

        var worker = CreateCharacter();
        task.MarkAssigned(worker);
        task.StartWork();
        Assert.Equal(FacilityState.Demolishing, facility.State);

        Assert.True(task.ProgressWork(worker, 20));
        Assert.Equal(0, task.PartsConsumed); // 拆除不消耗
        Assert.Equal(10, store.Balance);     // 完成返还
        Assert.Same(facility, removed);
        Assert.Equal(TaskState.Done, task.State);
    }

    /*****
    Date: 2026-09-25
    Name: DemolishTask_Cancel_RestoresPriorState
    Description: 拆除任务被取消时设施回滚到发起前状态（示例为运行），不卡在拆除中。
    *****/
    [Fact]
    public void DemolishTask_Cancel_RestoresPriorState()
    {
        FacilitySim facility = CreateFacility();
        facility.SetState(FacilityState.Operational);
        var task = new DemolishTask(facility, 20, 0, null);

        task.MarkAssigned(CreateCharacter());
        task.StartWork();
        Assert.Equal(FacilityState.Demolishing, facility.State);

        task.MarkCancelled();
        Assert.Equal(FacilityState.Operational, facility.State);
        Assert.Equal(TaskState.Cancelled, task.State);
    }

    /*****
    Date: 2026-09-25
    Name: RepairTask_Cancel_RestoresDamagedState
    Description: 修复任务被取消时设施回滚到发起前状态（损坏），避免卡在维修中无法再次修复。
    *****/
    [Fact]
    public void RepairTask_Cancel_RestoresDamagedState()
    {
        FacilitySim facility = CreateFacility();
        var task = new RepairTask(facility, 30, 0, null);

        task.MarkAssigned(CreateCharacter());
        task.StartWork();
        Assert.Equal(FacilityState.UnderRepair, facility.State);

        task.MarkCancelled();
        Assert.Equal(FacilityState.Damaged, facility.State);
    }

    /*****
    Date: 2026-09-25
    Name: BuildLoop_CompletesAtFacilityAnchor
    Description: Simulation 建造闭环：建造中设施占位阻挡、工人抵达开工、按时间推进并实时**从自己背包**扣件，完成后设施转运行、背包零件耗尽。
    *****/
    [Fact]
    public void BuildLoop_CompletesAtFacilityAnchor()
    {
        RingMapData ring = RingMapGenerator.Generate();
        var eventBus = new EventBus();
        var simulation = new Simulation(
            new GameClock(),
            ring.Map,
            new GridPathfinding(ring.Map),
            new TaskBoard(eventBus),
            new NeedSystem(),
            eventBus);

        var facility = new FacilitySim(null, ring.Rooms[ZoneId.LifeSupport].FacilityAnchor!.Value);
        facility.SetState(FacilityState.UnderConstruction);
        simulation.AddFacility(facility);

        var worker = new CharacterSim(null, ring.Rooms[ZoneId.Living].SpawnCells[0]);
        simulation.AddCharacter(worker);

        var parts = new FakeItemDef("parts", 100, weightKg: 0.5f);
        Assert.True(worker.Inventory.TryAdd(parts, 10)); // 自备零件：无需备料，直接开工
        var task = new BuildTask(facility, 10, 10, parts, maxWorkers: 1);
        simulation.TaskBoard.Submit(task);

        for (int i = 0; i < 6000 && task.State != TaskState.Done; i++)
        {
            simulation.Update(0.1);
        }

        Assert.Equal(TaskState.Done, task.State);
        Assert.Equal(FacilityState.Operational, facility.State);
        Assert.Equal(0, worker.Inventory.CountOf(parts));
        Assert.Equal(CharacterState.Idle, worker.State);
    }

    /*****
    Date: 2026-09-25
    Name: FacilityState_EnumValuesStable
    Description: FacilityState 新增值只能追加：既有整型值（0/1/2）保持不变，保护 .tres 与存档中 InitialState/State 的语义。
    *****/
    [Fact]
    public void FacilityState_EnumValuesStable()
    {
        Assert.Equal(0, (int)FacilityState.Damaged);
        Assert.Equal(1, (int)FacilityState.UnderRepair);
        Assert.Equal(2, (int)FacilityState.Operational);
        Assert.Equal(3, (int)FacilityState.UnderConstruction);
        Assert.Equal(4, (int)FacilityState.Demolishing);
    }

    /*****
    Date: 2026-09-25
    Name: FacilityCategory_EnumValuesStable
    Description: FacilityCategory 各分类整型值稳定（保护设施 .tres 中 Category 字段的语义）。
    *****/
    [Fact]
    public void FacilityCategory_EnumValuesStable()
    {
        Assert.Equal(0, (int)FacilityCategory.Furniture);
        Assert.Equal(1, (int)FacilityCategory.Power);
        Assert.Equal(2, (int)FacilityCategory.Oxygen);
        Assert.Equal(3, (int)FacilityCategory.Communication);
        Assert.Equal(4, (int)FacilityCategory.Water);
        Assert.Equal(5, (int)FacilityCategory.Building);
        Assert.Equal(6, (int)FacilityCategory.Medical);
        Assert.Equal(7, (int)FacilityCategory.Research);
    }

    /*****
    Date: 2026-09-25
    Name: FacilityCatalog_GroupsByCategoryInEnumOrder
    Description: 设施目录按分类分组且分类按枚举序，空分类返回空列表。FacilityDef 为 Godot Resource，引擎外测试进程无法构造（构造即崩），本用例跳过——目录分组由无头冒烟与建造面板实测覆盖。
    *****/
    [Fact(Skip = "FacilityDef(Resource) 需引擎运行时，由无头冒烟覆盖")]
    public void FacilityCatalog_GroupsByCategoryInEnumOrder()
    {
        var defs = new[]
        {
            new FacilityDef { Id = "a", Category = FacilityCategory.Medical },
            new FacilityDef { Id = "b", Category = FacilityCategory.Furniture },
            new FacilityDef { Id = "c", Category = FacilityCategory.Furniture },
        };
        var catalog = new FacilityCatalog(defs);

        Assert.Equal(3, catalog.All.Count);
        Assert.Equal(2, catalog.Categories.Count);
        Assert.Equal(FacilityCategory.Furniture, catalog.Categories[0]);
        Assert.Equal(FacilityCategory.Medical, catalog.Categories[1]);
        Assert.Equal(2, catalog.GetByCategory(FacilityCategory.Furniture).Count);
        Assert.Empty(catalog.GetByCategory(FacilityCategory.Power));
    }
}
