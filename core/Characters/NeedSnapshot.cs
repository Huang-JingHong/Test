namespace RelayStation.Core.Characters;

/*****
Date: 2026-09-06
Name: NeedSnapshot
Description: 需求快照；记录角色三项需求（呼吸/进食/休息）的当前数值（0-100）。
*****/
public sealed record NeedSnapshot(float Oxygen, float Food, float Rest);
