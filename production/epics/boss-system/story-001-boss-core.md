# Story 001: BOSS数据+AI

> **Epic**: BOSS战 | **Layer**: Feature | **Type**: Logic | **Estimate**: 10h
> **GDD**: `design/gdd/boss-system.md` | **Req**: BOSS-01~05, PHASE-01~07

## Acceptance Criteria
- [ ] BOSS出场演出(名号+称号+境界+特效+专属BGM)
- [ ] BOSS血条+阶段指示器+头顶UI
- [ ] 多阶段转换(HP阈值/时间/行为/环境触发)
- [ ] 阶段转换: 视觉变化+台词+新招式+喘息窗口
- [ ] BOSS属性按境界+组队人数缩放
- [ ] BOSS定义数据结构(BossDef)

## Implementation
- 新建 `BossAI.cs`, `BossDef.cs`
- 阶段转换: 70%/35%HP + 300s狂暴
- HP缩放: `BossHP = BaseHP × RealmMultiplier × PartySizeMultiplier`
- 境界压制: 高1境伤害+15%, 低1境伤害-25%

**Depends on**: None → **Unlocks**: 002, 003
