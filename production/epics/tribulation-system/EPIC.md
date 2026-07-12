# Epic: 渡劫系统

> **Layer**: Feature
> **GDD**: design/gdd/tribulation-system.md
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories tribulation-system`

## Overview

实现修炼终极考验——渡劫系统。玩家主动前往天劫台引劫，经历雷劫(操作考验)→心魔劫(意志考验)→天道问心(价值观考验)三阶段。四维准备体系汇聚前面所有系统产出。失败不死亡而是积累经验。散修更难但道体更稀有。与CultivationManager.cs深度衔接。

## Existing Code to Modify
- `Assets/Scripts/Core/CultivationManager.cs` — 衔接渡劫触发

## New Modules
- `TribulationManager.cs` — 渡劫主控
- `TribulationPlatform.cs` — 天劫台交互
- `ThunderTribulation.cs` — 雷劫阶段
- `HeartDemonTribulation.cs` — 心魔劫阶段
- `DaoQuestioning.cs` — 天道问心阶段
- `TribulationBody.cs` — 道体生成与特性
- `TribulationUI.cs` — 渡劫界面

## Definition of Done
- 天劫台三种品质可用
- 四维准备评分正确计算
- 雷劫逐道递增+走位操作
- 心魔基于玩家历史生成
- 天道问心解析回答→道体
- 失败积累经验+保底机制
- 散修补偿机制正确
