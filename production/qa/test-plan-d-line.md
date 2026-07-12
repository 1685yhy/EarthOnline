# 地球Online — 测试计划 d-Line

> 版本：v1.0 | 创建日期：2026-07-12 | QA Lead: Claude

---

## 一、测试范围

本项目主要覆盖四个核心系统的单元测试和集成测试：

| 系统 | 测试类型 | 测试数量 | 优先级 |
|------|----------|----------|--------|
| EventBus 事件总线 | 单元测试 | 10 | P0 |
| CultivationManager 修炼系统 | 单元测试 | 13 | P0 |
| SaveManager 存档系统 | 单元测试 | 14 | P0 |
| CraftingManager 制造系统 | 单元测试 | 17 | P1 |

**总计：54 个测试用例**

---

## 二、测试架构

### 2.1 目录结构

```
Assets/
  Scripts/
    Scripts.asmdef                                  # 主程序集（包含所有游戏脚本）
  Tests/
    Helpers/
      TestEventBus.cs                # EventBus 测试辅助（Spy监视器、重置、断言）
      TestFactory.cs                 # 测试数据工厂（SaveData、Item、Recipe payload）
    EditMode/
      EditModeTests.asmdef           # EditMode 测试程序集（引用 EarthOnline.Scripts）
      EventBusTests.cs               # EventBus 单元测试
      CultivationManagerTests.cs     # 修炼系统单元测试
      SaveManagerTests.cs            # 存档系统单元测试
      CraftingSystemTests.cs         # 制造系统单元测试
    PlayMode/
      PlayModeTests.asmdef           # PlayMode 测试程序集（预留）
```

### 2.2 测试框架

- **框架**：Unity Test Framework 1.1.33（com.unity.test-framework）
- **运行时**：EditMode 测试（不需进入 Play Mode）
- **断言**：NUnit 3.5
- **程序集**：通过 .asmdef 显式引用，确保测试代码与产品代码分离

### 2.3 辅助类设计

| 类 | 职责 |
|----|------|
| `TestEventBus` | 提供 EventBus 的 Spy 监听器、状态重置、断言辅助 |
| `EventSpy` | 记录事件调用次数和负载数据 |
| `TestFactory` | 创建标准化的测试对象（SaveData、Item、payload 字典） |

---

## 三、测试用例详情

### 3.1 EventBus 事件总线（P0）

| 编号 | 测试名称 | 验证内容 |
|------|----------|----------|
| EB-01 | Subscribe_Publishes_ReceivesData | 订阅后发布事件，处理器收到正确的负载 |
| EB-02 | Subscribe_MultipleHandlers_AllCalled | 同一事件的多个订阅者全部触发 |
| EB-03 | Unsubscribe_Removes_Handler | 取消订阅后处理器不再被调用 |
| EB-04 | Unsubscribe_NonExistent_DoesNotThrow | 取消不存在的订阅不抛异常 |
| EB-05 | Publish_NoListeners_DoesNotThrow | 无监听的事件发布不抛异常 |
| EB-06 | Publish_NullData_UsesEmptyDict | 发布 null 数据时自动转换为空字典 |
| EB-07 | Clear_RemovesAllListeners | Clear 后所有监听器被移除 |
| EB-08 | DifferentEvents_DoNot_Interfere | 不同事件彼此隔离 |
| EB-09 | HandlerException_DoesNot_BreakOtherHandlers | 异常处理器不影响其他同一事件的处理器 |
| EB-10 | Publish_Payload_MultipleKeys | 复杂负载的数据正确传递 |

### 3.2 CultivationManager 修炼系统（P0）

| 编号 | 测试名称 | 验证内容 |
|------|----------|----------|
| CM-01 | DefaultRealm_IsMortal | 初始境界为凡人 |
| CM-02 | DefaultLayer_IsZero | 初始层数为0 |
| CM-03 | RealmName_Mortal_ReturnsChinese | 境界名中文映射：凡人 |
| CM-04 | FullTitle_Mortal_ReturnsMortal | 完整称号格式：凡人 |
| CM-05 | GetNextLayerCultivation_QiRefining_Base100 | 练气期第1层修为门槛计算 |
| CM-06 | GetNextLayerCultivation_Mortal_Returns100 | 凡人的默认门槛100 |
| CM-07 | CheckLayerAdvance_SufficientCultivation_AdvancesLayer | 修为达标时自动升层 |
| CM-08 | CheckLayerAdvance_InsufficientCultivation_DoesNotAdvance | 修为不足不升层 |
| CM-09 | AttemptBreakthrough_Success_ChangesRealm | 突破成功提升境界（随机性容忍） |
| CM-10 | RealmBreakthrough_EventPublished | 突破事件广播格式正确 |
| CM-11 | RealmNameMapping_AllRealms_HaveChineseName | 8个境界全部有中文名 |
| CM-12 | MaxLayer_IsPlayer_Returns13 | 主角每境13层（特权） |
| CM-13 | MaxLayer_NotPlayer_Returns9 | NPC每境9层 |

### 3.3 SaveManager 存档系统（P0）

