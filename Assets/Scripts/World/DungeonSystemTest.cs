using EarthOnline.World;
using UnityEngine;

/// <summary>
/// Runtime test harness for DungeonInstance + DungeonRoomGenerator.
/// Attach to any GameObject and invoke from the Inspector context menu.
/// </summary>
public class DungeonSystemTest : MonoBehaviour
{
    [ContextMenu("Run Dungeon System Tests")]
    public void RunAllTests()
    {
        Debug.Log("=== Dungeon System Test Suite ===");

        TestSeedDeterminism();
        TestDifficultyConfigs();
        TestBranchCount();
        TestRoomTypes();
        TestPassageMethods();
        TestBossRoom();

        Debug.Log("=== All Tests Complete ===");
    }

    private void TestSeedDeterminism()
    {
        var inst = gameObject.AddComponent<DungeonInstance>();
        inst.EnterDungeon("test_player");
        inst.SelectDifficulty(DungeonDifficulty.Normal);

        int seed1 = inst.Seed;
        var layout1 = CloneLayout(inst.Layout);

        inst.ResetInstance();
        inst.EnterDungeon("test_player");
        inst.SelectDifficulty(DungeonDifficulty.Normal);
        int seed2 = inst.Seed;

        Debug.Assert(seed1 == seed2, "[Test] Same seed on re-entry: PASS");
        Debug.Log($"[Test] Seed determinism: seed={seed1}, rooms={layout1.RoomTypes.Count}");

        // Different player = different seed
        inst.ResetInstance();
        inst.EnterDungeon("other_player");
        inst.SelectDifficulty(DungeonDifficulty.Normal);
        Debug.Assert(inst.Seed != seed1, "[Test] Different player produces different seed: PASS");

        Destroy(inst);
    }

    private void TestDifficultyConfigs()
    {
        foreach (DungeonDifficulty diff in System.Enum.GetValues(typeof(DungeonDifficulty)))
        {
            var inst = gameObject.AddComponent<DungeonInstance>();
            inst.EnterDungeon("tester");
            inst.SelectDifficulty(diff);

            Debug.Log($"[Test] {diff}: {inst.Layout.RoomCount} rooms, seed={inst.Seed}");
            Debug.Assert(inst.Layout.RoomCount >= 3, $"[Test] {diff} has enough rooms: PASS");
            Destroy(inst);
        }
    }

    private void TestBranchCount()
    {
        var inst = gameObject.AddComponent<DungeonInstance>();
        inst.EnterDungeon("branch_test");
        inst.SelectDifficulty(DungeonDifficulty.Normal);

        bool allValid = true;
        for (int i = 0; i < inst.Layout.RoomCount; i++)
        {
            var room = inst.Layout.GetRoom(i);
            if (room.Branches.Count > 0 && (room.Branches.Count < 2 || room.Branches.Count > 3))
            {
                Debug.LogWarning($"[Test] Room {i} has {room.Branches.Count} branches (expected 2-3)");
                allValid = false;
            }
        }
        Debug.Assert(allValid, "[Test] Branch count 2-3: PASS");
        Destroy(inst);
    }

    private void TestRoomTypes()
    {
        var inst = gameObject.AddComponent<DungeonInstance>();
        inst.EnterDungeon("type_test");
        inst.SelectDifficulty(DungeonDifficulty.Hard);

        var types = new System.Collections.Generic.HashSet<RoomType>();
        for (int i = 0; i < inst.Layout.RoomCount; i++)
            types.Add(inst.Layout.GetRoom(i).RoomType);

        Debug.Log($"[Test] Room types encountered: {string.Join(", ", types)}");
        Debug.Assert(types.Contains(RoomType.Boss), "[Test] Boss room exists: PASS");
        Destroy(inst);
    }

    private void TestPassageMethods()
    {
        var inst = gameObject.AddComponent<DungeonInstance>();
        inst.EnterDungeon("passage_test");
        inst.SelectDifficulty(DungeonDifficulty.Normal);

        bool allHaveMethods = true;
        for (int i = 0; i < inst.Layout.RoomCount; i++)
        {
            var room = inst.Layout.GetRoom(i);
            if (room.PassageMethods.Count < 1)
            {
                Debug.LogWarning($"[Test] Room {i} has no passage methods!");
                allHaveMethods = false;
            }
        }
        Debug.Assert(allHaveMethods, "[Test] All rooms have passage methods: PASS");
        Destroy(inst);
    }

    private void TestBossRoom()
    {
        var inst = gameObject.AddComponent<DungeonInstance>();
        inst.EnterDungeon("boss_test");
        inst.SelectDifficulty(DungeonDifficulty.Normal);

        var lastRoom = inst.Layout.GetRoom(inst.Layout.RoomCount - 1);
        Debug.Assert(lastRoom.RoomType == RoomType.Boss, "[Test] Last room is Boss: PASS");
        Debug.Assert(lastRoom.PassageMethods.Contains(PassageMethod.Combat), "[Test] Boss room has Combat passage: PASS");
        Destroy(inst);
    }

    private DungeonLayoutSnapshot CloneLayout(DungeonLayout layout)
    {
        var snapshot = new DungeonLayoutSnapshot();
        for (int i = 0; i < layout.RoomCount; i++)
        {
            var room = layout.GetRoom(i);
            snapshot.RoomTypes.Add(room.RoomType);
            snapshot.BranchCounts.Add(room.Branches.Count);
        }
        return snapshot;
    }

    private class DungeonLayoutSnapshot
    {
        public System.Collections.Generic.List<RoomType> RoomTypes = new System.Collections.Generic.List<RoomType>();
        public System.Collections.Generic.List<int> BranchCounts = new System.Collections.Generic.List<int>();
    }
}
