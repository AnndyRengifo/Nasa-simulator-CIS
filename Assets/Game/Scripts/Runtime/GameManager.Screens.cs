using System.Text;
using Sol3.Game.Art;
using Sol3.Game.Audio;
using Sol3.Game.Data;
using Sol3.Game.Sim;
using Sol3.Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sol3.Game
{
    /// <summary>Pantallas: menú principal, pausa, cómo jugar, archivo científico y pantalla final con el informe.</summary>
    public sealed partial class GameManager
    {
        // ------------------------------------------------------------------ menú principal

        void ShowMainMenu()
        {
            m_Flow = Flow.Menu;
            m_Windows.CloseAll();
            m_Queue.Clear();
            ClearMenu();
            m_HudLayer.gameObject.SetActive(false);
            GameAudio.Instance.SetNight(true);

            var bg = UIKit.Rect(m_MenuLayer, "Background").Stretch();
            var image = bg.gameObject.AddComponent<Image>();
            image.sprite = Sprites.MenuBackground();
            var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;
            var shade = UIKit.Plain(m_MenuLayer, Palette.Ink.WithAlpha(0.35f), "Shade");
            shade.rectTransform.Stretch();

            var panel = UIKit.Rect(m_MenuLayer, "Panel");
            panel.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(90f, 0f), new Vector2(520f, 620f));
            var layout = UIKit.VList(panel, 14f);
            layout.childAlignment = TextAnchor.MiddleLeft;
            UIKit.Text(panel, "SOL-3", 84f, Palette.SoftWhite, TextAlignmentOptions.Left, FontStyles.Bold);
            UIKit.Text(panel, "BASE AURORA", 34f, Palette.Cyan, TextAlignmentOptions.Left, FontStyles.Bold);
            UIKit.Text(panel, "Mantén con vida una colonia en Marte durante tres soles. Gestiona la energía, repara los sistemas, investiga y protege a la tripulación de la actividad solar.", 19f, Palette.SoftWhite.WithAlpha(0.85f));
            UIKit.Size(UIKit.Rect(panel, "Gap"), -1f, 8f);
            UIKit.Button(panel, "Nueva misión", StartNewGame, Palette.Mars.Shade(0.1f), 24f, 60f, Icon.Sun);
            UIKit.Button(panel, "Cómo jugar", OpenHowTo, Palette.Steel, 22f, 52f, Icon.Book);
            UIKit.Button(panel, "Archivo científico", OpenArchive, Palette.Steel, 22f, 52f, Icon.Flask);
            var sound = UIKit.Button(panel, SoundLabel(), null, Palette.Steel, 22f, 52f, Icon.Sound);
            sound.onClick.AddListener(() =>
            {
                ToggleSound();
                UIKit.SetButtonLabel(sound, SoundLabel());
            });
            if (Progress.Best > 0)
                UIKit.Text(panel, $"Mejor puntuación: <b>{Progress.Best}</b>", 18f, Palette.Amber);

            var footer = UIKit.Text(m_MenuLayer, "Proyecto educativo independiente. Usa datos públicos de la NASA cuando se indica como REAL; no está avalado ni certificado por la NASA. Gráficos y música generados por código para este juego.", 14f, Palette.SoftWhite.WithAlpha(0.6f), TextAlignmentOptions.Center);
            footer.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(1100f, 40f));
        }

        void ClearMenu()
        {
            for (int i = m_MenuLayer.childCount - 1; i >= 0; i--)
                Destroy(m_MenuLayer.GetChild(i).gameObject);
            if (m_HudLayer != null)
                m_HudLayer.gameObject.SetActive(true);
        }

        string SoundLabel() => GameAudio.Instance != null && GameAudio.Instance.Muted ? "Sonido: desactivado" : "Sonido: activado";

        void ToggleSound()
        {
            GameAudio.Instance.ToggleMute();
            if (m_Flow == Flow.Playing)
                Toast(GameAudio.Instance.Muted ? "Sonido desactivado." : "Sonido activado.", Palette.Cyan, GameAudio.Instance.Muted ? Icon.Mute : Icon.Sound, 2.5f);
        }

        // ------------------------------------------------------------------ pausa

        void OpenPause()
        {
            if (m_Flow != Flow.Playing || m_Windows.AnyOpen)
                return;
            m_Windows.Open("Pausa", new Vector2(520f, 560f), w =>
            {
                UIKit.Text(w.Body, $"Sol {m_Sim.State.Sol} · {m_Sim.ClockText()} · La simulación está detenida.", 17f, UIKit.Muted);
                UIKit.Button(w.Body, "Continuar", w.Close, Palette.Green.Shade(0.3f), 22f, 56f);
                UIKit.Button(w.Body, "Cómo jugar", () => { w.Close(); OpenHowTo(); }, Palette.Steel, 20f, 50f, Icon.Book);
                UIKit.Button(w.Body, "Archivo científico", () => { w.Close(); OpenArchive(); }, Palette.Steel, 20f, 50f, Icon.Flask);
                var sound = UIKit.Button(w.Body, SoundLabel(), null, Palette.Steel, 20f, 50f, Icon.Sound);
                sound.onClick.AddListener(() =>
                {
                    ToggleSound();
                    UIKit.SetButtonLabel(sound, SoundLabel());
                });
                UIKit.Button(w.Body, "Reiniciar misión", StartNewGame, Palette.Amber.Shade(0.4f), 20f, 50f);
                UIKit.Button(w.Body, "Menú principal", ShowMainMenu, Palette.Steel.Shade(0.2f), 20f, 50f);
            }, true, Icon.Pause);
        }

        // ------------------------------------------------------------------ cómo jugar

        void OpenHowTo()
        {
            m_Windows.Open("Cómo jugar", new Vector2(920f, 640f), w =>
            {
                var body = UIKit.Scroll(w.Body, 10f);
                Section(body, "Objetivo", "Sobrevive tres soles (días marcianos) en Base Aurora. Para ganar: la tripulación debe seguir a salvo, completar al menos 3 investigaciones y la Tierra debe recibir tu informe científico. Si la salud llega a cero, la misión fracasa.");
                Section(body, "Controles", "Ordenador: <b>WASD</b> o <b>flechas</b> para moverte, <b>E</b> (o Espacio) para interactuar, <b>Esc</b> o <b>P</b> para pausar.\nMóvil: joystick a la izquierda y botón <b>ACCIÓN</b> a la derecha. Mando: stick izquierdo y botón A.");
                Section(body, "Recursos", "<b>Energía</b>: la producen los paneles de día y se guarda en baterías. <b>Oxígeno</b> y <b>agua</b>: dependen del soporte vital. <b>Comida</b>: se cosecha en el invernadero. <b>Salud</b>: baja si falta oxígeno, agua, comida o hay mucha radiación. <b>Radiación</b>: sube durante la actividad solar si la tripulación no está en el refugio.");
                Section(body, "Los tres soles", "Sol 1: aprendizaje guiado por AURA. Sol 2: decisiones y la primera actividad solar. Sol 3: desafío final con más emergencias. De noche no hay producción solar: planifica la batería.");
                Section(body, "Efecto dominó", "Los equipos se desgastan. Si dejas que la batería baje o no haces mantenimiento, aumentan las probabilidades de nuevas averías. Una avería sin reparar empeora los recursos y puede provocar otras.");
                Section(body, "Reparaciones", "Las máquinas con luz roja están averiadas y las de luz ámbar necesitan atención. Reparar abre un minijuego: circuitos, calibración de oxígeno, reciclaje de agua o alineación de comunicaciones. Cada reparación usa un repuesto; sin repuestos es más difícil.");
                Section(body, "La simulación se detiene", "Mientras usas una consola, un minijuego o una ventana, el tiempo de la colonia se detiene para que puedas leer y decidir con calma.");
                Section(body, "Datos REALES y SIMULACIÓN", "Todo lo marcado como <color=#65C69A>REAL</color> procede de fuentes verificables (con enlace en el Archivo científico). Lo marcado como <color=#E2B45A>SIMULACIÓN</color> son valores del modelo del juego, simplificados para que sea jugable.");
            }, true, Icon.Book);
        }

        static void Section(Transform parent, string title, string text)
        {
            var card = Card(parent);
            UIKit.Text(card, title.ToUpperInvariant(), 16f, Palette.Cyan, TextAlignmentOptions.Left, FontStyles.Bold);
            UIKit.Text(card, text, 17f);
        }

        // ------------------------------------------------------------------ archivo científico

        void OpenArchive()
        {
            if (m_Windows.AnyOpen && m_Flow == Flow.Playing)
                return;
            m_Windows.Open("Archivo científico", new Vector2(980f, 660f), w =>
            {
                var body = UIKit.Scroll(w.Body, 10f);
                var archive = GameData.Archive;
                if (!string.IsNullOrEmpty(archive.disclaimer))
                    UIKit.Text(body, archive.disclaimer, 16f, UIKit.Muted);
                foreach (var c in archive.cards)
                {
                    var card = Card(body);
                    var head = UIKit.Row(card, 10f, 28f);
                    bool real = c.category == "REAL";
                    UIKit.Tag(head, real ? "REAL" : "SIMULACIÓN", real ? Palette.Green : Palette.Amber);
                    UIKit.Size(UIKit.Text(head, $"<b>{c.title}</b>", 19f), 700f, 28f, 1f);
                    UIKit.Text(card, c.summary, 16f);
                    if (!string.IsNullOrEmpty(c.usedInGame))
                        UIKit.Text(card, "<b>En el juego:</b> " + c.usedInGame, 15f, Palette.Hex("#BFE9F0"));
                    if (!string.IsNullOrEmpty(c.source))
                    {
                        var foot = UIKit.Row(card, 10f, 40f);
                        UIKit.Size(UIKit.Text(foot, "Fuente: " + c.source, 14f, UIKit.Muted), 640f, 36f, 1f);
                        if (!string.IsNullOrEmpty(c.url))
                        {
                            string url = c.url;
                            UIKit.Size(UIKit.Button(foot, "Abrir enlace", () => Application.OpenURL(url), Palette.Steel, 15f, 36f), 160f, 36f);
                        }
                    }
                }

                var donki = GameData.Donki;
                var dcard = Card(body);
                var dhead = UIKit.Row(dcard, 10f, 28f);
                UIKit.Tag(dhead, "REAL · HISTÓRICO", Palette.Green);
                UIKit.Text(dhead, "<b>Registros de clima espacial (DONKI, NASA)</b>", 19f);
                if (donki.HasRecords)
                {
                    UIKit.Text(dcard, $"Consultados el {donki.queriedAt} para el periodo {donki.startDate} – {donki.endDate}. No son datos en tiempo real. {donki.note}", 15f, UIKit.Muted);
                    var sb = new StringBuilder();
                    int count = 0;
                    foreach (var r in donki.records)
                    {
                        sb.Append("<b>").Append(r.type).Append("</b>  ").Append(r.time).Append("  ·  ").Append(r.detail).Append('\n');
                        if (++count >= 14) break;
                    }
                    UIKit.Text(dcard, sb.ToString().TrimEnd(), 15f);
                }
                else
                {
                    UIKit.Text(dcard, "No hay registros descargados en esta versión. Para obtenerlos, ejecuta el script <b>scripts/fetch-nasa-data.mjs</b> con tu clave NASA_API_KEY (ver README). El juego funciona igual sin ellos.", 15f);
                }
            }, true, Icon.Book);
        }

        // ------------------------------------------------------------------ pantalla final

        void ShowEndScreen()
        {
            m_Windows.CloseAll();
            var s = m_Sim.State;
            int score = Progress.Score(m_Sim);
            var unlocked = Progress.Record(m_Sim, score, out bool best);
            string title = s.Outcome == MissionOutcome.Success ? "MISIÓN CUMPLIDA" : s.Outcome == MissionOutcome.Partial ? "MISIÓN PARCIAL" : "MISIÓN FALLIDA";
            var accent = s.Outcome == MissionOutcome.Success ? Palette.Green : s.Outcome == MissionOutcome.Partial ? Palette.Amber : Palette.Alert;
            m_Windows.Open(title, new Vector2(1040f, 660f), w =>
            {
                var columns = UIKit.Row(w.Body, 18f);
                UIKit.Size(columns, -1f, 470f);
                var left = UIKit.Rect(columns, "Left");
                UIKit.VList(left, 10f);
                UIKit.Size(left, 470f, 470f);
                var right = UIKit.Rect(columns, "Right");
                UIKit.VList(right, 0f);
                UIKit.Size(right, 480f, 470f, 1f);
                var rightContent = UIKit.Scroll(right, 10f);

                UIKit.Text(left, s.OutcomeReason, 19f);
                UIKit.Text(left, $"Puntuación: <b>{score}</b>" + (best ? "  <color=#E2B45A>¡Nuevo récord!</color>" : $"   <color=#9FB3C4>(récord {Progress.Best})</color>"), 26f, accent);
                var stats = Card(left);
                float solsDone = Mathf.Min(s.Time, m_Sim.MissionSeconds) / m_Sim.Config.solSeconds;
                UIKit.Text(stats, "INFORME DE LA MISIÓN", 16f, Palette.Cyan, TextAlignmentOptions.Left, FontStyles.Bold);
                UIKit.Text(stats,
                    $"Soles completados: <b>{solsDone:0.0} / {m_Sim.Config.sols}</b>\n" +
                    $"Investigación: <b>{s.Research} / {m_Sim.Config.goals.research}</b>   ·   Informe: <b>{(s.Report == ReportStatus.Delivered ? "recibido" : "no recibido")}</b>\n" +
                    $"Reparaciones: <b>{s.Repairs}</b> (fallidas {s.FailedRepairs})   ·   Cosechas: <b>{s.Harvests}</b>\n" +
                    $"Emergencias: <b>{s.EventsHandled}</b>   ·   Apagones: <b>{s.Blackouts}</b>\n" +
                    $"Actividad solar: <b>{s.StormsSheltered}</b> protegida(s), <b>{s.StormsUnsheltered}</b> sin protección\n" +
                    $"Mínimos: energía {s.MinEnergy:0} % · oxígeno {s.MinOxygen:0} % · agua {s.MinWater:0} % · comida {s.MinFood:0} %\n" +
                    $"Salud final: <b>{s.Health:0} %</b>   ·   Radiación máxima: <b>{s.MaxRadiation:0}</b> (índice simulado)", 16f);
                var buttons = UIKit.Row(left, 12f, 56f);
                UIKit.Size(UIKit.Button(buttons, "Volver a jugar", StartNewGame, Palette.Mars.Shade(0.1f), 20f, 54f), 220f, 54f);
                UIKit.Size(UIKit.Button(buttons, "Menú principal", ShowMainMenu, Palette.Steel, 20f, 54f), 220f, 54f);

                var ach = Card(rightContent);
                UIKit.Text(ach, "LOGROS", 16f, Palette.Cyan, TextAlignmentOptions.Left, FontStyles.Bold);
                var sb = new StringBuilder();
                foreach (var a in Progress.Achievements)
                {
                    bool now = unlocked.Contains(a);
                    bool has = Progress.IsUnlocked(a.Id);
                    sb.Append(has ? (now ? "<color=#E2B45A>NUEVO</color> " : "<color=#65C69A>[sí]</color> ") : "<color=#5A6B7C>[no]</color> ")
                      .Append("<b>").Append(a.Title).Append("</b> <color=#9FB3C4>").Append(a.Description).Append("</color>\n");
                }
                UIKit.Text(ach, sb.ToString().TrimEnd(), 15f);

                var decisions = Card(rightContent);
                UIKit.Text(decisions, "DECISIONES CLAVE", 16f, Palette.Cyan, TextAlignmentOptions.Left, FontStyles.Bold);
                sb.Clear();
                int start = Mathf.Max(0, s.Decisions.Count - 12);
                for (int i = start; i < s.Decisions.Count; i++)
                    sb.Append("• ").Append(s.Decisions[i]).Append('\n');
                UIKit.Text(decisions, sb.Length > 0 ? sb.ToString().TrimEnd() : "Sin decisiones registradas.", 15f);
                UIKit.Text(rightContent, "Los valores de recursos, radiación y eventos son una SIMULACIÓN educativa.", 13f, UIKit.Muted);
            }, false, s.Outcome == MissionOutcome.Failure ? Icon.Alert : Icon.Check, accent);
        }
    }
}
