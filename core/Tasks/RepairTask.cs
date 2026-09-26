using RelayStation.Core.Characters;
using RelayStation.Core.Facilities;
using RelayStation.Core.Items;

namespace RelayStation.Core.Tasks;

/*****
Date: 2026-09-25
Name: RepairTask
Description: 修复任务；目标为损坏设施，完成后设施转为 Operational。公共机制（进度推进、实时零件消耗、多人协作、优先级）由基类 FacilityWorkTask 提供，本类只表达「修复」的专长与状态迁移语义。
*****/
public sealed class RepairTask : FacilityWorkTask
{
    /*****
    Date: 2026-09-25
    Name: RequiredSpecialty
    Description: 修复任务属维修专长。
    *****/
    public override Specialty? RequiredSpecialty => Specialty.Maintenance;

    /*****
    Date: 2026-09-25
    Name: RepairTask
    Description: 构造函数；target 为目标设施，repairGameMinutes 为基准修复耗时，repairPartsCost 为全程零件消耗（默认 0 不消耗），partsItem 为从主工背包扣除的零件物品定义（默认 null 不扣件；开发者模式与引擎外测试不注入），maxWorkers 为最大投入人数（默认 1 单人），priority 为任务优先级（默认 5 档）。
    *****/
    public RepairTask(FacilitySim target, double repairGameMinutes,
        int repairPartsCost = 0, IItemDef? partsItem = null, int maxWorkers = 1,
        TaskPriority priority = TaskPriority.P5)
        : base(target, repairGameMinutes, repairPartsCost, partsItem, maxWorkers, priority)
    {
    }

    /*****
    Date: 2026-09-25
    Name: OnWorkStarted
    Description: 开始修复：设施转入维修中。
    *****/
    protected override void OnWorkStarted() => Target?.SetState(FacilityState.UnderRepair);

    /*****
    Date: 2026-09-25
    Name: OnWorkCompleted
    Description: 修复完成：设施转为运行。
    *****/
    protected override void OnWorkCompleted() => Target?.SetState(FacilityState.Operational);

    /*****
    Date: 2026-09-25
    Name: OnWorkCancelled
    Description: 修复被取消（如作业格不可达）：设施回滚到发起前状态（通常为损坏），避免卡在维修中无法再次修复。
    *****/
    protected override void OnWorkCancelled() => Target?.SetState(PriorState);
}
