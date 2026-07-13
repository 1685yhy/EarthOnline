using System;
using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.World
{
    // ─── NPC Companion Class ───────────────────────────────────────────

    /// <summary>NPC companion class/role.</summary>
    public enum NpcClass
    {
        Warrior,  // 战士 — tank / melee
        Healer,   // 治疗 — heals party
        Mage,     // 法师 — ranged damage
        Rogue,    // 盗贼 — stealth / traps
        Archer    // 弓箭手 — ranged physical
    }

    // ─── NPC Companion Data ────────────────────────────────────────────

    /// <summary>Definition for an NPC companion that can join dungeon parties.</summary>
    [Serializable]
    public struct NpcCompanionData
    {
        public string NpcId;
        public string DisplayName;
        public NpcClass Class;
        public int Level;
        [TextArea] public string Description;
    }

    // ─── Event Data ────────────────────────────────────────────────────

    /// <summary>Published when a human player joins the party.</summary>
    public struct PartyMemberJoinedEvent
    {
        public string PlayerId;
        public string PlayerName;
        public int CurrentSize;
        public int MaxSize;
    }

    /// <summary>Published when a human player leaves the party.</summary>
    public struct PartyMemberLeftEvent
    {
        public string PlayerId;
        public string PlayerName;
        public int CurrentSize;
    }

    /// <summary>Published when an NPC companion is added to the party.</summary>
    public struct NpcCompanionAddedEvent
    {
        public string NpcId;
        public string DisplayName;
        public NpcClass Class;
        public int CurrentNpcCount;
    }

    /// <summary>Published when an NPC companion is removed from the party.</summary>
    public struct NpcCompanionRemovedEvent
    {
        public string NpcId;
        public string DisplayName;
        public int CurrentNpcCount;
    }

    /// <summary>Published when the party enters a dungeon together.</summary>
    public struct PartyDungeonEnterEvent
    {
        public string DungeonId;
        public int HumanCount;
        public int NpcCount;
        public int TotalCount;
    }

    // ─── Summary Types ─────────────────────────────────────────────────

    /// <summary>Individual party member info for UI.</summary>
    [Serializable]
    public struct PartyMemberInfo
    {
        public string Id;
        public string Name;
        public bool IsLeader;
        public bool IsNpc;
        public NpcClass NpcClass;
    }

    /// <summary>Full party summary for UI display.</summary>
    [Serializable]
    public struct PartySummary
    {
        public List<PartyMemberInfo> Members;
        public List<PartyMemberInfo> NpcMembers;
        public int TotalCount;
        public int HumanSlots;
        public int NpcSlots;
    }

    // ─── Party Manager ─────────────────────────────────────────────────

    /// <summary>
    /// Manages a dungeon party with up to 4 human players and up to 2 NPC companions.
    /// Handles join/leave lifecycle of both humans and NPCs.
    /// Publishes EventBus events for UI and other systems to react.
    /// </summary>
    public class DungeonParty : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private int _maxHumanPlayers = 4;
        [SerializeField] private int _maxNpcCompanions = 2;

        [Header("Available NPC Companions")]
        [SerializeField] private NpcCompanionData[] _availableNpcs = new NpcCompanionData[]
        {
            new NpcCompanionData
            {
                NpcId = "npc_warrior_001",
                DisplayName = "铁壁·赵刚",
                Class = NpcClass.Warrior,
                Level = 1,
                Description = "前御林军退役战士，擅长盾牌格挡与嘲讽。可靠的前排防御。"
            },
            new NpcCompanionData
            {
                NpcId = "npc_healer_001",
                DisplayName = "灵愈·素问",
                Class = NpcClass.Healer,
                Level = 1,
                Description = "云游医修，精通回春术与净化之术。队伍续航的保障。"
            },
            new NpcCompanionData
            {
                NpcId = "npc_mage_001",
                DisplayName = "炎术·离火",
                Class = NpcClass.Mage,
                Level = 1,
                Description = "火系法术专精者，群体伤害拔群。缺点是身板脆弱。"
            },
            new NpcCompanionData
            {
                NpcId = "npc_rogue_001",
                DisplayName = "影刺·夜莺",
                Class = NpcClass.Rogue,
                Level = 1,
                Description = "暗夜中的刺杀者，擅长解除陷阱和背刺。潜行路线必备。"
            },
            new NpcCompanionData
            {
                NpcId = "npc_archer_001",
                DisplayName = "逐风·羽",
                Class = NpcClass.Archer,
                Level = 1,
                Description = "百步穿杨的神射手，远程压制与侦查的好手。"
            }
        };

        // Runtime
        private readonly List<string> _humanPlayers = new List<string>();
        private readonly List<string> _humanPlayerNames = new List<string>();
        private readonly List<NpcCompanionData> _activeNpcs = new List<NpcCompanionData>();
        private string _partyLeaderId;

        // ─── Properties ──────────────────────────────────────────────────

        public int HumanCount => _humanPlayers.Count;
        public int NpcCount => _activeNpcs.Count;
        public int TotalCount => HumanCount + NpcCount;
        public int MaxHumanPlayers => _maxHumanPlayers;
        public int MaxNpcCompanions => _maxNpcCompanions;
        public bool IsFull => HumanCount >= _maxHumanPlayers;
        public bool IsNpcFull => NpcCount >= _maxNpcCompanions;
        public IReadOnlyList<string> HumanPlayers => _humanPlayers.AsReadOnly();
        public IReadOnlyList<NpcCompanionData> ActiveNpcs => _activeNpcs.AsReadOnly();
        public string PartyLeaderId => _partyLeaderId;
        public NpcCompanionData[] AvailableNpcs => _availableNpcs;

        // ─── Public API ──────────────────────────────────────────────────

        /// <summary>Initialize the party with a leader.</summary>
        public void Initialize(string leaderId, string leaderName)
        {
            _humanPlayers.Clear();
            _humanPlayerNames.Clear();
            _activeNpcs.Clear();

            _partyLeaderId = leaderId;
            _humanPlayers.Add(leaderId);
            _humanPlayerNames.Add(leaderName);

            Debug.Log($"[DungeonParty] Party created. Leader: {leaderName} ({leaderId})");
        }

        /// <summary>Add a human player to the party. Returns true on success.</summary>
        public bool AddPlayer(string playerId, string playerName)
        {
            if (_humanPlayers.Count >= _maxHumanPlayers)
            {
                Debug.LogWarning($"[DungeonParty] Party is full ({_maxHumanPlayers}/{_maxHumanPlayers})");
                return false;
            }

            if (_humanPlayers.Contains(playerId))
            {
                Debug.LogWarning($"[DungeonParty] Player '{playerName}' is already in the party");
                return false;
            }

            _humanPlayers.Add(playerId);
            _humanPlayerNames.Add(playerName);

            EventBus.Publish(new PartyMemberJoinedEvent
            {
                PlayerId = playerId,
                PlayerName = playerName,
                CurrentSize = HumanCount,
                MaxSize = _maxHumanPlayers
            });

            Debug.Log($"[DungeonParty] '{playerName}' joined. Party: {HumanCount}/{_maxHumanPlayers}");
            return true;
        }

        /// <summary>Remove a human player from the party. Returns true on success.</summary>
        public bool RemovePlayer(string playerId)
        {
            int index = _humanPlayers.IndexOf(playerId);
            if (index < 0)
            {
                Debug.LogWarning($"[DungeonParty] Player '{playerId}' is not in the party");
                return false;
            }

            string playerName = _humanPlayerNames[index];
            _humanPlayers.RemoveAt(index);
            _humanPlayerNames.RemoveAt(index);

            // Leadership transfer on leader departure
            if (playerId == _partyLeaderId && _humanPlayers.Count > 0)
            {
                _partyLeaderId = _humanPlayers[0];
                Debug.Log($"[DungeonParty] Party leadership transferred to '{_humanPlayerNames[0]}'");
            }
            else if (_humanPlayers.Count == 0)
            {
                _partyLeaderId = null;
            }

            EventBus.Publish(new PartyMemberLeftEvent
            {
                PlayerId = playerId,
                PlayerName = playerName,
                CurrentSize = HumanCount
            });

            Debug.Log($"[DungeonParty] '{playerName}' left. Party: {HumanCount}/{_maxHumanPlayers}");
            return true;
        }

        /// <summary>Add an NPC companion to the party. Returns true on success.</summary>
        public bool AddNpc(string npcId)
        {
            if (_activeNpcs.Count >= _maxNpcCompanions)
            {
                Debug.LogWarning($"[DungeonParty] NPC slots full ({_maxNpcCompanions}/{_maxNpcCompanions})");
                return false;
            }

            if (_activeNpcs.Exists(n => n.NpcId == npcId))
            {
                Debug.LogWarning($"[DungeonParty] NPC '{npcId}' is already in the party");
                return false;
            }

            var npc = FindNpcData(npcId);
            if (npc == null)
            {
                Debug.LogWarning($"[DungeonParty] NPC '{npcId}' not found in available companions");
                return false;
            }

            _activeNpcs.Add(npc.Value);

            EventBus.Publish(new NpcCompanionAddedEvent
            {
                NpcId = npc.Value.NpcId,
                DisplayName = npc.Value.DisplayName,
                Class = npc.Value.Class,
                CurrentNpcCount = NpcCount
            });

            Debug.Log($"[DungeonParty] NPC '{npc.Value.DisplayName}' ({npc.Value.Class}) joined. NPCs: {NpcCount}/{_maxNpcCompanions}");
            return true;
        }

        /// <summary>Remove an NPC companion from the party. Returns true on success.</summary>
        public bool RemoveNpc(string npcId)
        {
            int index = _activeNpcs.FindIndex(n => n.NpcId == npcId);
            if (index < 0)
            {
                Debug.LogWarning($"[DungeonParty] NPC '{npcId}' is not in the party");
                return false;
            }

            string displayName = _activeNpcs[index].DisplayName;
            _activeNpcs.RemoveAt(index);

            EventBus.Publish(new NpcCompanionRemovedEvent
            {
                NpcId = npcId,
                DisplayName = displayName,
                CurrentNpcCount = NpcCount
            });

            Debug.Log($"[DungeonParty] NPC '{displayName}' left. NPCs: {NpcCount}/{_maxNpcCompanions}");
            return true;
        }

        /// <summary>Toggle an NPC companion on/off by ID. Returns the new active state.</summary>
        public bool ToggleNpc(string npcId)
        {
            if (_activeNpcs.Exists(n => n.NpcId == npcId))
            {
                RemoveNpc(npcId);
                return false;
            }
            else
            {
                AddNpc(npcId);
                return true;
            }
        }

        /// <summary>Get a full party summary for UI display.</summary>
        public PartySummary GetSummary()
        {
            var humans = new List<PartyMemberInfo>();
            for (int i = 0; i < _humanPlayers.Count; i++)
            {
                humans.Add(new PartyMemberInfo
                {
                    Id = _humanPlayers[i],
                    Name = _humanPlayerNames[i],
                    IsLeader = _humanPlayers[i] == _partyLeaderId,
                    IsNpc = false
                });
            }

            var npcs = new List<PartyMemberInfo>();
            foreach (var npc in _activeNpcs)
            {
                npcs.Add(new PartyMemberInfo
                {
                    Id = npc.NpcId,
                    Name = npc.DisplayName,
                    IsLeader = false,
                    IsNpc = true,
                    NpcClass = npc.Class
                });
            }

            return new PartySummary
            {
                Members = humans,
                NpcMembers = npcs,
                TotalCount = TotalCount,
                HumanSlots = _maxHumanPlayers,
                NpcSlots = _maxNpcCompanions
            };
        }

        /// <summary>Notify systems that the party is entering a dungeon together.</summary>
        public void NotifyDungeonEnter(string dungeonId)
        {
            EventBus.Publish(new PartyDungeonEnterEvent
            {
                DungeonId = dungeonId,
                HumanCount = HumanCount,
                NpcCount = NpcCount,
                TotalCount = TotalCount
            });

            Debug.Log($"[DungeonParty] Party entering dungeon '{dungeonId}' with {TotalCount} members ({HumanCount} players + {NpcCount} NPCs)");
        }

        // ─── Internal ────────────────────────────────────────────────────

        private NpcCompanionData? FindNpcData(string npcId)
        {
            foreach (var npc in _availableNpcs)
            {
                if (npc.NpcId == npcId)
                    return npc;
            }
            return null;
        }
    }
}
