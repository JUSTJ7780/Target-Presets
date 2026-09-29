using UnityEngine;
using UnityEngine.UI;

namespace TargetFilterPresets
{
    internal sealed class TargetPresetHudMessage : MonoBehaviour
    {
        private Canvas _canvas;
        private CanvasGroup _group;
        private Text _text;
        private float _hideAt;
        private float _duration;

        public void Show(string message, float seconds)
        {
            EnsureUi();

            _text.text = message;
            _duration = Mathf.Max(0.1f, seconds);
            _hideAt = Time.unscaledTime + _duration;
            _canvas.gameObject.SetActive(true);
            _group.alpha = 1f;
        }

        //possibly add some config for times etc ugg make a note and remind me

        private void Update()
        {
            if (_canvas == null || !_canvas.gameObject.activeSelf)
                return;

            float remaining = _hideAt - Time.unscaledTime;
            if (remaining <= 0f)
            {
                _canvas.gameObject.SetActive(false);
                return;
            }

            float fadeWindow = Mathf.Min(0.35f, _duration * 0.5f);
            _group.alpha = remaining < fadeWindow ? Mathf.Clamp01(remaining / fadeWindow) : 1f;
        }

        private void EnsureUi()
        {
            if (_canvas != null)
                return;

            GameObject root = new GameObject("TargetFilterPresetHudMessage");
            DontDestroyOnLoad(root);

            _canvas = root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 25000;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            _group = root.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            GameObject panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.01f, 0.04f, 0.025f, 0.72f);
            panelImage.raycastTarget = false;

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.sizeDelta = new Vector2(560f, 42f);
            panelRect.anchoredPosition = new Vector2(0f, -58f);

            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.0f, 1f, 0.25f, 0.45f);
            outline.effectDistance = new Vector2(1f, -1f);

            GameObject label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(panel.transform, false);
            _text = label.AddComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _text.text = string.Empty;
            _text.fontSize = 22;
            _text.fontStyle = FontStyle.Bold;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.color = new Color(0.42f, 1f, 0.58f, 1f);
            _text.raycastTarget = false;

            RectTransform textRect = label.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 2f);
            textRect.offsetMax = new Vector2(-16f, -2f);

            Shadow shadow = label.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(1f, -1f);

            root.SetActive(false);
        }
    }
}
