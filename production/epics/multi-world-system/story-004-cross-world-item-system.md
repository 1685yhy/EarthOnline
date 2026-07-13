# Story 004: 跨世界物品系统

> **Epic**: multi-world-system
> **Status**: Ready
> **Layer**: Framework
> **Type**: Integration
> **Estimate**: 16h

## Context

**GDD**: `design/gdd/multi-world-system.md`
**Requirement**: IND-01 ~ IND-06, CON-01 ~ CON-06

**Engine**: Unity 2022.3.62t11 (Tuanjie 1.9.3) | **Risk**: MEDIUM

定义跨世界物品的三类体系（通用/世界绑定/灵魂绑定），实现世界专属背包和跨界仓库的双重结构，以及跨世界物品转化机制。

## Acceptance Criteria

- [ ] IND-01: 在A世界获得的等级/财富/装备在B世界不可见（显示为不可用）
- [ ] IND-02: A世界专属物品在B世界显示灰色并标记"本世界不可用"
- [ ] IND-03: A世界专属物品返回A世界后自动恢复可用状态
- [ ] IND-04: 每个世界独立保存任务进度
- [ ] IND-05: 跨世界仓库中存放的通用物品在所有世界均可使用
- [ ] IND-06: 进入新世界时自动激活该世界的专属系统UI
- [ ] CON-01: 每个世界至少有一个转化NPC，提供跨世界物品转化服务
- [ ] CON-02: 转化NPC对话中显示当前可转化的物品列表
- [ ] CON-03: 转化有对应的前置任务
- [ ] CON-04: 不同转化方式的汇率不同（任务链 vs 地下黑市）
- [ ] CON-05: 完美完成转化任务获得最高汇率系数
- [ ] CON-06: 未转化的绑定物品不可丢弃、不可交易

## Implementation Notes

- 物品系统扩展：`Item` 类增加 `itemCategory`（Universal/WorldBound/SoulBound）、`worldId`（绑定世界ID）、`isSoulBound` 字段
- 新建 `CrossWorldInventory.cs` — 跨界仓库（共享存储空间，有总容量限制）
- 修改 `Inventory.cs` — 每个世界持有一个 `WorldInstanceData.playerInventory`，切换世界时切换显示的背包数据
- 通用物品在跨界仓库中存储，在所有世界 `Inventory` 中同步显示可用
- 世界绑定物品跨世界显示：图标灰色 + MaterialPropertyBlock 灰色覆盖 + 右下角"⛔"标记
- 世界绑定物品交互限制：`Item.Use()` / `Item.Drop()` / `Item.Trade()` 在非绑定世界抛出不可用状态
- 进入新世界时 UI 系统检测 `WorldConfig.worldType` 激活对应系统UI
- 转化系统：
  - 每个世界配置1-2个转化NPC，挂载 `ItemConversionNPC.cs`
  - `ItemConversionData` SO 定义可转化的物品对和汇率参数
  - 转化任务链：前置任务→解锁转化资格→执行转化
  - 双渠道：任务链转化（汇率较高）+ 地下黑市转化（汇率0.3固定）
  - 转化品质判定：任务完成评价 0.5/0.8/1.0
  - 汇率公式：`转化价值 = 原价值 × 汇率系数 × 适配任务折扣`
- 新建 `ItemWorldBindingHandler.cs` 统一管理跨世界物品状态

## QA Test Cases

- **IND-02**: Given:携带灵气大陆飞剑穿越到都市世界, When:打开背包, Then:飞剑图标灰色，显示"本世界不可用"
- **IND-03**: Given:在都市世界看到灰色飞剑, When:返回灵气大陆, Then:飞剑恢复可用状态
- **CON-01**: Given:在都市世界, When:找到转化NPC, Then:对话显示可转化物品列表（如灵石→人民币）
- **CON-06**: Given:在末日废土持有灵气大陆丹药, When:尝试丢弃, Then:丢弃按钮灰色不可用

## Dependencies
- Depends on: WorldManager, Inventory 系统
- Unlocks: Story 005 (力量适配系统使用物品分类数据)
