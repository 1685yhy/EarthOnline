# 技术审计报告：D线 系统打磨

**审计日期**: 2026-07-11
**审计人**: Technical Director（技术总监）
**项目**: EarthOnline / 地球Online
**引擎**: Unity 2022.3.62t11 (团结引擎) / .NET Standard 2.1

---

## 目录

1. 代码质量扫描
2. 架构评估
3. 测试策略建议
4. 性能优化优先级
5. Editor工具链建议

---

## 1. 代码质量扫描

### 1.1 GameManager.cs (700行) — God类反模式

**概述**: GameManager承担了远超其职责的工作。它同时是系统注册中心、输入调度器、金手指注册表、存档触发器、制作确认管理器。

**问题清单**:
- **Update()长达200+行**: 检查15+个键盘快捷键（T/I/O/P/J/C/K/H/Shift+N/Shift+F/Tab/L/G/M/数字1-4/F5），每个frame执行。
- **金手指注册全内联**: 15个金手指的storyOrigin和storyMystery长篇文本直接写在代码中（行152-297），应迁移到JSON配置。
- **状态蔓延**: OnItemPickedUp()包含金手指觉醒逻辑、自动装备逻辑，与GameManager核心职责无关。
- **Coroutine泛滥**: ConfirmCraft/WaitForSellConfirm用每帧WaitForSeconds+键盘轮询，应改为模态UI。

**影响**: 高。任何系统变更都可能波及GameManager，测试难度极大。

### 1.2 EventBus.cs (51行) — 轻量但脆弱

**概述**: 全局静态字典事件总线，string key + Dictionary<string, object> payload。

**优点**:
- 各模块已解耦，事件驱动模式已确立
- 每个handler有try-catch，单handler异常不影响其他
- Clear()方法在OnDestroy时调用

**问题清单**:
- **类型不安全**: 所有payload用Dictionary<string, object>，频繁boxing/unboxing
- **字符串键**: 无编译期检查，拼写错误静默无订阅者（Publish到无订阅者事件不报错）
- **无生命周期管理**: 订阅者依赖手动Unsubscribe，已发现多个文件遗漏（如CultivationManager只unsubscribe了OnCultivationBoost，如果订阅了其他事件则泄漏）
- **无顺序保证**: 同一事件的handler调用顺序不可控
- **无async支持**: 发布事件是同步的，长操作阻塞主线程
- **性能**: 每次Publish创建新List快照（new List<...>(_listeners[eventType])），高频事件（如OnPlayerMove）会产生大量GC

### 1.3 SaveManager.cs (198行) — 基础但脆弱

**概述**: JSON存档，版本兼容，persistentDataPath存储。

**优点**:
- 有版本号(CURRENT_SAVE_VERSION = 2)
- 有版本升级钩子(UpgradeSaveData)
- try-catch包裹文件IO
- 存档成功/失败有事件通知

**问题清单**:
- **单存档槽位**: 只有一个save_001.json，无法多档位/自动备份
- **无写前备份**: File.WriteAllText直接覆盖，写入中断→存档损坏
- **无校验**: 无JSON Schema校验或Checksum，损坏的JSON静默返回null
- **无加密**: 玩家可随意修改JSON
- **RestoreToManagers硬编码**: 逐字段手动恢复（行120-146），每个新系统需修改此方法
- **_lastLoadedData脆弱的单例依赖**: GameManager通过EventBus触发恢复，时序依赖较强
- **不支持增量保存**: 每次都保存完整状态

### 1.4 CombatSystem.cs (259行) — 职责混合

**概述**: 包含目标选择、伤害计算、灵力管理、VFX触发、Debug GUI。

**问题清单**:
- **GameObject.FindGameObjectWithTag("Player")频繁调用**: 每次CastSpiritAttack和CastTechnique都调用（行183, 189），产生GC Alloc
- **CycleTarget使用FindObjectsOfType<EnemyAI>()**: 每次Tab切换执行全场景扫描（行230）
- **GetPlayerRealm()硬编码阈值**: 使用if链（100/300/600/1000/1500），未与CultivationManager的正式境界系统对齐
- **OnGUI()用于生产代码**: 应改为真正的UI系统
- **输入处理在Update**: 左键/右键/Q键直接绑定，不可重映射
- **VFX和Feedback调用在伤害逻辑中**: CombatFeedback.SpawnHitVFX和VFXManager.Instance?.SpawnSpiritBolt混合在业务逻辑中

### 1.5 CultivationManager.cs (206行) — 相对最清晰

**概述**: 境界系统，enum-based Realm，事件驱动突破。

**优点**:
- Enum定义清晰（Mortal→GreatAscension）
- 突破成功/失败逻辑分离
- 有事件通知（OnRealmBreakthrough event + EventBus publish）

