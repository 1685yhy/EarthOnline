# 战斗特效 Phase2 实现指南

## 概述

Phase1 完成了弹道、命中、暴击的基础 VFX。Phase2 的目标是增加**境界突破特效**、**低灵力警告**、**道韵暴击**三个子系统。以下是对 VFXManager.cs 的具体修改指南。

---

## 一、需要新增的数据结构

### 1.1 境界配置表（新增类 `RealmConfig`）

在 VFXManager.cs 中新增一个序列化类：

```
RealmConfig
├── realmName: string          // 境界名称：练气/筑基/金丹/元婴
├── breakEffectPrefab: GameObject  // 突破时的主特效预制体
├── bodyGlowMat: Material          // 突破后角色周身光晕材质
├── auraColor: Color               // 周身光颜色 (RGBA)
├── auraParticleCount: int         // 光晕粒子数量
├── auraParticleSpeed: float       // 粒子浮动速度
├── glowIntensity: float           // 光晕强度系数
└── groundCrackRadius: float       // 突破时地面裂纹范围
```

### 1.2 灵力状态配置（新增类 `QiStateConfig`）

```
QiStateConfig
├── stateName: string          // "Normal" / "Warning" / "Critical"
├── threshold: float           // 触发阈值（0.2 或 0.05）
├── pulseInterval: float       // 闪烁间隔秒数
├── pulseColorLow: Color       // 闪烁暗色
├── pulseColorHigh: Color      // 闪烁亮色
├── screenEffectMat: Material  // 屏幕后处理材质
├── vignetteIntensity: float   // 暗角强度
└── audioClip: AudioClip       // 警告音
```

### 1.3 道韵暴击配置（在现有暴击配置中增加字段）

现有 `CritConfig` 中新增：

```
├── isDaoRhythm: bool              // 是否道韵暴击（仅稀有触发时=true）
├── daoInkMat: Material            // 水墨风格命中爆发材质
├── daoInkParticlePrefab: GameObject  // 水墨粒子预制体
├── daoTrailMat: Material          // 弹道水墨轨迹材质
├── daoHoldDuration: float         // 水墨挂机帧停留时长
└── daoInkSplashCount: int         // 墨滴飞溅数量
```

---

## 二、境界突破特效系统

### 2.1 触发入口

在已有 `PlayBreakthroughEffect(Transform target)` 方法中改造逻辑：

```
PlayBreakthroughEffect(Transform target, int realmLevel)
```

- `realmLevel` 映射：1=练气, 2=筑基, 3=金丹, 4=元婴
- 根据 realmLevel 从 RealmConfig 数组中选取对应配置

### 2.2 每个境界的视觉差异

| 境界 | 主色调 | 粒子形态 | 地面裂纹 | 持续时间 |
|------|--------|----------|----------|----------|
| 练气→筑基 | 淡青→青绿 | 螺旋上升光点 | 小型环状裂纹 | 2s |
| 筑基→金丹 | 青绿→金色 | 光柱+环绕符文颗粒 | 中型放射裂纹 | 3s |
| 金丹→元婴 | 金色→紫色 | 三重光环扩散+雷电丝 | 大型龟裂+碎石 | 4s |
| 元婴→化神 | 紫色→七彩 | 天地异象(全屏变色)+龙形虚影 | 大范围地震波纹 | 5s |

### 2.3 具体实现步骤

```
步骤 1：在 VFXManager.Start() 中加载所有 RealmConfig 预制体
步骤 2：PlayBreakthroughEffect 中：
  2a. 瞬间播放 "破" 字 Shader 动画（屏幕上出现对应境界文字）
  2b. 从目标位置发出地面冲击波 ring
  2c. 目标身上逐层出现 aura 粒子（从脚底到头顶扩散）
  2d. 根据 realmLevel 切换角色的 bodyGlowMat（渐变过渡 0.5s）
  2e. 播放对应音效
  2f. 如果是金丹及以上，额外生成雷电粒子环绕
步骤 3：突破完成后：
  3a. aura 粒子持续环绕角色（强度随 realmLevel 递增）
  3b. 角色身上保留 glowing 材质效果（永久，直到下次突破替换）
```

### 2.4 编程注意事项

- 突破特效进行时，应禁用其他 VFX（如弹道），避免视觉冲突
- 地面裂纹使用 Decal Projector 实现，不实际修改地形 Mesh
- 不同境界之间的材质切换使用 Lerp 过渡，不硬切
- 特效池回收——突破完成后地面裂纹 2s 后自动消失

---

## 三、低灵力警告系统

### 3.1 核心逻辑

新增方法 `UpdateQiWarning(float currentQi, float maxQi)`，每帧由战斗管理器调用。

