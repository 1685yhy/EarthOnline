# Story 008: 存档与时间推进

> **Epic**: multi-world-system
> **Status**: Ready
> **Layer**: Framework
> **Type**: Integration
> **Estimate**: 14h

## Context

**GDD**: `design/gdd/multi-world-system.md`
**Requirement**: TIM-01 ~ TIM-05, SAV-01 ~ SAV-05, EDG-01 ~ EDG-08

**Engine**: Unity 2022.3.62t11 (Tuanjie 1.9.3) | **Risk**: MEDIUM

多世界系统的存档管理和时间线推进。每个世界独立保存和读取存档，不相互影响。各世界独立的游戏内时间线——在A世界活动时B世界按2x速度推进。处理各种边缘情况：主线阻塞保底、存档损坏隔离、物品转化死锁、断线重连、通关后自由模式等。

## Acceptance Criteria

- [ ] TIM-01: 在A世界度过1小时，B世界推进2小时游戏内时间
- [ ] TIM-02: 时间推进导致的任务过期正确触发
- [ ] TIM-03: 时间推进导致的资源产出正确累计
- [ ] TIM-04: 达到168小时上限后时间冻结
- [ ] TIM-05: 返回后正确一次性结算所有时间推进变化
- [ ] SAV-01: 离开世界时自动保存该世界的WorldSaveData
- [ ] SAV-02: 每15分钟自动保存当前世界状态
- [ ] SAV-03: 手动保存功能在系统菜单可用
- [ ] SAV-04: 回档操作仅影响指定世界的存档，不影响其他世界
- [ ] SAV-05: 穿越者印记（灵魂属性）跨世界共享且在存档中一致
- [ ] EDG-01: 所有世界主线均阻塞时触发"地球意志指引"保底事件
- [ ] EDG-02: 穿越过程中断线后重连出现在目标世界起始位置
- [ ] EDG-03: 单个世界存档损坏不影响其他世界的存档
- [ ] EDG-04: 新世界之前所有世界都陷入死局时，地球意志干预强制重置
- [ ] EDG-05: 神话纪元终章通关后所有世界压制率解除
- [ ] EDG-06: 通关后New Game+保留所有能力重新开始
- [ ] EDG-07: 跨世界仓库总容量限制（防止无限存储）
- [ ] EDG-08: 跨世界力量加成在到达新世界时以隐藏Buff形式生效

## Implementation Notes

### 存档系统 (SaveManager 扩展)
- 存档数据结构（扩展现有 SaveManager）：
  ```
  SaveData {
      saveVersion, timestamp,
      transmigratorMark: TransmigratorMark,
      worlds: Dictionary<string, WorldSaveData>
  }
  WorldSaveData { worldId, sceneName, playerPosition, inventory,
                  progress, levelData, lastSavedAt, entranceCount,
                  totalPlaytime, worldSpecificData }
  ```
- 保存触发点：离开世界时自动保存 + 游戏内每15分钟自动保存 + 手动保存
- 回档：选择目标世界 → 加载该世界上一版本的 WorldSaveData
- 跨世界回档隔离：各世界存档文件分段存储，互不影响
- 穿越者印记：在 SaveData 根层级统一保存，所有世界共享

### 时间推进 (WorldTimeManager)
- 新建 `WorldTimeManager.cs` — 各世界独立时间线管理
- 时间推进公式：A世界1小时（现实时间）→ B世界推进2小时（游戏内）
- 上限168小时（7天），超过后时间冻结
- 推进影响处理：任务过期检测、资源产出累计、NPC状态变更、世界事件推进
- 返回时生成结算摘要：`TimeSettlementManager.GenerateReport(worldId)`

### 边缘情况处理
- 主线阻塞保底：`WorldManager.DetectGlobalDeadlock()` → 检测到所有世界主线不可推进 → 在大厅激活 "EarthWillGuidance" 事件（提供下一步指引或临时跨界通道）
- 断线重连：应用启动时检测 `LastSessionState` → 如果正在穿越过程中 → 设置玩家到目标世界起始位置
- 存档损坏：`SaveManager.LoadGame()` 捕获单个世界加载异常 → 该世界标记损坏 → 其他世界正常加载 → 损坏世界显示"重置该世界进度"选项
- 全局死局保底：`EarthWillIntervention()` → 强制重置所有世界最新检查点
- 通关后状态：`GameCompletionManager` — 检测神话纪元主线第5章完成 → 解除所有压制率 → 世界绑定物品转为通用 → 显示最终传送门 → 解锁New Game+
- New Game+：保留所有能力 → 各世界主线重置 → 难度×2 → 掉落和奖励×3
- 跨界仓库容量：配置 `CrossWorldInventory.MaxSlots`（默认100格）
- 跨世界力量加成：`WorldManager.OnEnterWorld()` → 检查已解锁世界数 → 应用 `全属性加成 = 1 + 已解锁数 × 0.05` 隐藏Buff

## QA Test Cases

- **TIM-01**: Given:在灵气大陆活动, When:游戏时间经过1小时, Then:都市世界推进2小时
- **SAV-01**: Given:在都市世界点击"返回大厅", When:穿越完成, Then:SaveData中worlds["W2"]自动保存
- **EDG-03**: Given:W1存档文件损坏, When:加载游戏, Then:W2/W3/W4/W5正常加载，W1提示存档损坏可重置
- **EDG-05**: Given:神话纪元主线第5章完成, When:返回大厅, Then:所有压制率解除，世界绑定物品变为通用
- **EDG-06**: Given:通关后选择New Game+, When:开始新游戏, Then:保留所有能力，主线重置，难度×2

## Dependencies
- Depends on: SaveManager, WorldManager, Story 003 (世界穿越), Story 005 (力量适配)
- Unlocks: 多设备云同步、成就系统跨世界统计
