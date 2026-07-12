# Story 002: 风险评级系统

> **Epic**: 新地图系统
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h

## Context

**GDD**: `design/gdd/new-maps-system.md`
**Requirement**: RSK-01 ~ RSK-09

**Engine**: Unity 2022.3.62t11 (Tuanjie 1.9.3) | **Risk**: LOW

## Acceptance Criteria

- [ ] RSK-01: 靠近区域边界50m出现边缘预警（淡红+文字提示）
- [ ] RSK-02: 跨越边界弹出风险确认面板（名称+等级+威胁类型）
- [ ] RSK-03: 风险等级根据玩家境界动态变化
- [ ] RSK-04: 夜晚高风险区域风险等级提升(+15)
- [ ] RSK-05: 动态事件期间风险等级临时改变
- [ ] RSK-06: 越级区域显示"极度危险"
- [ ] RSK-07: 确认后玩家可自由进入，无阻拦
- [ ] RSK-08: HUD角落持续显示风险等级图标（绿→红）
- [ ] RSK-09: RiskFactor影响死亡修为损失

## Implementation Notes

- 新建 `RiskRating.cs`
- 公式: `RiskFactor = Clamp01((BaseRiskRating - PlayerEffectivePower) / BaseRiskRating)`
- 5级显示: 安全(<0.2) / 低风险 / 中等 / 高风险 / 极度危险(≥0.8)
- `NightRiskModifier = 15`, `EventRiskModifier = 20`
- **只预警不锁门** — 高风险区域玩家仍可自由踏入

## QA Test Cases

- **RSK-01**: Given:接近区域边界50m, When:边界预警触发, Then:淡红光晕+文字提示出现
- **RSK-03**: Given:练气/金丹角色, When:查看同一区域风险, Then:等级不同
- **RSK-06**: Given:练气期角色, When:靠近金丹区域, Then:显示"极度危险"

## Dependencies
- Depends on: Story 001 (迷雾需要区域定义)
- Unlocks: Story 003 (发现系统依赖风险评级)