| 编号 | 测试名称 | 验证内容 |
|------|----------|----------|
| SM-01 | HasSave_NoFile_ReturnsFalse | 无存档文件时返回 false |
| SM-02 | Save_ValidData_WritesFile | 存档写入文件系统 |
| SM-03 | Save_Sets_Version | 保存自动设置版本号到 CURRENT_SAVE_VERSION |
| SM-04 | Save_Sets_SaveTime | 保存自动设置时间戳 |
| SM-05 | Save_Triggers_OnGameSavedEvent | 保存后广播 OnGameSaved |
| SM-06 | Load_NoFile_ReturnsNull | 无存档时 Load 返回 null |
| SM-07 | Load_ExistingFile_ReturnsData | 读档返回正确的数据 |
| SM-08 | Load_Restores_NPCProgress | NPC 进度数据正确恢复 |
| SM-09 | Load_Updates_Version | V1 存档自动升级到 V2 |
| SM-10 | HasSave_AfterSave_ReturnsTrue | 存档后检测存在 |
| SM-11 | DeleteSave_RemovesFile | 删除存档文件 |
| SM-12 | DeleteSave_Triggers_OnSaveDeletedEvent | 删除后广播 OnSaveDeleted |
| SM-13 | DeleteSave_NoFile_DoesNotThrow | 文件不存在时删除不抛异常 |
| SM-14 | SaveAndLoad_RoundTrip_PreservesAllFields | 全字段往返完整性 |

### 3.4 CraftingManager 制造系统（P1）

| 编号 | 测试名称 | 验证内容 |
|------|----------|----------|
| CR-01 | GetAllRecipes_ReturnsAll | 获取全部注册配方 |
| CR-02 | Recipe_HealPill_Exists | 回血丹配方存在且属性正确 |
| CR-03 | Recipe_Ingredients_StoredCorrectly | 配方材料字典存储正确 |
| CR-04 | Recipe_SpiritCore_HasTwoIngredients | 复方配方的材料数量正确 |
| CR-05 | Recipe_Value_MatchesRegistration | 配方产物价值正确 |
| CR-06 | GetAvailableRecipes_WithoutItems_ReturnsEmpty | 空背包时无可用配方 |
| CR-07 | GetAvailableRecipes_WithIngredients_ReturnsRecipes | 有材料时显示可用配方 |
| CR-08 | Craft_WithoutInventory_ReturnsFalse | 无 InventoryManager 时制作失败 |
| CR-09 | Craft_UnknownRecipe_ReturnsFalse | 不存在的配方返回 false |
| CR-10 | Craft_InsufficientIngredients_ReturnsFalse | 材料不足时制作失败 |
| CR-11 | Craft_SufficientIngredients_Succeeds | 材料充足时制作成功 |
| CR-12 | Craft_RemovesIngredients | 制作消耗材料 |
| CR-13 | Craft_AddsResultItem | 制作产物进入背包 |
| CR-14 | Craft_Triggers_OnItemCraftedEvent | 制作后广播 OnItemCrafted |
| CR-15 | AllRecipeIds_AreUnique | 所有配方 ID 唯一 |
| CR-16 | AllRarities_Are_Valid | 所有配方稀有度有效（N/R/SR/SSR） |

---

## 四、执行计划

### 阶段一：基础设施搭建（当前）
- 创建 .asmdef 程序集定义
- 创建 Helpers/ 辅助类
- 创建首批 47 个测试用例

### 阶段二：编译验证
- 在 Unity Editor 中打开项目
- 确认所有 .asmdef 编译通过
- 运行 EditMode 测试套件
- 修复失败的测试

### 阶段三：扩展测试覆盖
- CombatSystem 战斗系统测试
- InventoryManager 背包系统测试
- QuestSystem 任务系统测试
- NPC 交互系统测试
- JSON 配置加载测试

### 阶段四：PlayMode 测试
- 场景加载测试
- 玩家移动/交互测试
- 战斗流程集成测试
- 存档/读档全流程测试

### 阶段五：回归测试套件
- 建立 CI 集成方案
- 自动运行 EditMode 测试
- 测试覆盖率报告

---

## 五、测试规范

### 5.1 命名规范

- **测试类**：`{SystemName}Tests`（如 `EventBusTests`）
- **测试方法**：`{Action}_{Condition}_{ExpectedResult}`（如 `Save_ValidData_WritesFile`）
- **测试文件**：与类同名，放在 `Tests/EditMode/` 下

### 5.2 测试模式

每个测试遵循 AAA 模式：
1. **Arrange**：准备测试数据和依赖
2. **Act**：执行被测试方法
3. **Assert**：验证结果

### 5.3 隔离原则

- 每个测试在 `[SetUp]` 中创建独立的对象
- 每个测试在 `[TearDown]` 中销毁所有对象
- 使用 `TestEventBus.Reset()` 清除全局 EventBus 状态
- 不依赖测试执行顺序
- 使用反射注入 Singleton 实例（因项目使用静态 Instance 模式）

### 5.4 特殊处理

由于项目大量使用 Singleton 模式（`*Manager.Instance`），测试中需要：
- 在 `[SetUp]` 中通过反射设置 `Instance` 字段
- 在 `[TearDown]` 中通过 `DestroyImmediate` 清理
- 避免测试间的单例状态污染

---

## 六、风险与待办

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| Singleton 依赖难以 mock | 测试隔离性受挑战 | 反射设置 Instance；考虑将来引入 DI 容器 |
| Unity Random 不可控 | 突破测试概率性失败 | 多次尝试 + Ignore 机制 |
| persistentDataPath 在 EditMode 中的行为 | 存档路径不一致 | 使用临时目录 |
| 无 asmdef 历史 | 编译配置可能需调整 | 首次编译后检查 Console |
| 团结引擎兼容性 | 部分 Unity API 可能不同 | 先运行基础测试验证 |
