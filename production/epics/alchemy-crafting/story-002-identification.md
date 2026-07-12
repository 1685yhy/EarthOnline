# Story 002: 灵材鉴定系统

> **Epic**: 丹药炼器 | **Layer**: Core | **Type**: Logic | **Estimate**: 4h
> **GDD**: `design/gdd/alchemy-crafting-system.md` | **Req**: IDN-01~06

## Acceptance Criteria
- [ ] 未鉴定灵材显示"未知药性"
- [ ] 自学鉴定消耗灵力且有成功率(50%+等级×0.3%)
- [ ] NPC鉴定100%成功但花费灵石(50)
- [ ] NPC鉴定后灵材信息可能泄露(20%)
- [ ] 鉴定成功后灵材完整属性解锁
- [ ] 试药鉴定可能中毒或炼废

## Implementation
- 新建 `IdentificationSystem.cs`
- 成功率: `IdentifyChance = 0.5 + Level×0.003 + ToolBonus - Difficulty`
- 鉴定后显示: 药性(寒/热/平/毒)、品质、可用配方

## QA
- Given:未鉴定灵材, When:查看属性, Then:显示"未知药性"
- Given:自学鉴定, When:成功鉴定, Then:属性完整显示

**Depends on**: Story 001 → **Unlocks**: Story 003
