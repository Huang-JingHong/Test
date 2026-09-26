using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Facilities;
using RelayStation.Core.Items;
using RelayStation.Core.Tasks;
using Xunit;

namespace RelayStation.Tests;

/*****
Date: 2026-09-25
Name: RepairTaskConsumptionTests
Description: 修复任务实时零件消耗与进度单元测试；覆盖免费模式行为不变、按比例整件扣除、**主工料尽即本人退场（不挂起任务、不补料）**、补料后重新加入并跑完、多人各扣各包、小数累计凑整与完成收尾。
2026-09-27 口径变更（份额制）：零件不再是「全局库存」或「主工专属」——**谁推进就从谁的背包扣**（ProgressWork 带推进者），各人扣自己那一份，合计恰好等于成本；某人背包不足以支付下一整件时，该人脱离任务（本人这一轮到头），任务与其余工人不受影响。
*****/
public sealed class RepairTaskConsumptionTests
{
    /*****
    Date: 2026-09-25
    Name: CreateFacility
    Description: 创建无定义损坏设施（引擎外可构造）。
    *****/
    private static FacilitySim CreateFacility() => new(null, new Vector2I(10, 10));

    /*****
    Date: 2026-09-27
    Name: CreatePartsDef
    Description: 创建备用零件替身定义（单组 100、单件 0.5kg）。
    *****/
    private static FakeItemDef CreatePartsDef() => new("parts", 100, weightKg: 0.5f);

    /*****
    Date: 2026-09-27
    Name: StartTask
    Description: 构造任务并推进到 InProgress（Pending → Assigned → InProgress）；返回任务与主工（carried 为预置在背包里的零件数）。
    *****/
    private static (RepairTask Task, CharacterSim Worker) StartTask(FacilitySim facility, double minutes,
        int cost, IItemDef? partsItem, int carried = 0, int maxWorkers = 1)
    {
        var worker = new CharacterSim(null, new Vector2I(9, 10));
        if (partsItem != null && carried > 0) Assert.True(worker.Inventory.TryAdd(partsItem, carried));

        var task = new RepairTask(facility, minutes, cost, partsItem, maxWorkers);
        task.MarkAssigned(worker);
        task.StartWork();
        return (task, worker);
    }

    /*****
    Date: 2026-09-25
    Name: NoPartsItem_ZeroConsumedAndProgressWorks
    Description: 未注入零件定义（开发者模式 / 引擎外免费模式）时不消耗零件，进度照常推进。
    *****/
    [Fact]
    public void NoPartsItem_ZeroConsumedAndProgressWorks()
    {
        var (task, worker) = StartTask(CreateFacility(), 30, 10, partsItem: null);

        bool done = task.ProgressWork(worker, 15);
        Assert.False(done);
        Assert.Equal(0, task.PartsConsumed);
        Assert.Equal(15, task.RemainingGameMinutes, 5);
        Assert.Equal(0.5, task.ProgressFraction, 5);
        Assert.True(task.ProgressWork(worker, 15));
        Assert.Equal(TaskState.Done, task.State);
    }

    /*****
    Date: 2026-09-25
    Name: ProgressHalf_ConsumesHalfCostFromOwnBackpack
    Description: 推进一半进度时按比例整件扣除零件（10 个成本的 50% = 5 件），且**扣的是推进者自己的背包**。
    *****/
    [Fact]
    public void ProgressHalf_ConsumesHalfCostFromOwnBackpack()
    {
        FakeItemDef parts = CreatePartsDef();
        var (task, worker) = StartTask(CreateFacility(), 30, 10, parts, carried: 10);

        bool done = task.ProgressWork(worker, 15);

        Assert.False(done);
        Assert.Equal(5, task.PartsConsumed);
        Assert.Equal(5, worker.Inventory.CountOf(parts));
        Assert.Equal(0.5, task.ProgressFraction, 5);
    }

    /*****
    Date: 2026-09-27
    Name: InsufficientParts_WorkerLeavesTask
    Description: 推进者背包付不出下一整件 = **他这一轮到头**：本人退出任务（保留进度与背包），任务仍为 InProgress、不挂起、不消耗零件；若没有别的工人，由模拟层的轮末扫描决定「自动开下一轮」还是「转已中止」。
    *****/
    [Fact]
    public void InsufficientParts_WorkerLeavesTask()
    {
        FakeItemDef parts = CreatePartsDef();
        FacilitySim facility = CreateFacility();
        var (task, worker) = StartTask(facility, 30, 10, parts, carried: 3);

        bool done = task.ProgressWork(worker, 15); // 需要 5 件，背包 3

        Assert.False(done);
        Assert.Equal(TaskState.InProgress, task.State); // 任务不受影响（不挂起）
        Assert.Empty(task.Workers);                     // 本人退出
        Assert.Null(worker.CurrentTask);
        Assert.Equal(30, task.RemainingGameMinutes, 5); // 进度原地保留
        Assert.Equal(3, worker.Inventory.CountOf(parts));
        Assert.Equal(0, task.PartsConsumed);
        Assert.Equal(0.0, task.ProgressFraction, 5);
        Assert.Equal(FacilityState.UnderRepair, facility.State); // 设施不回滚（保留作业态与警戒线）
        Assert.False(task.SuspendedByPartsShortage);
    }

