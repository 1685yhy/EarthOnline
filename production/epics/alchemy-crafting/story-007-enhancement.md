# Story 007: 装备强化系统

> **Epic**: 丹药炼器 | **Layer**: Core | **Type**: Logic | **Estimate**: 4h
> **GDD**: `design/gdd/alchemy-crafting-system.md` | **Req**: ENH-01~06

## Acceptance Criteria
- [ ] 可在炼器台强化装备
- [ ] 强化消耗材料+灵石
- [ ] 强化有成功率(基础80% × QualityMod × (1-Level×0.1))
- [ ] 失败只损失材料，装备不毁
- [ ] 强化等级上限受品质限制(R=5/SR=7/SSR=9/UR=10)
- [ ] 每级强化数值递增

## Implementation
- 扩展 `ForgeController.cs` 增加强化接口
- 强化数据存在装备实例上 `enhanceLevel`
- 成功率显示在UI中

## QA
- Given:SSR装备+强化材料, When:强化, Then:等级+1或失败材料损
- Given:R品质装备强化到5, When:尝试+6, Then:提示"已达上限"

**Depends on**: Story 004 → **Unlocks**: Story 008
