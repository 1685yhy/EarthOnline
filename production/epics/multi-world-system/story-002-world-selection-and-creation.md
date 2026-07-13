# Story 002: 世界选择与初始创建

> **Epic**: multi-world-system
> **Status**: Ready
> **Layer**: Framework
> **Type**: Logic
> **Estimate**: 12h

## Context

**GDD**: `design/gdd/multi-world-system.md`
**Requirement**: CRE-01 ~ CRE-05

**Engine**: Unity 2022.3.62t11 (Tuanjie 1.9.3) | **Risk**: MEDIUM

创建角色时，玩家从5个世界中选择一个作为起始世界。每个起始世界提供不同的初始体验、资源包和金手指。灵气大陆为默认推荐，但所有世界均可选。前30分钟可免费更换一次起始世界。

## Acceptance Criteria

- [ ] CRE-01: 创建角色时显示5个世界选项，每个世界展示名称、描述、初始资源包、金手指说明
- [ ] CRE-02: 选择任意起始世界后，游戏加载对应的新手引导和初始场景
- [ ] CRE-03: 前30分钟游戏时间内在系统菜单中可找到"更换起始世界"选项
- [ ] CRE-04: 超过30分钟后，"更换起始世界"选项消失
- [ ] CRE-05: 默认推荐灵气大陆，但其他世界选择无隐藏限制

## Implementation Notes

- 新建 `CharacterCreationUI.cs` — 角色创建界面，展示5个世界卡片
- 每个世界卡片从 `WorldConfig` SO 读取显示数据（name, description, starterPack, goldenFinger）
- 灵气大陆卡片标记"推荐"标签
- 新建 `WorldStarterConfig.cs` — 存储各世界的初始资源包和初始金手指数据
- 选择起始世界后：
  - 设置 `PlayerPrefs` / 存档中的起始世界ID
  - 调用 `WorldManager.EnterWorld(worldId)` 加载对应起始场景
  - 加载对应新手引导配置（每世界一套独立配置）
- 新手引导配置：`Assets/Config/Tutorial/` 下每个世界一个引导配置SO
- 30分钟倒计时：使用 `GameTimeManager` 的游戏内时间追踪
- 倒计时未结束前，系统菜单"更换起始世界"按钮调用 `WorldManager.RerollStartWorld()`
- 更换起始世界后，原世界进度保留，可通过大厅后续返回

## QA Test Cases

- **CRE-01**: Given:开启新游戏, When:进入角色创建界面, Then:显示5个世界卡片，含名称/描述/资源包/金手指
- **CRE-02**: Given:选择都市世界为起始, When:确认创建, Then:加载都市世界新手引导和初始场景
- **CRE-03**: Given:创建角色后游戏时间<30分钟, When:打开系统菜单, Then:可见"更换起始世界"选项

## Dependencies
- Depends on: WorldConfig (SO), WorldManager
- Unlocks: 各世界专属新手引导系统
