# Story 008: 世界地图界面

> **Epic**: 新地图系统
> **Status**: Ready
> **Layer**: Core
> **Type**: UI
> **Estimate**: 6h

## Context

**GDD**: `design/gdd/new-maps-system.md`
**Requirement**: INT-01 ~ INT-08

**Engine**: Unity 2022.3.62t11 (Tuanjie 1.9.3) | **Risk**: LOW

## Acceptance Criteria

- [ ] INT-01: 门派控制区域正确显示势力色
- [ ] INT-02: 地图上正确显示副本入口+推荐境界
- [ ] INT-03: 区域资源产出标记正确
- [ ] INT-04: 超过3个区域100%探索度不出现性能问题
- [ ] INT-05: 新手5分钟内可完成第一次探索（零学习成本）
- [ ] INT-06: 高等级玩家回低等级区域探索仍有收益
- [ ] INT-07: 越级探索收益与风险成正比
- [ ] INT-08: 地图支持缩放+拖拽+点击标记

## Implementation Notes

- 新建 `WorldMapUI.cs`，使用 UGUI
- M键打开世界地图，滚轮缩放，拖拽平移
- 已探索区域清晰显示，未探索区域暗色遮罩
- 显示：传送点图标/地标/POI/副本入口/势力范围
- 性能：超过3区域100%时使用对象池+视锥剔除

## QA Test Cases

- **INT-01**: Given:天元宗控制区域, When:打开地图, Then:该区域显示门派色
- **INT-08**: Given:世界地图打开, When:滚轮+拖拽, Then:缩放平移流畅
- **INT-04**: Given:3+区域100%探索, When:打开地图, Then:FPS ≥ 30

## Dependencies
- Depends on: Story 001~007
- Unlocks: None (last story)
