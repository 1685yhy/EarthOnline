# Story 002: BOSS弱点+侦察

> **Epic**: BOSS战 | **Layer**: Feature | **Type**: Integration | **Estimate**: 6h
> **GDD**: `design/gdd/boss-system.md` | **Req**: WEAK-01~05

## Acceptance Criteria
- [ ] 侦察后弱点显示在UI(属性相克×2/时机弱点×1.5)
- [ ] 4种侦察方式: 观察/望气术/NPC情报/战斗试探
- [ ] 针对弱点攻击有额外视觉特效
- [ ] 完美狩猎(利用所有弱点)→掉落品质+1档
- [ ] NPC情报100%成功率但花费灵石

## Implementation
- 新建 `BossWeaknessSystem.cs`
- 弱点类型: 属性/时机/部位/道具/环境/恐惧
- 侦察结果UI: 弱点提示面板

**Depends on**: 001 → **Unlocks**: 003, 004
