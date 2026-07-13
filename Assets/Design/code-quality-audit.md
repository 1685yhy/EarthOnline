# Code Quality Audit Report — EarthOnline

**Audit date:** 2026-07-13
**Scope:** 165 game scripts under `Assets/Scripts/` (11 subfolders)
**Auditor:** Lead Programmer (CCGS)

---

## Executive Summary

| Category | Severity | Count |
|---|---|---|
| Naming convention violations | HIGH | ~1,100+ lines across ~140 files |
| Missing `[RequireComponent]` | HIGH | 45 files |
| Inconsistent / missing namespace | MEDIUM | 8 legacy `StackingCute` + 5 missing |
| Magic numbers | MEDIUM | ~1,560 lines with hardcoded literals |
| Chinese comments in code | INFO | 147/165 files (89%), 5,815 lines |

The codebase was likely migrated from a prior project ("StackingCute") and has not undergone a systematic cleanup pass. The dominant theme is a consistent but incorrect naming convention for public fields (camelCase instead of PascalCase) and a near-total absence of `[RequireComponent]` declarations.

---

## 1. Naming Convention Violations (HIGH)

**Standard:** C# Unity convention — public fields should be **PascalCase**, private fields should be **_camelCase**.

**Finding:** ~1,100+ violations across approximately 140 of 165 scripts. Nearly every script that exposes serialized or public fields uses **camelCase for public fields** — the pattern that should only be used for private fields.

### Typical violation (from every folder):

```
// PlayerController.cs
public float walkSpeed = 3f;        // should be WalkSpeed
public float runSpeed = 6f;         // should be RunSpeed
public float jumpHeight = 1.5f;     // should be JumpHeight

// FactionSystem.cs
public string id, name, description;    // should be Id, Name, Description
public int playerReputation;            // should be PlayerReputation

// NPCBase.cs
public string npcId = "npc_001";        // should be NpcId
public string npcName = "无名老者";       // should be NpcName

// CombatSystem.cs
public float baseSpiritAttack = 15f;    // should be BaseSpiritAttack
public float castTime = 0.4f;           // should be CastTime

// SpiritVein.cs
public string veinName = "小型灵脉";     // should be VeinName
public float cultivationMultiplier = 1.5f; // should be CultivationMultiplier
```

### Exceptions — scripts that DO follow convention:

- `GameManager.cs` — uses `_camelCase` for private, PascalCase for public
- `EventBus.cs` — uses PascalCase correctly
- `PlayerController.cs` — uses `_camelCase` for private fields (but still has camelCase public fields)
- `CultivationManager.cs` — uses PascalCase for public properties

**Root cause:** Migrated codebase where the original project used JavaScript/UnityScript conventions (camelCase for public fields).

**Fix:** Bulk rename across all files. This is a mechanical change — rename to PascalCase and update all references.

---

## 2. Missing `[RequireComponent]` (HIGH)

**Standard:** Any MonoBehaviour that calls `GetComponent<T>()` should declare `[RequireComponent(typeof(T))]` to guarantee the dependency at edit time and avoid null references at runtime.

**Finding:** 45 of 57 scripts that call `GetComponent<T>()` do **not** declare the required component.

### Compliant files (12):

`DialogueTree.cs`, `NPCActivity.cs`, `NPCBond.cs`, `NPCMemory.cs`, `NPCNaturalSchedule.cs`, `NPCNetwork.cs`, `NPCRelationship.cs`, `NPCSchedule.cs`, `NPCSecret.cs`, `NPCWander.cs`, `PlayerController.cs`, `SpiritVeinChallenge.cs`

**Notable:** The NPC folder is the only folder with consistent `[RequireComponent]` usage — all 12 NPC-adjacent scripts declare the attribute. Every other folder is 0-1 files compliant.

### Missing — by folder:

| Folder | Missing | Total | Compliant |
|---|---|---|---|
| Combat | 12 | 12 | 0 |
| Core | 9 | 9 | 0 |
| World | 10 | 10 | 0 |
| UI | 5 | 5 | 0 |
| Editor | 5 | 5 | 0 |
| NPC | 2 | 14 | 12 |

### Additionally — GetComponentInChildren without RequireComponent:

`BossAI.cs`, `BossDiplomacy.cs`, `CraftingUI.cs`, `TribulationUI.cs`, `SectUI.cs`

