# Story 004: BOSS记仇+掉落

> **Epic**: BOSS战 | **Layer**: Feature | **Type**: Integration | **Estimate**: 6h
> **GDD**: `design/gdd/boss-system.md` | **Req**: GRD-01~06, DROP-01~06, RET-01~05

## Acceptance Criteria
- [ ] 逃跑记仇+1, 谈判反悔+2, 击杀同族+3
- [ ] 记仇等级4级(警惕/记恨/仇恨/宿敌)
- [ ] 击败BOSS记仇清零
- [ ] 30游戏天不进入区域记仇降1级
- [ ] 必定掉落: BOSS材料+灵石+修为结晶
- [ ] 概率掉落: 装备(SR/SSR/UR)+配方+技能书
- [ ] 首杀: 专属称号+特殊物品
- [ ] BOSS材料在炼器配方中可用
- [ ] 撤退: 跑出leashRange→BOSS重置+区域怪物攻击性+20%

## Implementation
- `BossGrudgeSystem.cs`: 记仇值追踪+等级判定
- `BossDropTable.cs`: 掉落计算+品质修正
- 撤退: `leashRange`检查+状态重置

**Depends on**: 001, 003 → **Unlocks**: None
