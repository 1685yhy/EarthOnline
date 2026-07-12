# Epic: 新地图系统

> **Layer**: Core
> **GDD**: design/gdd/new-maps-system.md
> **Status**: Ready
> **Stories**: 8 stories created

| # | Story | Type | Status |
|---|-------|------|--------|
| 001 | 迷雾系统 | Logic | Ready |
| 002 | 风险评级 | Logic | Ready |
| 003 | 三层发现系统 | Integration | Ready |
| 004 | 快速旅行重构 | Integration | Ready |
| 005 | 区域生态+声望 | Logic | Ready |
| 006 | 探索深度追踪 | Logic | Ready |
| 007 | 动态事件触发 | Integration | Ready |
| 008 | 世界地图界面 | UI | Ready |

## Overview

实现灵气大陆的世界层基础设施。将静态场景集合转变为活的生态系统——迷雾系统（未见之地保持隐藏）、发现系统（探索本身就是奖励）、风险预警系统（告知危险但永不阻止踏入）。扩展现有 FastTravel.cs 和 HiddenDiscovery.cs，新增 FogOfWar、RegionData、RiskRating、ExplorationDepth、AreaReputation 模块。

## Existing Code to Modify
- `Assets/Scripts/World/FastTravel.cs` — 重构为条件式传送
- `Assets/Scripts/World/HiddenDiscovery.cs` — 扩展为三层发现

## New Modules
- `FogOfWar.cs` — 迷雾3层系统
- `RegionData.cs` — 区域定义与生态参数
- `RiskRating.cs` — 风险评级与预警
- `ExplorationDepth.cs` — 探索度追踪
- `AreaReputation.cs` — 区域声望
- `WorldMapUI.cs` — 世界地图界面

## Definition of Done
- All 50+ acceptance criteria from GDD verified
- FastTravel 支持费用/读条/冷却/条件
- 迷雾3层正确运作
- 发现3等级触发正确
- 风险评级实时反映玩家境界
- 区域声望独立运作
