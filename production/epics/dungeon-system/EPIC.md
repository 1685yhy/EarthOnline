# Epic: 副本实例系统

> **Layer**: Feature
> **GDD**: design/gdd/dungeon-system.md
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories dungeon-system`

## Overview

替代现有极简 DungeonEntrance.cs（硬编码传送+2个怪），实现完整副本实例系统。支持动态难度(4级)、岔路选择(种子算法)、6种房间类型、4种通行方式(战斗/潜行/谈判/环境)、产出递减防刷、进度保存48小时、联机4人/NPC队友2人组队。

## Existing Code to Modify
- `Assets/Scripts/World/DungeonEntrance.cs` — 重构为完整副本入口

## New Modules
- `DungeonInstance.cs` — 副本实例化与生成
- `DungeonRoomGenerator.cs` — 房间生成器(种子算法)
- `DungeonDifficulty.cs` — 动态难度计算
- `DungeonProgress.cs` — 进度保存与续关
- `DungeonParty.cs` — 组队管理
- `DungeonReward.cs` — 奖励与防刷

## Definition of Done
- 动态难度4级正确运作
- 岔路选择生成不同路径
- 6种房间类型全部实现
- 4种通行方式可选
- 产出递减防刷机制生效
- 进度可保存48小时续关
