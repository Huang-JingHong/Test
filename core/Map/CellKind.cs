namespace RelayStation.Core.Map;

/*****
Date: 2026-09-25
Name: CellKind
Description: 格子类型枚举；标识地图上每一格的地形类别（Vacuum=真空不可通行、Floor=地板、Wall=墙、Door=门）。设施不再占用格子类型（历史 FacilitySlot 已移除）：设备直接放置于地板之上，占地阻挡由 Simulation 动态维护（见 GridPathfinding 阻塞注入）。
*****/
public enum CellKind { Vacuum, Floor, Wall, Door }
