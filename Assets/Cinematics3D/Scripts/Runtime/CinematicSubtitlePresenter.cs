using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace Sol3.Cinematics
{
    /// <summary>
    /// Capa de presentación de las cinemáticas: bandas negras adaptadas al dispositivo y subtítulos
    /// (TextMeshPro) sincronizados con el tiempo del PlayableDirector. Reproduce la voz de cada
    /// línea si la tiene. Se crea por código, así que no depende de ninguna escena ni prefab.
    /// La usa la vista previa de cada escena y la usará el CinematicManager (Fase E).
    /// </summary>
    [AddComponentMenu("")]
    public sealed class CinematicSubtitlePresenter : MonoBehaviour
    {
        const float ReferenceHeight = 1080f;

        PlayableDirector m_Director;
        SubtitleSheet m_Sheet;
        float m_TargetAspect = 2f;
        RectTransform m_BarA, m_BarB, m_Panel;
        TextMeshProUGUI m_Text;
        AudioSource m_Voice;
        SubtitleLine m_Current;
        Vector2Int m_LastScreen;

        /// <summary>Relación de aspecto de composición (las bandas cubren el resto).</summary>
        public float TargetAspect
        {
            get => m_TargetAspect;
            set { m_TargetAspect = Mathf.Max(1f, value); m_LastScreen = Vector2Int.zero; }
        }

        /// <summary>Volumen de las voces (0-1).</summary>
        public float VoiceVolume { get; set; } = 1f;

        public static CinematicSubtitlePresenter Create(Transform parent, float targetAspect)
        {
            var go = new GameObject("CinematicOverlay");
            if (parent != null)
                go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, ReferenceHeight);
            scaler.matchWidthOrHeight = 1f;

            var presenter = go.AddComponent<CinematicSubtitlePresenter>();
            presenter.Build(targetAspect);
            return presenter;
        }

        public void Bind(PlayableDirector director, SubtitleSheet sheet)
        {
            m_Director = director;
            m_Sheet = sheet;
            Show(null);
        }

        void Build(float targetAspect)
        {
            m_TargetAspect = Mathf.Max(1f, targetAspect);
            m_BarA = Bar("Bar_A");
            m_BarB = Bar("Bar_B");

            var panel = new GameObject("Subtitle", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(transform, false);
            m_Panel = (RectTransform)panel.transform;
            m_Panel.pivot = new Vector2(0.5f, 0f);
            panel.GetComponent<Image>().color = new Color(0.04f, 0.07f, 0.11f, 0.6f);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 16, 18);
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(panel.transform, false);
            m_Text = text.GetComponent<TextMeshProUGUI>();
            m_Text.fontSize = 40f;
            m_Text.alignment = TextAlignmentOptions.Center;
            m_Text.color = new Color(0.91f, 0.94f, 0.96f);
            m_Text.richText = true;

            m_Voice = gameObject.AddComponent<AudioSource>();
            m_Voice.playOnAwake = false;
            m_Voice.spatialBlend = 0f;
            m_Voice.ignoreListenerPause = true;
            Show(null);
        }

        RectTransform Bar(string name)
        {
            var bar = new GameObject(name, typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(transform, false);
            bar.GetComponent<Image>().color = Color.black;
            var rect = (RectTransform)bar.transform;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        void LateUpdate()
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (screen != m_LastScreen)
            {
                m_LastScreen = screen;
                LayoutBars(screen);
            }

            if (m_Director == null || m_Sheet == null)
                return;

            var line = m_Sheet.GetLineAt(m_Director.time);
            if (line != m_Current)
                Show(line);
        }

        void LayoutBars(Vector2Int screen)
        {
            float aspect = screen.y > 0 ? screen.x / (float)screen.y : m_TargetAspect;
            float bottom = 0f;
            if (aspect < m_TargetAspect)
            {
                // Pantalla más alta que la composición: bandas arriba y abajo.
                float band = (1f - aspect / m_TargetAspect) * 0.5f;
                SetAnchors(m_BarA, new Vector2(0f, 0f), new Vector2(1f, band));
                SetAnchors(m_BarB, new Vector2(0f, 1f - band), new Vector2(1f, 1f));
                bottom = band;
            }
            else
            {
                // Pantalla más ancha: bandas a los lados.
                float band = (1f - m_TargetAspect / aspect) * 0.5f;
                SetAnchors(m_BarA, new Vector2(0f, 0f), new Vector2(band, 1f));
                SetAnchors(m_BarB, new Vector2(1f - band, 0f), new Vector2(1f, 1f));
            }

            m_Panel.anchorMin = new Vector2(0.12f, bottom + 0.035f);
            m_Panel.anchorMax = new Vector2(0.88f, bottom + 0.035f);
            m_Panel.anchoredPosition = Vector2.zero;
            m_Panel.sizeDelta = new Vector2(0f, m_Panel.sizeDelta.y);
        }

        static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        void Show(SubtitleLine line)
        {
            m_Current = line;
            if (m_Panel == null)
                return;

            bool visible = line != null && !string.IsNullOrEmpty(line.text);
            m_Panel.gameObject.SetActive(visible);
            if (m_Voice != null && m_Voice.isPlaying)
                m_Voice.Stop();
            if (!visible)
                return;

            string body = line.text;
            if (line.speaker != null)
            {
                string color = ColorUtility.ToHtmlStringRGB(line.speaker.NameColor);
                body = $"<b><color=#{color}>{line.speaker.DisplayName}</color></b>\n{line.text}";
            }
            m_Text.text = body;

            if (line.voiceClip != null && m_Voice != null)
            {
                m_Voice.clip = line.voiceClip;
                m_Voice.volume = line.voiceVolume * VoiceVolume;
                m_Voice.Play();
            }
        }
    }
}
