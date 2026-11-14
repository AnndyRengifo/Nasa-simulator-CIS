using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Sol3.Cinematics
{
    /// <summary>
    /// Interfaz del sistema de cinemáticas, creada por código: fundidos a color, botón "Omitir"
    /// (táctil, legible en móvil) y presentador de subtítulos con bandas negras.
    /// Siempre usa tiempo no escalado, así que funciona con el juego pausado (Time.timeScale = 0).
    /// </summary>
    [AddComponentMenu("")]
    public sealed class CinematicOverlay : MonoBehaviour
    {
        CanvasGroup m_Fade;
        Image m_FadeImage;
        GameObject m_SkipButton;
        TextMeshProUGUI m_SkipLabel;
        Action m_OnSkip;

        public CinematicSubtitlePresenter Subtitles { get; private set; }
        public float FadeAlpha => m_Fade != null ? m_Fade.alpha : 0f;

        public static CinematicOverlay Create(Transform parent, float targetAspect, string skipLabel)
        {
            var go = new GameObject("CinematicOverlay_Root");
            go.transform.SetParent(parent, false);
            var overlay = go.AddComponent<CinematicOverlay>();
            overlay.Build(targetAspect, skipLabel);
            return overlay;
        }

        void Build(float targetAspect, string skipLabel)
        {
            Subtitles = CinematicSubtitlePresenter.Create(transform, targetAspect);
            Subtitles.gameObject.SetActive(false);

            var canvasObject = new GameObject("FadeAndControls", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var fade = new GameObject("Fade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            fade.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)fade.transform);
            m_FadeImage = fade.GetComponent<Image>();
            m_FadeImage.color = Color.black;
            m_Fade = fade.GetComponent<CanvasGroup>();
            m_Fade.alpha = 0f;
            m_Fade.blocksRaycasts = false;

            m_SkipButton = new GameObject("SkipButton", typeof(RectTransform), typeof(Image), typeof(Button));
            m_SkipButton.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)m_SkipButton.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-40f, 40f);
            rect.sizeDelta = new Vector2(260f, 96f);
            m_SkipButton.GetComponent<Image>().color = new Color(0.09f, 0.16f, 0.23f, 0.75f);
            m_SkipButton.GetComponent<Button>().onClick.AddListener(() => m_OnSkip?.Invoke());

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(m_SkipButton.transform, false);
            Stretch((RectTransform)label.transform);
            m_SkipLabel = label.GetComponent<TextMeshProUGUI>();
            m_SkipLabel.text = string.IsNullOrEmpty(skipLabel) ? "Omitir" : skipLabel;
            m_SkipLabel.fontSize = 36f;
            m_SkipLabel.alignment = TextAlignmentOptions.Center;
            m_SkipLabel.color = new Color(0.33f, 0.78f, 0.85f);
            m_SkipButton.SetActive(false);

            EnsureEventSystem();
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>El botón Omitir necesita un EventSystem; se crea uno solo si la escena no lo tiene.</summary>
        void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null)
                return;
            var system = new GameObject("EventSystem (Cinematics)", typeof(EventSystem));
            system.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
            system.AddComponent<InputSystemUIInputModule>();
#else
            system.AddComponent<StandaloneInputModule>();
#endif
        }

        public void SetSkip(bool visible, Action onSkip)
        {
            m_OnSkip = onSkip;
            if (m_SkipButton != null && m_SkipButton.activeSelf != visible)
                m_SkipButton.SetActive(visible);
        }

        public void SetFadeColor(Color color)
        {
            color.a = 1f;
            m_FadeImage.color = color;
        }

        public void SetFade(float alpha)
        {
            m_Fade.alpha = Mathf.Clamp01(alpha);
            m_Fade.blocksRaycasts = m_Fade.alpha > 0.99f;
        }

        /// <summary>Fundido en tiempo real (no escalado) hasta el alfa indicado.</summary>
        public IEnumerator FadeTo(float target, float duration)
        {
            float start = m_Fade.alpha;
            if (duration <= 0f)
            {
                SetFade(target);
                yield break;
            }
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                SetFade(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, time / duration)));
                yield return null;
            }
            SetFade(target);
        }
    }
}
