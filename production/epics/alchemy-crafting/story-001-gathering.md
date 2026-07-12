# Story 001: 采集与感知系统

> **Epic**: 丹药炼器 | **Layer**: Core | **Type**: Logic | **Estimate**: 6h
> **GDD**: `design/gdd/alchemy-crafting-system.md` | **Req**: GTH-01~10

## Acceptance Criteria
- [ ] 运行功法时周围灵材以光点显示在感知范围内(10m+境界×5m)
- [ ] 普通灵材靠近按F采集，无失败风险
- [ ] 高级灵材需特定工具(提示"需要玉锄")
- [ ] 采集有进度条，可被打断
- [ ] 同种灵材连续采集后资源点消失等待刷新
- [ ] 不同区域产不同灵材
- [ ] 有守护妖兽的灵材需先战斗
- [ ] 天材地宝采集时全区域公告
- [ ] 熟练度提升后增加产出和速度

## Implementation
- 新建 `GatheringSystem.cs`，感知公式: `PerceptionRadius = 10 + Stage×5`
- 工具匹配表: 草本→玉锄头, 矿物→寒铁镐, 液体→玉瓶
- 资源点类型: 普通/稀有/灵泉/矿脉/天材地宝

## QA
- Given:普通灵材点, When:按F, Then:采集成功+背包增加
- Given:高级灵材无工具, When:采集, Then:提示"需要玉锄"

**Depends on**: None → **Unlocks**: Story 002
