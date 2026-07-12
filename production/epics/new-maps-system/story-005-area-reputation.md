# Story 005: 区域生态+声望

> **Epic**: 新地图系统
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 8h

## Context

**GDD**: `design/gdd/new-maps-system.md`
**Requirement**: REP-01 ~ REP-07, 区域生态定义

**Engine**: Unity 2022.3.62t11 (Tuanjie 1.9.3) | **Risk**: MEDIUM

## Acceptance Criteria

- [ ] REP-01: 完成区域内任务后声望增加
- [ ] REP-02: 击杀区域内友好NPC后声望减少
- [ ] REP-03: 声望"尊敬"后商品8折
- [ ] REP-04: 声望"敌对"后部分NPC拒绝交易
- [ ] REP-05: 声望"仇恨"后进入区域触发追杀
- [ ] REP-06: 声望每日自然衰减（中立衰减最快50%）
- [ ] REP-07: 声望在-200~200范围内可正常传送
- [ ] 每个区域有独立的资源生态（灵材/妖兽/NPC/势力）

## Implementation Notes

- 新建 `AreaReputation.cs`、`RegionEcosystem.cs`
- 声望范围: -1000 ~ 1000，6个等级
- 每日衰减: `BaseDecay(5) × (1 - LevelFactor)`
- 生态: 资源池+刷新周期+动态事件槽位
- 与 FactionSystem 联动：门派控制区域影响声望

## QA Test Cases

- **REP-01**: Given:完成区域任务, When:检查声望, Then:声望值增加
- **REP-04**: Given:声望敌对, When:与NPC对话, Then:拒绝交易
- **REP-05**: Given:声望仇恨, When:踏入区域, Then:守卫追杀触发

## Dependencies
- Depends on: Story 004
- Unlocks: Story 006, Story 007