**问题清单**:
- **_breakthroughThresholds硬编码字典**: 应使用ScriptableObject配置，支持策划调优
- **突破成功率公式硬编码**: 70% + (层数-9)*3%，不可配置
- **GetNextLayerCultivation()使用整除**: `baseThreshold + CurrentLayer * (baseThreshold / 5)`，int除法导致低境界时精度问题

### 1.6 PlayerController.cs (115行) — 最干净的模块

**概述**: 标准第三人称控制器，WASD+鼠标+滚轮+跳跃。

**问题清单**:
- **Camera.main缓存**: 只在Start中获取，但如果摄像机切换则失效
- **Physics.Linecast每帧**: UpdateCameraPosition每帧做Linecast（行80），在复杂场景有性能开销
- **Cursor锁定**: 强制Cursor.lockState = CursorLockMode.Locked，不支持UI交互时的解锁

### 1.7 其他模块亮点

- **UIManager**: 代码创建UI，灵活但不可视化编辑。每帧更新所有HUD文本（行136-149），即使数据未变
- **ConfigLoader**: 使用Newtonsoft.Json，但配置文件和实际数据分离——只有2个JSON配置文件
- **NPCBase (254行)**: 较好的NPC架构，但DialogueTree和NPCSchedule等支持类规模可观
- **QuestSystem**: 硬编码任务注册在Start中（行49），未使用JSON配置
- **DualSoulManager**: 状态清晰，IsActive条件耦合到OriginManager

### 1.8 代码规范问题

- **无.asmdef文件**: 所有脚本编译到同一个程序集，编译时间长、模块边界不清晰
- **无XML文档注释标准**: 部分文件有中文注释，部分没有
- **Magic strings四处硬编码**: KeyCode绑定、EventBus事件名、物品ID
- **命名空间不一致**: 部分在EarthOnline命名空间下，部分在子命名空间
- **无代码分析规则**: 无.editorconfig或Roslyn analyzer配置

---

## 2. 架构评估

### 2.1 EventBus模式解耦度评估

| 维度 | 评分 | 说明 |
|------|------|------|
| 模块间解耦 | B+ | 大多数模块通过EventBus通信，不直接依赖 |
| 事件粒度 | B | 事件命名合理（OnItemPickup, OnRealmBreakthrough等） |
| 可追踪性 | C- | 字符串事件名，IDE无法追踪引用 |
| 性能 | D | 高频事件产生GC，boxing严重 |
| 类型安全 | D | 无泛型支持，payload任意 |
| 生命周期 | C- | 泄漏风险，无自动清理机制 |

**结论**: 在项目早期足以支撑开发，但当前代码量（88个脚本）已到达需升级的临界点。建议考虑泛型化或引入轻量级消息库。

### 2.2 性能瓶颈点

已识别的GC热点（按严重程度排序）:

1. **FindObjectsOfType调用** — CycleTarget (CombatSystem)、TryOpenShop (GameManager)
2. **GameObject.FindWithTag("Player")** — CombatSystem中多次调用
3. **EventBus List快照** — 每个Publish创建新List
4. **每帧字符串拼接** — UIManager.Update()每帧拼接HUD文本
5. **CharacterBuilder新建Material** — 每次BuildNPC创建Material，编辑器下可接受但运行时不应使用
6. **Physics.Linecast每帧** — PlayerController.UpdateCameraPosition
7. **OnGUI()** — CombatSystem Unity GUI每帧产生GC

### 2.3 存档系统健壮性评分

| 维度 | 评分 | 说明 |
|------|------|------|
| 数据完整性 | D | 无写前备份，无校验 |
| 版本兼容 | B | 有版本号+升级钩子 |
| 安全性 | F | 纯文本JSON，玩家可直接修改 |
| 扩展性 | C | 手动恢复，每添加系统需修改RestoreToManagers |
| 多档位 | D | 单槽位 |
| 错误恢复 | C | try-catch但损坏数据静默丢失 |

---

## 3. 测试策略建议

### 3.1 高风险区域（优先测试）

按业务影响和复杂度排序：

| 优先级 | 系统 | 风险原因 | 建议测试类型 |
|--------|------|----------|-------------|
| P0 | **SaveManager** | 存档损坏→玩家数据丢失 | 单元测试(序列化/反序列化/版本升级) |
| P0 | **CultivationManager** | 突破逻辑涉及成功率/数值计算 | 单元测试(边界值/层数阈值/突破判定) |
| P0 | **CombatSystem** | 伤害计算涉及多重乘区易出错 | 单元测试(伤害公式/境界压制/暴击) |
| P1 | **EventBus** | 核心基础设施，所有模块依赖 | 单元测试(订阅/取消/异常隔离/并发) |
| P1 | **QuestManager** | 任务状态机，推进逻辑 | 单元测试(状态转换/条件判定) |
| P1 | **InventoryManager** | 物品添加/移除/堆叠逻辑 | 单元测试(边界条件/满背包) |
| P2 | **CraftingSystem** | 配方匹配/材料消耗 | 单元测试(配方匹配/材料扣除) |
| P2 | **ReputationSystem** | 声望增减规则 | 单元测试(边界值) |
| P3 | **PlayerController** | 移动/跳跃/视角 | 集成测试(场景内) |
| P3 | **NPC系统** | 对话树/日程/关系 | 集成测试 |

