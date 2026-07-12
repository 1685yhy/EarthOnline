# Story 004: 炼器四步流程

> **Epic**: 丹药炼器 | **Layer**: Core | **Type**: Logic | **Estimate**: 10h
> **GDD**: `design/gdd/alchemy-crafting-system.md` | **Req**: FRG-01~09

## Acceptance Criteria
- [ ] 需要炼器台，靠近后触发炼器UI
- [ ] 熔炼→塑形→淬火→开光四步完整可操作
- [ ] 熔炼温度影响材料纯度
- [ ] 锤击塑形有力道条(类QTE)
- [ ] 不同淬火液赋予不同属性(灵泉/妖兽血)
- [ ] 开光灵力注入量影响亲和度
- [ ] R/SR/SSR/UR产出分布合理
- [ ] SSR以上有词缀+特效
- [ ] 高难度装备有境界要求

## Implementation
- 新建 `ForgeController.cs`
- 装备属性: `Stats = Base×MatMultiplier×Quality×EnhanceLevel`
- 词缀: Quality<0.6→0条, ≥0.6→1条, ≥0.8→2条, ≥0.95→3条
- 强化: `EnhanceChance = 0.8 × QualityMod × (1 - Level×0.1)`

## QA
- Given:完整四步操作, When:炼器, Then:装备生成+品质判定
- Given:妖兽血淬火, When:完成炼器, Then:装备带对应属性

**Depends on**: Story 003 → **Unlocks**: Story 005
