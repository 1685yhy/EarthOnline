using System;
using UnityEngine;
using EarthOnline.Combat;
using EarthOnline.Framework;
using EarthOnline.World;

namespace EarthOnline.Core
{
    /// <summary>
    /// SceneBootstrap — 系统初始化总入口。
    ///
    /// 职责（按执行顺序）:
    ///   1. Load BossDef configs and assign to all BossAI instances
    ///   2. Initialize RecipeDatabase via RecipeDataLoader
    ///   3. Initialize SectManager (auto-configures from built-in defaults)
    ///   4. Log completion — VerificationRunner validates at 0.1s later
    ///
    /// Execution:
    ///   Uses [DefaultExecutionOrder(-1000)] to guarantee it runs before
    ///   any other Awake (including BossAI, which checks bossDef in Awake).
    ///
    /// 依赖:
    ///   - BossConfigLoader (Combat) — loads BossDef[] from Resources/Data/BossConfigs.json
    ///   - RecipeDataLoader / RecipeDatabase (Framework) — loads recipe JSON into DB
    ///   - SectManager (World) — singleton for sect join/leave lifecycle
    ///   - VerificationRunner (Core) — auto-created, validates all systems @ 0.1s
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class SceneBootstrap : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════════
        //  Inspector Configuration
        // ═══════════════════════════════════════════════════════════════════

        [Header("=== Bootstrap Control ===")]

        [SerializeField, Tooltip("Automatically run Initialize on Awake")]
        private bool initializeOnAwake = true;

        [Header("=== Subsystem Toggles ===")]

        [SerializeField, Tooltip("Load BossDef configs and assign to BossAI instances")]
        private bool initBossDefs = true;

        [SerializeField, Tooltip("Load recipe JSON and populate RecipeDatabase")]
        private bool initRecipeDatabase = true;

        [SerializeField, Tooltip("Load weather/time cycle config from WeatherTimeConfig.json")]
        private bool initWeatherTimeConfig = true;

        [SerializeField, Tooltip("Ensure SectManager singleton exists with configs")]
        private bool initSectManager = true;

        [Header("=== Debug ===")]

        [SerializeField, Tooltip("Verbose logging of each initialization step")]
        private bool verboseLogging = false;

        // ═══════════════════════════════════════════════════════════════════
        //  Unity Lifecycle
        // ═══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (initializeOnAwake)
                Initialize();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Auto-Create (for "one click Play" support)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// If no SceneBootstrap exists in the scene, auto-create one.
        /// This enables "one click Play" without manually placing the prefab.
        ///
        /// Runs after scene load, so all GameObjects and their Awake() calls
        /// have already completed. The Bootstrap handles this gracefully:
        /// components like BossAI that disabled themselves due to missing data
        /// will be re-enabled and initialized after the bootstrap assigns their data.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindObjectOfType<SceneBootstrap>() != null)
                return;

