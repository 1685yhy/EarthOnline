# DualSoul 复仇任务链设计文档

**日期：** 2026-07-11 | **出身：** #8 DualSoul | **门派：** 太虚宗·天元宗

---

## 概述

仅 DualSoul 出身激活的 5 章主线任务链。以"穿越者+原主人共享一具身体"为核心机制，与 `DualSoulManager` 深度联动（信任度/觉醒度双轨并进）。

- **任务总数：** 5 个（觉醒 → 试探 → 反击 → 决战 → 真相）
- **核心NPC：** 苏念雪(小师妹·林梦) / 明长老 / 天玄子(师父) / 见证长老
- **双轨计量：** 信任度(Trust 0-100) + 觉醒度(Awakening 0-100)
- **原主人：** 念安（元婴期修为，性格极度懦弱）

### 数据来源对照

任务数据同时存在于 `DualSoulQuestChain.cs` 运行时定义和 `QuestSystem.cs` 的 `QuestData` 注册表。以下"任务结构"一栏展示了每个字段在两个系统中的映射关系。

---

## 任务1：觉醒·第一声低语

| 字段 | 值 |
|------|-----|
| **ID** | `ds_01` |
| **章节** | 觉醒 |
| **标题** | 觉醒·第一声低语 |
| **类型** | Talk（对话） |
| **GiverNPC** | 命运（自动激活） |
| **目标** | 目睹林师妹偷走丹药，并回应内心的声音 |
| **完成条件** | `littleSisterStolen == true && firstTalkDone == true` |
| **nextQuestId** | `ds_02` |

### 任务结构

| QuestData 字段 | 值 | DualSoulQuest 扩展字段 |
|---|---|---|
| id | ds_01 | chapter = "觉醒" |
| title | 觉醒·第一声低语 | shortObjective = "目睹林师妹偷走丹药，并回应内心的声音" |
| type | QuestType.Talk | trigger = { type: Auto, autoDelay: 5s } |
| description | "你灵魂中有一个声音在告诉你——有人在偷东西。你不敢看——但这次你得看看。" | completionCheck → ctx.littleSisterStolen && ctx.firstTalkDone |
| giverNpcId | ""（自动触发） | onAccept → ctx.TriggerLittleSisterScene() |
| giverName | "命运" | onComplete → AddTrust(+8) AddAwakening(+5) |
| targetId | "" | — |
| targetCount | 1 | — |
| rewardSpiritStones | 50 | — |
| rewardCultivation | 20 | — |
| rewardItemId | "" | — |

### 触发条件

- **类型：** 自动触发（Auto）
- **延迟：** 5 秒（进入游戏后自动弹出）
- **前置要求：** 无。觉醒是故事起点，不可跳过

### 奖励

| 类型 | 数值 |
|------|------|
| 灵石 | 50 |
| 修为 | 20 |
| 信任度 | +8（"念安第一次相信了你的话"） |
| 觉醒度 | +5（"发现小师妹偷丹药的真相"） |

### 接受对话

> "（内心低语）'你……你真的是在和我说话？'"

### 场景流程（onAccept）

自动触发林师妹偷丹药事件：
1. 林师妹进入房间，熟练地翻开储物袋
2. 取走三枚聚灵丹
3. 自言自语："师兄不会发现的——他从来不敢说话。"
4. 穿越者的"内心声音"让念安看到了这一幕

### 完成对话

> "'她……她真的拿了。我一直以为她只是……只是借。但是她没有还过。从来没有。'"

### 完成文本

> 念安开始动摇了。他的世界——第一次出现了裂痕。

### 失败条件

无——觉醒是故事起点，不可跳过。

---

## 任务2：试探·第一次出手

| 字段 | 值 |
|------|-----|
| **ID** | `ds_02` |
| **章节** | 试探 |
| **标题** | 试探·第一次出手 |
| **类型** | Combat（战斗） |
| **GiverNPC** | 林师妹（`npc_lin_meng`） |
| **目标** | 在宗门小比中击败明长老安排的对手 |
| **完成条件** | `tournamentWon == true` |
| **nextQuestId** | `ds_03` |