**Fix:** For each script, identify the types obtained via `GetComponent<T>()` and add `[RequireComponent(typeof(T))]` at the class level.

---

## 3. Namespace Inconsistencies (MEDIUM)

### 3a. Legacy `StackingCute` namespace (8 files)

These files still use the old project namespace:

| File | Current | Should Be |
|---|---|---|
| `Core/BlockController.cs` | `StackingCute` | `EarthOnline.Core` |
| `Core/DebrisEffect.cs` | `StackingCute` | `EarthOnline.Core` |
| `Core/EditorGameLoop.cs` | `StackingCute` | `EarthOnline.Core` |
| `Core/GameManager.cs` | `StackingCute` | `EarthOnline.Core` |
| `Core/PerfectEffect.cs` | `StackingCute` | `EarthOnline.Core` |
| `Core/TowerManager.cs` | `StackingCute` | `EarthOnline.Core` |
| `Data/LevelConfig.cs` | `StackingCute` | `EarthOnline.Data` |
| `UI/GameOverUI.cs` | `StackingCute` | `EarthOnline.UI` |

### 3b. Missing namespace entirely (5 files)

`Core/VerificationRunner.cs`, `Editor/AudioPlaceholderGenerator.cs`, `Editor/AutoWireComponents.cs`, `Editor/WireComponents.cs`, `World/DungeonSystemTest.cs`

### 3c. Inconsistent namespace depth within same folder

**Core folder** uses 3 different namespaces:

| Namespace | Files |
|---|---|
| `EarthOnline` | 17 files (AudioManager, CultivationManager, PlayerStats, etc.) |
| `EarthOnline.Core` | 11 files (AlchemyController, DaoQuestioning, ForgeController, etc.) |
| `StackingCute` | 6 files (BlockController, DebrisEffect, etc.) |

**World folder** uses 2 different namespaces:

| Namespace | Files |
|---|---|
| `EarthOnline` | 18 files (CrimeSystem, FactionSystem, FishingSpot, etc.) |
| `EarthOnline.World` | 23 files (AreaReputation, DiscoverySystem, DungeonInstance, etc.) |

### Namespace distribution (all folders):

```
 EarthOnline          38  (root — overused, should be sub-namespaced)
 EarthOnline.World    23
 EarthOnline.Framework 19
 EarthOnline.Combat   16
 EarthOnline.Gifts    15
 EarthOnline.NPC      13
 EarthOnline.Core     11
 EarthOnline.UI        9
 StackingCute          8  (legacy — must migrate)
 EarthOnline.Editor    6
 EarthOnline.Player    1
 EarthOnline.Data      1
```

**Fix:** Migrate `StackingCute` to `EarthOnline.*`, add namespaces to the 5 missing files, and sub-namespace the 38 loose `EarthOnline` files into their proper folder-based namespace.

---

## 4. Magic Numbers (MEDIUM)

**Finding:** ~1,560 lines contain hardcoded numeric literals used in comparisons, calculations, or logic.

### Common patterns:

| Pattern | Example Files | Suggested Fix |
|---|---|---|
| Gameplay constants as field defaults | `CombatSystem.cs`: `baseSpiritAttack = 15f` | Named constant or config file |
| Threshold comparisons | `accuracy >= 0.95f`, `progress <= 0.55f` | Named constants with design intent |
| Timing values | `castTime = 0.4f`, `dialogueDisplayTime = 4f` | Config-driven or named const |
| Color RGBA | `new Color(0.3f, 0.1f, 0.5f)` | Named color palette constants |
| Range attributes | `[Range(0f, 0.5f)]` | Already semi-documented, but could use constants |
| Drop/probability tables | `drop = 0.55f, cultivation = 0.45f` | Config-driven from data files |

### Example of a "good" pattern (already exists):

```csharp
// CultivationManager.cs — region-based MaxLayer expression
public int MaxLayer => IsPlayer ? 13 : 9;  // 13 for player, 9 for NPC
```

This is documented with a comment. Most magic numbers lack this context.

### Cross-cutting duplicated constants (most critical):

These numeric values appear in **multiple unrelated files** and should be shared constants:

| Constant | Files Using It |
|---|---|
| `86400` (seconds/day) | `SectManager.cs`, `SectWarSystem.cs`, `DungeonReward.cs` |
| `3600` (seconds/hour) | `SecretLearning.cs`, `SectWarSystem.cs` |
| `100, 300, 600, 1000, 1500` (realm thresholds) | `PlayerStats.cs`, `CombatSystem.cs`, `WorldConfig.cs` |
| `0.02, 0.01` (reputation price modifiers) | `ShopSystem.cs`, `ReputationSystem.cs`, `MarketSystem.cs` |
| `30f` (witness/detection range) | `CrimeSystem.cs`, `WitnessSystem.cs` |
| `180f` (3-minute interval) | `AntagonistSystem.cs`, `RumorSystem.cs` |
| `100` (max reputation/affinity range) | `FactionSystem.cs`, `NPCRelationship.cs`, `AreaReputation.cs` |
| Realm power multipliers `{1.0, 1.5, 2.5, 4.0, 6.5, 10.0, 16.0}` | `BossDef.cs`, `EnemySpawnLoader.cs` |

### Most concentrated files:

| File | Magic Number Lines | Types of Constants |
|---|---|---|
| `AlchemyController.cs` | ~30 lines | Quality thresholds, mutation chances, proficiency gains |
| `ForgeController.cs` | ~40 lines | Strike configs, purity rates, success rates |
| `DungeonProgress.cs` | ~25 lines | Scoring thresholds, rating tiers, difficulty multipliers |
| `EquipmentEnhancement.cs` | ~20 lines | Cost tables, success rates, level caps |
| `CrimeSystem.cs` | ~15 lines | Bounty values, thresholds, damage formulas |
| `VFXManager.cs` | ~25 lines | Trail config, particle counts, pillar heights |
| `EnemySpawnLoader.cs` | ~20 lines | Tier scaling, speed ranges, stat multipliers |
| `UI/UIManager.cs` | ~20 lines | Font sizes, panel dimensions, layout constants |

**Fix:** Not urgent for rapid prototyping, but for release/maintenance:
- **High:** Deduplicate cross-cutting constants (86400, 3600, realm thresholds) into a shared `GameConstants` class
- **High:** Extract realm/cultivation thresholds from 3 duplicate files into a ScriptableObject config
- **Medium:** Move UI layout constants (font sizes, panel dimensions) into a USS stylesheet
- **Medium:** Extract balance numbers (crit rates, multipliers, costs) into ScriptableObject configs
- **Low:** Consider a `GameBalance` SO for all tuning parameters

---

## 5. Chinese Comments and Strings (INFO — accepted)

**Finding:** 147 of 165 scripts (89%) contain Chinese characters, totaling 5,815 lines.

### Distribution by context:

| Context | Lines | % |
|---|---|---|
| String literals (UI text, logs, display names) | ~3,143 | 54% |
| XML doc comments (`/// summary`) | ~1,208 | 21% |
| Inline comments (`//`) | ~780 | 13% |
| Unity attribute parameters (`[Header("中文")]`) | ~444 | 8% |
| Expression-bodied members (`=> "中文"`) | ~270 | 5% |

### Folder coverage:

| Folder | Chinese Files / Total | Coverage |
|---|---|---|
| Combat | 16/16 | 100% |
| Gifts | 15/15 | 100% |
| NPC | 13/13 | 100% |
| Player | 1/1 | 100% |
| UI | 9/9 | 100% |
| Framework | 19/20 | 95% |
| World | 41/45 | 91% |
| Core | 28/35 | 80% |
| Editor | 5/9 | 56% |
| Camera | 0/1 | 0% |
| Data | 0/1 | 0% |

**Assessment:** Appropriate for this project. The game is Chinese-themed (xianxia/cultivation), the developer team is Chinese-speaking, and Chinese appears only in comments, strings, and attribute parameters — never in identifiers. No action required.

---

## Top 10 Issues Ranked by Severity

