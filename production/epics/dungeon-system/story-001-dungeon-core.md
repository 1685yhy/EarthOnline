# Story 001: 副本实例化与生成

> **Epic**: 副本实例 | **Layer**: Feature | **Type**: Logic | **Estimate**: 10h
> **GDD**: `design/gdd/dungeon-system.md`

## Acceptance Criteria
- [ ] 副本入口交互→触发难度选择(简单/普通/困难/噩梦)
- [ ] 动态难度按境界公式自动适配
- [ ] 房间种子算法生成岔路(每路口2~3条)
- [ ] 6种房间类型全部实现
- [ ] 4种通行方式可选(战斗/潜行/谈判/环境)
- [ ] 击败终点BOSS后完成副本

## Implementation
- 重构 `DungeonEntrance.cs` → `DungeonInstance.cs`
- 新建 `DungeonRoomGenerator.cs` (种子算法)
- 房间类型: 战斗/宝藏/陷阱/商人/休息/BOSS
- Seed = Hash(playerId + dungeonId + visitCount)

**Depends on**: None → **Unlocks**: 002
