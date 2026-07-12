# Epic: 丹药炼器系统

> **Layer**: Core
> **GDD**: design/gdd/alchemy-crafting-system.md
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories alchemy-crafting`

## Overview

重构现有扁平配方系统为完整五阶段体验弧线：探索感知→采集获取→鉴别分类→炼制加工→使用循环。新增控火炼丹、熔炼塑形淬火开光四步炼器、药抗机制、变异系统、设备耐久、熟练度成长。扩展现有 CraftingSystem.cs 和 AlchemyMaster.cs。

## Existing Code to Modify
- `Assets/Scripts/Framework/CraftingSystem.cs` — 重构为五阶段系统
- `Assets/Scripts/Gifts/AlchemyMaster.cs` — 集成控火接口
- `Assets/Scripts/World/ItemDatabase.cs` — 扩展物品属性

## New Modules
- `AlchemyController.cs` — 控火炼丹核心
- `ForgeController.cs` — 炼器四步流程
- `GatheringSystem.cs` — 采集与感知
- `IdentificationSystem.cs` — 灵材鉴定
- `RecipeDatabase.cs` — 配方管理
- `EquipmentDurability.cs` — 设备耐久

## Definition of Done
- 采集→鉴定→炼丹/炼器→使用 完整循环
- 控火三档切换+投料顺序影响品质
- 炼器四步流程可操作
- 药抗机制正确运作
- 变异配方可触发
- 49+验收条件通过
