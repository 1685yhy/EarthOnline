# Epic: BOSS战系统

> **Layer**: Feature
> **GDD**: design/gdd/boss-system.md
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories boss-system`

## Overview

为灵气大陆实现完整的BOSS战斗系统。每个BOSS有独特行为机制、可侦察弱点、多阶段转换、四种应对路径（正面/谈判/潜行/援军）。BOSS记仇系统让每次遭遇带有叙事重量。BOSS掉落材料是高级炼器的核心来源。

## Existing Code to Modify
- `Assets/Scripts/Combat/EnemyAI.cs` — 扩展为BOSS专用AI

## New Modules
- `BossDef.cs` — BOSS数据定义
- `BossAI.cs` — BOSS行为树与阶段管理
- `BossWeaknessSystem.cs` — 弱点侦察与利用
- `BossDiplomacy.cs` — 谈判系统
- `BossGrudgeSystem.cs` — 记仇追踪
- `BossEncounterManager.cs` — 遭遇管理(出场/封锁/撤退)

## Definition of Done
- BOSS出场演出完整
- 多阶段转换机制正确
- 弱点侦察4种方式可用
- 4种应对路径全部实现
- 记仇系统追踪玩家行为
- BOSS材料链接炼器配方
