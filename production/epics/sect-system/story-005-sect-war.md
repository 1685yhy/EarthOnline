# Story 005: 门派战

> **Epic**: 门派系统 | **Layer**: Feature | **Type**: Integration | **Estimate**: 8h
> **GDD**: `design/gdd/sect-system.md` | **Req**: AC-5

## Acceptance Criteria
- [ ] 声望3级以上门派可宣战(消耗10000灵石)
- [ ] 至少2种战争形式(战场副本/资源点争夺)
- [ ] 72小时后判定胜负(积分制)
- [ ] 积分: 击杀弟子+10/摧毁旗帜+100/击杀掌门+500
- [ ] 胜方获赔偿+势力范围, 败方失势力范围+支付赔款
- [ ] 战争期间双方区域风险等级临时改变

## Implementation
- 新建 `SectWarSystem.cs`
- 战争状态72h倒计时
- 积分系统+排行榜
- 战场副本: 独立场景实例
- 资源点: 地图标记+争夺机制

**Depends on**: 004 → **Unlocks**: 006
