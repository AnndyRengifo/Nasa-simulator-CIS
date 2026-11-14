using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sol3.Cinematics
{
    /// <summary>
    /// Banco de pruebas: simula el juego 2D (sol, energía, oxígeno y tiempo de juego que avanzan con
    /// Time.deltaTime) para comprobar que cada cinemática pausa el juego, se reproduce, se puede omitir
    /// y devuelve el control con el estado intacto. Incluye pruebas de error (identificador desconocido,
    /// escena ausente y petición duplicada). No forma parte del juego final.
    /// </summary>
    [AddComponentMenu("SOL-3/Cinemáticas/Testing/Cinematic Test Harness")]
    public sealed class CinematicTestHarness : MonoBehaviour
    {
        [SerializeField] CinematicManager manager;
        [SerializeField] SimpleCinematicGameBridge bridge;

        float m_PlayTime;
        float m_Energy = 100f;
        float m_Oxygen = 100f;
        int m_Sol = 1;
        TextMeshProUGUI m_State;
        TextMeshProUGUI m_Log;
        RectTransform m_Spinner;
        readonly List<string> m_Lines = new List<string>();

        void Start()
        {
            if (manager == null)
                manager = CinematicManager.Instance;
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var system = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
                system.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                system.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            }
            BuildUi();
            if (manager != null)
                manager.CinematicFinished += result => Write($"← {result}   (estado del juego conservado: {StateSummary()})");
            Write("Pulsa un botón para pedir una cinemática como lo haría el juego 2D.");
        }

        void Update()
        {
            // Estado simulado del juego 2D: avanza con el tiempo ESCALADO, así que se congela durante las cinemáticas.
            m_PlayTime += Time.deltaTime;
            m_Energy = Mathf.Max(0f, m_Energy - Time.deltaTime * 0.4f);
            m_Oxygen = Mathf.Max(0f, m_Oxygen - Time.deltaTime * 0.15f);
            if (m_Spinner != null)
                m_Spinner.Rotate(0f, 0f, -90f * Time.deltaTime);
            if (m_State != null)
            {
                string playing = manager != null && manager.IsPlaying ? $"  ·  CINEMÁTICA EN CURSO: {manager.CurrentCinematicId}" : string.Empty;
                m_State.text = $"Juego 2D simulado  ·  {StateSummary()}  ·  timeScale {Time.timeScale:0}{playing}";
            }
        }

        string StateSummary() => $"Sol {m_Sol} · Energía {m_Energy:0.0}% · Oxígeno {m_Oxygen:0.0}% · Tiempo {m_PlayTime:0.0} s";

        void Request(string id)
        {
            if (manager == null)
            {
                Write("No hay CinematicManager.");
                return;
            }
            bool accepted = manager.Play(id);
            Write(accepted ? $"→ Pedida '{id}' (estado al pausar: {StateSummary()})" : $"× '{id}' rechazada (ver Consola)");
            if (accepted && id == CinematicIds.Sol3Start)
                m_Sol = 3;
        }

        void BuildUi()
        {
            var canvasObject = new GameObject("HarnessCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)canvasObject.transform;

            var background = Panel(root, "Background", new Color(0.094f, 0.157f, 0.227f));
            Stretch(background, Vector2.zero, Vector2.one);

            m_State = Label(root, "State", 34f, new Color(0.91f, 0.94f, 0.96f));
            Stretch(m_State.rectTransform, new Vector2(0.03f, 0.88f), new Vector2(0.97f, 0.97f));

            var spinner = Panel(root, "GameActivity", new Color(0.33f, 0.78f, 0.85f));
            m_Spinner = spinner;
            spinner.anchorMin = spinner.anchorMax = new Vector2(0.9f, 0.6f);
            spinner.sizeDelta = new Vector2(120f, 120f);

            string[] labels =
            {
                "Nueva partida → intro", "Primera noche → sol1_night", "Evento solar → solar_alert", "Tercer sol → sol3_start",
                "Victoria → ending_success", "Derrota → ending_failure", "Final parcial → ending_partial",
            };
            for (int i = 0; i < CinematicIds.All.Count; i++)
            {
                string id = CinematicIds.All[i];
                var button = Button(root, labels[i], () => Request(id));
                PlaceButton(button, i, 0);
            }

            PlaceButton(Button(root, "Prueba: id desconocido", () => Request("id_inexistente")), 0, 1);
            PlaceButton(Button(root, "Prueba: escena ausente", PlayMissingScene), 1, 1);
            PlaceButton(Button(root, "Prueba: petición duplicada", () => { Request(CinematicIds.Sol1Night); Request(CinematicIds.Sol1Night); }), 2, 1);
            PlaceButton(Button(root, "Omitir (API)", () => Write(manager != null && manager.Skip() ? "Omitida por API" : "No se puede omitir ahora")), 3, 1);
            PlaceButton(Button(root, "Partida nueva (olvidar vistas)", () => { bridge?.ResetShownCinematics(); m_Sol = 1; Write("Registro de cinemáticas vistas reiniciado."); }), 4, 1);

            m_Log = Label(root, "Log", 26f, new Color(0.65f, 0.78f, 0.7f));
            m_Log.alignment = TextAlignmentOptions.TopLeft;
            Stretch(m_Log.rectTransform, new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.28f));
        }

        void PlayMissingScene()
        {
            // Definición temporal sin escena: debe terminar como Failed sin bloquear el juego.
            var missing = ScriptableObject.CreateInstance<CinematicDefinition>();
            missing.name = "PruebaEscenaAusente";
            bool accepted = manager != null && manager.PlayDefinition(missing, result => Write($"Prueba de escena ausente → {result.Reason}"));
            Write(accepted ? "→ Pedida una cinemática sin escena (debe fallar y volver al juego)" : "× Rechazada");
        }

        void Write(string line)
        {
            m_Lines.Insert(0, line);
            if (m_Lines.Count > 7)
                m_Lines.RemoveAt(m_Lines.Count - 1);
            if (m_Log != null)
                m_Log.text = string.Join("\n", m_Lines);
            Debug.Log("[Cinematics·Test] " + line);
        }

        static RectTransform Panel(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return (RectTransform)go.transform;
        }

        static TextMeshProUGUI Label(RectTransform parent, string name, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Left;
            return text;
        }

        static RectTransform Button(RectTransform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.188f, 0.275f, 0.365f);
            go.GetComponent<Button>().onClick.AddListener(onClick);
            var text = Label((RectTransform)go.transform, "Label", 28f, new Color(0.91f, 0.94f, 0.96f));
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            Stretch(text.rectTransform, Vector2.zero, Vector2.one);
            return (RectTransform)go.transform;
        }

        static void PlaceButton(RectTransform button, int row, int column)
        {
            float x = column == 0 ? 0.03f : 0.42f;
            float top = 0.84f - row * 0.075f;
            button.anchorMin = new Vector2(x, top - 0.065f);
            button.anchorMax = new Vector2(x + 0.36f, top);
            button.offsetMin = Vector2.zero;
            button.offsetMax = Vector2.zero;
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
