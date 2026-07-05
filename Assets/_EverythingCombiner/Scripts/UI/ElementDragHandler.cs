using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

namespace EverythingCombiner
{
    [RequireComponent(typeof(CanvasGroup), typeof(RectTransform))]
    public class ElementDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
    {
        [Header("元素数据")]
        [SerializeField] private ElementData elementData;
        public ElementData ElementData => elementData;

        [Header("视觉反馈")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image rarityBorder;
        [SerializeField] private GameObject selectionHighlight;
        [SerializeField] private float dragScale = 1.2f;
        [SerializeField] private float selectedScale = 1.1f;

        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private Vector3 originalPosition;
        private Transform originalParent;
        private Canvas rootCanvas;
        private bool isSelected;
        private AnimationCurve bounceCurve;

        private static readonly Color[] RarityColors = new Color[]
        {
            new Color(0.6f, 0.6f, 0.6f),
            new Color(0.2f, 0.6f, 1f),
            new Color(0.7f, 0.2f, 1f),
            new Color(1f, 0.7f, 0f),
            new Color(1f, 0.2f, 0.4f),
        };

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = GetComponent<RectTransform>();
            rootCanvas = GetComponentInParent<Canvas>();
            originalPosition = rectTransform.anchoredPosition;
            originalParent = transform.parent;

            bounceCurve = new AnimationCurve();
            bounceCurve.AddKey(new Keyframe(0f, 0f, 0f, 2f));
            bounceCurve.AddKey(new Keyframe(0.5f, 1.2f, 0f, 0f));
            bounceCurve.AddKey(new Keyframe(1f, 1f, -2f, 0f));

            if (selectionHighlight != null)
                selectionHighlight.SetActive(false);
        }

        public void Initialize(ElementData data, Sprite icon)
        {
            elementData = data;
            if (iconImage != null && icon != null)
                iconImage.sprite = icon;
            UpdateRarityVisual();
        }

        private void UpdateRarityVisual()
        {
            if (rarityBorder != null && elementData != null)
            {
                int rarityIndex = Mathf.Clamp((int)elementData.rarity, 0, RarityColors.Length - 1);
                rarityBorder.color = RarityColors[rarityIndex];
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (GameManager.Instance == null) return;

            if (GameManager.Instance.currentState == GameState.Idle)
            {
                SelectAsElementA();
            }
            else if (GameManager.Instance.currentState == GameState.Selecting)
            {
                GameManager.Instance.SelectElementB(elementData);
            }
        }

        private void SelectAsElementA()
        {
            isSelected = true;
            GameManager.Instance.SelectElementA(elementData);

            if (selectionHighlight != null)
                selectionHighlight.SetActive(true);

            rectTransform.localScale = Vector3.one * selectedScale;
            StartCoroutine(BounceEffect());
        }

        public void Deselect()
        {
            isSelected = false;
            if (selectionHighlight != null)
                selectionHighlight.SetActive(false);
            rectTransform.localScale = Vector3.one;
        }

        private IEnumerator BounceEffect()
        {
            float duration = 0.3f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scale = Mathf.Lerp(1f, selectedScale, bounceCurve.Evaluate(t));
                rectTransform.localScale = Vector3.one * scale;
                yield return null;
            }
            rectTransform.localScale = Vector3.one * selectedScale;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            canvasGroup.blocksRaycasts = false;
            transform.SetParent(rootCanvas.transform);
            rectTransform.localScale = Vector3.one * dragScale;
        }

        public void OnDrag(PointerEventData eventData)
        {
            rectTransform.anchoredPosition += eventData.delta / rootCanvas.scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.blocksRaycasts = true;

            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (var result in results)
            {
                var targetHandler = result.gameObject.GetComponent<ElementDragHandler>();
                if (targetHandler != null && targetHandler != this)
                {
                    if (GameManager.Instance != null && GameManager.Instance.currentState == GameState.Selecting)
                    {
                        GameManager.Instance.SelectElementB(targetHandler.ElementData);
                    }
                    break;
                }
            }

            transform.SetParent(originalParent);
            rectTransform.anchoredPosition = originalPosition;
            rectTransform.localScale = Vector3.one * (isSelected ? selectedScale : 1f);
        }

        public void PlayDiscoveryAnimation()
        {
            StopAllCoroutines();
            StartCoroutine(DiscoveryPulse());
        }

        private IEnumerator DiscoveryPulse()
        {
            float duration = 0.6f;
            float elapsed = 0f;
            Vector3 originalScale = rectTransform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float pulse = 1f + Mathf.Sin(t * Mathf.PI * 3) * 0.3f * (1f - t);
                rectTransform.localScale = originalScale * pulse;
                yield return null;
            }

            rectTransform.localScale = originalScale;
        }
    }
}
