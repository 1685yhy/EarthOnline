using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthOnline.Framework
{
    /// <summary>
    /// 金手指管理器。管理所有已注册的金手指模板 + 玩家当前激活的金手指。
    /// </summary>
    public class GiftManager : MonoBehaviour
    {
        public static GiftManager Instance { get; private set; }

        private Dictionary<string, GiftBase> _giftTemplates = new Dictionary<string, GiftBase>();
        private List<GiftBase> _activeGifts = new List<GiftBase>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void RegisterTemplate(GiftBase gift)
        {
            _giftTemplates[gift.GiftId] = gift;
            Debug.Log($"[GiftManager] Registered template: {gift.GiftName} [{gift.Rarity}]");
        }

        public GiftBase GetTemplate(string giftId)
        {
            _giftTemplates.TryGetValue(giftId, out var gift);
            return gift;
        }

        public List<GiftBase> GetAllTemplates()
        {
            return _giftTemplates.Values.ToList();
        }

        public GiftBase ActivateGift(string giftId)
        {
            if (!_giftTemplates.ContainsKey(giftId))
            {
                Debug.LogError($"[GiftManager] Gift not found: {giftId}");
                return null;
            }

            // 防止重复激活同一个金手指
            if (_activeGifts.Exists(g => g.GiftId == giftId))
            {
                Debug.LogWarning($"[GiftManager] Gift already active: {giftId}");
                return _activeGifts.Find(g => g.GiftId == giftId);
            }

            var gift = _giftTemplates[giftId];
            gift.Activate();
            _activeGifts.Add(gift);

            EventBus.Publish("OnGiftActivated", new Dictionary<string, object>
            {
                {"giftId", gift.GiftId}, {"giftName", gift.GiftName}, {"rarity", gift.Rarity}
            });

            Debug.Log($"[GiftManager] Activated: {gift.GiftName} [{gift.Rarity}]");
            return gift;
        }

        public List<GiftBase> GetActiveGifts()
        {
            return new List<GiftBase>(_activeGifts);
        }

        public bool HasGiftOfType(string giftType)
        {
            return _activeGifts.Any(g => g.GiftType == giftType);
        }
    }
}
