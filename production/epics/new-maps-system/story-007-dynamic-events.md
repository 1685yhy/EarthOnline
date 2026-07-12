# Story 007: 动态事件触发

> **Epic**: 新地图系统
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 6h

## Context

**GDD**: `design/gdd/new-maps-system.md`
**Requirement**: EVT-01 ~ EVT-06

**Engine**: Unity 2022.3.62t11 (Tuanjie 1.9.3) | **Risk**: MEDIUM

## Acceptance Criteria

- [ ] EVT-01: 动态事件在区域中按概率触发(5%/游戏小时)
- [ ] EVT-02: 事件触发时区域内玩家收到通知
- [ ] EVT-03: 事件期间区域风险等级改变
- [ ] EVT-04: 同一区域最多同时触发3个事件
- [ ] EVT-05: 互斥事件合并为连锁事件
- [ ] EVT-06: 事件结束后区域状态恢复

## Implementation Notes

- 新建 `DynamicEventSystem.cs`
- 每区域定义事件列表+刷新概率
- 公式: `EventTriggerChance = 0.05/小时 × ActivityModifier × TimeModifier`
- 事件兼容性检查: 兼容→叠加 / 互斥→合并连锁
- 事件总线: `EventBus.Publish("OnDynamicEvent", ...)`

## QA Test Cases

- **EVT-01**: Given:玩家在区域活跃, When:多游戏小时, Then:事件概率触发
- **EVT-03**: Given:事件进行中, When:查看风险等级, Then:等级临时改变
- **EVT-04**: Given:已有3个事件, When:第4个触发, Then:进入等待队列

## Dependencies
- Depends on: Story 005, Story 006
- Unlocks: Story 008
