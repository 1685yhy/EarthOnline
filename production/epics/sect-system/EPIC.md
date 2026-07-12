# Epic: 门派系统

> **Layer**: Feature
> **GDD**: design/gdd/sect-system.md
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories sect-system`

## Overview

在现有 FactionSystem.cs 声望框架上扩展完整身份管理系统。实现加入/退出/叛逃/散修四种身份路径、5级职级晋升体系、偷学禁术机制、门派战系统、跨门派周旋与卧底。门派是身份+资源+社交圈，不是"选职业"。

## Existing Code to Modify
- `Assets/Scripts/World/FactionSystem.cs` — 扩展为完整门派管理

## New Modules
- `SectManager.cs` — 门派身份管理
- `SectRankSystem.cs` — 职级与晋升
- `BetrayalSystem.cs` — 叛逃与追杀
- `SecretLearning.cs` — 偷学机制
- `SectWarSystem.cs` — 门派战
- `SectContributionUI.cs` — 门派界面

## Definition of Done
- 5门派可加入/退出/叛逃
- 叛逃3级追杀流程完整
- 散修路线可独立完成游戏
- 偷学4种途径+风险判定
- 门派战72小时时限+积分制
- 跨门派声望联动正确