### 任务结构

| QuestData 字段 | 值 | DualSoulQuest 扩展字段 |
|---|---|---|
| id | ds_02 | chapter = "试探" |
| title | 试探·第一次出手 | shortObjective = "在宗门小比中击败明长老安排的对手" |
| type | QuestType.Combat | trigger = { type: NpcTalk, npcId: "npc_lin_meng" } |
| description | "宗门小比在即。明长老故意安排你和筑基期师兄对战——他想看你出丑。但这次——你有帮手。" | completionCheck → ctx.tournamentWon |
| giverNpcId | "npc_lin_meng" | onAccept → ctx.RegisterCombatListener("ds_02_boss") |
| giverName | "林师妹" | onComplete → AddTrust(+10) AddAwakening(+8) |
| targetId | "ds_02_boss" | — |
| targetCount | 1 | — |
| rewardSpiritStones | 120 | — |
| rewardCultivation | 60 | — |
| rewardItemId | "item_dualsoul_talisman_01" | — |

### 触发条件

- **类型：** NpcTalk（与林师妹对话触发）
- **前置要求：** 无信任度/觉醒度门槛
- **接受条件：** 林师妹关心你——"师兄你……真的要参加小比吗？明长老安排的是筑基期的对手……"

### 奖励

| 类型 | 数值 |
|------|------|
| 灵石 | 120 |
| 修为 | 60 |
| 物品 | 双魂护符·初（`item_dualsoul_talisman_01`） |
| 信任度 | +10（"在你的指挥下赢得了宗门小比"） |
| 觉醒度 | +8（"第一次尝到'反抗'的甜头"） |

### 接受对话

> "（内心低语）'好。听你的。但我……我怕。'"

### 场景流程（onAccept）

- 注册战斗监听器 `"ds_02_boss"`——这个 Boss 代表小比中明长老安排的对手
- 战斗中双魂出战标志激活——穿越者提供战术指挥，念安提供修为输出

### 完成对话

> "'赢了？我赢了？！那个声音——是你的功劳。谢谢。'"

### 完成文本

> 胜利的滋味是甜的。念安第一次知道——反抗并不总是带来惩罚。

### 失败条件

| 条件 | 惩罚 |
|------|------|
| 输掉小比 | 信任度 -5，觉醒度 -3 |
| 重试 | 可重试 |
| 失败回调 | AddTrust(-5, "输掉小比，念安更加退缩") / AddAwakening(-3, "反抗失败加重自我怀疑") |

---

## 任务3：反击·真相在眼前

| 字段 | 值 |
|------|-----|
| **ID** | `ds_03` |
| **章节** | 反击 |
| **标题** | 反击·真相在眼前 |
| **类型** | Talk（对话 / 收集证据） |
| **GiverNPC** | 命运（自动激活） |
| **目标** | 收集3件证据并在宗门会审上揭发明长老 |
| **完成条件** | `evidenceCollected >= 3 && elderExposed == true` |
| **nextQuestId** | `ds_04` |

### 任务结构

| QuestData 字段 | 值 | DualSoulQuest 扩展字段 |
|---|---|---|
| id | ds_03 | chapter = "反击" |
| title | 反击·真相在眼前 | shortObjective = "收集3件证据并在宗门会审上揭发明长老" |
| type | QuestType.Talk | trigger = { type: Auto, requiredTrust: 20 } |
| description | "明长老再次设局陷害你——这次是栽赃你偷了宗门秘典。但你早已准备好了证据。在所有人面前——揭穿他。" | completionCheck → ctx.evidenceCollected >= 3 && ctx.elderExposed |
| giverNpcId | ""（自动触发） | onAccept → ctx.RegisterCollectListener("item_evidence_01", "item_evidence_02", "item_evidence_03") |
| giverName | "命运" | onComplete → AddTrust(+12) AddAwakening(+15) |
| targetId | "" | — |
| targetCount | 3（证据） | — |
| rewardSpiritStones | 200 | — |
| rewardCultivation | 80 | — |
| rewardItemId | "item_evidence_scroll" | — |

