using UnityEditor;
using UnityEngine;

public class WireComponents : EditorWindow
{
    [MenuItem("Window/EarthOnline Wire Systems")]
    static void ShowWindow()
    {
        Wire();
    }

    [MenuItem("EarthOnline/Wire All Systems")]
    static void Wire()
    {
        var lev = GameObject.Find("Enemy_Leviathan");
        if (lev != null)
        {
            if (lev.GetComponent("BossAI") == null) lev.AddComponent(typeof(EarthOnline.Combat.BossAI));
            if (lev.GetComponent("BossWeaknessSystem") == null) lev.AddComponent(typeof(EarthOnline.Combat.BossWeaknessSystem));
            if (lev.GetComponent("BossGrudgeSystem") == null) lev.AddComponent(typeof(EarthOnline.Combat.BossGrudgeSystem));
            if (lev.GetComponent("BossDropTable") == null) lev.AddComponent(typeof(EarthOnline.Combat.BossDropTable));
        }

        var gm = GameObject.Find("GameManager");
        if (gm != null)
        {
            if (gm.GetComponent("FogOfWar") == null) gm.AddComponent(typeof(EarthOnline.World.FogOfWar));
            if (gm.GetComponent("RiskRating") == null) gm.AddComponent(typeof(EarthOnline.World.RiskRating));
            // DiscoverySystem not yet implemented
            if (gm.GetComponent("AreaReputation") == null) gm.AddComponent(typeof(EarthOnline.World.AreaReputation));
            if (gm.GetComponent("ExplorationDepth") == null) gm.AddComponent(typeof(EarthOnline.World.ExplorationDepth));
            if (gm.GetComponent("DynamicEventSystem") == null) gm.AddComponent(typeof(EarthOnline.World.DynamicEventSystem));
        }

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            if (player.GetComponent("GatheringSystem") == null) player.AddComponent(typeof(EarthOnline.World.GatheringSystem));
        }

        Debug.Log("[Wire] Done. BOSS=" + (lev!=null) + " Map=" + (gm!=null) + " Player=" + (player!=null));
    }
}
