# Story 001: 迷雾系统

> **Epic**: 新地图系统
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 8h

## Context

**GDD**: `design/gdd/new-maps-system.md`
**Requirement**: FOG-01 ~ FOG-09

**Engine**: Unity 2022.3.62t11 (Tuanjie 1.9.3) | **Risk**: LOW

## Acceptance Criteria

- [ ] FOG-01: 新玩家世界地图完全被迷雾覆盖，只显示地形轮廓
- [ ] FOG-02: 玩家移动时15m范围内迷雾消散至Layer 1
- [ ] FOG-03: 已走过路径在地图上留下浅色轨迹
- [ ] FOG-04: 高处/瞭望塔视野临时扩大至45m
- [ ] FOG-05: 离开高处后临时视野30秒内消退
- [ ] FOG-06: 使用"详细地图"道具后整个区域迷雾降至Layer 1
- [ ] FOG-07: 死亡后已探索区域不重置迷雾
- [ ] FOG-08: Layer 0/1/2在不同探索度下信息量差异明显
- [ ] FOG-09: 迷你地图只显示当前已探索区域内的信息

## Implementation Notes

- 新建 `FogOfWar.cs`，挂载到 GameManager
- 每个区域存储 `exploredCells[Layer]` 位图
- 迷雾消散半径公式: `BaseReveal(15m) × (1 + PerceptionBonus)`
- 高处视野: `HeightMultiplier = 3`, 持续 `AerialRevealDuration = 30s`

## QA Test Cases

- **FOG-02**: Given:新玩家进入未探索区域, When:移动15m, Then:半径15m迷雾消散至Layer1
- **FOG-04**: Given:玩家攀上瞭望塔, When:站在高处, Then:视野扩大到45m
- **FOG-07**: Given:已探索区域, When:死亡重生, Then:迷雾状态不变

## Dependencies
- Depends on: None
- Unlocks: Story 002 (风险评级需要区域数据)