### 触发条件

- **类型：** 自动触发（Auto）
- **前置要求：** 信任度 >= 20
- **逻辑：** 当信任度达到 20 时，明长老的栽赃事件自动触发

### 收集的证据清单

| 证据ID | 名称 | 获取方式 |
|--------|------|----------|
| `item_evidence_01` | 明长老通敌信件 | 在明长老房间书桌找到 |
| `item_evidence_02` | 被盗丹药清单 | 从林师妹处获得（或交易） |
| `item_evidence_03` | 栽赃秘典 | 明长老放置处反向追踪 |

### 奖励

| 类型 | 数值 |
|------|------|
| 灵石 | 200 |
| 修为 | 80 |
| 物品 | 真相卷轴（`item_evidence_scroll`） |
| 信任度 | +12（"在所有人面前揭穿了陷害者"） |
| 觉醒度 | +15（"宗门震惊——念安不再是任人宰割的懦夫"） |
| 复仇名单 | 明长老从灰变红——记录 "多次诬陷、抢夺丹药、栽赃秘典" |

### 接受对话

> "（坚定）'够了。这次——我不躲了。'"

### 场景流程（onAccept）

- 注册收集监听器——追踪 `item_evidence_01` / `item_evidence_02` / `item_evidence_03` 三件证据
- 每收集一件：日志输出 `[双魂·证据] 📄 收集证据 (n/3)`
- 三件集齐后，在宗门会审上自动触发揭穿环节

### 完成对话

> "'你看——所有人的表情。他们不敢相信。不敢相信我会反抗。'"

### 完成文本

> 宗门炸开了锅。那个任人欺负的懦夫——居然在所有人面前揭穿了真相。

### 失败条件

| 条件 | 惩罚 |
|------|------|
| 证据不足无法揭穿 | 觉醒度 -5，明长老怀疑加深 |
| 失败回调 | AddAwakening(-5, "证据不足，陷害成功") |

---

## 任务4：决战·元婴之怒

| 字段 | 值 |
|------|-----|
| **ID** | `ds_04` |
| **章节** | 决战 |
| **标题** | 决战·元婴之怒 |
| **类型** | Boss（BOSS战） |
| **GiverNPC** | 天玄子（`npc_tian_xuanzi`） |
| **目标** | 以元婴全力击败天玄子派来的大弟子 |
| **完成条件** | `bossDefeated == true` |
| **nextQuestId** | `ds_05` |

### 任务结构

| QuestData 字段 | 值 | DualSoulQuest 扩展字段 |
|---|---|---|
| id | ds_04 | chapter = "决战" |
| title | 决战·元婴之怒 | shortObjective = "以元婴全力击败天玄子派来的大弟子" |
| type | QuestType.Boss | trigger = { type: NpcTalk, npcId: "npc_tian_xuanzi" } |
| description | "天玄子（师父）察觉到了你的变化。他派出了大弟子——元婴中期修为——来'清理门户'。这一次，你不再隐藏真正的实力。" | completionCheck → ctx.bossDefeated |
| giverNpcId | "npc_tian_xuanzi" | onAccept → ctx.RegisterCombatListener("boss_senior_001") + EventBus.Publish("OnDualSoulFullPower") |
| giverName | "天玄子" | onComplete → AddTrust(+15) AddAwakening(+20) + 天地异变事件 |
| targetId | "boss_senior_001" | — |
| targetCount | 1 | — |
| rewardSpiritStones | 500 | — |
| rewardCultivation | 200 | — |
| rewardItemId | "item_dualsoul_awaken_core" | — |

### 触发条件

- **类型：** NpcTalk（与天玄子对话触发）
- **前置要求：** 无额外信任度/觉醒度门槛（承接任务3自然解锁）

### BOSS信息

| 字段 | 值 |
|------|-----|
| ID | `boss_senior_001` |
| 身份 | 天玄子座下大弟子 |
| 修为 | 元婴中期 |
| 特性 | 清理门户——天玄子已察觉念安的变化 |

