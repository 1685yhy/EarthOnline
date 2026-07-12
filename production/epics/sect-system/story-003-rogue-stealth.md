# Story 003: 散修+偷学

> **Epic**: 门派系统 | **Layer**: Feature | **Type**: Logic | **Estimate**: 6h
> **GDD**: `design/gdd/sect-system.md` | **Req**: AC-3, AC-4

## Acceptance Criteria
- [ ] 散修不加入任何门派正常推进游戏
- [ ] 散修可接散修联盟悬赏
- [ ] 散修渡劫失败率+20%成功道体+1级
- [ ] 至少2种偷学途径(潜入藏经阁/贿赂)
- [ ] 偷学基础成功率25% + 潜行加成 - 警戒度 + 时段加成
- [ ] 被发现后不同严重程度不同处理(警告/关禁闭/强制叛逃)
- [ ] 偷学成功获得功法残篇(50-80%)

## Implementation
- 新建 `SecretLearning.cs`
- 偷学成功: `25% + Stealth×2% - Alertness + TimeBonus`
- 时段加成: 白天0%, 夜晚+10%, 深夜+20%
- 散修补偿: 渡劫失败率+20%↔道体品质+1

**Depends on**: 001, 002 → **Unlocks**: 004
