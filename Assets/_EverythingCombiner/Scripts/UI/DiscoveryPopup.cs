using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace EverythingCombiner
{
    public class DiscoveryPopup : MonoBehaviour
    {
        [Header("UI组件")]
        [SerializeField] private Image elementIcon;
        [SerializeField] private TextMeshProUGUI elementNameText;
        [SerializeField] private TextMeshProUGUI rarityText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI discoveryQuoteText;
        [SerializeField] private Image rarityGlow;
        [SerializeField] private GameObject particleEffect;
        [SerializeField] private Button closeButton;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("动画参数")]
        [SerializeField] private float appearDuration = 0.5f;

        private AnimationCurve scaleCurve;

        private static readonly Color[] RarityGlowColors = new Color[]
        {
            new Color(0.5f, 0.5f, 0.5f, 0.5f),
            new Color(0.2f, 0.5f, 1f, 0.5f),
            new Color(0.6f, 0.2f, 1f, 0.5f),
            new Color(1f, 0.6f, 0f, 0.5f),
            new Color(1f, 0.1f, 0.3f, 0.5f),
        };

        private static readonly string[] RarityNames = { "普通", "稀有", "史诗", "传说", "神话" };

        private void Awake()
        {
            scaleCurve = new AnimationCurve();
            scaleCurve.AddKey(new Keyframe(0f, 0f, 0f, 2f));
            scaleCurve.AddKey(new Keyframe(0.5f, 1.2f, 0f, 0f));
            scaleCurve.AddKey(new Keyframe(1f, 1f, -2f, 0f));
        }

        public void Show(ElementData element)
        {
            if (element == null) return;

            if (elementIcon != null) elementIcon.sprite = element.icon;
            if (elementNameText != null) elementNameText.text = element.elementName;
            if (rarityText != null)
            {
                int rarityIdx = (int)element.rarity;
                string rarityName = rarityIdx < RarityNames.Length ? RarityNames[rarityIdx] : "未知";
                rarityText.text = rarityName;
                rarityText.color = rarityIdx < RarityGlowColors.Length ? RarityGlowColors[rarityIdx] : Color.white;
            }
            if (descriptionText != null) descriptionText.text = element.description;
            if (discoveryQuoteText != null) discoveryQuoteText.text = element.discoveryQuote;
            if (rarityGlow != null)
            {
                int idx = Mathf.Clamp((int)element.rarity, 0, RarityGlowColors.Length - 1);
                rarityGlow.color = RarityGlowColors[idx];
            }

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            StartCoroutine(AppearAnimation());

            if (particleEffect != null)
                particleEffect.SetActive(true);
        }

        private IEnumerator AppearAnimation()
        {
            canvasGroup.alpha = 0f;
            transform.localScale = Vector3.zero;

            float elapsed = 0f;
            while (elapsed < appearDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / appearDuration;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
                transform.localScale = Vector3.one * scaleCurve.Evaluate(t);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            transform.localScale = Vector3.one;
        }

        private IEnumerator HideAnimation()
        {
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;

            while (elapsed < 0.3f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.3f;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
                transform.localScale = startScale * (1f - t * 0.3f);
                yield return null;
            }

            Destroy(gameObject);
        }

        public void Close()
        {
            StartCoroutine(HideAnimation());
        }
    }
}
