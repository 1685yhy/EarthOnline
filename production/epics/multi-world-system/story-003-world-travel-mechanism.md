# Story 003: 世界穿越机制

> **Epic**: multi-world-system
> **Status**: Ready
> **Layer**: Framework
> **Type**: Integration
> **Estimate**: 16h

## Context

**GDD**: `design/gdd/multi-world-system.md`
**Requirement**: TRV-01 ~ TRV-08

**Engine**: Unity 2022.3.62t11 (Tuanjie 1.9.3) | **Risk**: MEDIUM

玩家通过穿越者大厅的传送门在不同世界间穿梭。穿越方式包括主动穿越、任务触发、紧急召回、跨世界道具和强制穿越。主动穿越有冷却机制，紧急召回有进度回退惩罚。每次穿越触发过渡动画和世界intro叙事。

## Acceptance Criteria

- [ ] TRV-01: 在穿越者大厅点击已解锁世界的传送门，触发穿越动画和场景加载
- [ ] TRV-02: 首次进入新世界时触发世界intro叙事（文本+环境展示）
- [ ] TRV-03: 非首次进入新世界时出现在最后的安全区存档点
- [ ] TRV-04: 主动穿越后有2小时冷却，冷却期间传送门按钮显示剩余时间
- [ ] TRV-05: 冷却期间通过任务触发的穿越不受限制
- [ ] TRV-06: 紧急召回穿越导致当前世界主线进度回退至上一检查点
- [ ] TRV-07: 紧急召回穿越后传送门显示额外冷却惩罚
- [ ] TRV-08: 穿越过程在战斗状态不可使用

## Implementation Notes

- 核心逻辑在 `WorldManager.cs` 中的 `EnterWorld(worldId)` 和 `ExitWorld(worldId)`
- 穿越流程：
  1. 调用 `EnterWorld(worldId)` → 锁输入
  2. 播放白色渐变过渡（1.5秒），后台异步加载目标场景
  3. 过渡画面显示混沌虚空飞越效果
  4. 首次进入：触发 `WorldIntroManager.PlayIntro(worldId)`（文本+环境展示，5-10秒）
  5. 非首次：加载存档点位置 `WorldSaveData.playerPosition`
  6. 系统弹窗显示世界规则摘要和力量适配状态
- 冷却管理：`TravelCooldownManager` 追踪 `ActiveTravelCooldown`（默认7200秒）
- 冷却公式：`7200 - 已解锁世界数 × 600 - 惩罚冷却(紧急召回+3600)`
- 冷却下限：3600秒（1小时）
- `TaskTravel` 标签的任务触发穿越无视冷却
- 紧急召回：`EmergencyRecall()` → 回退 `WorldSaveData.progress` 至上一检查点 + 增加惩罚冷却
- 战斗状态检测：`CombatManager.IsInCombat` 阻止穿越和"返回大厅"
- `WorldIntroManager.cs` — 管理各世界intro叙事
- `TravelEffectController.cs` — 过渡动画控制

## QA Test Cases

- **TRV-01**: Given:大厅有发光门扉, When:点击门扉, Then:触发过渡动画→目标场景加载
- **TRV-04**: Given:刚完成主动穿越, When:立即再次点击传送门, Then:按钮显示剩余冷却时间且不可用
- **TRV-06**: Given:玩家濒死(HP≤0), When:选择"返回大厅", Then:当前世界主线回退至上一检查点

## Dependencies
- Depends on: Story 001 (穿越者大厅), SceneManager, WorldManager
- Unlocks: Story 006 (世界解锁递进需要穿越机制运作)
