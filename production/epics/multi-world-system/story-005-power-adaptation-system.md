# Story 005: 力量适配系统

> **Epic**: multi-world-system
> **Status**: Ready
> **Layer**: Framework
> **Type**: Logic
> **Estimate**: 20h

## Context

**GDD**: `design/gdd/multi-world-system.md`
**Requirement**: ADT-01 ~ ADT-09

**Engine**: Unity 2022.3.62t11 (Tuanjie 1.9.3) | **Risk**: HIGH

跨世界平衡的核心机制。当玩家携带灵魂绑定能力穿越到新世界时，目标世界的规则会"压制"外来力量。玩家需通过时间适应、专项训练、主线任务、特殊契合事件和因果之力逐步降低压制率。

## Acceptance Criteria

- [ ] ADT-01: 进入新世界后，来自其他世界的能力显示压制率
- [ ] ADT-02: 压制率随停留时间逐日降低
- [ ] ADT-03: 完成专项训练后压制率即时降低
- [ ] ADT-04: 主线章节完成时压制率大幅度降低
- [ ] ADT-05: 触发特殊契合事件后压制率降低
- [ ] ADT-06: 压制率最低不低于5%
- [ ] ADT-07: 压制的力量以弱化/变异形式保留部分效果
- [ ] ADT-08: 被压制的力量在返回原世界后恢复完全状态
- [ ] ADT-09: 已完成全部主线后压制率可降至0%

## Implementation Notes

- 新建 `PowerAdaptationSystem.cs`（单例）— 压制率计算和状态管理
- 压制率计算公式实现：
  ```
  初始压制率 = BaseSuppression × (1 - WorldAffinityModifier) × PowerCompatibilityFactor
  ```
  从3.6.1节的压制率矩阵读取 `BaseSuppression`
- 日降压制率：
  ```
  第n天的压制率 = 初始压制率 × (1 - 0.10)^n - 已完成的专项训练次数×0.05
  ```
- 适配还原方式（通过事件监听实现）：
  - 基础适应期：`WorldTimeManager` 每日触发 `OnDayPassed` → `PowerAdaptationSystem.ReduceSuppression()`
  - 专项训练：`TrainingFacility.FinishTraining()` → `ReduceSuppression(5%)`，每世界上限5次
  - 主线解锁：`QuestManager.OnChapterComplete` → `ReduceSuppression(20%)`
  - 特殊契合事件：`SpecialEvent.Trigger()` → `ReduceSuppression(25%)`
  - 因果之力：消耗因果值 → `ReduceSuppression(当前压制率 × 0.5)`
- 最低压制率5%，终章完成可降至0%
- 压制率HUD组件：`PowerSuppressionHUD.cs` — 左上角列表显示所有异世界力量当前压制率
- 被压制能力弱化表现：每种能力在目标世界注册 `SuppressedEffect` — 如剑气→直觉气场、火焰操控→热能管理
- 返回源世界时压制率自动重置为0%
- `SuppressionData` 保存每个源世界→目标世界对的当前压制率

## QA Test Cases

- **ADT-01**: Given:从灵气大陆穿越到都市世界, When:画面加载完成, Then:HUD显示"修真修为 压制率80%"
- **ADT-02**: Given:在都市世界持续停留3天, When:查看压制率, Then:压制率从80%降至约58.3%
- **ADT-06**: Given:完成所有适配方式, When:查看压制率, Then:显示不低于5%（除非终章完成）
- **ADT-07**: Given:修真剑气在都市世界被压制, When:尝试使用, Then:剑气表现为"直觉气场"（谈判感知增强）

## Dependencies
- Depends on: Story 003 (世界穿越), Story 006 (主线进度检测), WorldTimeManager
- Unlocks: 跨世界战斗平衡系统
