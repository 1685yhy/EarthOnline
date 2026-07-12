# Story 006: 配方系统+变异

> **Epic**: 丹药炼器 | **Layer**: Core | **Type**: Integration | **Estimate**: 6h
> **GDD**: `design/gdd/alchemy-crafting-system.md` | **Req**: 配方章节

## Acceptance Criteria
- [ ] 配方5种获取方式(基础/门派/探索/BOSS/自创)
- [ ] 配方结构完整(id/类型/难度/材料/顺序/温度/时长/结果池)
- [ ] 变异触发: 非标准投料→可能产出变异物品
- [ ] 变异成功配方记录到"自创配方"列表
- [ ] 自创配方可分享/出售
- [ ] 首次炼制新配方额外熟练度+10
- [ ] 配方搜索和收藏功能

## Implementation
- 重构 `CraftingSystem.cs` → `RecipeDatabase.cs`
- 配方数据从 JSON 配置加载
- 变异判定: 投料顺序≠标准顺序→`MutationChance = 0.15 × Proficiency`
- 自创配方存储: PlayerPrefs/SaveData

## QA
- Given:非标准投料顺序, When:炼丹, Then:可能触发变异产出
- Given:变异成功, When:查看配方列表, Then:新配方出现

**Depends on**: Story 003, Story 004 → **Unlocks**: Story 007