            var go = new GameObject("[SceneBootstrap]");
            go.AddComponent<SceneBootstrap>();
            DontDestroyOnLoad(go);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Public Initialization Entry Point
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Run full system initialization.
        /// Safe to call manually (e.g., from editor test scripts or after re-load).
        /// </summary>
        public void Initialize()
        {
            Log("[Bootstrap] Starting system initialization...");

            // ── Step 1: BossDef Data ────────────────────────────────────
            if (initBossDefs)
                InitializeBossDefs();

            // ── Step 2: Recipe Database ─────────────────────────────────
            if (initRecipeDatabase)
                InitializeRecipeDatabase();

            // ── Step 3: Weather/Time Config ─────────────────────────────
            if (initWeatherTimeConfig)
                InitializeWeatherTimeConfig();

            // ── Step 4: Sect Manager ────────────────────────────────────
            if (initSectManager)
                InitializeSectManager();

            // ── Completion ──────────────────────────────────────────────
            Debug.Log("[Bootstrap] Systems wired: BOSS Map Player");

            // VerificationRunner fires automatically at 0.1s via its own
            // Invoke("RunVerification", 0.1f) in Start().
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Step 1: BossDef Data
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Load all BossDef configs from Resources/Data/BossConfigs.json
        /// and assign them to every BossAI found in the scene.
        ///
        /// Because SceneBootstrap runs Awake at -1000 execution order,
        /// this assignment happens BEFORE BossAI.Awake checks bossDef,
        /// preventing BossAI from disabling itself due to a missing definition.
        /// </summary>
        private void InitializeBossDefs()
        {
            // ── 1. Load configs ──────────────────────────────────────────
            BossDef[] bosses = BossConfigLoader.LoadAllBosses();

            if (bosses == null || bosses.Length == 0)
            {
                Debug.LogWarning("[Bootstrap] No BossDefs loaded from config. " +
                                 "BossAI instances will need manual assignment.");
                return;
            }

            Log($"[Bootstrap] Loaded {bosses.Length} BossDef configs from Resources");

            // ── 2. Find all BossAI instances in the scene ─────────────────
            BossAI[] bossAIs = FindObjectsOfType<BossAI>(true); // include inactive
            if (bossAIs == null || bossAIs.Length == 0)
            {
                Log("[Bootstrap] No BossAI instances in scene; BossDefs loaded for later use.");
                return;
            }

            // ── 3. Assign bossDef to each BossAI ────────────────────────
            int assigned = 0;
            int skippedBecauseAlreadySet = 0;
            int reEnabled = 0;

            foreach (BossAI bossAI in bossAIs)
            {
                if (bossAI == null)
                    continue;

                // If already assigned (e.g., by scene inspector), skip
                if (bossAI.bossDef != null)
                {
                    skippedBecauseAlreadySet++;
                    continue;
                }

                // Try to find a matching BossDef by the GameObject's name
                // (e.g., "Enemy_Leviathan" contains "leviathan" → search bossId & displayName)
                BossDef match = FindBestMatch(bossAI.gameObject.name, bosses);

                if (match != null)
                {
                    bossAI.bossDef = match;
                    Log($"[Bootstrap] Assigned BossDef '{match.displayName}' → {bossAI.gameObject.name}");
                }
                else
                {
                    // Fallback: assign the first available BossDef so the system works
                    bossAI.bossDef = bosses[0];
                    Debug.Log($"[Bootstrap] WARN: No matching BossDef for '{bossAI.gameObject.name}'. " +
                              $"Assigned default '{bosses[0].displayName}'.");
                }

                assigned++;

                // Handle the case where BossAI.Awake already checked bossDef
                // (null at that point) and disabled itself. If the component is
                // disabled, re-enable now that bossDef is assigned, and manually
                // call InitializeBoss since Start was skipped.
                if (!bossAI.enabled)
                {
                    bossAI.enabled = true;
                    bossAI.InitializeBoss();
                    reEnabled++;
                    Log($"[Bootstrap] Re-enabled BossAI on {bossAI.gameObject.name} after BossDef assignment");
                }
            }

            Debug.Log($"[Bootstrap] BossDef data initialized: " +
                      $"{assigned} assigned, {skippedBecauseAlreadySet} already set, " +
                      $"{reEnabled} re-enabled, {bossAIs.Length} total BossAI instances");
        }

        /// <summary>
        /// Find the BossDef whose bossId or displayName best matches the given GameObject name.
        /// Matching is case-insensitive and checks for substring containment.
        /// </summary>
        private static BossDef FindBestMatch(string gameObjectName, BossDef[] bosses)
        {
            if (string.IsNullOrEmpty(gameObjectName) || bosses == null)
                return null;

            string goNameLower = gameObjectName.ToLowerInvariant();

            // Pass 1: exact match on bossId or displayName
            foreach (BossDef boss in bosses)
            {
                if (boss == null) continue;

                if (!string.IsNullOrEmpty(boss.bossId) &&
                    string.Equals(boss.bossId, goNameLower, System.StringComparison.OrdinalIgnoreCase))
                    return boss;

                if (!string.IsNullOrEmpty(boss.displayName) &&
                    string.Equals(boss.displayName, goNameLower, System.StringComparison.OrdinalIgnoreCase))
                    return boss;
            }

            // Pass 2: substring match (e.g., "Enemy_Leviathan" contains "leviathan")
            foreach (BossDef boss in bosses)
            {
                if (boss == null) continue;

                if (!string.IsNullOrEmpty(boss.bossId) &&
                    goNameLower.Contains(boss.bossId.ToLowerInvariant()))
                    return boss;

                if (!string.IsNullOrEmpty(boss.displayName) &&
                    goNameLower.Contains(boss.displayName.ToLowerInvariant()))
                    return boss;
            }

            // Pass 3: check if bossId/displayName contains the game object name
            foreach (BossDef boss in bosses)
            {
                if (boss == null) continue;

                if (!string.IsNullOrEmpty(boss.bossId) &&
                    boss.bossId.ToLowerInvariant().Contains(goNameLower))
                    return boss;

                if (!string.IsNullOrEmpty(boss.displayName) &&
                    boss.displayName.ToLowerInvariant().Contains(goNameLower))
                    return boss;
            }

            return null;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Step 2: Recipe Database
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Ensure RecipeDatabase singleton exists and load all recipe data
        /// from Resources/Data/Recipes.json via RecipeDataLoader.
        ///
        /// If RecipeDatabase doesn't exist in the scene, creates one.
        /// Since this runs in Awake at -1000, the fresh AddComponent will
        /// immediately trigger RecipeDatabase.Awake (setting the singleton).
        /// </summary>
        private void InitializeRecipeDatabase()
        {
            // ── 1. Ensure RecipeDatabase singleton ──────────────────────
            RecipeDatabase db = RecipeDatabase.Instance;

            if (db == null)
            {
                db = FindObjectOfType<RecipeDatabase>();
            }

            if (db == null)
            {
                var dbGO = new GameObject("[RecipeDatabase]");
                db = dbGO.AddComponent<RecipeDatabase>();
                DontDestroyOnLoad(dbGO);
                Log("[Bootstrap] Created RecipeDatabase GameObject (was missing from scene)");
            }

            // ── 2. Load recipes from Resources JSON ────────────────────
            int count = RecipeDataLoader.LoadFromResources();

            if (count > 0)
            {
                Debug.Log($"[Bootstrap] RecipeDatabase initialized: {count} recipes loaded from JSON");
            }
            else
            {
                // RecipeDatabase.Start() has built-in fallback to builtin recipes
                Log("[Bootstrap] RecipeDatabase using built-in recipes (no JSON found or fallback)");
            }

            // ── 3. Verify loaded state ──────────────────────────────────
            if (RecipeDatabase.Instance != null && RecipeDatabase.Instance.IsLoaded)
            {
                Log($"[Bootstrap] RecipeDatabase confirmed loaded: " +
                    $"{RecipeDatabase.Instance.TotalRecipeCount} total, " +
                    $"{RecipeDatabase.Instance.KnownRecipeCount} known");
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Step 3: Weather/Time Config
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Load weather and time cycle configuration from Resources/Data/WeatherTimeConfig.json.
        /// Provides data-driven tuning for day cycle, seasons, weather types, time events,
        /// and zone-specific weather overrides used by TimeManager and WeatherSystem.
        /// </summary>
        private void InitializeWeatherTimeConfig()
        {
            WeatherTimeDataLoader.Load();

            if (WeatherTimeDataLoader.IsLoaded)
            {
                var data = WeatherTimeDataLoader.RawData;
                Log($"[Bootstrap] Weather/Time config loaded: {data.weatherTypes?.Length ?? 0} weather types, " +
                    $"{data.seasons?.Length ?? 0} seasons, {data.timeEvents?.Length ?? 0} time events");
            }
            else
            {
                Debug.LogWarning("[Bootstrap] Weather/Time config failed to load. Using defaults.");
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Step 4: Sect Manager
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Ensure SectManager singleton exists and is initialized with configs.
        ///
        /// SectManager has built-in default configs for all 5 sect types.
        /// External config overrides can be loaded via the Inspector.
        ///
        /// Note: SectConfigLoader is not currently implemented. When/if it
        /// exists in the future, this method will be extended to call it
        /// and pass the loaded configs to SectManager.
        /// </summary>
        private void InitializeSectManager()
        {
            // ── 1. Ensure SectManager singleton ─────────────────────────
            SectManager sm = SectManager.Instance;

            if (sm == null)
            {
                sm = FindObjectOfType<SectManager>();
            }

            if (sm == null)
            {
                var smGO = new GameObject("[SectManager]");
                sm = smGO.AddComponent<SectManager>();
                DontDestroyOnLoad(smGO);
                Log("[Bootstrap] Created SectManager GameObject (was missing from scene)");
            }

            // ── 2. Future: external config loading ──────────────────────
            // SectManager currently uses built-in default configs (see
            // SectManager.DefaultConfigs). When SectConfigLoader is implemented,
            // load external JSON and apply via:
            //
            //   #if SECT_CONFIG_LOADER_EXISTS
            //   var configs = SectConfigLoader.LoadAllConfigs();
            //   if (configs != null) sm.ApplyConfigOverrides(configs);
            //   #endif
            //
            // For now, the defaults cover all 5 SectType entries with full
            // join/leave/trial/cooldown configuration.

            Debug.Log("[Bootstrap] SectManager initialized with default configs " +
                      "(TianYuanZong, QingYunMen, ShangMeng, YuShouYiZu, SanXiuLianMeng)");
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Utilities
        // ═══════════════════════════════════════════════════════════════════

        private void Log(string message)
        {
            if (verboseLogging)
                Debug.Log(message);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Editor Manual Re-init
        // ═══════════════════════════════════════════════════════════════════

        [ContextMenu("Re-Initialize All Systems")]
        private void ReInitialize()
        {
            Initialize();
        }

        [ContextMenu("Initialize BossDefs Only")]
        private void ReInitializeBossDefs()
        {
            InitializeBossDefs();
        }

        [ContextMenu("Initialize Recipes Only")]
        private void ReInitializeRecipes()
        {
            InitializeRecipeDatabase();
        }

        [ContextMenu("Initialize Weather/Time Config Only")]
        private void ReInitializeWeatherTime()
        {
            InitializeWeatherTimeConfig();
        }

        [ContextMenu("Initialize SectManager Only")]
        private void ReInitializeSects()
        {
            InitializeSectManager();
        }
    }
}
