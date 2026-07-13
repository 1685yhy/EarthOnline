using UnityEditor;
using UnityEngine;

public static class AutoWireComponents
{
    [MenuItem("EarthOnline/Wire Components")]
    public static void WireAllComponents()
    {
        if (Application.isPlaying) return;

        var lev = GameObject.Find("Enemy_Leviathan");
        if (lev != null)
        {
            if (lev.GetComponent("BossAI") == null)
                lev.AddComponent(System.Type.GetType("EarthOnline.Combat.BossAI, Assembly-CSharp"));
        }

        var gm = GameObject.Find("GameManager");
        if (gm != null)
        {
            if (gm.GetComponent("FogOfWar") == null)
                gm.AddComponent(System.Type.GetType("EarthOnline.World.FogOfWar, Assembly-CSharp"));
        }

        Debug.Log("[AutoWire] Done.");
    }
}
