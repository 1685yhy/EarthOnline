# Story 004: 渡劫UI+边缘情况

> **Epic**: 渡劫 | **Layer**: Feature | **Type**: UI | **Estimate**: 6h
> **GDD**: `design/gdd/tribulation-system.md` | **Req**: EDG-01~07, INT-01~08

## Acceptance Criteria
- [ ] 渡劫确认面板: 准备评分+成功率预估+建议清单
- [ ] 雷劫HUD: 天雷计数器+雷序指示
- [ ] 心魔UI: 意志值条+选项面板
- [ ] 天道问心: 问题+回答输入框
- [ ] 道体面板: 品质+特性+外观
- [ ] 断线5分钟保护(每日1次)
- [ ] PVP区结界可被攻击
- [ ] 渡劫成功→OnRealmBreakthrough事件触发
- [ ] 无渡劫系统时回退旧突破逻辑

## Implementation
- `TribulationUI.cs`: 全套界面
- 断线保护: 5分钟窗口+BackupState
- 与CultivationManager衔接: 成功→触发突破

**Depends on**: 001~003 → **Unlocks**: None
