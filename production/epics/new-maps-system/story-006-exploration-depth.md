# Story 006: 探索深度追踪

> **Epic**: 新地图系统
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 4h

## Context

**GDD**: `design/gdd/new-maps-system.md`
**Requirement**: EXP-01 ~ EXP-05

**Engine**: Unity 2022.3.62t11 (Tuanjie 1.9.3) | **Risk**: LOW

## Acceptance Criteria

- [ ] EXP-01: 首次踏入区域获得5%探索度
- [ ] EXP-02: 发现地标和POI时探索度增加
- [ ] EXP-03: 探索度40%后中小型POI开始显示
- [ ] EXP-04: 探索度80%后隐藏入口提示出现
- [ ] EXP-05: 探索度100%后获得区域"探索大师"称号

## Implementation Notes

- 新建 `ExplorationDepth.cs`
- 探索度0-100%，5个阶段
- 增加方式: 踏入+5%, 地标+3~5%, POI+1~2%, 隐藏+2~3%
- 覆盖面积: 每1%地图面积+0.1%
- 100%触发称号系统: `TitleSystem.Grant("探索大师")`

## QA Test Cases

- **EXP-01**: Given:首次踏入区域, When:跨过边界, Then:探索度+5%
- **EXP-03**: Given:探索度39%→40%, When:达到40%, Then:中小POI在地图显示
- **EXP-05**: Given:探索度99%→100%, When:达到100%, Then:称号解锁

## Dependencies
- Depends on: Story 005
- Unlocks: Story 007
