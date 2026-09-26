using RelayStation.Core.Characters;
using RelayStation.Core.Facilities;
using RelayStation.Core.Items;

namespace RelayStation.Core.Tasks;

/*****
Date: 2026-09-25
Name: BuildTask
Description: 建造任务；目标为「建造中（UnderConstruction）」的占位设施（放置时即已入模拟并阻挡寻路），完成后设施转为 Operational。零件随建造进度实时扣除（由基类完成）；若任务被取消（如作业格不可达），经注入的 onAbandoned 回调移除未完成的设施（已消耗零件不退还，沉没成本）。公共机制由基类 FacilityWorkTask 提供。
*****/
public sealed class BuildTask : FacilityWorkTask
{
    /*****
    Date: 2026-09-25
    Name: _onAbandoned
    Description: 任务被取消时的回调（参数：未完成的设施）；由装配方注入以从模拟中移除该设施并同步表现层，可为 null（测试场景）。
    *****/
    private readonly Action<FacilitySim>? _onAbandoned;

    /*****
    Date: 2026-09-25
    Name: RequiredSpecialty
    Description: 建造任务属维修专长（与修复同类作业）。
    *****/
    public override Specialty? RequiredSpecialty => Specialty.Maintenance;

    /*****
    Date: 2026-09-25
    Name: BuildTask
    Description: 构造函数；target 为建造中的占位设施，buildGameMinutes 为基准建造耗时，buildPartsCost 为全程零件消耗（默认 0 不消耗），partsItem 为从主工背包扣除的零件物品定义（默认 null 不扣件；开发者模式与测试不注入），maxWorkers 为最大投入人数（默认 1），priority 为任务优先级（默认 5 档），onAbandoned 为取消回调（默认 null）。
    *****/
    public BuildTask(FacilitySim target, double buildGameMinutes,
        int buildPartsCost = 0, IItemDef? partsItem = null, int maxWorkers = 1,
        TaskPriority priority = TaskPriority.P5, Action<FacilitySim>? onAbandoned = null)
        : base(target, buildGameMinutes, buildPartsCost, partsItem, maxWorkers, priority)
    {
        _onAbandoned = onAbandoned;
    }

    /*****
    Date: 2026-09-25
    Name: OnWorkStarted
    Description: 开始建造：确保设施处于建造中（放置时已置，幂等）。
    *****/
    protected override void OnWorkStarted() => Target?.SetState(FacilityState.UnderConstruction);

    /*****
    Date: 2026-09-25
    Name: OnWorkCompleted
    Description: 建造完成：设施转为运行。
    *****/
    protected override void OnWorkCompleted() => Target?.SetState(FacilityState.Operational);

    /*****
    Date: 2026-09-25
    Name: OnWorkCancelled
    Description: 建造被取消：通知装配方移除未完成的设施（设施从未建成，视为撤销建造指令）。
    *****/
    protected override void OnWorkCancelled()
    {
        if (Target is { } facility) _onAbandoned?.Invoke(facility);
    }
}