```
qiPercent = currentQi / maxQi

if qiPercent > 0.2f  →  Normal 状态（无特效）
if 0.05f < qiPercent <= 0.2f  →  Warning 状态（阈值1）
if qiPercent <= 0.05f  →  Critical 状态（阈值2）
```

### 3.2 < 20% 警告（Warning 状态）

视觉元素：
- 屏幕边缘出现**淡红色暗角**，强度 0.3
- HUD 灵力条开始**缓慢脉冲闪烁**（周期 0.8s）
- 角色身上粒子偶尔飘散出零星火星（白色半透明）
- 无音效

### 3.3 < 5% 警告（Critical 状态）

视觉元素叠加：
- 暗角加深为**浓红色**，强度 0.7，且快速脉冲（周期 0.3s）
- 灵力条快速闪烁（红白交替）
- 角色身上连续飘散红色粒子
- 屏幕边缘出现**血丝状裂纹**（使用 Screen Space 叠加层）
- 播放紧迫音效（低频心跳声，循环）
- 低灵力时所有技能弹道尾部拖出红色稀薄尾迹

### 3.4 实现步骤

```
步骤 1：新增 UpdateQiWarning() 方法
步骤 2：内部维护一个 qiWarningLevel (0/1/2)
步骤 3：每帧对比 qiPercent：
  3a. 如果 level 变化 → 执行 TransitionToQiState(newLevel)
  3b. TransitionToQiState：
    - 淡入/淡出 screenEffectMat（duration 0.3s）
    - 切换 pulse 协程（旧的 Stop，新的 Start）
    - 音频 fade in/out
步骤 4：Pulse 协程逻辑：
  loop：
    lerp 0→1  pulseInterval 时间内
    lerp 1→0  pulseInterval 时间内
    设置 Material 的 _PulseFactor 属性
```

### 3.5 编程注意事项

- 低灵力特效不能遮挡战斗视野 → 暗角最多覆盖屏幕边缘 15%
- 警告特效的 Update 开销必须低：每帧只做一次百分比计算和阈值比较
- Pulse 协程的 Material 属性修改使用 SharedMaterial 会影响所有实例——必须用 MaterialPropertyBlock
- 从 Warning 回到 Normal 时，过渡时间 0.5s（不要硬切，避免闪烁感）

---

## 四、道韵暴击（Dao Rhythm Crit）

### 4.1 触发逻辑

在现有 `PlayCritEffect()` 中增加稀有判定：

```
// 在已有暴击逻辑中
bool isDaoRhythm = Random.value <= 0.05f;  // 5% 概率触发道韵暴击

if (isDaoRhythm) {
    PlayDaoRhythmEffect(hitPoint, target);
} else {
    // 原有普通暴击逻辑
}
```

### 4.2 视觉设计（水墨风格）

相比普通暴击（明亮斩击+爆发闪光）：

| 元素 | 普通暴击 | 道韵暴击 |
|------|----------|----------|
| 弹道轨迹 | 明亮光尾 | 墨色毛笔笔触，拖尾延迟消失 |
| 命中爆发 | 白色/金色闪光 | 墨汁飞溅，黑色水滴四散 |
| 停留时间 | 0.1s | 0.4s（水墨挂机帧） |
| 屏幕效果 | 轻微震动 | 全屏单帧墨色浸染（水墨晕开） |
| 颜色 | 暖色系 | 纯黑+留白+少量朱红点缀 |
| 形态 | 粒子爆炸 | 墨水笔触+书法文字浮空 |

### 4.3 实现步骤

```
步骤 1：新增 PlayDaoRhythmEffect(Vector3 hitPoint, Transform target)
步骤 2：暂停后续 VFX 0.4s（"挂机帧"概念）：
  2a. Time.timeScale 短暂调整为 0.3（慢动作）
  2b. 或者采用局部暂停——队列中其他特效延迟 0.4s 播放
步骤 3：在 hitPoint 实例化 ink splash 预制体：
  3a. 使用 daoInkMat 渲染
  3b. 生成 splashCount 个墨滴粒子，沿法线方向散射
  3c. 粒子使用 gravity 模拟墨水下坠
步骤 4：屏幕叠加水墨晕染序列帧：
  4a. 全屏 Canvas 层显示一张水墨序列图（4 帧，每帧 0.1s）
  4b. 序列结束后逐渐透明消失（0.2s 淡出）
步骤 5：场景中残留一道墨迹（Decal，2s 后淡出）
步骤 6：恢复 timeScale 或释放队列
```

### 4.4 编程注意事项

- 挂机帧期间，玩家的输入应该仍然有效（只停视觉，不停逻辑）
- 所以**不能使用 Time.timeScale = 0**，应该用专门的 VFX 延迟队列
- 墨迹 Decal 需要贴在地形上，用射线检测获得地面高度
- 墨滴粒子与现有粒子系统共用池，但材质不同
- 如果与其他玩家联网，道韵暴击特效需作为网络事件同步发送

