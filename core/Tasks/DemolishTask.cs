using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Facilities;
using RelayStation.Core.Resources;

namespace RelayStation.Core.Tasks;

/*****
Date: 2026-09-25
Name: DemolishTask
Description: 拆除任务；目标为已建成的设施，开始作业时设施转入「拆除中（Demolishing）」，完成后经注入的 onRemoved 回调从模拟中移除该设施并按配置返还备用零件（占位资源）。拆除不消耗零件；若任务被取消，设施回滚到发起前状态（避免卡在拆除中）。公共机制由基类 FacilityWorkTask 提供。
2026-09-27 备注：零件消耗已改为「从主工背包扣除」（见 FacilityWorkTask），拆除本身不耗料，故基类零件定义传 null（无需备料）；返还仍走注入的库存（落地点为设施原位）。
*****/
public sealed class DemolishTask : FacilityWorkTask
{
    /*****
    Date: 2026-09-25
    Name: _refundParts
    Description: 完成拆除时返还的备用零件数（0 表示不返还）。
    *****/
    private readonly int _refundParts;

    /*****
    Date: 2026-09-25
    Name: _refundStore
    Description: 返还备用零件的入账库存（生产运行时为地上物品堆 GroundItemStore）；null 表示不返还（开发者模式与测试不注入）。
    *****/
    private readonly IResourceStore? _refundStore;

    /*****
    Date: 2026-09-25
    Name: _onRemoved
    Description: 拆除完成时的回调（参数：被拆除的设施）；由装配方注入以从模拟中移除该设施并同步表现层，可为 null（测试场景）。
    *****/
    private readonly Action<FacilitySim>? _onRemoved;

    /*****
    Date: 2026-09-25
    Name: RequiredSpecialty
    Description: 拆除任务属外勤专长。
    *****/
    public override Specialty? RequiredSpecialty => Specialty.FieldWork;

    /*****
    Date: 2026-09-25
    Name: DemolishTask
    Description: 构造函数；target 为目标设施，demolishGameMinutes 为基准拆除耗时，refundParts 为完成时返还的备用零件数（默认 0），refundStore 为返还入账的库存（默认 null 不返还；开发者模式与测试不注入），maxWorkers 为最大投入人数（默认 1），priority 为任务优先级（默认 5 档），onRemoved 为完成回调（默认 null）。拆除**不消耗零件**，故基类的零件定义传 null（无需备料）。
    *****/
    public DemolishTask(FacilitySim target, double demolishGameMinutes, int refundParts = 0,
        IResourceStore? refundStore = null, int maxWorkers = 1,
        TaskPriority priority = TaskPriority.P5, Action<FacilitySim>? onRemoved = null)
        : base(target, demolishGameMinutes, 0, null, maxWorkers, priority)
    {
        _refundParts = Math.Max(0, refundParts);
        _refundStore = refundStore;
        _onRemoved = onRemoved;
    }

    /*****
    Date: 2026-09-25
    Name: OnWorkStarted
    Description: 开始拆除：设施转入拆除中。
    *****/
    protected override void OnWorkStarted() => Target?.SetState(FacilityState.Demolishing);

    /*****
    Date: 2026-09-26
    Name: OnWorkCompleted
    Description: 拆除完成：**先**通知装配方从模拟中移除设施（腾空其占地格），**再**把返还的备用零件投放到该设施原位——顺序不可颠倒，否则物品会落在仍被设施占用的格子上，破坏「设施与物品不可同格」的互斥不变量。
    *****/
    protected override void OnWorkCompleted()
    {
        if (Target is not { } facility) return;
        Vector2I origin = facility.OriginCell;
        _onRemoved?.Invoke(facility);
        if (_refundParts > 0) _refundStore?.Add(_refundParts, origin);
    }

    /*****
    Date: 2026-09-25
    Name: OnWorkCancelled
    Description: 拆除被取消（如作业格不可达）：设施回滚到发起前状态，避免卡在拆除中。
    *****/
    protected override void OnWorkCancelled() => Target?.SetState(PriorState);
}
