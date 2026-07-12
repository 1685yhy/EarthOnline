# Story 001: 天劫台+准备系统

> **Epic**: 渡劫 | **Layer**: Feature | **Type**: Integration | **Estimate**: 8h
> **GDD**: `design/gdd/tribulation-system.md` | **Req**: PLT-01~08

## Acceptance Criteria
- [ ] 天劫台三种品质(Normal/Ancient/Secret)
- [ ] 未达渡劫大圆满不可激活
- [ ] 确认面板: 准备评分+预估成功率+天劫台信息
- [ ] 四维准备评分(丹药0.25+装备0.30+阵法0.20+护法0.25)
- [ ] 散修限制: 护法3人+效果70%+失败率+20%↔道体+1
- [ ] 引劫后生成结界(半径30m)

## Implementation
- `TribulationPlatform.cs`: 天劫台交互+数据
- 准备评分: `ReadinessScore = Pill×0.25 + Equip×0.30 + Form×0.20 + Escort×0.25`
- 结界: 金色半透明光罩+100耐久

**Depends on**: None → **Unlocks**: 002
