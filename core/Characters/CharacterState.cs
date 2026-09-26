namespace RelayStation.Core.Characters;

/*****
Date: 2026-09-06
Name: CharacterState
Description: 角色行为状态枚举；描述角色当前所处的行为阶段（Idle=待机、Moving=移动、Working=作业、Interrupted=被打断）。
*****/
public enum CharacterState { Idle, Moving, Working, Interrupted }