### 奖励

| 类型 | 数值 |
|------|------|
| 灵石 | 500 |
| 修为 | 200 |
| 物品 | 双魂觉醒核心（`item_dualsoul_awaken_core`） |
| 信任度 | +15（"念安全力出手的震撼"） |
| 觉醒度 | +20（"天地异变——元婴之力的真相"） |

### 接受对话

> "（冷笑）'他们想看看我有多强？好。让他们看。'"

### 场景流程（onAccept）

1. 注册战斗监听器 `"boss_senior_001"`
2. 通知战斗系统开启 **双魂全力模式**（`OnDualSoulFullPower`）
3. 战斗过程中念安不再压制修为，元婴期真实实力爆发

### 完成效果

- 信任度 +15（念安全力出手的震撼）
- 觉醒度 +20
- **天地异变：** 发送 `OnWorldEvent { eventId: "world_sky_change", intensity: "high" }`
- 全宗门震动——天象变色，元婴之威笼罩

### 完成对话

> "'这就是……我的力量？不——这是我们的力量。'"

### 完成文本

> 天象变色。元婴之威震动整个宗门。所有人——都在颤抖。

### 失败条件

| 条件 | 惩罚 |
|------|------|
| 战败 | 信任 -10，觉醒 -5 |
| 重试 | 24 小时后可重试 |
| 失败回调 | AddTrust(-10, "全力出手却战败") / AddAwakening(-5, "'我果然还是不够强……'") |

---

## 任务5：真相·二十年骗局

| 字段 | 值 |
|------|-----|
| **ID** | `ds_05` |
| **章节** | 真相 |
| **标题** | 真相·二十年骗局 |
| **类型** | Talk（对话·最终对峙） |
| **GiverNPC** | 天玄子（`npc_tian_xuanzi`） |
| **目标** | 进入密室，面对师父，做出最终选择 |
| **完成条件** | `truthRevealed == true && finalChoiceMade == true` |
| **nextQuestId** | ""（终点） |

### 任务结构

| QuestData 字段 | 值 | DualSoulQuest 扩展字段 |
|---|---|---|
| id | ds_05 | chapter = "真相" |
| title | 真相·二十年骗局 | shortObjective = "进入密室，面对师父，做出最终选择" |
| type | QuestType.Talk | trigger = { type: Auto, requiredAwakening: 40, requiredTrust: 40 } |
| description | "天玄子召你进入他的闭关密室。二十年的疑惑即将揭晓——为什么师父养大你却从不真正教你？为什么所有人都在欺负你而他视而不见？因为你的身体——是万年难遇的'道胎'。他养你——只是为了炼你。" | completionCheck → ctx.truthRevealed && ctx.finalChoiceMade |
| giverNpcId | "npc_tian_xuanzi" | onAccept → EventBus.Publish("OnDualSoulFinalConfrontation") |
| giverName | "天玄子" | onComplete → AddTrust(+20) AddAwakening(+30) + 链完成标记 |
| targetId | "" | — |
| targetCount | 1 | — |
| rewardSpiritStones | 1000 | — |
| rewardCultivation | 500 | — |
| rewardItemId | "item_dualsoul_final_relic" | — |

### 触发条件

- **类型：** 自动触发（Auto）
- **前置要求：**
  - 觉醒度 >= 40
  - 信任度 >= 40
- **触发场景：** 当两个条件都满足时，天玄子召你进入闭关密室

### 最终对峙场景

1. 天玄子转过身来——他看你的眼神不像看弟子，像看一件工具
2. 他知道双魂的存在——"念安——或者说，你体内的那位。你终于来了。"
3. 二十年的真相揭开——"你知道我等了多久吗？二十年。你的道胎……终于成熟了。"
4. 念安不是弟子——他是天玄子准备炼成丹药的道胎容器

### 最终选择（3个结局分支）

> 对外接口：`MakeFinalChoice(int choice)`

