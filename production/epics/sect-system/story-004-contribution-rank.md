# Story 004: 贡献与晋升

> **Epic**: 门派系统 | **Layer**: Feature | **Type**: Logic | **Estimate**: 6h
> **GDD**: `design/gdd/sect-system.md` | **Req**: AC-6

## Acceptance Criteria
- [ ] 4种贡献获取途径+4种消耗途径
- [ ] 5职级(外门→内门→核心→长老→掌门)
- [ ] 晋升条件: 贡献+境界+考核
- [ ] 2→3级需战斗+笔试考核
- [ ] 3→4级需完成门派级大任务
- [ ] 4→5级需特殊事件(掌门退位/战死/禅让)
- [ ] 各职级不同特权(藏经阁层数/折扣/投票权)

## Implementation
- 新建 `SectRankSystem.cs`
- 贡献门槛: 200/500/2000 + 境界门槛
- 兑换折扣: 3级9折, 4级8折, 5级7折
- 门派任务每日上限5次

**Depends on**: 001 → **Unlocks**: 005, 006
