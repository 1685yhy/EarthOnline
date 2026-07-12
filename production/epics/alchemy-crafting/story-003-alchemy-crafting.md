# Story 003: 控火炼丹核心

> **Epic**: 丹药炼器 | **Layer**: Core | **Type**: Logic | **Estimate**: 10h
> **GDD**: `design/gdd/alchemy-crafting-system.md` | **Req**: ALM-01~10

## Acceptance Criteria
- [ ] 需要丹炉才能炼丹，靠近触发炼丹UI
- [ ] 三个火候档位可切换(大火/中火/小火)，1.5s CD
- [ ] 火候影响品质: 沸腾期大火→融合期中火→提纯期小火
- [ ] 投料顺序影响最终品质(错1次-15%)
- [ ] 错误火候持续太久会炸炉
- [ ] 炸炉: 材料损失50-100%+丹炉耐久-20~50+玩家受伤
- [ ] 产出品质四级: 下品(白)/中品(绿)/上品(蓝)/极品(紫)
- [ ] 极品丹药有额外特效
- [ ] 变异配方可产出非标准物品
- [ ] 炼丹熟练度正确累计

## Implementation
- 新建 `AlchemyController.cs`
- 品质公式: `FinalQuality = Base×TempScore×OrderScore×MatScore×EquipMod`
- 炸炉概率: `0.05 × OverheatFactor × HealthFactor`
- 控火: 大火+8°C/s, 中火+2°C/s, 小火-1°C/s

## QA
- Given:正确控火+标准材料, When:炼丹, Then:中品以上产出
- Given:持续大火, When:超过安全时间, Then:炸炉+材料损失

**Depends on**: Story 002 → **Unlocks**: Story 004