---

## 五、VFXManager.cs 的结构改动总结

### 5.1 新增字段 / Inspector 暴露

```
[Header("=== Phase 2: Realm Breakthrough ===")]
[SerializeField] private RealmConfig[] realmConfigs;        // 4 个境界配置
[SerializeField] private GameObject breakthroughRingPrefab; // 冲击波圆环
[SerializeField] private GameObject groundCrackPrefab;      // 地面裂纹

[Header("=== Phase 2: Low Qi Warning ===")]
[SerializeField] private QiStateConfig qiWarningConfig;     // <20%
[SerializeField] private QiStateConfig qiCriticalConfig;    // <5%
[SerializeField] private MaterialPropertyBlock mpb;         // 材质属性块

[Header("=== Phase 2: Dao Rhythm Crit ===")]
[SerializeField] private Material daoInkMat;
[SerializeField] private GameObject daoInkSplashPrefab;
[SerializeField] private AnimationCurve inkSplashCurve;     // 墨滴运动曲线
[SerializeField] private Sprite[] daoSequenceFrames;         // 水墨晕染序列帧
[SerializeField] private float daoHoldDuration = 0.4f;      // 挂机帧时长
[SerializeField] private float daoRhythmChance = 0.05f;     // 触发概率
```

### 5.2 新增方法清单

```
// 境界突破
public void PlayBreakthroughEffect(Transform target, int realmLevel)
private IEnumerator BreakAuraGrow(Transform target, RealmConfig config)
private IEnumerator GroundCrackExpand(Transform target, RealmConfig config)

// 低灵力
public void UpdateQiWarning(float currentQi, float maxQi)
private void TransitionToQiState(int newLevel)
private IEnumerator PulseEffect(int level)
private void CleanupQiEffects()

// 道韵暴击
private void PlayDaoRhythmEffect(Vector3 hitPoint, Transform target)
private IEnumerator DaoHoldFrame()
private IEnumerator DaoInkSplash(Vector3 origin, Vector3 normal)
private void DaoScreenOverlay()
```

### 5.3 需要修改的既有方法

```
PlayCritEffect()  →  插入道韵判定分支（~5 行改动）
PlayBreakthroughEffect()  →  完全重写（原占位方法替换）
Update()  →  新增 qiWarning 状态机的每帧更新（~10 行新增）
OnDestroy()  →  确保低灵力协程停止 + 材质还原（~5 行新增）
```

### 5.4 资源依赖清单（程序需要向美术/TA 要）

```
1. RealmBreak_Particle_QiLian.prefab    — 练气突破粒子预制体
2. RealmBreak_Particle_ZhuJi.prefab     — 筑基突破粒子预制体
3. RealmBreak_Particle_JinDan.prefab    — 金丹突破粒子预制体
4. RealmBreak_Particle_YuanYing.prefab  — 元婴突破粒子预制体
5. BodyGlow_QiLian.mat ～ BodyGlow_YuanYing.mat — 4 个境界光晕材质
6. QiWarning_ScreenEffect.mat           — 低灵力屏幕特效材质
7. QiCritical_ScreenEffect.mat          — 灵力危急屏幕特效材质
8. DaoInk_Splash.mat + DaoInk_Trail.mat — 水墨材质
9. DaoSequence_Sprites 4 帧              — 水墨晕染序列帧
10. GroundCrack_Decal 预制体            — 地面裂纹贴花
11. Breakthrough_Ring 预制体            — 冲击波圆环
12. Heartbeat_Loop.ogg                  — 灵力危急心跳音效
```

---

## 六、性能预算

| 子系统 | 粒子数上限 | DrawCall | 额外开销 |
|--------|-----------|----------|----------|
| 境界突破（播放时） | 300 | 3 | 2 Decal Projector |
| 低灵力警告 | 50（持续） | 1 | 1 屏幕后处理 Pass |
| 道韵暴击 | 100（爆发） | 2 | 1 Canvas Overlay |
| 合计 | ≤450 | ≤6 | — |

---

## 七、验收标准

程序员写完后，PM/策划检查以下验收点：

1. 练气突破是青色螺旋，元婴突破是七彩天地异象，视觉差异明显
2. 灵力从>20%降到<20%时屏幕边缘渐变为淡红暗角
3. 灵力<5%时心跳音效+血丝裂纹出现
4. 普通暴击打 20 次，至少出现 1 次水墨风格暴击（概率可调 Inspector）
5. 道韵暴击时画面有 0.4s 慢动作感，但不影响技能实际冷却和伤害结算
6. 三个子系统同时播放时（如境界突破中低灵力），优先保证突破特效完整
7. 所有特效开关可控（Inspector 勾选 EnableRealmVFX / EnableQiWarning / EnableDaoRhythm）