### 3.2 推荐测试框架配置

```
框架: Unity Test Framework (EditMode + PlayMode)
断言: NUnit (内置)
Mock: NSubstitute (轻量, .NET Standard 2.1兼容)
代码覆盖率: Unity Code Coverage package

目录结构:
Assets/
  Scripts/
    Core/
    Combat/
    Framework/
    ...
  Tests/
    Runtime/          ← PlayMode tests
      Core/
      Combat/
      Framework/
    Editor/           ← EditMode tests
      SaveManagerTests.cs
    TestUtilities/    ← 测试辅助类
      MockEventBus.cs
      TestDataBuilder.cs

Assembly定义:
  Scripts/  → EarthOnline.Runtime.asmdef
  Tests/Runtime/ → EarthOnline.Tests.asmdef (引用 Runtime)
  Tests/Editor/ → EarthOnline.Editor.Tests.asmdef (引用 Runtime + Editor)
```

### 3.3 集成测试范围建议

1. **关键路径流程**: 游戏启动→加载存档→玩家移动→进入战斗→存档→退出
2. **存档兼容性**: v1→v2版本升级路径
3. **EventBus集成**: 多模块间的事件驱动流程（如击杀敌人→任务更新→经验增加→境界突破）
4. **UI数据绑定**: 游戏状态变化→HUD正确更新

### 3.4 测试基础设施先行

在写第一个测试前需要：

1. 添加`EarthOnline.Runtime.asmdef`和测试专用`EarthOnline.Tests.asmdef`
2. 创建MockEventBus（可订阅可验证的测试替身）
3. 创建TestDataBuilder（Builder模式构造测试数据）
4. 配置Unity Code Coverage package

---

## 4. 性能优化优先级

### 4.1 Top 5 性能问题

| 排名 | 问题 | 位置 | 影响 | 修复难度 | 预估收益 |
|------|------|------|------|---------|---------|
| 1 | **FindObjectsOfType高频调用** | CombatSystem.CycleTarget / GameManager.TryOpenShop | 帧率抖动 | 低 | 高 |
| 2 | **GameObject.FindWithTag("Player")在伤害循环中** | CombatSystem.CastSpiritAttack（每击调用2次） | 累积GC | 低 | 高 |
| 3 | **UIManager每帧更新所有HUD文本** | UIManager.Update() 行136-149 | 持续CPU开销 | 中 | 高 |
| 4 | **EventBus List快照分配** | EventBus.Publish 行37（`new List<>(...)`） | 累积GC | 低 | 中 |
| 5 | **GameManager.Update 200行键盘轮询** | GameManager.Update 行315-511 | 每帧CPU开销 | 中 | 中 |

### 4.2 推荐优化顺序

**第一阶段：低成本高收益（1-2天）**
1. 缓存`GameObject.FindGameObjectWithTag("Player")`到CombatSystem字段
2. 在CycleTarget中缓存敌人列表，仅当场景变化时刷新
3. 替换`new List<...>(_listeners[eventType])`为`_listeners[eventType].ToArray()`或直接迭代

**第二阶段：中等成本（3-5天）**
4. UIManager实现脏标记更新——仅当数据变化时更新文本
5. GameManager输入调度迁移到独立的InputManager或Unity Input System package
6. 消除OnGUI()，改为UI Toolkit或ugui

**第三阶段：重构级（1-2周）**
7. EventBus升级到泛型版本或集成轻量级消息库
8. CombatSystem职责拆分：InputHandler / DamageCalculator / VFXController分离
9. 添加.asmdef定义，优化编译流水线

---

## 5. Editor工具链建议

### 5.1 现有工具评估

| 工具 | 用途 | LOC | 质量 | 备注 |
|------|------|-----|------|------|
| CharacterBuilder | 程序化NPC生成 | 621 | B | 功能完整但创建新Material每次调用 |
| SceneSetup | 测试场景一键搭建 | 137 | B- | 硬编码路径/标签，缺少清理逻辑 |
| EarthOnlineSetup | 项目初始化向导 | ? | ? | 未检查 |
| AutoSetup | 自动配置 | ? | ? | 未检查 |
| NPCVisualDatabase | NPC视觉配置数据 | ? | ? | 未检查 |
| AudioPlaceholderGenerator | 音频占位 | ? | ? | 未检查 |
| AudioResourceDownloader | 音频资源下载 | ? | ? | 未检查 |

