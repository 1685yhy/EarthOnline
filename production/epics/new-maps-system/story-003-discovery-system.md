# Story 003: 三层发现系统

> **Epic**: 新地图系统
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 8h

## Context

**GDD**: `design/gdd/new-maps-system.md`
**Requirement**: DSC-01 ~ DSC-10

**Engine**: Unity 2022.3.62t11 (Tuanjie 1.9.3) | **Risk**: MEDIUM

## Acceptance Criteria

- [ ] DSC-01: 靠近地标15m自动触发发现事件(名称+背景文案)
- [ ] DSC-02: 发现地标后永久记录在世界地图上
- [ ] DSC-03: POI需要进入触发半径且迷雾已消散才触发
- [ ] DSC-04: POI发现后地图上显示问号标记
- [ ] DSC-05: 隐藏发现不满足条件时不触发
- [ ] DSC-06: 隐藏发现满足条件时正常触发并给予奖励
- [ ] DSC-07: 隐藏发现奖励包含道具/修为/声望至少一种
- [ ] DSC-08: 隐藏发现触发后不自动标记在地图上
- [ ] DSC-09: "神识探查"技能可检测周围隐藏发现
- [ ] DSC-10: 天气/时间条件影响隐藏发现触发

## Implementation Notes

- 扩展现有 `HiddenDiscovery.cs` → 三层架构
- Landmark: 15m自动触发 + 永久标记
- POI: 10m + 迷雾已消散 + 问号标记
- Hidden: 6m + 条件检测 + 不自动标记
- 发现概率公式: `DetectionChance = 0.6 / (1 + (Dist/IdealRadius)²)`

## QA Test Cases

- **DSC-01**: Given:地标位置, When:玩家靠近15m, Then:屏幕显示名称+文案+地图标记
- **DSC-05**: Given:低境界角色, When:走近隐藏发现, Then:不触发
- **DSC-06**: Given:满足条件角色, When:同位置, Then:触发+奖励

## Dependencies
- Depends on: Story 001, Story 002
- Unlocks: Story 004 (快速旅行需发现点为传送候选)