| Rank | Issue | Severity | Impact | Affected Files |
|---|---|---|---|---|
| 1 | Public fields use camelCase instead of PascalCase | HIGH | Violates C# convention; confuses Unity serialization; hinders maintainability | ~140 files |
| 2 | GetComponent calls without [RequireComponent] | HIGH | Runtime null references; no editor-time dependency guarantee | 45 files |
| 3 | Legacy StackingCute namespace still in use | HIGH | Cross-namespace type resolution failures; assembly conflicts | 8 files |
| 4 | Missing namespace on game scripts | HIGH | Global namespace pollution; potential type collisions | 5 files |
| 5 | Inconsistent namespace depth within Core folder | MEDIUM | Confusion about which namespace to import; 3 different namespaces in one folder | Core folder (35 files) |
| 6 | Magic balance numbers hardcoded as defaults | MEDIUM | Cannot tune without recompilation; no data-driven balance | ~60 files with public field defaults |
| 7 | GetComponentInChildren calls without [RequireComponent] | MEDIUM | Missing child component dependency declaration | 5 files |
| 8 | Magic threshold values in comparisons | MEDIUM | Design intent unclear; hard to find and adjust | ~40 files with >.5f comparisons |
| 9 | Magic color values in code | LOW | Scattered color definitions; inconsistent palette | ~25 files with `new Color()` |
| 10 | `SectUI.cs` in World folder, namespace `EarthOnline.UI` | LOW | Folder-namespace mismatch; confusing project structure | 1 file |

---

## Recommended Fixes

### Immediate (High Priority)

1. **Establish naming convention rule in project guidelines:**
   - Public fields = `PascalCase`
   - Private fields = `_camelCase`
   - Serialized fields = `[SerializeField] private _camelCase`

2. **Bulk rename all public fields:**
   - Use a global search-and-replace script across all scripts
   - Example: `public float walkSpeed` → `public float WalkSpeed`
   - Update all references (this will be the bulk of the work)

3. **Add `[RequireComponent]` to all 45 missing files:**
   - Scan each file's `GetComponent<T>()` calls
   - Add `[RequireComponent(typeof(T))]` to the class declaration

### Short-term (Medium Priority)

4. **Migrate `StackingCute` namespace to `EarthOnline.*`:**
   - Replace `namespace StackingCute` with the correct folder-based namespace
   - Update all `using StackingCute` references

5. **Add namespaces to 5 missing files:**
   - `VerificationRunner.cs` → `namespace EarthOnline.Core`
   - Editor files → `namespace EarthOnline.Editor`
   - `DungeonSystemTest.cs` → `namespace EarthOnline.World`

6. **Unify Core folder namespace:**
   - Decide: should Core scripts be `EarthOnline.Core` or stay at `EarthOnline`?
   - Recommendation: Use `EarthOnline.Core` for Core, `EarthOnline` for root-level types only

### Long-term (Low Priority)

7. **Extract magic numbers into ScriptableObject configs:**
   - Create `GameBalanceConfig` ScriptableObject
   - Move tuning values (speeds, costs, cooldowns, thresholds) into the config
   - Reference config via singleton or DI

8. **Create a color palette constants class:**
   - Centralize frequently used colors
   - Replace scattered `new Color()` calls with named constants

9. **Separate Chinese UI strings from code:**
   - Create localization data files (JSON/CSV)
   - Reference strings by key rather than hardcoding

---

## Files Requiring Immediate Attention

### Critical (combines multiple issues):

| File | Issues |
|---|---|
| `Core/VerificationRunner.cs` | No namespace, missing `[RequireComponent]`, GetComponent calls |
| `World/DungeonSystemTest.cs` | No namespace, magic numbers |
| `Core/GameManager.cs` | Legacy `StackingCute` namespace, magic numbers |
| `Data/LevelConfig.cs` | Legacy `StackingCute` namespace |

### Require `[RequireComponent]` + naming fix:

All files in `Combat/` (12 files), `UI/` (5 files), plus:
- `Core/BlockController.cs`, `Core/OriginManager.cs`, `Core/TribulationManager.cs`
- `World/CrimeSystem.cs`, `World/FastTravel.cs`, `World/SpiritVein.cs`
- `NPC/NPCBase.cs`, `NPC/NPCDialogueLoader.cs`, `NPC/NPCScheduleLoader.cs`

### Legacy namespace migration:

- `Core/BlockController.cs`, `Core/DebrisEffect.cs`, `Core/EditorGameLoop.cs`, `Core/GameManager.cs`, `Core/PerfectEffect.cs`, `Core/TowerManager.cs`
- `Data/LevelConfig.cs`
- `UI/GameOverUI.cs`

---

## Methodology

Scans were performed using:
- `grep -Pn` with CJK Unicode ranges for Chinese character detection
- `awk` + `grep` patterns for field naming analysis
- Cross-referencing `GetComponent` calls against `[RequireComponent]` declarations
- Pattern matching for hardcoded numeric literals in logic contexts

All 165 game scripts under `Assets/Scripts/` were analyzed (excluding TextMesh Pro examples and Package code).
