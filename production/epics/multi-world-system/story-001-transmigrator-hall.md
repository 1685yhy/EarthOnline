# Story 001: 穿越者大厅

> **Epic**: multi-world-system
> **Status**: Ready
> **Layer**: Framework
> **Type**: Integration
> **Estimate**: 16h

## Context

**GDD**: `design/gdd/multi-world-system.md`
**Requirement**: HLL-01 ~ HLL-06

**Engine**: Unity 2022.3.62t11 (Tuanjie 1.9.3) | **Risk**: MEDIUM

穿越者大厅是贯穿始终的中枢场景，悬浮在混沌虚空中的平台。玩家在此查看世界之门、使用跨界仓库、查看穿越者印记和手册，是不同世界间的转场空间。

## Acceptance Criteria

- [ ] HLL-01: 完成灵气大陆主线第1章后，首次进入穿越者大厅
- [ ] HLL-02: 大厅中显示所有已解锁世界的发光传送门
- [ ] HLL-03: 未解锁世界的传送门显示解锁条件
- [ ] HLL-04: 大厅中显示跨界仓库、穿越者印记、穿越者手册界面
- [ ] HLL-05: 在任何世界的系统菜单中可找到"返回大厅"按钮
- [ ] HLL-06: 战斗状态下"返回大厅"按钮不可用

## Implementation Notes

- 新建穿越者大厅场景 `Scenes/Hall/TransmigratorHall`
- 新建 `WorldGateUI.cs` — 世界之门交互逻辑（传送门布局3×3预留扩展位）
- 新建 `TransmigratorMarkUI.cs` — 灵魂属性展示面板
- 新建 `CrossWorldInventoryUI.cs` — 跨界仓库界面
- 新建 `TransmigratorHandbookUI.cs` — 穿越者手册界面
- 新建 `HallManager.cs` — 大厅状态管理和初始化
- 大厅外观风格随世界解锁进度变化
- 世界之门发光状态绑定 `WorldManager.GetUnlockedWorlds()`
- 未解锁门扉显示解锁条件文本，绑定 `WorldConfig.unlockCondition`
- 系统菜单"返回大厅"按钮在战斗状态调用 `TribeManager.Instance.IsInBattle` 判定
- 首次进入触发条件：检测 `worldSaveData["W1"].MainQuestChapter >= 1`

## QA Test Cases

- **HLL-01**: Given:新角色在灵气大陆, When:完成主线第1章, Then:触发首次大厅进入流程
- **HLL-02**: Given:已解锁2个世界, When:进入大厅, Then:看到2个发光门扉+3个灰色门扉
- **HLL-05**: Given:玩家在非战斗状态, When:打开系统菜单, Then:"返回大厅"按钮可见且可点击

## Dependencies
- Depends on: WorldManager, WorldConfig (SO)
- Unlocks: Story 003 (世界穿越机制依赖大厅传送门)
