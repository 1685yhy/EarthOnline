# Story 003: BOSS四种路径

> **Epic**: BOSS战 | **Layer**: Feature | **Type**: Integration | **Estimate**: 8h
> **GDD**: `design/gdd/boss-system.md` | **Req**: PATH-01~09

## Acceptance Criteria
- [ ] 正面战斗: 完整掉落+修为+声望+称号
- [ ] 外交谈判: 可谈判BOSS显示对话选项, 接受条件→和平通过
- [ ] 谈判反悔: BOSS狂暴+记仇+4
- [ ] 潜行绕过: 成功率60%+技能修正-BOSS感知
- [ ] 潜行失败: 进入战斗+初始BOSS好感-30
- [ ] 援军: NPC/门派弟子/召唤符/临时组队
- [ ] 四种路径奖励梯度: 战斗>谈判>援军>潜行

## Implementation
- `BossDiplomacy.cs`: 谈判条件生成+交涉判定
- 潜行: `StealthSuccess = 0.6 + Level×0.01 + EquipBonus - BossPerception×0.02`
- 援军: 消耗道具+AI战斗

**Depends on**: 002 → **Unlocks**: 004
