# Story 003: 天道问心+道体+结局

> **Epic**: 渡劫 | **Layer**: Feature | **Type**: Integration | **Estimate**: 8h
> **GDD**: `design/gdd/tribulation-system.md` | **Req**: DAO-01~07, BOD-01~06, RSL-01~08

## Acceptance Criteria
- [ ] 心魔后自动进入天道问心
- [ ] 问题池至少6个，回答解析到4维度(道之心/力量观/情绪/执念)
- [ ] 不同解析→不同道体(守成/破虚/超然/凡人)
- [ ] 品质1-5级(凡体→混沌体)，外观逐级递升
- [ ] 散修道体品质+1
- [ ] 成功→突破至大成+区域声望+200
- [ ] 品质≥4世界公告
- [ ] 失败→修为跌回大圆满+经验+5%(上限25%)
- [ ] 第4次失败保底无额外惩罚

## Implementation
- `DaoQuestioning.cs`: 问题池+回答解析
- `TribulationBody.cs`: 道体生成+特性映射
- 道体特性永久存储: SaveData

**Depends on**: 002 → **Unlocks**: 004
