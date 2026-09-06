using RelayStation.Core.Characters;
using RelayStation.Core.Facilities;
using RelayStation.Core.Tasks;

namespace RelayStation.Core.Events;

/*****
Date: 2026-09-06
Name: TaskSubmittedEvent
Description: 任务提交事件；新任务进入任务板待处理队列时发布。
*****/
public sealed record TaskSubmittedEvent(ITask Task) : IGameEvent;

/*****
Date: 2026-09-06
Name: TaskStateChangedEvent
Description: 任务状态变更事件；任务发生生命周期迁移（认领/开始/完成/取消）时发布。
*****/
public sealed record TaskStateChangedEvent(ITask Task, TaskState NewState) : IGameEvent;

/*****
Date: 2026-09-06
Name: FacilityStateChangedEvent
Description: 设施状态变更事件；设施在 损坏/维修中/运行 之间迁移时发布。
*****/
public sealed record FacilityStateChangedEvent(FacilitySim Facility, FacilityState NewState) : IGameEvent;

/*****
Date: 2026-09-06
Name: CharacterStateChangedEvent
Description: 角色状态变更事件；角色行为状态机迁移（待机/移动/作业/被打断）时发布。
*****/
public sealed record CharacterStateChangedEvent(CharacterSim Character, CharacterState NewState) : IGameEvent;
