# Blender → Unity 模型导出操作指南

> 适用人群：非技术背景 PM / 设计师 / 策划  
> 前置条件：已安装 Blender (推荐 4.0+) 和 Unity 2022 LTS 或更新版本  
> 最后更新：2026-07-11

---

## 目录

1. [Blender 导出 FBX 标准流程](#1-blender-导出-fbx-标准流程)
2. [Unity 导入 FBX 设置详解](#2-unity-导入-fbx-设置详解)
3. [修改 Mixamo 模型（改色 / 加服饰 / 换配件）](#3-修改-mixamo-模型改色--加服饰--换配件)
4. [替换 CharacterBuilder 中的 Primitive 模型](#4-替换-characterbuilder-中的-primitive-模型)
5. [常见问题排查](#5-常见问题排查)

---

## 1. Blender 导出 FBX 标准流程

### 1.1 准备工作

**建议在开始建模或下载模型前就设置好单位，避免后续缩放麻烦。**

| 步骤 | 操作 | 截图参考 |
|------|------|----------|
| 1 | 打开 Blender，新建项目（File → New → General） | *左上角菜单栏，选中 General 的场景模板* |
| 2 | 切换到 **Scene Properties** 面板（右侧属性栏，小圆圈图标） | *属性栏图标排列中第4个，鼠标悬停显示"Scene"* |
| 3 | 找到 **Units** 部分，将 **Unit System** 设为 `Metric`，**Unit Scale** 保持 `1.000` | *Units 下拉菜单，选择 Metric* |
| 4 | **Length** 选择 `Centimeters` | *Blender 默认是 Meters，Unity 用 1 unit = 1m，但人物模型建议以 cm 为单位建模更方便* |

> **为什么强调单位？**  
> - Blender 默认 1 unit = 1 米  
> - Unity 默认 1 unit = 1 米  
> - 如果模型在 Blender 中是 1.8（对应 1.8 米 = 180cm），导入 Unity 设置 Scale Factor = 1，模型就是 1.8 Unity units  
> - **常见问题**：很多人导出后发现模型在 Unity 里巨大或巨小，90% 是单位/缩放设置不一致

### 1.2 模型整理（导出前最重要的一步）

**这一步决定了导出的 FBX 在 Unity 中是否好用。**

```
[Blender 场景检查清单]
□ 所有模型是否在 1 个 Collection 内？（便于管理）
□ 多余的空物体（Empty）、辅助线是否已删除？
□ 是否有未应用（Apply）的旋转/缩放/位置？
□ 模型的面朝向（Normal）是否正确？
□ 材质命名是否有意义？
□ UV 是否已展开？
```

#### 1.2.1 应用变换（Apply Transform）—— 必做

如果对模型做过缩放（Scale）、旋转（Rotation）、移动（Location），**导出前必须 Apply**，否则 Unity 里会乱。

```
操作步骤：
1. 选中模型 → 按 Ctrl+A
2. 在弹出的菜单中选 "All Transforms"
```

> 【截图描述】*Ctrl+A 后弹出菜单，包含 Location、Rotation、Scale、All Transforms 四个选项，光标悬停在 All Transforms 上*

**如何检查是否已经 Apply？**  
选中模型，按 `N` 打开侧边栏（Sidebar），看 **Transform** 面板中的数值：
- Location: 最好是 `0, 0, 0` 或偏移量较小
- Rotation: 最好是 `0°, 0°, 0°`
- Scale: **必须是 `1.000, 1.000, 1.000`**

**【特别注意】Scale 没有 Apply**  
如果 Scale 显示是 `0.5, 0.5, 0.5` 或 `2.0, 2.0, 2.0` 等，不 Apply 直接导出，Unity 里模型的缩放就是错乱的，后续调骨骼、调碰撞体都会出问题。

#### 1.2.2 清理网格数据

```
1. 进入 Edit Mode（按 Tab）
2. 按 A 全选所有顶点
3. 按 M → Merge By Distance（合并重叠顶点）
4. 上方菜单 Mesh → Clean Up → Degenerate Dissolve（清理退化面）
5. 检查法线：Mesh → Normals → Recalculate Outside（或 Shift+N）
```

#### 1.2.3 材质命名

**在 Unity 中是通过材质名称来匹配的**，建议提前命名好：

```
1. 选中模型 → 进入 Shader Editor 工作区
2. 每个 Material 节点，双击名称改成有意义的名字
   好例子：Char_Body_Main、Char_Armor_Shoulder
   坏例子：Material.001、Material.002
```

### 1.3 导出 FBX —— 分场景操作

Blender 默认主菜单选 **File → Export → FBX (.fbx)**。

#### 场景 A：导出静态模型（建筑、道具、场景物件）

```
┌─────────────────────────────────────────────┐
│ FBX 导出设置（静态模型）                      │
├─────────────────────────────────────────────┤
│ Include:                                    │
│   ☑ Selected Objects（仅选中的物体）          │
│   ☐ Object Types > Empty（取消勾选）          │
│   ☐ Custom Properties（一般不需要）            │
│                                             │
│ Transform:                                  │
│   Scale: 1.000                              │
│   ☑ Apply Scalings: All Local               │
│   Forward: -Z Forward                       │
│   Up: Y Up                                  │
│   ☑ Apply Unit                               │
│   ☐ Use Space Transform                      │
│                                             │
│ Geometry:                                   │
│   ☐ Apply Modifiers（如果已应用就勾选）        │
│   ☑ Triangulate Faces（建议勾选）             │
│   ☐ Use Mesh Edge Attributes                 │
│   ☐ Use Mesh Vertex Crease                   │
│                                             │
│ Armature: （不涉及，不用管）                   │
│                                             │
│ Animation: （不涉及，不用管）                  │
└─────────────────────────────────────────────┘
```

> 【截图描述】*Blender FBX 导出面板，右侧包含 Include / Transform / Geometry 三个折叠区，Transform 区的 Scale:1.0、Apply Scalings、Forward/Up 已按上述设置*

#### 场景 B：导出带骨骼的动画模型（人物、怪物）

```
┌─────────────────────────────────────────────┐
│ FBX 导出设置（带动画角色模型）                │
├─────────────────────────────────────────────┤
│ Include:                                    │
│   ☑ Selected Objects                         │
│   ☐ Leaf Bones（取消勾选，保持骨骼层级完整）    │
│                                             │
│ Transform:                                  │
│   Scale: 1.000                              │
│   ☑ Apply Scalings: All Local               │
│   Forward: -Z Forward                       │
│   Up: Y Up                                  │
│   ☑ Apply Unit                               │
│   ☐ Use Space Transform                      │
│   Path Mode: Copy（如果贴图路径要保留）        │
│                                             │
│ Geometry:                                   │
│   ☑ Apply Modifiers                         │
│   ☑ Triangulate Faces                       │
│   ☐ Loose Edges                              │
│                                             │
│ Armature:                                   │
│   ☑ Export Armatures                         │
│   ☑ Primary Bone Axis: +Y                   │
│   ☑ Secondary Bone Axis: X                  │
│   Armature FBXNode Type: Null                │
│   ☐ Only Deform Bones（建议取消，保留所有骨骼）│
│   ☐ Add Leaf Bones                           │
│                                             │
│ Animation:                                  │
│   ☑ Bake Animation（如果有动画）              │
│   ☑ Key All Bone Properties                  │
│   ☐ NLA Strip Key All                        │
│   ☐ Sampling Rate: 24.0（或 30.0 匹配项目帧率）│
│   ☐ Simplify                                   │
│   ☐  1.0                                       │
│                                             │
│ Experimental:                                │
│   保持默认，不要勾选任何选项                   │
└─────────────────────────────────────────────┘
```

> 【截图描述】*导出面板中展开 Armature 部分，勾选 Export Armatures，Primary Bone Axis 选 +Y，Secondary 选 X*

#### 关于 Forward / Up 设置的说明

| 坐标系 | Forward | Up | 适用 |
|--------|---------|----|------|
| Blender 默认 | -Z | Y | 建模时 |
| Unity 默认 | Z | Y | 游戏中 |
| **推荐导出** | **-Z Forward** | **Y Up** | **最常用** |

- `-Z Forward, Y Up`：模型正面朝向 Unity 的 +Z 方向（即蓝色轴朝向摄像机）
- 这是业界的标准设置，**绝大多数引擎（Unity、Unreal、Godot）都使用这个约定**

#### 路径模式（Path Mode）选择

| 选项 | 作用 | 推荐场景 |
|------|------|----------|
| Auto | Blender 自动判断 | 个人项目 |
| Copy | 把贴图复制到 FBX 旁 | 团队协作，需要传给他人 |
| Strip | 清除路径，仅保留文件名 | 导入 Unity 后 Unity 自己管理贴图 |

**推荐使用 Copy**，这样导出的 .fbx 旁边会有一个 Textures 文件夹，贴图不会丢失。

---

## 2. Unity 导入 FBX 设置详解

### 2.1 导入 FBX 到 Unity

```
1. 打开 Unity 项目
2. 在 Project 窗口中，找到你的 Assets 文件夹（或你想放模型的子文件夹）
3. 将 .fbx 文件直接从文件管理器拖入 Project 窗口
   （或右键 Project 面板 → Import New Asset... → 选择 .fbx 文件）
4. 如果使用了 Path Mode = Copy，贴图会自动关联
```

> 【截图描述】*Unity Project 窗口，一个 .fbx 文件刚拖入后的状态，显示模型图标和一个三角形展开箭头*

### 2.2 FBX 导入设置（选中 .fbx 文件后在 Inspector 面板操作）

Unity 选中 FBX 后，Inspector 会显示多个标签页，逐项设置：

---

#### 2.2.1 Model 标签页

> 【截图描述】*Inspector 面板顶部的 Model 标签卡，显示以下设置项*

```
┌──────────────────────────────────────────────┐
│ Model │ Rig │ Animation │ Materials │         │
└──────────────────────────────────────────────┘

Scale:
  Scale Factor: 1          ← 关键！如果模型在 Blender 里以厘米建模，设为 0.01
  ☑ Convert Units          ← 始终勾选，让 Unity 自动处理单位转换
  ☐ Bake Axis Conversion   ← 通常不勾选

Mesh Compression: Off
☑ Read/Write Enabled      ← 如果需要运行时修改网格数据（如 Mesh Collider）则勾选
☐ Optimize Mesh           ← 建议勾选，减少 GPU 开销
☐ Generate Colliders      ← 静态物体可勾选，角色不需要

Generate Lightmap UVs:    ← 如果模型需要烘焙光照贴图则勾选
  ☐ | 点击右侧按钮展开详细设置
```

##### Scale Factor 究竟设多少？

| 你在 Blender 中的建模方式 | Scale Factor | 说明 |
|---------------------------|-------------|------|
| 1 Blender unit = 1 米，模型大小合理 | 1 | 模型以米为单位 |
| 1 Blender unit = 1 厘米，人物约 180 单位高 | **0.01** | 模型以厘米为单位 |
| 不确定，但导入 Unity 后模型巨大 | 尝试 0.01 | 然后看效果 |
| 不确定，但导入 Unity 后模型极小 | 尝试 100 | 然后看效果 |

**快速验证方法：**  
导入后在 Scene 中创建一个 `Cube`（默认 1m³），和你的模型对比大小。如果人物模型和 Cube 差不多高（约 1.7-1.8m），Scale 就是对的。

---

#### 2.2.2 Rig 标签页（带动画的模型）

> 【截图描述】*Inspector → Rig 标签，Animation Type 下拉菜单处于打开状态*

```
Animation Type:
  ┌─────────────────────────────────────────────┐
  │ None          ← 静态模型（无骨骼）           │
  │ Humanoid      ← 人形角色（建议选这个）       │
  │ Generic       ← 非人形（四足/怪物/机械）     │
  │ Legacy        ← 旧版动画系统（不推荐）       │
  └─────────────────────────────────────────────┘
```

##### Humanoid vs Generic 选择建议

| 角色类型 | 推荐 Animation Type | 原因 |
|----------|-------------------|------|
| 标准人形角色（双手双脚） | **Humanoid** | 可用 Unity 的 Avatar 系统，动画重定向，IK |
| Mixamo 下载的角色 | **Humanoid** | Mixamo 骨骼符合 Humanoid 标准 |
| 四足动物、怪物 | **Generic** | 骨骼不匹配 Humanoid 模板 |
| 机械、道具 | **None** | 没有骨骼 |

##### Avatar 配置（仅 Humanoid）

```
设定 Animation Type = Humanoid 后：

1. 点击下方的 "Configure..." 按钮
2. Unity 会自动尝试映射骨骼：
   - 绿色 = 已正确映射
   - 黄色 = 可能有问题，需要确认
   - 红色 = 未映射，必须手动指定
3. 检查关键骨骼：
   □ Hips（盆骨）        □ Spine（脊柱）
   □ Chest（胸部）       □ Neck（脖子）
   □ Head（头部）         □ Left/Right Upper Arm（上臂）
   □ Left/Right Lower Arm（前臂）  □ Left/Right Hand（手）
   □ Left/Right Upper Leg（大腿）  □ Left/Right Lower Leg（小腿）
   □ Left/Right Foot（脚）
4. 确认无误后点 "Apply"
5. 再点 "Done" 关闭配置窗口
```

> 【截图描述】*Avatar Configuration 窗口，左侧显示骨骼映射列表，右侧 3D 视口中彩色骨骼高亮*

**常见问题：Mixamo 模型的脚骨骼（Foot）容易映射错误**  
如果脚部动画异常（脚掌翻转），在 Avatar Config 中手动将 Left Foot / Right Foot 指定到正确的骨骼。

---

#### 2.2.3 Animation 标签页（如果有动画）

```
┌──────────────────────────────────────────────┐
│ Model │ Rig │ Animation │ Materials │         │
└──────────────────────────────────────────────┘

Import Animation: ☑（如果有动画才勾选）

Bake Animations:
  ☐ 一般不需要勾选

Resample Curves: ☑

Anim. Compression:
  ┌──────────────────────────────────────────────┐
  │ Off             ← 质量最高，文件最大         │
  │ Keyframe Reduction  ← 推荐，平衡质量和大小    │
  │ Optimal         ← 压缩最狠，可能丢细节       │
  └──────────────────────────────────────────────┘

  Keyframe Reduction 的默认值：
    Rotation Error: 0.5
    Position Error: 0.5
    Scale Error: 0.5

Clips（动画片段分割）:
  如果 FBX 包含多个动画（如 Idle、Run、Jump 在一个文件中），
  在此处添加 Animation Clip 并设置 Start / End 帧：

  [+]
  ├─ idle    (Start: 0, End: 60)
  ├─ run     (Start: 61, End: 120)
  └─ jump    (Start: 121, End: 150)
```

**如何知道动画帧范围？**  
在 Blender 的时间线（Timeline）窗口底部，可以看到当前动画的总帧数。拖动播放头查看不同动作对应的帧区间。

---

#### 2.2.4 Materials 标签页

> 【截图描述】*Inspector → Materials 标签，展示以下设置*

```
Location:
  ┌───────────────────────────────────────────────┐
  │ Use Embedded Materials ← 贴图嵌入 FBX（推荐） │
  │ Use External Materials  ← 在 FBX 旁生成 .mat │
  │                      ← 文件（团队项目推荐）   │
  └───────────────────────────────────────────────┘

推荐选择 "Use Embedded Materials" 或 "Use External Materials"：
- 个人项目 / 快速原型 → Embedded
- 团队协作 / Git 版本管理 → External（方便材质独立修改）

☐ Naming: By Material Name（建议保持）
```

**导入后点击底部的 "Apply" 按钮**，否则设置不会生效。

---

### 2.3 导入后的检查清单

```
□ 模型在 Scene 中显示正常（无破损面、无闪烁）
□ 材质/贴图已正确显示（不是粉红色 Missing）
□ 缩放比例合适（和 Cube 对比）
□ 骨骼绑定正确（选中模型可看到骨骼线）
□ 动画播放正常（拖入 Animator Controller 测试）
```

---

## 3. 修改 Mixamo 模型（改色 / 加服饰 / 换配件）

### 3.1 从 Mixamo 下载模型的推荐设置

```
Mixamo 下载页面设置：
  Format: FBX Binary (.fbx)
  FPS: 30
  Skin: Without Skin（如果你想保留 Mixamo 的默认贴图）
    或 With Skin（如果需要完整的材质和贴图）
```

> 【截图描述】*Mixamo 网站下载弹窗，Format 选择 FBX Binary，FPS 选择 30，下方有 Download 按钮*

### 3.2 在 Blender 中修改 Mixamo 模型

#### 3.2.1 准备工作：导入 Mixamo 模型

```
1. File → Import → FBX
2. 选择从 Mixamo 下载的 .fbx 文件
3. 保持导入设置默认即可
```

**导入后你会看到：**
- 一个带骨骼的完整角色
- 材质是 Mixamo 默认的（通常是一个基本色贴图）
- 骨骼名称符合 Mixamo 命名规则（mixamorig:Hips, mixamorig:Spine 等）

#### 3.2.2 改色（修改材质颜色）

**方法一：修改 Base Color（最快速）**

```
1. 选中模型 → 切换到 Shader Editor 工作区
2. 你会看到 Principled BSDF 节点
3. 找到 Base Color 输入口：
   - 如果是纯色，直接点击颜色块修改
   - 如果连了一个 Image Texture 节点，说明用了贴图
     a. 在 Image Texture 和 Principled BSDF 之间加一个 ColorRamp 或 Hue/Saturation 节点
     b. 或者直接替换 Image Texture 的贴图文件
4. 改完后不要保存贴图，只是修改材质，导出时选材质的最终颜色即可
```

> 【截图描述】*Shader Editor 中显示 Principled BSDF 节点，Base Color 连了 Image Texture，中间插入一个 Hue/Saturation 节点*

**方法二：新建材质（更干净）**

```
1. 选中模型 → 进入 Shader Editor
2. 点击 "New" 创建一个新材质
3. 重新连接贴图或设置颜色
4. 给材质命名（如 Char_Red_Variant）
```

#### 3.2.3 增加服饰（添加外部模型）

**场景**：你下载了一件外套模型（.fbx 或 .obj），想穿在 Mixamo 角色身上。

```
步骤：
1. File → Import → 导入服饰模型
2. 将服饰模型移动到角色身上：
   - 用移动（G键）、旋转（R键）、缩放（S键）调整位置
   - 可以从不同视角查看对齐情况（按小键盘 1=正面，3=右面，7=顶部）
3. 服饰默认是独立物体，需要绑定到角色的骨骼上：
```

**服饰绑定到骨骼的两种方法：**

**方法 A：使用 Data Transfer 修改器（推荐新手）**

```
1. 选中服饰 → 加一个 Data Transfer 修改器
2. Source: 选择角色身体模型
3. Vertex Data → Vertex Group: ☑
4. 勾选 "Auto Transform" 或手动选 Mapping
5. 点 "Generate Data Layers"
6. 再勾选一下 "Face Corner Data: Custom Normals" 让法线一致
7. 但服饰此时还是独立的——要让它随骨骼动，需要用下面的方法
```

**方法 B：权重传递（更正确的方法）**

```
1. 打开 Blender 的权重绘制工作区（Weight Paint）
2. 先选择角色身体 → 按住 Shift 再选服饰 → Ctrl+P → With Automatic Weights
3. 这个操作会把角色的骨骼和权重信息传给服饰
4. 如果要精细调整权重，进入 Weight Paint 模式：
   - 红色区域 = 完全受当前骨骼影响
   - 蓝色区域 = 不受影响
   - 用画笔工具调整
```

> 【截图描述】*Weight Paint 模式，角色手臂部分显示红色渐变到蓝色，表示骨骼权重过渡*

**方法 C：直接附着（无骨骼服饰）**

对于不动的配件（帽子、眼镜、腰带等），可以把它们设为角色的子物体：

```
1. 选中配件
2. 按住 Shift 选中角色的某根骨骼（如 Head 骨骼）
3. Ctrl+P → Bone
4. 配件会成为该骨骼的子物体，随骨骼运动
```

#### 3.2.4 换配件

```
1. 选中原来的配件 → X → Delete
2. 导入新配件模型
3. 用 Ctrl+P → Bone 绑定到对应骨骼
4. 调整位置/旋转/缩放
5. 如果配件需要权重，参考上面的权重传递方法
```

### 3.3 修改后的导出设置

```
和前面 1.3 场景 B 一致：
- Forward: -Z Forward
- Up: Y Up
- ☑ Apply Scalings: All Local
- ☑ Apply Unit
- ☑ Apply Modifiers
- ☑ Triangulate Faces
- ☑ Export Armatures
- ☐ Only Deform Bones（取消勾选）
- 如果修改了材质且想保留材质颜色，确保材质已保存
```

> **注意：** 如果服饰修改器使用了 Armature Modifier，导出时记得勾选 Apply Modifiers，或者在导出前直接 Apply 掉修改器。

---

## 4. 替换 CharacterBuilder 中的 Primitive 模型

### 4.1 理解 CharacterBuilder 的工作方式

CharacterBuilder 目前使用 Primitive（基本几何体）作为占位角色。你需要：

```
当前状态：
  ┌─ Player ── Capsule（胶囊体，Primitive）
  └─ Enemy  ── Cube（立方体，Primitive）

目标状态：
  ┌─ Player ── 你的 FBX 模型（带骨骼/动画）
  └─ Enemy  ── 另一个 FBX 模型
```

### 4.2 操作步骤

#### Step 1: 确认模型在 Unity 中导入正确

按照第 2 章的流程导入 FBX，确保在 Scene 中预览正常。

#### Step 2: 找到 CharacterBuilder 的 Prefab/脚本设置

**典型路径（根据你的项目结构可能不同）：**

```
Assets/
├── Scripts/
│   ├── CharacterBuilder.cs
│   └── ...
├── Prefabs/
│   ├── Player.prefab    ← Player 预设体
│   └── Enemy.prefab     ← Enemy 预设体
└── Models/
    ├── YourModel.fbx    ← 刚导入的模型
    └── ...
```

#### Step 3: 在 Prefab 中替换模型

**方法 A：直接替换 Prefab 的 Mesh（最简单）**

```
1. 在 Project 面板找到 Player.prefab
2. 双击打开 Prefab 编辑模式
3. 在 Hierarchy 中找到当前的 Primitive 子物体（如 Capsule）
4. 在 Inspector 中找到 Mesh Filter 组件
5. 将 Mesh 从 Capsule 替换为你的模型网格：
   - 点击 Mesh 右侧的小圆点
   - 在弹出的 Select Mesh 窗口中搜索你的模型名
   - 选中后，Mesh 自动替换
6. 同理，替换 Mesh Renderer 中的材质
7. 点击 Prefab 编辑器的 "Save" 按钮

或者更彻底的方法：
4. 直接删除 Capsule 子物体
5. 右键 Hierarchy → 3D Object → 或者从 Project 直接把你的 FBX 拖入 Hierarchy
6. 调整位置到 Capsule 原来的位置（Position 复位到 0,0,0）
7. 在 Inspector 最上方点 "Overrides" → "Apply All"
```

> 【截图描述】*Prefab 编辑模式，左侧 Hierarchy 中显示 Capsule 被选中，右侧 Inspector 显示 Mesh Filter 组件，Mesh 字段右侧的小圆点高亮*

**方法 B：在 CharacterBuilder.cs 中修改代码**

如果 CharacterBuilder 用代码生成 Primitive，需要修改脚本：

```csharp
// 找到类似这样的代码段（大致结构，实际代码可能不同）：
public class CharacterBuilder : MonoBehaviour
{
    public GameObject characterPrefab;   // 在 Inspector 中拖入你的模型 Prefab
    public RuntimeAnimatorController animController;

    void Start()
    {
        // 旧的 Primitive 方式（注释掉或删除）
        // GameObject prim = GameObject.CreatePrimitive(PrimitiveType.Capsule);

        // 新的方式：实例化你的模型
        GameObject character = Instantiate(characterPrefab, transform.position, transform.rotation);
        character.transform.SetParent(transform);

        // 添加 Animator 组件（如果模型没有自带）
        Animator anim = character.GetComponent<Animator>();
        if (anim == null)
            anim = character.AddComponent<Animator>();

        // 设置 Animator Controller
        anim.runtimeAnimatorController = animController;
    }
}
```

**项目实际情况可能不同，请根据 CharacterBuilder.cs 的具体内容调整。**

#### Step 4: 调整 Animator Controller

```
1. 在 Project 面板找到你的 Animator Controller（通常是 .controller 文件）
2. 双击打开 Animator 窗口
3. 确认你需要的动画状态（Idle, Run, Jump 等）已导入并连接正确
4. 每个动画状态右侧的 Motion 字段应指向从 FBX 中切割出来的 Animation Clip
5. 设置 Entry 节点的默认状态
```

> 【截图描述】*Animator 窗口，显示 Idle 指向 Run 的过渡箭头，Idle 为默认状态（橙色）*

#### Step 5: 场景中测试

```
1. 回到 Scene 视图
2. 如果场景中已有 CharacterBuilder 对象：
   - 选中它 → 在 Inspector 中查看组件
   - 找到 characterPrefab 字段 → 从 Project 拖入你的模型 Prefab
   - 点击 Play 测试
3. 如果还没有 CharacterBuilder 对象：
   - 从 Project 把 Prefab 拖入场景
   - 点击 Play 测试
```

### 4.3 验证清单

```
□ 模型正确显示（没有破损、缺失面）
□ 材质颜色正确
□ 模型位置 / 朝向正常（不是横躺或倒立）
□ 动画正确播放（Idle / Run / Jump）
□ 碰撞体（Collider）和模型匹配（或至少能正常碰撞）
□ 光照表现正常（不是全黑或全亮）
```

#### 如果发现模型朝向不对

```
在 Prefab 上：
1. 选中模型子物体
2. Inspector → Transform
3. Rotation 调整为 (0, 0, 0) 或 (0, 180, 0) 让角色面朝 Z 正方向
4. 或者在导入 FBX 时，在 Model 标签页的 Rotation Offset 中调整
```

#### 如果碰撞体和模型不匹配

```
1. 在模型 Prefab 上添加/修改 Collider 组件
2. 选中 Prefab 中的模型子物体
3. Inspector → Add Component → Physics → Box Collider / Capsule Collider
4. 调整 Center 和 Size 参数使其包裹模型
```

---

## 5. 常见问题排查

### Q1: Unity 导入后模型是粉红色

**原因：** 材质没有找到贴图（Missing Material）。

**解决方法：**
```
1. 选中 FBX → Inspector → Materials 标签
2. 检查 Location 是否选对（推荐 Embedded Materials）
3. 点击 "Extract Textures..." → 选择 Assets 下的文件夹保存贴图
4. 点击 "Extract Materials..." → 同样保存
5. 点击 Apply
```

### Q2: 模型导入后巨大或巨小

**原因：** 单位/缩放不一致。

**解决方法：**
```
1. 选中 FBX → Inspector → Model 标签
2. 调整 Scale Factor：
   - 模型太大 → 设为 0.01（cm→m）
   - 模型太小 → 设为 100（m→cm）
3. 或重新检查 Blender 中是否 Apply Scale
```

### Q3: 动画错乱（手脚扭曲）

**原因：** Avatar 骨骼映射不正确，或动画是 Generic 但设置了 Humanoid（反之亦然）。

**解决方法：**
```
1. 选中 FBX → Inspector → Rig 标签
2. 点击 Configure... 检查骨骼映射
3. 特别注意脚、手指映射是否正确
4. 如果无法正确映射，尝试 Generic 代替 Humanoid
5. 对应地，检查 Animator Controller 中的 Avatar 是否匹配
```

### Q4: 骨骼权重错误（服饰穿透身体或拉伸变形）

**原因：** 权重传递不完整或未正确 Apply。

**解决方法：**
```
1. 在 Blender 中检查服饰的 Vertex Groups
2. 确认和主模型有相同的顶点组名称
3. 在 Weight Paint 模式下手动修补问题区域
4. 重新导出并导入 Unity
```

### Q5: 模型有接缝或黑线

**原因：** 法线问题或 UV 接缝。

**解决方法：**
```
1. Blender 中 Edit Mode → 全选 → Shift+N（Recalculate Normals）
2. 检查是否有重叠顶点：全选 → M → Merge By Distance
3. 导入 Unity 时勾选 Model 标签的 "Generate Lightmap UVs"
```

### Q6: 导入 Unity 后模型没有动画

**原因：** FBX 导出时未勾选 Animation，或 Unity 导入设置中 Import Animation 未勾选。

**解决方法：**
```
1. 在 Blender 重新导出：确认导出面板已勾选 Animation 部分的选项
2. 在 Unity：选中 FBX → Inspector → Animation 标签 → 勾选 Import Animation
```

---

## 附录：推荐工作流程速查

### 快速流程（从头到尾 5 分钟）

```
Blender 侧：
  1. 建模完成 → 检查单位（Metric/cm）
  2. Ctrl+A Apply All Transforms
  3. File → Export → FBX
     - Forward: -Z Forward, Up: Y Up
     - Scale: 1.0
     - 勾选 Apply Unit, Triangulate Faces
     - 有骨骼勾选 Export Armatures

Unity 侧：
  1. 拖入 FBX 到 Project
  2. 选中 FBX → Model → Scale Factor 调好 → Apply
  3. Rig → Animation Type → Humanoid → Configure → Apply
  4. Animation → Import Animation → 切割 Clip → Apply
  5. 将模型拖入场景 → 创建 Animator Controller → 关联动画

CharacterBuilder 替换：
  1. 确认模型导入正常
  2. 替换 Prefab 的 Mesh / 或修改代码的 Instantiate 对象
  3. 关联 Animator Controller
```

### 推荐的 Blender 插件

| 插件名 | 功能 | 建议 |
|--------|------|------|
| **Better FBX Importer/Exporter** | 更稳定的 FBX 导入导出 | 如果默认导出有问题可以试试 |
| **Auto-Rig Pro** | 快速绑骨、重定向动画 | 需要精细控制 Mixamo 权重时推荐 |
| **Mesh Transfer Tool** | 在模型间传递顶点数据 | 修改服饰时很有用 |

---

> **文档维护者：** PM 闫海洋  
> **适用范围：** 供应商系统重构项目 - 模型资产管线  
> **配套文档：** `npc-visual-spec.md`（NPC 视觉效果规范）、`ui-p0-systems.md`（UI 系统）
