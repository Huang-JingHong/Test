namespace RelayStation.Core.Tasks;

/*****
Date: 2026-09-06
Name: TaskState
Description: 任务状态枚举；描述任务在生命周期中的当前阶段（Pending=待处理、Assigned=已认领、InProgress=执行中、Done=已完成、Cancelled=已取消）。
*****/
public enum TaskState { Pending, Assigned, InProgress, Done, Cancelled }