    /*****
    Date: 2026-09-27
    Name: RefilledWorkerRejoins_ResumeAndComplete
    Description: 料尽退场后补料重新加入：可继续推进并最终完成（设施转 Operational、零件总消耗等于成本、恰好扣满）。
    *****/
    [Fact]
    public void RefilledWorkerRejoins_ResumeAndComplete()
    {
        FakeItemDef parts = CreatePartsDef();
        FacilitySim facility = CreateFacility();
        var (task, worker) = StartTask(facility, 30, 10, parts, carried: 3);

        task.ProgressWork(worker, 15); // 料尽 → 本人退场
        Assert.Empty(task.Workers);

        worker.SetState(CharacterState.Idle); // 退场后角色为 Interrupted，回到空闲才能重新加入
        Assert.True(worker.Inventory.TryAdd(parts, 12)); // 补零件
        Assert.True(task.TryJoin(worker));                // 空闲后重新加入（InProgress 仍可加入）
        Assert.Same(worker, task.Assignee);

        bool done = task.ProgressWork(worker, 15); // 恢复推进：扣 5

        Assert.False(done);
        Assert.Equal(5, task.PartsConsumed);
        Assert.Equal(10, worker.Inventory.CountOf(parts)); // 3+12-5
        Assert.True(task.ProgressWork(worker, 15)); // 完成：再扣 5（3+12 = 15 → 剩 5）
        Assert.Equal(10, task.PartsConsumed);
        Assert.Equal(5, worker.Inventory.CountOf(parts)); // 全程只耗成本内的 10 件，多带的留在背包里
        Assert.Equal(TaskState.Done, task.State);
        Assert.Equal(FacilityState.Operational, facility.State);
        Assert.Equal(1.0, task.ProgressFraction, 5);
    }

    /*****
    Date: 2026-09-27
    Name: TwoWorkers_PayFromTheirOwnBackpacks
    Description: 多人各扣各的：两名工人各带 5 件、各推进一半 → 两人都扣 5 件、合计恰好等于成本 10，且无人中途退场。
    *****/
    [Fact]
    public void TwoWorkers_PayFromTheirOwnBackpacks()
    {
        FakeItemDef parts = CreatePartsDef();
        FacilitySim facility = CreateFacility();
        var (task, first) = StartTask(facility, 30, 10, parts, carried: 5, maxWorkers: 2);
        var second = new CharacterSim(null, new Vector2I(8, 10));
        Assert.True(second.Inventory.TryAdd(parts, 5));
        Assert.True(task.TryJoin(second));

        Assert.False(task.ProgressWork(first, 15));  // 前半程：扣 first 的 5 件
        Assert.True(task.ProgressWork(second, 15));  // 后半程：扣 second 的 5 件并完成

        Assert.Equal(2, task.Workers.Count); // 两人都没被退场
        Assert.Equal(10, task.PartsConsumed);
        Assert.Equal(0, first.Inventory.CountOf(parts));
        Assert.Equal(0, second.Inventory.CountOf(parts));
        Assert.Equal(TaskState.Done, task.State);
    }

    /*****
    Date: 2026-09-25
    Name: FractionalAccrual_ConsumesAtWholeBoundaries
    Description: 小数累计凑整：成本 1、总时 30 分钟，前半程不扣（累计 0.5），后半程凑整扣 1 件并完成。
    *****/
    [Fact]
    public void FractionalAccrual_ConsumesAtWholeBoundaries()
    {
        FakeItemDef parts = CreatePartsDef();
        FacilitySim facility = CreateFacility();
        var (task, worker) = StartTask(facility, 30, 1, parts, carried: 1);

        Assert.False(task.ProgressWork(worker, 15));
        Assert.Equal(0, task.PartsConsumed); // 0.5 件不足整件
        Assert.Equal(1, worker.Inventory.CountOf(parts));

        Assert.True(task.ProgressWork(worker, 15));
        Assert.Equal(1, task.PartsConsumed);
        Assert.Equal(0, worker.Inventory.CountOf(parts));
        Assert.Equal(TaskState.Done, task.State);
        Assert.Equal(FacilityState.Operational, facility.State);
    }

    /*****
    Date: 2026-09-25
    Name: CompletionSpendsOnlyAccruedRemainder
    Description: 完成帧只按有效进度扣费（推进量超过剩余量时不超额消耗），并由收尾结算补齐余额。
    *****/
    [Fact]
    public void CompletionSpendsOnlyAccruedRemainder()
    {
        FakeItemDef parts = CreatePartsDef();
        FacilitySim facility = CreateFacility();
        var (task, worker) = StartTask(facility, 30, 10, parts, carried: 10);

        task.ProgressWork(worker, 15); // 扣 5
        bool done = task.ProgressWork(worker, 999); // 一次推进到完成（有效 15 分钟 → 扣 5）

        Assert.True(done);
        Assert.Equal(10, task.PartsConsumed);
        Assert.Equal(0, worker.Inventory.CountOf(parts));
        Assert.Equal(TaskState.Done, task.State);
    }
}