| 选项 | 数值 | 描述 |
|------|------|------|
| 0 | 灵魂融合 | "我们合二为一吧。不分开——也不分彼此。"——灵魂融合路线 |
| 1 | 灵魂分离 | "给我一具肉身。我们各自作为独立的人活下去。"——分离路线 |
| 2 | 继续共生 | "就这样吧。你站在阳光下——我在你心里。永远。"——继续共生路线 |

### 奖励

| 类型 | 数值 |
|------|------|
| 灵石 | 1000 |
| 修为 | 500 |
| 物品 | 双魂最终遗物（`item_dualsoul_final_relic`） |
| 信任度 | +20（"共同面对师父——灵魂真正的战友"） |
| 觉醒度 | +30（"二十年的骗局——全部揭穿"） |

### 接受对话

> "（平静）'走吧。到该去的地方去。'"

### 完成对话

> "'二十年——原来我从来不是他弟子。我是他的丹药。但你——你是真的。只有你是真的。'"

### 完成文本

> 真相大白。道胎的秘密。二十年的骗局。以及——一个新世界的开始。

### 链完成效果

- `chainCompleted = true`
- 全服公告：双魂一体主线全通关
- 触发 `OnDualSoulFinale` 事件——传递最终信任度和觉醒度
- 解锁终极能力/结局分支

### 失败条件

| 条件 | 惩罚 |
|------|------|
| 师父发现双魂存在且出手剥离 | 触发 Bad Ending |
| 抵抗条件 | 觉醒度 >= 60 可抵抗剥离 |
| 失败回调 | 日志输出 "BAD ENDING——师父剥离了穿越者的灵魂。念安回到了孤独……" |
| 事件触发 | `OnDualSoulBadEnding` |

---

## 奖励汇总

| 任务 | ID | 灵石 | 修为 | 物品 | 信任度 | 觉醒度 |
|------|----|------|------|------|--------|--------|
| 觉醒·第一声低语 | ds_01 | 50 | 20 | — | +8 | +5 |
| 试探·第一次出手 | ds_02 | 120 | 60 | 双魂护符·初 | +10 | +8 |
| 反击·真相在眼前 | ds_03 | 200 | 80 | 真相卷轴 | +12 | +15 |
| 决战·元婴之怒 | ds_04 | 500 | 200 | 双魂觉醒核心 | +15 | +20 |
| 真相·二十年骗局 | ds_05 | 1000 | 500 | 双魂最终遗物 | +20 | +30 |
| **合计** | | **1870** | **860** | 4件 | **+65** | **+78** |

> 注：信任度和觉醒度不通过 QuestData.reward 字段发放，而是由 `DualSoulQuestChain` 在各任务的 `onComplete` 回调中调用 `DualSoulManager.AddTrust()` 和 `DualSoulManager.AddAwakening()` 直接注入。

---

## 触发条件矩阵

| 任务 | 触发类型 | 对话NPC | 信任度门槛 | 觉醒度门槛 | 延迟 |
|------|----------|---------|-----------|-----------|------|
| ds_01 | Auto | — | 0 | 0 | 5s |
| ds_02 | NpcTalk | npc_lin_meng (林梦) | 0 | 0 | — |
| ds_03 | Auto | — | 20 | 0 | — |
| ds_04 | NpcTalk | npc_tian_xuanzi (天玄子) | 0 | 0 | — |
| ds_05 | Auto | — | 40 | 40 | — |

---

## 完成条件（运行时上下文）

| 任务 | 上下文条件 | 说明 |
|------|-----------|------|
| ds_01 | `littleSisterStolen && firstTalkDone` | 目睹偷丹 + 第一次灵魂对话 |
| ds_02 | `tournamentWon` | 小比战胜筑基对手 |
| ds_03 | `evidenceCollected >= 3 && elderExposed` | 集齐3证据 + 会审揭发 |
| ds_04 | `bossDefeated` | 击败大弟子 boss_senior_001 |
| ds_05 | `truthRevealed && finalChoiceMade` | 进入密室 + 做出最终选择 |

---

## NPC 映射表

