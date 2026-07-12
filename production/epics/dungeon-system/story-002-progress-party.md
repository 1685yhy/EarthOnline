# Story 002: 副本进度+防刷+组队

> **Epic**: 副本实例 | **Layer**: Feature | **Type**: Integration | **Estimate**: 8h
> **GDD**: `design/gdd/dungeon-system.md`

## Acceptance Criteria
- [ ] 进度可保存48小时续关
- [ ] 产出递减防刷: 同副本连刷7天后掉到10%保底
- [ ] 联机4人组队支持
- [ ] NPC队友2人可选
- [ ] 通关评价S/A/B/C/D五级
- [ ] 评价影响额外奖励

## Implementation
- `DungeonProgress.cs`: 存档进度+48h过期
- `DungeonReward.cs`: 递减公式 `DropRate = Base×(1-min(DailyCount,7)×0.13)`
- `DungeonParty.cs`: Photon/自定义联机

**Depends on**: 001 → **Unlocks**: 003
