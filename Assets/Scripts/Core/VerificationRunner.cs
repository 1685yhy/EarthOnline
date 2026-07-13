using UnityEngine;
using System.Collections.Generic;
using System.Text;
using EarthOnline.Combat;
using EarthOnline.World;

/// <summary>
/// Verifies that all expected game systems are present on their designated GameObjects
/// at runtime. Provides a clear pass/fail console readout in Play mode.
///
/// Activation:
///   A) Auto-creates via [RuntimeInitializeOnLoadMethod] if no instance exists.
///   B) Can be manually attached to any GameObject in the scene.
/// </summary>
public class VerificationRunner : MonoBehaviour
{
    // ── Expected wiring table ────────────────────────────────────────────────
    private struct SystemCheck
    {
        public string gameObjectName;
        public string systemName;
        public string shortLabel;
        public FindMethod findMethod;
    }

    private enum FindMethod { ByName, ByTag }

    private static readonly SystemCheck[] Checks =
    {
        // Enemy_Leviathan — Boss systems
        new() { gameObjectName = "Enemy_Leviathan", systemName = "EarthOnline.Combat.BossAI",             shortLabel = "BossAI",             findMethod = FindMethod.ByName },
        new() { gameObjectName = "Enemy_Leviathan", systemName = "EarthOnline.Combat.BossWeaknessSystem", shortLabel = "BossWeakness",       findMethod = FindMethod.ByName },
        new() { gameObjectName = "Enemy_Leviathan", systemName = "EarthOnline.Combat.BossGrudgeSystem",   shortLabel = "BossGrudge",         findMethod = FindMethod.ByName },
        new() { gameObjectName = "Enemy_Leviathan", systemName = "EarthOnline.Combat.BossDropTable",      shortLabel = "BossDropTable",      findMethod = FindMethod.ByName },

        // GameManager — Map / World systems
        new() { gameObjectName = "GameManager",     systemName = "EarthOnline.World.FogOfWar",           shortLabel = "FogOfWar",           findMethod = FindMethod.ByName },
        new() { gameObjectName = "GameManager",     systemName = "EarthOnline.World.RiskRating",         shortLabel = "RiskRating",         findMethod = FindMethod.ByName },
        new() { gameObjectName = "GameManager",     systemName = "EarthOnline.World.AreaReputation",     shortLabel = "AreaReputation",     findMethod = FindMethod.ByName },
        new() { gameObjectName = "GameManager",     systemName = "EarthOnline.World.ExplorationDepth",   shortLabel = "ExplorationDepth",   findMethod = FindMethod.ByName },
        new() { gameObjectName = "GameManager",     systemName = "EarthOnline.World.DynamicEventSystem", shortLabel = "DynamicEvent",       findMethod = FindMethod.ByName },

        // Player — Gathering system
        new() { gameObjectName = "Player",          systemName = "EarthOnline.World.GatheringSystem",     shortLabel = "GatheringSystem",    findMethod = FindMethod.ByTag },
    };

    // ── Lifecycle ────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        // If an instance already exists in the scene, don't duplicate.
        if (FindObjectOfType<VerificationRunner>() != null)
            return;

        var go = new GameObject("[VerificationRunner]");
        go.AddComponent<VerificationRunner>();
        DontDestroyOnLoad(go);
    }

    private void Start()
    {
        // Small delay so BootstrapRunner has finished wiring components.
        Invoke(nameof(RunVerification), 0.1f);
    }

    // ── Known types for typeof() check (more robust than string-based) ────────
    private static readonly Dictionary<string, System.Type> KnownTypes = new()
    {
        { "EarthOnline.Combat.BossAI",             typeof(BossAI) },
        { "EarthOnline.Combat.BossWeaknessSystem", typeof(BossWeaknessSystem) },
        { "EarthOnline.Combat.BossGrudgeSystem",   typeof(BossGrudgeSystem) },
        { "EarthOnline.Combat.BossDropTable",      typeof(BossDropTable) },
        { "EarthOnline.World.FogOfWar",            typeof(FogOfWar) },
        { "EarthOnline.World.RiskRating",          typeof(RiskRating) },
        { "EarthOnline.World.AreaReputation",      typeof(AreaReputation) },
        { "EarthOnline.World.ExplorationDepth",    typeof(ExplorationDepth) },
        { "EarthOnline.World.DynamicEventSystem",  typeof(DynamicEventSystem) },
        { "EarthOnline.World.GatheringSystem",     typeof(GatheringSystem) },
    };

    // ── Verification logic ───────────────────────────────────────────────────

    private void RunVerification()
    {
        var passed   = new List<string>(Checks.Length);
        var failed   = new List<string>(Checks.Length);

        foreach (var check in Checks)
        {
            GameObject go = check.findMethod switch
            {
                FindMethod.ByTag => GameObject.FindGameObjectWithTag(check.gameObjectName),
                _                => GameObject.Find(check.gameObjectName),
            };

            if (go == null)
            {
                failed.Add($"{check.shortLabel}(NO_GO)");
                continue;
            }

            // Preferred: typeof() lookup for known types (compile-time safe)
            Component comp = null;
            if (KnownTypes.TryGetValue(check.systemName, out System.Type knownType))
            {
                comp = go.GetComponent(knownType);
            }

            // Fallback: string-based lookup for types not in KnownTypes
            if (comp == null)
            {
                comp = go.GetComponent(check.systemName);
            }

            if (comp == null)
            {
                failed.Add($"{check.shortLabel}(MISSING)");
                continue;
            }

            passed.Add(check.shortLabel);
        }

        // ── Build summary ────────────────────────────────────────────────────
        var sb = new StringBuilder();
        sb.Append("[Verify] ");

        foreach (var label in passed)
            sb.Append($"{label}:OK ");
        foreach (var label in failed)
            sb.Append($"{label}:FAIL ");

        sb.Append($"-- Passed {passed.Count}/{Checks.Length}");

        if (failed.Count == 0)
        {
            Debug.Log(sb.ToString());
        }
        else
        {
            sb.Append(" | FAILURES: ");
            sb.Append(string.Join(", ", failed));
            Debug.LogWarning(sb.ToString());
        }
    }

    // ── Manual re-run (handy via Inspector context menu) ─────────────────────

    [ContextMenu("Re-Run Verification")]
    private void ReRun()
    {
        RunVerification();
    }
}
