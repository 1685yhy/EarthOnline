# Story 002: 雷劫+心魔

> **Epic**: 渡劫 | **Layer**: Feature | **Type**: Logic | **Estimate**: 10h
> **GDD**: `design/gdd/tribulation-system.md` | **Req**: THD-01~12, DEM-01~10

## Acceptance Criteria
- [ ] 天雷逐道落下(9+修正)，伤害逐道+15%
- [ ] 预兆圈1秒→落雷，走位可躲避
- [ ] 溅射伤害: 直击30%, 每远1m-10%
- [ ] 抗雷甲/阵法/护法正确减免
- [ ] 完美闪避→心魔难度-15%+道体+1
- [ ] 心魔基于玩家历史生成(7种)
- [ ] 意志值100→0则失败
- [ ] 4种破解方式不同成功率
- [ ] 凝心丹/辟邪佩正确降低意志扣减

## Implementation
- `ThunderTribulation.cs`: 天雷生成+伤害+走位检测
- `HeartDemonTribulation.cs`: 心魔场景+意志值系统
- 心魔数据源: AntagonistSystem/FactionSystem/NPC好感

**Depends on**: 001 → **Unlocks**: 003