| 叙事角色 | 代码ID | 备注 |
|----------|--------|------|
| 林梦（林师妹/小师妹） | `npc_lin_meng` | 初始偷丹药者，触发 ds_02 的关键NPC |
| 明长老 | `npc_ming_zhanglao` | 反派——多次陷害主角，ds_03 被揭穿 |
| 天玄子（师父） | `npc_tian_xuanzi` | PUA 念安二十年的幕后黑手，最终 BOSS |
| 见证长老（中立） | `npc_witness_elder` | 宗门会审的中立见证者 |
| 念安（原主人） | — | 宿主·元婴期修为·性格懦弱·双魂共生 |
| 大弟子（元婴中期） | `boss_senior_001` | ds_04 的 BOSS 战目标 |

---

## 双轨系统（信任度 + 觉醒度）

- **信任度（Trust）**：原主人念安对穿越者的信任程度。0-100。初始值 5。
  - >= 16：可主动对话（按 T 键）
  - >= 20：触发 ds_03 自动激活
  - >= 40：触发 ds_05 自动激活（与觉醒度共同判定）
  - >= 50：念安开始真正信任你
  
- **觉醒度（Awakening）**：念安对世界真相的认知程度。0-100。初始值 0。
  - >= 21：念安开始怀疑师父的话
  - >= 40：触发 ds_05 自动激活（与信任度共同判定）
  - >= 60：可抵抗 Bad Ending（师父剥离灵魂）
  - >= 61：念安正在经历心理破碎——重建世界观
  - >= 100：念安全觉醒——道胎真相震动天元宗

- **同步率（Sync Rate）**：`(trust + awakening) * 0.5`
  - >= 90：双魂合一
  - >= 70：高度同步
  - >= 40：半同步
  - < 40：各自为战

---

## 复仇名单

系统自动记录所有欺负过原主人的人。每个条目包含：

| 字段 | 说明 |
|------|------|
| targetName | 目标姓名 |
| crime | 罪行描述 |
| activated | 原主人觉醒后激活（awakening >= 21） |
| avenged | 是否已清算 |
| manual | 是否穿越者手动标记 |

任务3完成后明长老自动加入复仇名单：
- **目标：** 明长老
- **罪行：** "多次诬陷、抢夺丹药、栽赃秘典"
- **状态：** 觉醒后自动激活

清算复仇条目可获得：信任度 +5，觉醒度 +3。

---

## 调试命令

Unity Inspector 中可通过右键 `DualSoulQuestChain` 组件调用：

| 命令 | 功能 |
|------|------|
| Force Start Quest Chain | 强制启动双魂任务链 |
| Force Complete Current Quest | 强制完成当前任务 |
| Print Chain Status | 打印任务链状态（当前任务/信任度/觉醒度/同步率/复仇条目） |

---

## 关键事件总线事件

| 事件 | 发送时机 | 负载 |
|------|---------|------|
| OnDualSoulQuestStarted | 每个任务启动时 | questId, title, chapter |
| OnDualSoulQuestCompleted | 每个任务完成时 | questId, chapter |
| OnDualSoulQuestFailed | 任务失败时 | questId, chapter |
| OnDualSoulFullPower | ds_04 接受时（开启全力模式） | — |
| OnWorldEvent | ds_04 完成时（天地异变） | eventId, intensity |
| OnDualSoulFinalConfrontation | ds_05 接受时（锁定入口） | — |
| OnDualSoulFinale | ds_05 完成时（终极能力解锁） | trust, awakening |
| OnDualSoulBadEnding | ds_05 失败时（Bad Ending） | — |
| OnDualSoulAwakened | 觉醒度 100 时 | — |

---

## 代码参考

- `DualSoulQuestChain.cs` → `/Assets/Scripts/Core/DualSoulQuestChain.cs`（任务定义 + 运行时管理）
- `DualSoulManager.cs` → `/Assets/Scripts/Core/DualSoulManager.cs`（信任度/觉醒度/复仇系统）
- `QuestSystem.cs` → `/Assets/Scripts/Framework/QuestSystem.cs`（QuestData 基础结构）
