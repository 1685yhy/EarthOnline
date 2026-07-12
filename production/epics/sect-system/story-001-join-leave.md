# Story 001: 门派加入/退出

> **Epic**: 门派系统 | **Layer**: Feature | **Type**: Integration | **Estimate**: 8h
> **GDD**: `design/gdd/sect-system.md` | **Req**: AC-1

## Acceptance Criteria
- [ ] 5个门派各有限制条件+入门考核场景
- [ ] 不满足条件显示缺失项列表
- [ ] 考核通过→门派令牌+门派UI解锁
- [ ] 和平退出保留50%贡献、声望-30、7天不可再加
- [ ] 贡献降至-100被逐出门派
- [ ] 不允许同时加入两个正式门派
- [ ] 散修联盟不视为正式门派

## Implementation
- 扩展 `FactionSystem.cs` → `SectManager.cs`
- 门派数据 ScriptableObject: 5门派完整配置
- 准入条件: 境界+声望+考核场景
- 退出流程: 检查条件→扣除→冷却

**Depends on**: None → **Unlocks**: 002