### 5.2 高优先级新工具

**P0 - 核心生产力**:
1. **Quest Editor Window**: 可视化任务编辑，支持拖拽配置任务链、条件、奖励，JSON导出
   - 理由: 任务目前硬编码在QuestManager.Start()中（20+个任务），策划无法调优
   - 估算: 3-5天

2. **EventBus Debugger**: 实时监控所有EventBus事件，显示发布频率、订阅者列表、payload内容
   - 理由: 当前事件调试只能靠Debug.Log，无法追踪事件流转
   - 估算: 2-3天

**P1 - 日常开发效率**:
3. **Save File Inspector**: 可视化查看/编辑/删除存档文件，支持版本迁移测试
   - 理由: 当前只能通过删除persistentDataPath来重置
   - 估算: 1-2天

4. **Gift Config Editor**: 配置金手指参数、觉醒条件、故事文本
   - 理由: 15个金手指数据全在GameManager代码中，应该可配置
   - 估算: 2-3天

5. **Balance Data Viewer**: 展示所有可调参数（修炼阈值、伤害公式、物价等）的一览表
   - 估算: 1-2天

**P2 - 质量保证**:
6. **Scene Validation Tool**: 检查场景中是否缺失必要的Manager组件、Tag设置、光照配置
   - 估算: 1天

7. **Console Filter & Analyzer**: 在Editor控制台中对游戏事件日志进行过滤、分类、统计
   - 估算: 1天

### 5.3 现有工具改进建议

1. **CharacterBuilder**: 
   - 使用共享材质模板而非每次创建新Material
   - 支持导出为Prefab
   - 添加随机化参数（高度/体型/服饰颜色）

2. **SceneSetup**:
   - 支持多场景模板（城镇/野外/副本）
   - 添加Cleanup功能移除测试对象

3. **通用改进**:
   - 所有Editor工具添加Undo支持
   - 添加进度条（ProgressBar）处理耗时操作

---

## 附录A：文件清单（已审查88个脚本中的核心文件）

| 文件 | 行数 | 评估 |
|------|------|------|
| Core/GameManager.cs | 700 | God类，需拆分 |
| Core/CultivationManager.cs | 206 | 架构清晰，配置可外移 |
| Core/DualSoulManager.cs | ~150 | 状态清晰 |
| Core/PlayerStats.cs | ~80 | 轻量数据类 |
| Core/TimeManager.cs | ~100 | 标准时间系统 |
| Core/WeatherSystem.cs | ~150 | 状态驱动 |
| Combat/CombatSystem.cs | 259 | 职责过重 |
| Combat/EnemyAI.cs | ~200 | 需配合CombatSystem审查 |
| Combat/BuffSystem.cs | 75 | 精简 |
| Combat/SkillComboSystem.cs | ~100 | 连击系统 |
| Framework/EventBus.cs | 51 | 核心基础设施，需升级 |
| Framework/SaveManager.cs | 198 | 健壮性不足 |
| Framework/ConfigLoader.cs | 43 | 简单封装，够用 |
| Framework/InventoryManager.cs | ~120 | 基础功能完整 |
| Framework/QuestSystem.cs | ~500+ | 硬编码任务，需配置化 |
| Player/PlayerController.cs | 115 | 最干净的模块 |
| UI/UIManager.cs | 194 | 代码创建UI，性能可优化 |
| NPC/NPCBase.cs | 254 | 状态+对话+关系混合 |
| Editor/CharacterBuilder.cs | 621 | 功能完整，内存使用可优化 |
| Editor/SceneSetup.cs | 137 | 基础场景搭建 |

## 附录B：风险矩阵

| 风险 | 概率 | 影响 | 等级 | 建议行动 |
|------|------|------|------|---------|
| 存档损坏导致玩家数据丢失 | 中 | 极高 | **关键** | 写前备份+CRC校验+多槽位 |
| EventBus事件泄漏导致内存泄漏 | 高 | 中 | **高** | 自动清理+订阅审计 |
| 战斗数值错误（多重乘区） | 中 | 高 | **高** | 单元测试覆盖伤害公式 |
| God类难以维护和测试 | 高 | 高 | **高** | 拆分GameManager为多个Service |
| FindObjectsOfType性能雪崩 | 中 | 中 | **中** | 缓存+对象池 |
| JSON配置与实际逻辑不匹配 | 中 | 中 | **中** | 配置Schema校验 |
| 突破成功率bug影响游戏进度 | 低 | 高 | **中** | 单元测试覆盖边界条件 |
