namespace RelayStation.Core.Tasks;

/*****
Date: 2026-09-06
Name: TaskState
Description: 任务状态枚举；描述任务在生命周期中的当前阶段（Pending=待处理、Assigned=已认领、InProgress=执行中、Suspended=已中止〔挂起，保留进度、可恢复〕、Done=已完成、Cancelled=已取消）。
2026-09-26 末尾追加 Suspended：仅用于「设施中止」——任务挂起而非取消，故进度与设施状态（含施工警戒线）天然保留；恢复即回到 Pending 重新排队。
*****/
public enum TaskState { Pending, Assigned, InProgress, Suspended, Done, Cancelled }
