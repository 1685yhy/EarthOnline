# Story 008: 炼制UI界面

> **Epic**: 丹药炼器 | **Layer**: Core | **Type**: UI | **Estimate**: 8h
> **GDD**: `design/gdd/alchemy-crafting-system.md` | **Req**: INT-01~10

## Acceptance Criteria
- [ ] 炼丹UI: 丹炉面板+火候切换按钮+温度条+药液颜色变化+投料槽
- [ ] 炼器UI: 四步进度条+锤击力度指示器+淬火液选择+灵力注入条
- [ ] 采集感知: 灵材光点+迷你地图绿色标记+边界范围可视化
- [ ] 配方界面: 列表/搜索/收藏/自创配方标记
- [ ] 熟练度显示: 等级+称号+进度条
- [ ] 设备状态: 耐久度条+维修按钮
- [ ] 采集→鉴定→炼制→使用循环无阻塞

## Implementation
- 新建 `AlchemyUI.cs`, `ForgeUI.cs`, `GatheringHUD.cs`
- 使用 UGUI + Canvas
- 火候切换: 三按钮+颜色反馈+CD指示器
- 炼器QTE: 力度条动画

## QA
- Given:靠近丹炉, When:交互, Then:炼丹UI正常打开
- Given:控火操作, When:切换火候, Then:温度条实时变化+药液颜色改变

**Depends on**: Story 001~007 → **Unlocks**: None
