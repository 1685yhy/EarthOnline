# Story 006: 门派界面+跨门派联动

> **Epic**: 门派系统 | **Layer**: Feature | **Type**: UI | **Estimate**: 6h
> **GDD**: `design/gdd/sect-system.md` | **Req**: AC-7, AC-8

## Acceptance Criteria
- [ ] 门派身份面板: 门派名+职级+贡献度+声望
- [ ] 门派任务面板: 每日任务+悬赏
- [ ] 兑换商店界面: 功法/丹药/装备
- [ ] 同盟门派声望联动生效
- [ ] 加入一个门派影响其他门派初始态度
- [ ] 双身份/卧底机制可触发
- [ ] 门派被灭→弟子转散修
- [ ] 掌门叛逃→全门派危机

## Implementation
- 新建 `SectUI.cs`
- UGUI面板: 身份/任务/商店三个Tab
- 声望联动: 加入门派A→盟友+10/敌人-20
- 卧底: 特殊道具触发双身份

**Depends on**: 001~005 → **Unlocks**: None
