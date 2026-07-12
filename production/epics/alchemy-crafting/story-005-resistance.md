# Story 005: 药抗与使用循环

> **Epic**: 丹药炼器 | **Layer**: Core | **Type**: Logic | **Estimate**: 4h
> **GDD**: `design/gdd/alchemy-crafting-system.md` | **Req**: USG-01~08

## Acceptance Criteria
- [ ] 同种丹药连续服用效果逐步衰减(100%→70%→40%→0%)
- [ ] 第4次服用效果为0
- [ ] 抗性每日衰减1计数(7天恢复)
- [ ] 不同丹药交替服用可最大化收益
- [ ] 上品以上抗性衰减更慢
- [ ] 装备耐久度战斗后消耗
- [ ] 耐久归零装备失效但不消失
- [ ] 装备可维修

## Implementation
- 扩展 PlayerStats 增加药抗追踪 + 装备耐久系统
- 药抗字典: `Dictionary<ItemId, int> _resistanceCount`
- 衰减: 每游戏日 `_resistanceCount[itemId] -= 1`
- 耐久: `EquipmentDurability.cs` 追踪每个装备

## QA
- Given:连续服用同种丹药4次, When:第4次, Then:效果为0
- Given:7游戏日后, When:再服用, Then:恢复100%

**Depends on**: Story 003, Story 004 → **Unlocks**: Story 006
