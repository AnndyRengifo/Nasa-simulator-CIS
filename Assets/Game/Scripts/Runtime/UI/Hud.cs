using System;
using System.Collections.Generic;
using Sol3.Game.Art;
using Sol3.Game.Player;
using Sol3.Game.Sim;
using Sol3.Game.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sol3.Game.UI
{
    /// <summary>
    /// Interfaz durante la partida: recursos, sol y hora, botones (archivo, sonido, pausa), objetivos,
    /// notificaciones de AURA, banner de alerta, indicación de interacción y minimapa.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        sealed class Chip
        {
            public string Stat;
            public TextMeshProUGUI Value;
            public Bar Bar;
            public Image Icon;
            public bool Inverse;
        }

        sealed class ToastItem
        {
            public CanvasGroup Group;
            public float Life;
        }

        readonly List<Chip> m_Chips = new List<Chip>();
        readonly List<ToastItem> m_Toasts = new List<ToastItem>();
        readonly Dictionary<RoomId, Image> m_MapRooms = new Dictionary<RoomId, Image>();
        RectTransform m_Root, m_ToastList, m_ObjectiveList, m_MapArea, m_PlayerDot;
        TextMeshProUGUI m_Sol, m_Clock, m_Status, m_Prompt, m_Banner, m_ObjectiveTitle;
        Image m_PhaseIcon, m_BannerBg, m_SoundIcon;
        Bar m_SolBar;
        CanvasGroup m_PromptGroup, m_BannerGroup;
        float m_ObjectiveTimer, m_Time;
        string m_ObjectiveKey = string.Empty;

        public Action OnPause, OnArchive, OnSound, OnTerminalHint;

        public void Build(RectTransform root)
        {
            m_Root = root;
            BuildResources();
            BuildSolPanel();
            BuildObjectives();
            BuildToasts();
            BuildBanner();
            BuildPrompt();
            BuildMinimap();
        }

        public void SetVisible(bool visible) => m_Root.gameObject.SetActive(visible);

        // ------------------------------------------------------------------ construcción

        void BuildResources()
        {
            var panel = UIKit.Panel(m_Root, UIKit.PanelColor, "Resources");
            panel.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -12f), new Vector2(800f, 66f));
            var row = UIKit.HList(panel.transform, 6f, 6);
            row.childForceExpandWidth = true;
            AddChip(panel.transform, "energy", "Energía", Icon.Energy, Palette.Amber, false);
            AddChip(panel.transform, "oxygen", "Oxígeno", Icon.Oxygen, Palette.Cyan, false);
            AddChip(panel.transform, "water", "Agua", Icon.Water, Palette.Hex("#5B9BE0"), false);
            AddChip(panel.transform, "food", "Comida", Icon.Food, Palette.Green, false);
            AddChip(panel.transform, "health", "Salud", Icon.Health, Palette.Alert, false);
            AddChip(panel.transform, "radiation", "Radiación", Icon.Radiation, Palette.Mars, true);
        }

        void AddChip(Transform parent, string stat, string label, Icon icon, Color color, bool inverse)
        {
            var cell = UIKit.Rect(parent, "Chip_" + stat);
            UIKit.Size(cell, 126f, 54f, 1f);
            var row = UIKit.Rect(cell, "Row").Stretch(4f, 4f, 2f, 16f);
            UIKit.HList(row, 6f);
            var image = UIKit.Icon(row, icon, color, 28f);
            var col = UIKit.Rect(row, "Col");
            UIKit.VList(col, -4f);
            UIKit.Size(col, -1f, -1f, 1f);
            UIKit.Text(col, label, 13f, UIKit.Muted);
            var value = UIKit.Text(col, "--", 20f, Palette.SoftWhite, TextAlignmentOptions.Left, FontStyles.Bold);
            var bar = UIKit.Bar(cell, color, 8f);
            var brt = (RectTransform)bar.transform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.offsetMin = new Vector2(4f, 2f);
            brt.offsetMax = new Vector2(-4f, 10f);
            m_Chips.Add(new Chip { Stat = stat, Value = value, Bar = bar, Icon = image, Inverse = inverse });
        }

        void BuildSolPanel()
        {
            var panel = UIKit.Panel(m_Root, UIKit.PanelColor, "SolPanel");
            panel.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -12f), new Vector2(440f, 66f));
            var row = UIKit.HList(panel.transform, 8f, 7);
            row.padding = new RectOffset(12, 8, 7, 7);
            m_PhaseIcon = UIKit.Icon(panel.transform, Icon.Sun, Palette.Amber, 34f);
            var col = UIKit.Rect(panel.transform, "Col");
            UIKit.VList(col, 2f);
            UIKit.Size(col, 150f, 52f, 1f);
            var top = UIKit.Row(col, 8f, 26f);
            m_Sol = UIKit.Text(top, "SOL 1", 22f, Palette.SoftWhite, TextAlignmentOptions.Left, FontStyles.Bold);
            m_Clock = UIKit.Text(top, "06:00", 22f, Palette.Cyan, TextAlignmentOptions.Left, FontStyles.Bold);
            m_SolBar = UIKit.Bar(col, Palette.Amber, 8f);
            UIKit.IconButton(panel.transform, Icon.Book, () => OnArchive?.Invoke());
            var sound = UIKit.IconButton(panel.transform, Icon.Sound, () => OnSound?.Invoke());
            m_SoundIcon = sound.transform.GetChild(0).GetComponent<Image>();
            UIKit.IconButton(panel.transform, Icon.Pause, () => OnPause?.Invoke());
        }

        void BuildObjectives()
        {
            var panel = UIKit.Panel(m_Root, UIKit.PanelColor, "Objectives");
            panel.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -88f), new Vector2(350f, 10f));
            var layout = UIKit.VList(panel.transform, 6f, 12);
            layout.padding = new RectOffset(14, 14, 10, 12);
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            m_ObjectiveTitle = UIKit.Text(panel.transform, "OBJETIVOS", 17f, Palette.Cyan, TextAlignmentOptions.Left, FontStyles.Bold);
            m_ObjectiveList = UIKit.Rect(panel.transform, "List");
            UIKit.VList(m_ObjectiveList, 4f);
            m_Status = UIKit.Text(panel.transform, "", 15f, UIKit.Muted);
        }

        void BuildToasts()
        {
            m_ToastList = UIKit.Rect(m_Root, "Toasts");
            m_ToastList.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -90f), new Vector2(390f, 420f));
            var layout = UIKit.VList(m_ToastList, 8f);
            layout.childAlignment = TextAnchor.UpperRight;
        }

        void BuildBanner()
        {
            m_BannerBg = UIKit.Panel(m_Root, Palette.Alert.WithAlpha(0.92f), "Banner");
            m_BannerBg.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(560f, 48f));
            m_BannerBg.raycastTarget = false;
            m_BannerGroup = m_BannerBg.gameObject.AddComponent<CanvasGroup>();
            m_BannerGroup.blocksRaycasts = false;
            var row = UIKit.HList(m_BannerBg.transform, 10f);
            row.padding = new RectOffset(14, 14, 6, 6);
            row.childAlignment = TextAnchor.MiddleCenter;
            UIKit.Icon(m_BannerBg.transform, Icon.Alert, Palette.SoftWhite, 26f);
            m_Banner = UIKit.Text(m_BannerBg.transform, "", 18f, Palette.SoftWhite, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Size(m_Banner, 480f, 36f);
            m_BannerGroup.alpha = 0f;
        }

        void BuildPrompt()
        {
            var panel = UIKit.Panel(m_Root, UIKit.PanelColor, "Prompt");
            panel.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(520f, 52f));
            panel.raycastTarget = false;
            UIKit.Outline(panel.transform, Palette.Cyan.WithAlpha(0.6f));
            m_PromptGroup = panel.gameObject.AddComponent<CanvasGroup>();
            m_PromptGroup.blocksRaycasts = false;
            m_Prompt = UIKit.Text(panel.transform, "", 20f, Palette.SoftWhite, TextAlignmentOptions.Center);
            m_Prompt.rectTransform.Stretch(10, 10, 4, 4);
            m_PromptGroup.alpha = 0f;
        }

        void BuildMinimap()
        {
            const float scale = 6f;
            var panel = UIKit.Panel(m_Root, UIKit.PanelColor, "Minimap");
            panel.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 14f), new Vector2(BaseLayout.Width * scale + 20f, BaseLayout.Height * scale + 20f));
            panel.raycastTarget = false;
            m_MapArea = UIKit.Rect(panel.transform, "Area").Stretch(10, 10, 10, 10);
            foreach (var c in BaseLayout.Corridors)
                MapRect(c, Palette.Steel, scale);
            foreach (var room in BaseLayout.Rooms)
                m_MapRooms[room.Id] = MapRect(room.Rect, room.Floor.Shade(0.15f), scale);
            m_PlayerDot = UIKit.Plain(m_MapArea, Palette.Mars, "Player").rectTransform;
            var dot = m_PlayerDot.GetComponent<Image>();
            dot.sprite = Sprites.Dot();
            m_PlayerDot.anchorMin = m_PlayerDot.anchorMax = Vector2.zero;
            m_PlayerDot.sizeDelta = new Vector2(12, 12);
        }

        Image MapRect(RectInt r, Color color, float scale)
        {
            var image = UIKit.Plain(m_MapArea, color, "R");
            image.raycastTarget = false;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(r.x * scale, r.y * scale);
            rt.sizeDelta = new Vector2(r.width * scale, r.height * scale);
            return image;
        }

        // ------------------------------------------------------------------ actualización

        public void Tick(ColonySimulation sim, PlayerController player, IReadOnlyList<Machine> machines, string alert, Color alertColor)
        {
            m_Time += Time.unscaledDeltaTime;
            var s = sim.State;
            foreach (var chip in m_Chips)
            {
                float v = s.Get(chip.Stat);
                chip.Value.text = chip.Inverse ? $"{v:0}" : $"{v:0}%";
                bool danger = chip.Inverse ? v > sim.Config.rates.radiationHarm * 0.8f : v < 20f;
                bool warn = chip.Inverse ? v > sim.Config.rates.radiationHarm * 0.5f : v < 40f;
                chip.Bar.Set(v / 100f);
                float blink = danger ? 0.55f + 0.45f * Mathf.Sin(m_Time * 8f) : 1f;
                chip.Value.color = (danger ? Palette.Alert : warn ? Palette.Amber : Palette.SoftWhite).WithAlpha(blink);
            }

            m_Sol.text = $"SOL {s.Sol}/{sim.Config.sols}";
            m_Clock.text = sim.ClockText() + (s.Phase == DayPhase.Night ? "  NOCHE" : "  DÍA");
            m_SolBar.Set(sim.SolProgress, s.Phase == DayPhase.Night ? Palette.Hex("#7C8CC4") : Palette.Amber);
            m_PhaseIcon.sprite = Sprites.Icon(s.Phase == DayPhase.Night ? Icon.Moon : Icon.Sun);
            m_PhaseIcon.color = s.Phase == DayPhase.Night ? Palette.Hex("#B9C4F0") : Palette.Amber;
            if (Audio.GameAudio.Instance != null)
                m_SoundIcon.sprite = Sprites.Icon(Audio.GameAudio.Instance.Muted ? Icon.Mute : Icon.Sound);

            m_ObjectiveTimer -= Time.unscaledDeltaTime;
            if (m_ObjectiveTimer <= 0f)
            {
                m_ObjectiveTimer = 0.5f;
                RefreshObjectives(sim);
            }

            // Banner de alerta
            bool hasAlert = !string.IsNullOrEmpty(alert);
            if (hasAlert)
            {
                m_Banner.text = alert;
                m_BannerBg.color = alertColor.WithAlpha(0.8f + 0.15f * Mathf.Sin(m_Time * 5f));
            }
            m_BannerGroup.alpha = Mathf.MoveTowards(m_BannerGroup.alpha, hasAlert ? 1f : 0f, Time.unscaledDeltaTime * 4f);

            // Indicación de interacción
            var near = player != null ? player.Nearby : null;
            if (near != null)
                m_Prompt.text = (Application.isMobilePlatform ? "<b>ACCIÓN</b>  " : "<color=#53C7D8><b>[E]</b></color>  ") + near.Title;
            m_PromptGroup.alpha = Mathf.MoveTowards(m_PromptGroup.alpha, near != null ? 1f : 0f, Time.unscaledDeltaTime * 6f);

            // Minimapa
            if (player != null)
            {
                var p = (Vector2)player.transform.position;
                m_PlayerDot.anchoredPosition = p * 6f;
                m_PlayerDot.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(m_Time * 6f));
            }
            foreach (var room in BaseLayout.Rooms)
            {
                bool broken = false;
                foreach (var m in machines)
                    if (m.Room == room.Id && m.System.HasValue && s[m.System.Value].Broken)
                        broken = true;
                var baseColor = room.Floor.Shade(0.15f);
                m_MapRooms[room.Id].color = broken ? Color.Lerp(baseColor, Palette.Alert, 0.5f + 0.5f * Mathf.Sin(m_Time * 5f)) : baseColor;
            }

            // Notificaciones
            for (int i = m_Toasts.Count - 1; i >= 0; i--)
            {
                var toast = m_Toasts[i];
                toast.Life -= Time.unscaledDeltaTime;
                toast.Group.alpha = Mathf.Clamp01(toast.Life / 0.6f);
                if (toast.Life <= 0f)
                {
                    Destroy(toast.Group.gameObject);
                    m_Toasts.RemoveAt(i);
                }
            }
        }

        void RefreshObjectives(ColonySimulation sim)
        {
            var list = Objectives.For(sim);
            var s = sim.State;
            string key = s.Sol.ToString();
            foreach (var o in list)
                key += o.Done ? "1" : "0" + o.Text;
            string report = s.Report == ReportStatus.Delivered ? "recibido" : s.Report == ReportStatus.Transmitting ? $"transmitiendo ({Mathf.CeilToInt(s.TransmissionRemaining)} s)" : s.AntennaAligned ? "antena alineada" : "pendiente";
            m_Status.text = $"Investigación {s.Research}/{sim.Config.goals.research}   ·   Repuestos {s.SpareParts}\nInforme: {report}";
            if (key == m_ObjectiveKey)
                return;
            m_ObjectiveKey = key;
            string phase = s.Sol == 1 ? "APRENDIZAJE" : s.Sol == 2 ? "DECISIONES" : "DESAFÍO FINAL";
            m_ObjectiveTitle.text = $"OBJETIVOS · SOL {s.Sol} · {phase}";
            for (int i = m_ObjectiveList.childCount - 1; i >= 0; i--)
                Destroy(m_ObjectiveList.GetChild(i).gameObject);
            foreach (var objective in list)
            {
                var row = UIKit.Row(m_ObjectiveList, 8f);
                row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
                UIKit.Icon(row, objective.Done ? Icon.Check : Icon.Alert, objective.Done ? Palette.Green : Palette.Amber.WithAlpha(0.8f), 20f);
                var text = UIKit.Text(row, objective.Done
                        ? $"<s><color=#9FB3C4>{objective.Text}</color></s>"
                        : $"{objective.Text}\n<size=13><color=#9FB3C4>{objective.Where}</color></size>", 16f);
                UIKit.Size(text, 290f);
            }
        }

        public void Toast(string text, Color color, Icon icon, float seconds = 6f)
        {
            var panel = UIKit.Panel(m_ToastList, UIKit.PanelColor, "Toast");
            panel.raycastTarget = false;
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var accent = UIKit.Plain(panel.transform, color, "Accent");
            accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            accent.rectTransform.offsetMin = new Vector2(0f, 8f);
            accent.rectTransform.offsetMax = new Vector2(5f, -8f);
            accent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var layout = UIKit.HList(panel.transform, 10f);
            layout.padding = new RectOffset(16, 12, 10, 10);
            layout.childAlignment = TextAnchor.MiddleLeft;
            UIKit.Icon(panel.transform, icon, color, 26f);
            var t = UIKit.Text(panel.transform, text, 16f);
            UIKit.Size(t, 320f);
            UIKit.Size(panel, 390f);
            panel.transform.SetAsFirstSibling();
            m_Toasts.Add(new ToastItem { Group = group, Life = seconds });
            while (m_Toasts.Count > 5)
            {
                Destroy(m_Toasts[0].Group.gameObject);
                m_Toasts.RemoveAt(0);
            }
        }
    }
}
