# Epic: 多世界系统

> **Layer**: Framework
> **GDD**: design/gdd/multi-world-system.md
> **Status**: Ready
> **Stories**: 8 stories created

| # | Story | Type | Status |
|---|-------|------|--------|
| 001 | 穿越者大厅 | Integration | Ready |
| 002 | 世界选择与初始创建 | Logic | Ready |
| 003 | 世界穿越机制 | Integration | Ready |
| 004 | 跨世界物品系统 | Integration | Ready |
| 005 | 力量适配系统 | Logic | Ready |
| 006 | 世界解锁递进 | Logic | Ready |
| 007 | 返回旧世界 | Logic | Ready |
| 008 | 存档与时间推进 | Integration | Ready |

## Overview

多世界系统是地球Online的叙事主骨架。定义了一套统一的跨世界框架——玩家以"穿越者"身份在5个世界观截然不同的世界间穿梭，将每个世界获得的力量带回，最终聚合为拯救地球的本钱。系统涵盖穿越者大厅（中枢场景）、世界穿越机制、跨世界物品分类规则、力量压制与适配系统、世界递进解锁、返回旧世界保护机制、独立时间线推进和跨世界存档。

## Dependencies

- **WorldConfig** (ScriptableObject) — 强依赖，每个世界的数据定义
- **SceneManager** (Unity) — 强依赖，加载和切换世界场景
- **SaveManager** — 强依赖，多世界独立存档和读取
- **穿越者大厅场景** — 需要独立构建的Unity场景
- **WorldManager** (单例) — P0，必须先实现
- **PowerAdaptationSystem** — P0，必须先实现
- **CrossWorldInventory** — P1，第一个跨世界版本需要
- **WorldTimeManager** — P2，多世界稳定后再实现

## New Modules

- `WorldManager.cs` — 运行时管理世界切换和状态列表
- `WorldConfig.cs` (SO) — 各世界配置数据定义
- `WorldInstanceData.cs` — 各世界运行时数据结构
- `PowerAdaptationSystem.cs` — 跨世界力量压制与还原计算
- `CrossWorldInventory.cs` — 跨界仓库共享存储
- `WorldTimeManager.cs` — 各世界独立时间线推进
- `TransmigratorMark.cs` — 灵魂属性数据
- `IWorldSystem.cs` — 世界系统统一接口
- 穿越者大厅场景（含世界之门UI）
- 压制率HUD组件

## Existing Code to Modify

- `SaveManager` — 扩展为支持多世界独立存档
- `Inventory` — 需要支持世界专属背包+跨界仓库双重结构
- `QuestManager` — 需要支持跨世界任务追踪
- 各世界专属系统 — 需要实现 `IWorldSystem` 接口

## Definition of Done

- All 60+ acceptance criteria from GDD verified
- 穿越者大厅场景可用，包含世界之门、跨界仓库、穿越者印记、穿越者手册
- 5个起始世界选择正常，新手引导按世界加载
- 世界穿越流程完整：冷却、动画、intro叙事、存档点恢复
- 跨世界物品分类正确：通用/世界绑定/灵魂绑定三类规则生效
- 力量压制率按公式计算，适配还原方式全部可用
- 世界解锁条件检测正确，解锁动画触发
- 返回旧世界力量软锁定+世界排斥保护机制生效
- 多世界存档独立保存和读取，时间推进正确累计
