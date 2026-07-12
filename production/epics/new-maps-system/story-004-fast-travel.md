# Story 004: 快速旅行重构

> **Epic**: 新地图系统
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 6h

## Context

**GDD**: `design/gdd/new-maps-system.md`
**Requirement**: TRV-01 ~ TRV-11

**Engine**: Unity 2022.3.62t11 (Tuanjie 1.9.3) | **Risk**: LOW

## Acceptance Criteria

- [ ] TRV-01: 首次到达传送点自动发现并记录
- [ ] TRV-02: 已发现传送点A可传送至已发现传送点B
- [ ] TRV-03: 不可传送至未发现的传送点
- [ ] TRV-04: 传送消耗正确灵石（BaseCost × Distance × RiskFactor）
- [ ] TRV-05: 传送有读条，移动/受伤打断
- [ ] TRV-06: 打断不消耗灵石
- [ ] TRV-07: 打断后30秒禁止传送debuff
- [ ] TRV-08: 战斗中不可使用快速旅行
- [ ] TRV-09: 高危区域传送费用更高
- [ ] TRV-10: 死亡后重生在最近安全点
- [ ] TRV-11: 灭世级事件期间传送点暂时禁用

## Implementation Notes

- 重构现有 `FastTravel.cs`
- 4种传送点类型: 村落/野外哨站/隐秘/破损
- 费用: `TravelCost = 50 × Distance/1000 × (1 + RiskFactor×0.5)`
- 读条: `3s × (1 + RiskFactor×0.5)`
- 事件总线: `EventBus.Publish("OnFastTravel", ...)`

## QA Test Cases

- **TRV-02**: Given:已发现A和B, When:从A传送到B, Then:传送成功+灵石扣除
- **TRV-05**: Given:读条中, When:受击, Then:打断+不扣灵石+30s冷却
- **TRV-10**: Given:死亡在无安全点区域, When:死亡, Then:重生在最近已知安全点

## Dependencies
- Depends on: Story 003 (发现点作为传送候选)
- Unlocks: Story 005
