using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FPSMultiplayer.Core;

namespace FPSMultiplayer.Infrastructure
{
    public interface ILoadingScreenService
    {
        void Show(string message, bool hideOnNextSceneLoaded = false);
        void Hide();
        bool IsVisible { get; }
    }

    public class LoadingScreenService : MonoBehaviour, ILoadingScreenService
    {
        [SerializeField] private string _defaultMessage = "Cargando";

        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private TMP_Text _messageText;
        private Coroutine _dotsRoutine;
        private bool _hideOnNextSceneLoaded;
        private bool _isVisible;

        public bool IsVisible => _isVisible;

        private void Awake()
        {
            BuildUi();
            HideImmediate();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        public void Show(string message, bool hideOnNextSceneLoaded = false)
        {
            if (_canvas == null)
                BuildUi();

            _hideOnNextSceneLoaded = hideOnNextSceneLoaded;
            _isVisible = true;

            if (_canvas != null)
                _canvas.gameObject.SetActive(true);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;
            }

            if (_messageText != null)
                _messageText.text = string.IsNullOrWhiteSpace(message) ? _defaultMessage : message;

            if (_dotsRoutine != null)
                StopCoroutine(_dotsRoutine);

            _dotsRoutine = StartCoroutine(AnimateDots());
        }

        public void Hide()
        {
            _hideOnNextSceneLoaded = false;
            HideImmediate();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_hideOnNextSceneLoaded)
                return;

            HideImmediate();
        }

        private void HideImmediate()
        {
            _isVisible = false;

            if (_dotsRoutine != null)
            {
                StopCoroutine(_dotsRoutine);
                _dotsRoutine = null;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }

            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
        }

        private IEnumerator AnimateDots()
        {
            string baseMessage = _messageText != null ? _messageText.text : _defaultMessage;
            string[] dots = { "", ".", "..", "..." };
            int index = 0;

            while (_isVisible)
            {
                if (_messageText != null)
                    _messageText.text = baseMessage + dots[index % dots.Length];

                index++;
                yield return new WaitForSecondsRealtime(0.35f);
            }
        }

        private void BuildUi()
        {
            if (_canvas != null)
                return;

            var root = new GameObject("LoadingScreenUI");
            root.transform.SetParent(transform, false);

            _canvas = root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5000;

            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();

            var group = new GameObject("Group");
            group.transform.SetParent(root.transform, false);
            _canvasGroup = group.AddComponent<CanvasGroup>();

            var background = new GameObject("Background");
            background.transform.SetParent(group.transform, false);
            var backgroundImage = background.AddComponent<Image>();
            backgroundImage.color = new Color(0f, 0f, 0f, 0.72f);
            var backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            var panel = new GameObject("Panel");
            panel.transform.SetParent(group.transform, false);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.12f, 0.08f, 0.05f, 0.94f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 220f);
            panelRect.anchoredPosition = Vector2.zero;

            var title = CreateText(panel.transform, "Cargando", 36, FontStyles.Bold);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 28f);
            titleRect.sizeDelta = new Vector2(460f, 60f);

            _messageText = CreateText(panel.transform, _defaultMessage, 22, FontStyles.Normal);
            var messageRect = _messageText.rectTransform;
            messageRect.anchorMin = new Vector2(0.5f, 0.5f);
            messageRect.anchorMax = new Vector2(0.5f, 0.5f);
            messageRect.anchoredPosition = new Vector2(0f, -20f);
            messageRect.sizeDelta = new Vector2(480f, 60f);

            var tip = CreateText(panel.transform, "Espera un momento", 18, FontStyles.Italic);
            var tipRect = tip.rectTransform;
            tipRect.anchorMin = new Vector2(0.5f, 0.5f);
            tipRect.anchorMax = new Vector2(0.5f, 0.5f);
            tipRect.anchoredPosition = new Vector2(0f, -74f);
            tipRect.sizeDelta = new Vector2(420f, 40f);
            tip.color = new Color(1f, 1f, 1f, 0.72f);
        }

        private static TMP_Text CreateText(Transform parent, string text, int size, FontStyles style)
        {
            var go = new GameObject(text);
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;

            return tmp;
        }
    }
}