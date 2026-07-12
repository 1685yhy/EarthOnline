# Story 002: 叛逃与追杀

> **Epic**: 门派系统 | **Layer**: Feature | **Type**: Integration | **Estimate**: 8h
> **GDD**: `design/gdd/sect-system.md` | **Req**: AC-2

## Acceptance Criteria
- [ ] 至少3种触发叛逃行为(偷学/杀同门/双身份暴露)
- [ ] 叛逃后触发追兵事件(强度=基础+境界×0.5+偷学×0.3+职级×0.2)
- [ ] 叛逃后原门派声望锁定-100
- [ ] 叛逃等级3级(轻/中/重)
- [ ] 至少1种结束逃亡方式(到法外之地/渡劫净化/赔偿)
- [ ] 叛逃后功法威力-30%
- [ ] "叛徒"标签影响其他门派初始态度

## Implementation
- 新建 `BetrayalSystem.cs`
- 追杀强度: `1+floor(Intensity/3)` 追兵
- 追兵境界: `叛逃者境界 + floor(Intensity/3)`
- 逃亡结束判定: 到达法外之地 OR 渡劫成功 OR 支付赔偿

**Depends on**: 001 → **Unlocks**: 003
