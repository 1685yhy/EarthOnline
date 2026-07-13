# Story 006: 世界解锁递进

> **Epic**: multi-world-system
> **Status**: Ready
> **Layer**: Framework
> **Type**: Logic
> **Estimate**: 12h

## Context

**GDD**: `design/gdd/multi-world-system.md`
**Requirement**: UNL-01 ~ UNL-06

**Engine**: Unity 2022.3.62t11 (Tuanjie 1.9.3) | **Risk**: LOW

世界解锁呈渐进式递进——从已知世界到未知世界。每个新世界需要完成前置条件（其他世界的主线进度）才能解锁。解锁时触发过场动画。选择不同起始世界时默认解锁世界相应变更。

## Acceptance Criteria

- [ ] UNL-01: 创建角色时只显示起始世界已解锁，其他世界显示解锁条件
- [ ] UNL-02: 满足解锁条件后，对应世界的传送门变为发光状态
- [ ] UNL-03: 解锁时触发过场动画
- [ ] UNL-04: 解锁动画后可选择立即前往或稍后探索
- [ ] UNL-05: 选择不同起始世界时，默认已解锁世界相应变更
- [ ] UNL-06: 解锁条件检测基于实际完成状态，不因起始世界不同而改变难度

## Implementation Notes

- 解锁条件在 `WorldConfig.cs` 中定义：`unlockConditionType`（DefaultUnlocked / MainQuestThreshold / MultiWorldThreshold）和 `unlockParameter`（章节数/世界数）
- 解锁条件检测在 `WorldManager.CheckUnlockCondition(worldId)`：
  - W1 灵气大陆：`DefaultUnlocked`（起始可选/起始为其他世界时变为 W2 主线第2章完成）
  - W2 都市世界：W1 主线第3章完成 OR 起始世界
  - W3 末日废土：完成任意2个世界主线第2章
  - W4 星际纪元：完成任意3个世界主线第3章
  - W5 神话纪元：完成所有其他4个世界主线第4章
- 异常规则处理：起始世界为W2时，W1解锁条件切换为"W2主线第2章完成"
- 解锁条件检测在以下时机触发：
  - 主线章节完成时
  - 进入穿越者大厅时
  - 手动检查（系统菜单）
- 解锁动画流程：
  1. 传送门从灰色→发光（材质/Lerp过渡）
  2. 触发 `EarthWhisperManager.PlayUnlockNarration(worldId)`（地球意志语音+文字）
  3. 传送门打开动画 → 显示目标世界标志性画面（cutscene）
  4. 弹出选择面板："立即前往" / "稍后探索"
  5. 被动解锁时在任务日志添加高亮提醒
- `WorldUnlockController.cs` — 解锁动画管理和播放
- 条件检测使用 `WorldSaveData[worldId].MainQuestChapter` 数据
- 跨世界力量加成公式：`全属性加成 = 1 + 已解锁世界数 × 0.05`

## QA Test Cases

- **UNL-01**: Given:新创建的角色, When:进入大厅, Then:起始世界门扉发光，其他门扉灰色显示条件
- **UNL-02**: Given:W1主线第3章完成, When:检查W2门扉, Then:门扉变为发光状态
- **UNL-03**: Given:满足解锁条件瞬间, When:切换到大厅, Then:播放解锁过场动画
- **UNL-05**: Given:角色起始世界为都市世界, When:进入大厅, Then:W2门扉发光，W1门扉灰色显示"需完成都市世界第2章"

## Dependencies
- Depends on: Story 003 (世界穿越), WorldManager, WorldConfig
- Unlocks: Story 005 (压制率矩阵需要世界解锁状态)
