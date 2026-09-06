namespace RelayStation.Core.Map;

/*****
Date: 2026-09-06
Name: CellKind
Description: 格子类型枚举；标识地图上每一格的地形类别（Vacuum=真空不可通行、Floor=地板、Wall=墙、Door=门、FacilitySlot=设施占位）。
*****/
public enum CellKind { Vacuum, Floor, Wall, Door, FacilitySlot }
