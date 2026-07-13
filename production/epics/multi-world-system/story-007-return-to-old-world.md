# Story 007: 返回旧世界

> **Epic**: multi-world-system
> **Status**: Ready
> **Layer**: Framework
> **Type**: Logic
> **Estimate**: 14h

## Context

**GDD**: `design/gdd/multi-world-system.md`
**Requirement**: RET-01 ~ RET-07

**Engine**: Unity 2022.3.62t11 (Tuanjie 1.9.3) | **Risk**: MEDIUM

已解锁的世界可随时从大厅返回。返回后所有专属能力/物品恢复至离开时状态。为防止"满级大佬屠新手村"，实现力量上限软锁定、世界意志关注、低回报循环等保护机制。各世界时间线独立推进，离开期间的变化在返回时一次性结算。

## Acceptance Criteria

- [ ] RET-01: 已解锁的世界可随时从大厅返回
- [ ] RET-02: 返回旧世界时战力被软锁定不超过该世界终局BOSS的120%
- [ ] RET-03: 战力超出限制时，超额部分显示灰色"封印中"
- [ ] RET-04: 离开该世界后封印自动解除
- [ ] RET-05: 世界排斥效果（敌意+掉落惩罚）在回归力量过强时生效
- [ ] RET-06: 任务日志显示离开期间的时间推进变化
- [ ] RET-07: 超过7天未返回的世界进入时间冻结，返回时一次性结算

## Implementation Notes

- 返回流程：在穿越者大厅点击发光门扉 → `WorldManager.EnterWorld(worldId)` + 跳过intro
- 状态恢复：从 `WorldSaveData` 加载该世界专属进度、等级、装备、资产
- 力量软锁定机制 `ReturnPowerSoftLock`：
  - 获取该世界终极BOSS战力 `WorldConfig.finalBossPower`
  - 上限 = `finalBossPower × 1.2`
  - 如果玩家当前综合战力 > 上限 → PlayerStats.ModifyPower() 施加临时 Modifier
  - 超出部分显示灰色 `"封印中"`（通过 `StatsDisplay.cs` 颜色切换）
  - 离开世界时封印自动移除
- 世界排斥系统 `WorldRejectionSystem`：
  - 检测条件：玩家进入时战力 > 该世界终局BOSS战力
  - 触发效果：全地图NPC敌意+20（`RelationshipManager.ModifyGlobalHostility(20)`）
  - 稀有掉落率-50%（`LootTableManager.ModifyDropRate(worldId, 0.5)`）
  - 隐藏BOSS"穿越者猎杀者"激活（`BossSpawner.ActivateHiddenBoss()`）
- 时间推进结算：
  - 返回时调用 `WorldTimeManager.SettleTimeChanges(worldId)`
  - 生成结算摘要UI：`TimeSettlementUI` — 过期任务/资源产出/NPC变动/事件推进
  - 超过7天时间冻结后返回时一次性显示所有累计变化
- 收益递减：在低级世界刷资源时检查 `LootTableManager.IsBelowLevel(worldId, playerLevel)` → 产出降低至10%

## QA Test Cases

- **RET-01**: Given:已解锁W1和W2, When:在W2点击W1传送门, Then:返回W1且所有能力/物品恢复至离开时状态
- **RET-02**: Given:携带大成期修为返回练气期副本区, When:查看战力面板, Then:有效战力显示为终局BOSS×1.2，超额部分灰色标注"封印中"
- **RET-05**: Given:满级回归低级世界, When:进入城市, Then:NPC敌意增加，稀有掉落率降低50%
- **RET-07**: Given:离开W1超过7天(游戏内), When:返回W1, Then:显示时间结算界面（过期任务/资源产出/NPC变动）

## Dependencies
- Depends on: Story 003 (世界穿越), WorldTimeManager, WorldManager
- Unlocks: 跨世界任务链、多世界NPC互动系统
