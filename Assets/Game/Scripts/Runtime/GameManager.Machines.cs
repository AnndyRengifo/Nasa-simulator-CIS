using System.Text;
using Sol3.Game.Art;
using Sol3.Game.Audio;
using Sol3.Game.Data;
using Sol3.Game.Minigames;
using Sol3.Game.Sim;
using Sol3.Game.UI;
using Sol3.Game.World;
using TMPro;
using UnityEngine;

namespace Sol3.Game
{
    /// <summary>Consolas de las máquinas: estado, acciones del jugador y acceso a los minijuegos de reparación.</summary>
    public sealed partial class GameManager
    {
        static readonly Vector2 MachineWindowSize = new Vector2(900f, 600f);

        void OnInteract(Machine machine)
        {
            if (m_Flow != Flow.Playing || m_Windows.AnyOpen)
                return;
            GameAudio.Instance.Play(Sfx.Click);
            var accent = machine.System.HasValue && m_Sim.State[machine.System.Value].Broken ? Palette.Alert : Palette.Cyan;
            m_Windows.Open(machine.Title, MachineWindowSize, w => BuildMachine(w, machine), true, IconOf(machine.Kind), accent);
        }

        static Icon IconOf(MachineKind kind)
        {
            switch (kind)
            {
                case MachineKind.CommsConsole: return Icon.Signal;
                case MachineKind.OxygenGenerator: return Icon.Oxygen;
                case MachineKind.WaterRecycler: return Icon.Water;
                case MachineKind.CropBed:
                case MachineKind.GreenhouseLights: return Icon.Leaf;
                case MachineKind.ResearchBench: return Icon.Flask;
                case MachineKind.BatteryBank: return Icon.Battery;
                case MachineKind.PowerPanel: return Icon.Sun;
                case MachineKind.ShelterConsole: return Icon.Shield;
                case MachineKind.RadiationMonitor: return Icon.Radiation;
                default: return Icon.Energy;
            }
        }

        void BuildMachine(Window w, Machine machine)
        {
            switch (machine.Kind)
            {
                case MachineKind.ControlTerminal: BuildTerminal(w); return;
                case MachineKind.CommsConsole: BuildComms(w); return;
                case MachineKind.CropBed: BuildCrop(w, machine.Index); return;
                case MachineKind.ResearchBench: BuildLab(w); return;
                case MachineKind.ShelterConsole: BuildShelter(w); return;
                case MachineKind.RadiationMonitor: BuildRadiation(w); return;
            }
            var body = UIKit.Scroll(w.Body, 12f);
            SystemBlock(body, w, machine.System.Value);
            switch (machine.Kind)
            {
                case MachineKind.OxygenGenerator:
                    Paragraph(body, "Produce oxígeno respirable para la tripulación. Si se detiene, el oxígeno baja de forma constante y la salud empieza a resentirse por debajo del 18 %.");
                    Science(body, "Ciencia real: en Marte, la atmósfera es sobre todo dióxido de carbono. El experimento MOXIE del rover Perseverance demostró que se puede obtener oxígeno a partir de ese CO₂.");
                    break;
                case MachineKind.WaterRecycler:
                    Paragraph(body, "Recupera el agua de la base. Sin reciclaje, el agua baja con el consumo de la tripulación y del invernadero.");
                    Science(body, "Ciencia real: en la Estación Espacial Internacional, el sistema de soporte vital recupera la mayor parte del agua a partir de la humedad del aire y de la orina.");
                    break;
                case MachineKind.GreenhouseLights:
                    Paragraph(body, "Las luces de cultivo hacen crecer las plantas. Sin luz o sin agua, los cultivos se marchitan pasados unos segundos. Apagarlas ahorra energía pero detiene el crecimiento.");
                    Science(body, "Ciencia real: los cultivos espaciales usan luces LED que emiten sobre todo en rojo y azul, los colores que más aprovechan las plantas para la fotosíntesis.");
                    break;
                case MachineKind.BatteryBank:
                    Paragraph(body, $"Carga actual: <b>{m_Sim.State.Energy:0} %</b>. Producción solar: <b>{m_Sim.Production():0.00}</b> · Consumo: <b>{m_Sim.Load():0.00}</b> unidades/s (SIMULACIÓN).\nDe noche los paneles no producen: las baterías deben durar hasta el amanecer. Si se averían, el consumo aumenta por pérdidas.");
                    break;
                case MachineKind.PowerPanel:
                    Paragraph(body, $"Luz solar ahora: <b>{m_Sim.State.Daylight * 100f:0} %</b>. Producción: <b>{m_Sim.Production():0.00}</b> unidades/s. El desgaste reduce la producción hasta que se hace mantenimiento.");
                    Science(body, "Ciencia real: el polvo marciano reduce la energía de los paneles solares. Una gran tormenta de polvo en 2018 dejó sin energía al rover Opportunity.");
                    break;
            }
        }

        // ------------------------------------------------------------------ bloques comunes

        void SystemBlock(Transform parent, Window w, SystemId id)
        {
            var s = m_Sim.State;
            var system = s[id];
            var card = Card(parent);
            var row = UIKit.Row(card, 14f, 40f);
            string state = system.Broken ? "<color=#DB6262>AVERIADO</color>" : !system.Powered ? "<color=#9FB3C4>APAGADO</color>" : s.Blackout ? "<color=#E2B45A>SIN ENERGÍA</color>" : "<color=#65C69A>OPERATIVO</color>";
            UIKit.Size(UIKit.Text(row, $"<b>{SystemNames.Title(id)}</b>   {state}", 22f), 420f, 36f, 1f);
            UIKit.Size(UIKit.Text(row, $"Integridad {system.Integrity * 100f:0} %", 18f, UIKit.Muted, TextAlignmentOptions.Right), 180f, 36f);
            var bar = UIKit.Bar(card, system.Integrity < 0.4f ? Palette.Alert : system.Integrity < 0.6f ? Palette.Amber : Palette.Green, 12f);
            bar.Set(system.Integrity);

            var active = ActiveBreakdown(id);
            if (active != null)
            {
                UIKit.Text(card, $"<color=#DB6262><b>{active.Def.title}</b></color> · gravedad {Severity(active.Def.severity)}\n{active.Def.description}", 18f);
                if (!string.IsNullOrEmpty(active.Def.science))
                    Science(card, active.Def.science);
            }

            var actions = UIKit.Row(card, 12f, 54f);
            if (id != SystemId.Solar && id != SystemId.Batteries && id != SystemId.Shelter)
            {
                UIKit.Size(UIKit.Button(actions, system.Powered ? "Apagar" : "Encender", () =>
                {
                    m_Sim.SetPower(id, !system.Powered);
                    w.Rebuild();
                }, system.Powered ? Palette.Steel : Palette.Green.Shade(0.25f), 20f, 52f, Icon.Energy), 200f, 52f);
            }
            if (m_Sim.CanRepair(id))
            {
                bool hard = s.SpareParts == 0;
                string label = system.Broken ? "Reparar" : "Mantenimiento";
                label += hard ? " (sin repuestos)" : " (1 repuesto)";
                UIKit.Size(UIKit.Button(actions, label, () => StartRepair(w, id), system.Broken ? Palette.Alert.Shade(0.15f) : Palette.Amber.Shade(0.35f), 20f, 52f, Icon.Wrench), 360f, 52f);
            }
            else
            {
                UIKit.Text(actions, "No necesita mantenimiento por ahora.", 17f, UIKit.Muted);
            }
        }

        void StartRepair(Window w, SystemId id)
        {
            var kind = id == SystemId.LifeSupport ? MinigameKind.Oxygen
                : id == SystemId.Recycler ? MinigameKind.Water
                : id == SystemId.Comms ? MinigameKind.Comms
                : MinigameKind.Circuits;
            bool hard = m_Sim.State.SpareParts == 0;
            w.Close();
            Minigame.Open(m_Windows, kind, hard, m_Seed + m_Sim.State.Repairs * 31 + m_Sim.State.FailedRepairs * 7, result =>
            {
                if (result == MinigameResult.Success)
                {
                    m_Sim.CompleteRepair(id);
                    if (id == SystemId.Comms)
                        m_Sim.State.AntennaAligned = true;
                }
                else if (result == MinigameResult.Failure)
                {
                    m_Sim.FailRepair(id);
                    Toast("AURA: La reparación ha fallado y se ha perdido algo de energía. Puedes volver a intentarlo.", Palette.Amber, Icon.Wrench);
                }
            });
        }

        ActiveEvent ActiveBreakdown(SystemId id)
        {
            foreach (var active in m_Sim.Events.Active)
                if (active.Def.kind == "breakdown" && active.Def.TargetSystem == id && active.Stage == EventStage.Active)
                    return active;
            return null;
        }

        static string Severity(int severity) => severity >= 3 ? "ALTA" : severity == 2 ? "MEDIA" : "BAJA";

        static RectTransform Card(Transform parent)
        {
            var card = UIKit.Panel(parent, UIKit.PanelLight.WithAlpha(0.45f), "Card", 2f);
            var layout = UIKit.VList(card.transform, 8f, 14);
            layout.childControlHeight = true;
            return card.rectTransform;
        }

        static TextMeshProUGUI Paragraph(Transform parent, string text, float size = 18f) => UIKit.Text(parent, text, size);

        static void Science(Transform parent, string text)
        {
            var card = UIKit.Panel(parent, Palette.Hex("#1F3B4A").WithAlpha(0.9f), "Science", 2f);
            var layout = UIKit.HList(card.transform, 12f, 12);
            layout.childAlignment = TextAnchor.UpperLeft;
            UIKit.Icon(card.transform, Icon.Book, Palette.Cyan, 26f);
            UIKit.Size(UIKit.Text(card.transform, text, 16f, Palette.Hex("#BFE9F0")), 760f, -1f, 1f);
        }

        // ------------------------------------------------------------------ terminal de control

        void BuildTerminal(Window w)
        {
            var s = m_Sim.State;
            s.VisitedControl = true;
            var columns = UIKit.Row(w.Body, 18f);
            UIKit.Size(columns, -1f, 470f);
            var left = UIKit.Rect(columns, "Left");
            UIKit.VList(left, 6f);
            UIKit.Size(left, 470f, 470f);
            var right = UIKit.Rect(columns, "Right");
            UIKit.Size(right, 350f, 470f, 1f);
            UIKit.VList(right, 0f);
            var rightContent = UIKit.Scroll(right, 10f);

            UIKit.Text(left, "SISTEMAS DE LA COLONIA", 17f, Palette.Cyan, TextAlignmentOptions.Left, FontStyles.Bold);
            foreach (SystemId id in System.Enum.GetValues(typeof(SystemId)))
            {
                var system = s[id];
                var row = UIKit.Panel(left, UIKit.PanelLight.WithAlpha(system.Broken ? 0.9f : 0.4f), "System", 3f);
                if (system.Broken) row.color = Palette.Alert.WithAlpha(0.35f);
                var h = UIKit.HList(row.transform, 8f);
                h.padding = new RectOffset(10, 8, 4, 4);
                UIKit.Size(row, -1f, 46f);
                var dot = UIKit.Plain(row.transform, system.Broken ? Palette.Alert : s.Works(id) ? Palette.Green : Palette.Steel, "Dot");
                dot.sprite = Sprites.Dot();
                UIKit.Size(dot, 14f, 14f);
                UIKit.Size(UIKit.Text(row.transform, SystemNames.Title(id), 17f), 160f, 30f);
                var bar = UIKit.Bar(row.transform, system.Integrity < 0.6f ? Palette.Amber : Palette.Green, 10f);
                bar.Set(system.Integrity);
                UIKit.Size(bar, 110f, 10f);
                bool toggle = id != SystemId.Solar && id != SystemId.Batteries && id != SystemId.Shelter;
                if (toggle)
                {
                    UIKit.Size(UIKit.Button(row.transform, system.Powered ? "ON" : "OFF", () =>
                    {
                        m_Sim.SetPower(id, !system.Powered);
                        w.Rebuild();
                    }, system.Powered ? Palette.Green.Shade(0.3f) : Palette.Steel.Shade(0.2f), 16f, 36f), 80f, 36f);
                }
                else
                {
                    UIKit.Size(UIKit.Text(row.transform, id == SystemId.Shelter ? (s.ShelterActive ? "ACTIVO" : "LISTO") : "AUTO", 14f, UIKit.Muted, TextAlignmentOptions.Center), 80f, 36f);
                }
            }

            float net = m_Sim.Production() - m_Sim.Load();
            var energy = Card(rightContent);
            UIKit.Text(energy, "BALANCE ENERGÉTICO", 16f, Palette.Cyan, TextAlignmentOptions.Left, FontStyles.Bold);
            UIKit.Text(energy, $"Producción  <b>{m_Sim.Production():0.00}</b>/s\nConsumo     <b>{m_Sim.Load():0.00}</b>/s\nBalance     <b><color={(net >= 0 ? "#65C69A" : "#DB6262")}>{net:+0.00;-0.00}</color></b>/s", 17f);
            if (net < 0f)
            {
                float seconds = s.Energy / -net;
                UIKit.Text(energy, $"Batería agotada en ~{seconds:0} s de juego si nada cambia.", 15f, Palette.Amber);
            }
            UIKit.Text(energy, "Valores del modelo del juego (SIMULACIÓN).", 13f, UIKit.Muted);

            var alerts = Card(rightContent);
            UIKit.Text(alerts, "ALERTAS ACTIVAS", 16f, Palette.Cyan, TextAlignmentOptions.Left, FontStyles.Bold);
            var sb = new StringBuilder();
            foreach (var active in m_Sim.Events.Active)
            {
                if (active.Stage == EventStage.Resolved) continue;
                sb.Append(active.Stage == EventStage.Warning ? "<color=#E2B45A>Aviso</color> · " : "<color=#DB6262>Activa</color> · ");
                sb.Append(active.Def.title);
                if (active.Def.kind == "breakdown") sb.Append($" ({RoomOf(active.Def.TargetSystem)})");
                sb.Append('\n');
            }
            UIKit.Text(alerts, sb.Length > 0 ? sb.ToString().TrimEnd() : "Sin alertas.", 16f);

            var goals = Card(rightContent);
            UIKit.Text(goals, $"OBJETIVOS DEL SOL {s.Sol}", 16f, Palette.Cyan, TextAlignmentOptions.Left, FontStyles.Bold);
            sb.Clear();
            foreach (var o in Objectives.For(m_Sim))
                sb.Append(o.Done ? "<color=#65C69A>[hecho]</color> " : "• ").Append(o.Text).Append('\n');
            UIKit.Text(goals, sb.ToString().TrimEnd(), 16f);

            var earth = Card(rightContent);
            UIKit.Text(earth, "ENLACE CON LA TIERRA", 16f, Palette.Cyan, TextAlignmentOptions.Left, FontStyles.Bold);
            UIKit.Text(earth, $"Retraso actual de la señal: <b>{m_EarthDelayMinutes:0} min</b> solo ida <color=#E2B45A>(SIMULACIÓN)</color>.\n" +
                              "<color=#65C69A>REAL:</color> según la posición de la Tierra y Marte, la luz tarda entre unos 3 y 22 minutos en llegar. Por eso la colonia debe resolver sus emergencias sin esperar órdenes.", 15f);
        }

        // ------------------------------------------------------------------ comunicaciones

        void BuildComms(Window w)
        {
            var s = m_Sim.State;
            var body = UIKit.Scroll(w.Body, 12f);
            SystemBlock(body, w, SystemId.Comms);
            var card = Card(body);
            UIKit.Text(card, "INFORME CIENTÍFICO PARA LA TIERRA", 17f, Palette.Cyan, TextAlignmentOptions.Left, FontStyles.Bold);
            string status;
            switch (s.Report)
            {
                case ReportStatus.Delivered: status = "<color=#65C69A>Recibido por Control Tierra.</color>"; break;
                case ReportStatus.Transmitting: status = $"Transmitiendo… faltan <b>{Mathf.CeilToInt(s.TransmissionRemaining)} s</b>. Si las comunicaciones fallan, se perderá."; break;
                default: status = $"Pendiente. Investigación completada: <b>{s.Research}/{m_Sim.Config.goals.research}</b>. Antena: <b>{(s.AntennaAligned ? "alineada" : "sin alinear")}</b>."; break;
            }
            UIKit.Text(card, status, 18f);
            var actions = UIKit.Row(card, 12f, 54f);
            if (s.Report == ReportStatus.NotSent)
            {
                if (!s.AntennaAligned)
                {
                    var align = UIKit.Button(actions, "Alinear antena", () =>
                    {
                        w.Close();
                        Minigame.Open(m_Windows, MinigameKind.Comms, false, m_Seed + 99, result =>
                        {
                            if (result != MinigameResult.Success) return;
                            s.AntennaAligned = true;
                            Toast("AURA: Antena alineada con la Tierra.", Palette.Green, Icon.Signal);
                        });
                    }, Palette.Steel, 20f, 52f, Icon.Signal);
                    align.interactable = s.Works(SystemId.Comms);
                    UIKit.Size(align, 260f, 52f);
                }
                var send = UIKit.Button(actions, "Enviar informe", () =>
                {
                    if (m_Sim.SendReport())
                    {
                        GameAudio.Instance.Play(Sfx.Transmit);
                        Toast("AURA: Transmisión iniciada. Mantén las comunicaciones funcionando hasta que termine.", Palette.Cyan, Icon.Signal, 8f);
                    }
                    w.Rebuild();
                }, Palette.Green.Shade(0.3f), 20f, 52f, Icon.Signal);
                send.interactable = m_Sim.CanSendReport && s.AntennaAligned;
                UIKit.Size(send, 260f, 52f);
            }
            Paragraph(body, "Para enviar el informe necesitas completar la investigación mínima, alinear la antena y que las comunicaciones funcionen durante toda la transmisión. La transmisión consume energía.");
            Science(body, $"Ciencia real: las señales de radio viajan a la velocidad de la luz y tardan entre unos 3 y 22 minutos en ir de Marte a la Tierra. En esta partida, el retraso simulado es de {m_EarthDelayMinutes:0} minutos.");
        }

        // ------------------------------------------------------------------ cultivos

        void BuildCrop(Window w, int index)
        {
            var s = m_Sim.State;
            var crop = s.Crops[index];
            var body = UIKit.Scroll(w.Body, 12f);
            var card = Card(body);
            string state = !crop.Planted ? "Vacío" : crop.Ready ? "<color=#65C69A>Listo para cosechar</color>" : $"Creciendo · {crop.Progress * 100f:0} %";
            UIKit.Text(card, $"<b>Cultivo {index + 1}</b>   {state}", 22f);
            if (crop.Planted && !crop.Ready)
            {
                UIKit.Bar(card, Palette.Green, 12f).Set(crop.Progress);
                if (!s.Works(SystemId.Greenhouse))
                    UIKit.Text(card, "Las luces del invernadero no funcionan: el cultivo no crece y se marchitará pronto.", 17f, Palette.Alert);
                else if (s.ShelterActive)
                    UIKit.Text(card, "Crecimiento en pausa mientras el refugio está activo.", 17f, Palette.Amber);
            }
            var actions = UIKit.Row(card, 12f, 54f);
            if (!crop.Planted)
            {
                var plant = UIKit.Button(actions, $"Plantar (−{m_Sim.Config.rates.plantWaterCost:0} agua)", () =>
                {
                    if (m_Sim.Plant(index))
                    {
                        GameAudio.Instance.Play(Sfx.Pickup);
                        s.Decisions.Add($"Sol {s.Sol}: plantó el cultivo {index + 1}.");
                    }
                    w.Rebuild();
                }, Palette.Green.Shade(0.3f), 20f, 52f, Icon.Leaf);
                plant.interactable = s.Water >= m_Sim.Config.rates.plantWaterCost;
                UIKit.Size(plant, 300f, 52f);
            }
            else if (crop.Ready)
            {
                UIKit.Size(UIKit.Button(actions, $"Cosechar (+{m_Sim.Config.rates.cropYield:0} comida)", () =>
                {
                    if (m_Sim.Harvest(index))
                    {
                        GameAudio.Instance.Play(Sfx.Success);
                        Toast("AURA: Cosecha almacenada en la despensa.", Palette.Green, Icon.Food);
                    }
                    w.Rebuild();
                }, Palette.Green.Shade(0.3f), 20f, 52f, Icon.Food), 320f, 52f);
            }
            Paragraph(body, $"Cada cultivo tarda unos {m_Sim.Config.rates.cropSeconds:0} s de juego en crecer si tiene luz y agua. Mientras crece, el invernadero consume agua. Valores simplificados (SIMULACIÓN).");
            Science(body, "Ciencia real: en la Estación Espacial Internacional se han cultivado y comido lechugas cultivadas a bordo con el sistema Veggie. Cultivar alimentos será clave en misiones largas a Marte.");
        }

        // ------------------------------------------------------------------ laboratorio

        void BuildLab(Window w)
        {
            var s = m_Sim.State;
            var body = UIKit.Scroll(w.Body, 12f);
            SystemBlock(body, w, SystemId.Lab);
            var card = Card(body);
            UIKit.Text(card, $"INVESTIGACIÓN · {s.Research}/{m_Sim.Config.goals.research} necesarias (hay {GameData.Experiments.Length})", 17f, Palette.Cyan, TextAlignmentOptions.Left, FontStyles.Bold);
            if (s.ExperimentIndex >= 0)
            {
                var exp = GameData.Experiments[s.ExperimentIndex];
                UIKit.Text(card, $"<b>{exp.Title}</b>\n{exp.Description}", 18f);
                if (s.ExperimentReady)
                {
                    UIKit.Size(UIKit.Button(card, "Registrar resultados", () =>
                    {
                        if (m_Sim.CollectExperiment())
                        {
                            GameAudio.Instance.Play(Sfx.Success);
                            Toast($"Investigación completada: {exp.Title}. {exp.Science}", Palette.Cyan, Icon.Flask, 10f);
                        }
                        w.Rebuild();
                    }, Palette.Green.Shade(0.3f), 20f, 52f, Icon.Flask), 320f, 52f);
                }
                else
                {
                    UIKit.Bar(card, Palette.Cyan, 12f).Set(s.ExperimentProgress);
                    string why = !s.Works(SystemId.Lab) ? " · <color=#DB6262>detenido: el laboratorio no funciona</color>" : s.ShelterActive ? " · <color=#E2B45A>en pausa: refugio activo</color>" : " · en curso (consume energía)";
                    UIKit.Text(card, $"Progreso {s.ExperimentProgress * 100f:0} %{why}", 16f, UIKit.Muted);
                }
            }
            else
            {
                for (int i = 0; i < GameData.Experiments.Length; i++)
                {
                    var exp = GameData.Experiments[i];
                    bool done = s.CompletedExperiments.Contains(i);
                    var row = UIKit.Row(card, 12f, 56f);
                    UIKit.Size(UIKit.Text(row, (done ? "<color=#65C69A>[completado]</color> " : "") + $"<b>{exp.Title}</b>\n<size=14><color=#9FB3C4>{exp.Description}</color></size>", 17f), 560f, 54f, 1f);
                    if (!done)
                    {
                        int index = i;
                        var start = UIKit.Button(row, "Iniciar", () =>
                        {
                            if (m_Sim.StartExperiment(index))
                            {
                                GameAudio.Instance.Play(Sfx.Click);
                                s.Decisions.Add($"Sol {s.Sol}: inició la investigación «{exp.Title}».");
                            }
                            w.Rebuild();
                        }, Palette.Steel, 18f, 46f, Icon.Flask);
                        start.interactable = s.Works(SystemId.Lab);
                        UIKit.Size(start, 150f, 46f);
                    }
                }
            }
            Paragraph(body, $"Cada experimento dura unos {m_Sim.Config.rates.experimentSeconds:0} s de juego y consume energía mientras está en marcha. Al completar la segunda investigación se fabrica un repuesto.");
        }

        // ------------------------------------------------------------------ refugio y radiación

        void BuildShelter(Window w)
        {
            var s = m_Sim.State;
            var storm = m_Sim.Events.CurrentStorm;
            var body = UIKit.Scroll(w.Body, 12f);
            var card = Card(body);
            UIKit.Text(card, $"<b>Protocolo de refugio</b>   {(s.ShelterActive ? "<color=#65C69A>ACTIVO</color>" : "<color=#9FB3C4>INACTIVO</color>")}", 22f);
            string stormText = storm == null ? "No hay actividad solar registrada ahora mismo."
                : storm.Stage == EventStage.Warning ? $"<color=#E2B45A>Aviso de actividad solar: llegada estimada en {Mathf.CeilToInt(storm.Timer)} s.</color>"
                : $"<color=#DB6262>Actividad solar en curso: {Mathf.CeilToInt(storm.Timer)} s restantes.</color>";
            UIKit.Text(card, stormText, 18f);
            var actions = UIKit.Row(card, 12f, 54f);
            UIKit.Size(UIKit.Button(actions, s.ShelterActive ? "Desactivar refugio" : "Activar refugio", () =>
            {
                m_Sim.SetShelter(!s.ShelterActive);
                GameAudio.Instance.Play(s.ShelterActive ? Sfx.Warning : Sfx.Click);
                if (s.ShelterActive)
                    Toast("AURA: Protocolo de refugio activado. La tripulación se dirige al Refugio.", Palette.Cyan, Icon.Shield);
                w.Rebuild();
            }, s.ShelterActive ? Palette.Steel : Palette.Amber.Shade(0.35f), 20f, 52f, Icon.Shield), 320f, 52f);
            Paragraph(body, "Con el refugio activo, la tripulación se protege de la radiación. A cambio, los experimentos y el crecimiento de los cultivos se detienen, y el refugio consume energía. Actívalo durante la actividad solar y desactívalo cuando termine.");
            Science(body, "Ciencia real: las tormentas de partículas solares son un riesgo para los astronautas fuera de la protección de la Tierra. Las misiones planean zonas con más blindaje (agua, equipos, materiales) para resguardarse.");
        }

        void BuildRadiation(Window w)
        {
            var s = m_Sim.State;
            var body = UIKit.Scroll(w.Body, 12f);
            var card = Card(body);
            UIKit.Text(card, $"<b>Índice de radiación de la base:</b> {s.Radiation:0} / 100", 22f);
            UIKit.Bar(card, s.Radiation > m_Sim.Config.rates.radiationHarm ? Palette.Alert : Palette.Mars, 12f).Set(s.Radiation / 100f);
            UIKit.Text(card, $"Por encima de {m_Sim.Config.rates.radiationHarm:0} la salud de la tripulación empieza a bajar.", 16f, UIKit.Muted);
            var tagRow = UIKit.Row(card, 8f, 26f);
            UIKit.Tag(tagRow, "SIMULACIÓN", Palette.Amber);
            UIKit.Text(card, "Este índice es un valor del juego. No es una dosis real ni se calcula a partir de la clase de una fulguración.", 15f, UIKit.Muted);

            var donki = Card(body);
            var donkiTag = UIKit.Row(donki, 8f, 26f);
            UIKit.Tag(donkiTag, "REAL · HISTÓRICO", Palette.Green);
            var record = GameData.PickSolarRecord(m_Seed);
            if (record != null)
            {
                UIKit.Text(donki, $"Registro de DONKI (NASA) consultado el {GameData.Donki.queriedAt}:\n<b>{record.type}</b> · {record.time} · {record.detail}", 17f);
                UIKit.Text(donki, "Es un evento histórico real usado como contexto; no ocurre ahora ni determina la radiación del juego.", 15f, UIKit.Muted);
            }
            else
            {
                UIKit.Text(donki, "Aún no hay registros históricos descargados. Se obtienen con el script scripts/fetch-nasa-data.mjs (ver README).", 17f);
            }
        }

        // ------------------------------------------------------------------ emergencias y decisiones

        void OpenEventWindow(EventDef def, bool warning)
        {
            if (m_Flow != Flow.Playing)
                return;
            bool storm = def.kind == "storm";
            var accent = warning ? Palette.Amber : Palette.Alert;
            m_Windows.Open((warning ? "AVISO: " : "EMERGENCIA: ") + def.title, new Vector2(860f, 560f), w =>
            {
                var body = UIKit.Scroll(w.Body, 12f);
                var card = Card(body);
                UIKit.Text(card, $"Gravedad: <b>{Severity(def.severity)}</b>", 17f, accent);
                UIKit.Text(card, warning && !string.IsNullOrEmpty(def.warning) ? def.warning : def.description, 20f);
                string todo = storm
                    ? "Qué hacer: ve al <b>Refugio</b> y activa el protocolo antes de que llegue la actividad solar. Desactívalo cuando termine."
                    : def.kind == "breakdown"
                        ? $"Qué hacer: ve a <b>{RoomOf(def.TargetSystem)}</b> y repara {SystemNames.Get(def.TargetSystem)}. Mientras siga averiado, sus efectos se acumulan."
                        : "Qué hacer: revisa la situación en el Centro de control.";
                UIKit.Text(card, todo, 18f, Palette.SoftWhite);
                if (!string.IsNullOrEmpty(def.science))
                    Science(body, def.science);
                if (storm)
                {
                    var record = GameData.PickSolarRecord(m_Seed);
                    if (record != null)
                        Science(body, $"Contexto real (histórico, DONKI): {record.type} registrado el {record.time} · {record.detail}. El evento de este juego es ficticio y su radiación es simulada.");
                }
                UIKit.Size(UIKit.Button(w.Body, "Entendido", w.Close, accent.Shade(0.25f), 22f, 56f), -1f, 56f);
            }, false, storm ? Icon.Radiation : Icon.Alert, accent);
        }

        void OpenDecisionWindow()
        {
            var pending = m_Sim.Events.PendingDecision;
            if (pending == null || m_Flow != Flow.Playing)
                return;
            var def = pending.Def;
            m_Windows.Open("DECISIÓN: " + def.title, new Vector2(900f, 600f), w =>
            {
                var body = UIKit.Scroll(w.Body, 12f);
                UIKit.Text(body, def.description, 19f);
                if (!string.IsNullOrEmpty(def.science))
                    Science(body, def.science);
                for (int i = 0; i < def.options.Length; i++)
                {
                    var option = def.options[i];
                    int index = i;
                    var card = Card(body);
                    UIKit.Text(card, $"<b>{option.label}</b>\n<size=15><color=#9FB3C4>{option.explanation}</color></size>\n<size=15>{Effects(option)}</size>", 18f);
                    UIKit.Size(UIKit.Button(card, "Elegir esta opción", () =>
                    {
                        w.Close();
                        m_Sim.Events.ResolveDecision(index);
                    }, Palette.Steel, 18f, 46f), 280f, 46f);
                }
            }, false, Icon.Alert, Palette.Amber);
        }

        static string Effects(EventOption option)
        {
            var sb = new StringBuilder("Efectos: ");
            bool any = false;
            foreach (var e in option.effects)
            {
                if (any) sb.Append(", ");
                sb.Append(StatName(e.stat)).Append(' ').Append(e.delta > 0 ? "<color=#65C69A>+" : "<color=#DB6262>").Append(e.delta.ToString("0")).Append("</color>");
                any = true;
            }
            if (!string.IsNullOrEmpty(option.powerOff) && System.Enum.TryParse(option.powerOff, true, out SystemId off))
            {
                if (any) sb.Append(", ");
                sb.Append("apaga ").Append(SystemNames.Get(off));
                any = true;
            }
            if (option.shelter)
            {
                if (any) sb.Append(", ");
                sb.Append("activa el refugio");
                any = true;
            }
            return any ? sb.ToString() : "Efectos: ninguno inmediato.";
        }

        static string StatName(string stat)
        {
            switch (stat)
            {
                case "energy": return "Energía";
                case "oxygen": return "Oxígeno";
                case "water": return "Agua";
                case "food": return "Comida";
                case "health": return "Salud";
                case "radiation": return "Radiación";
                case "parts": return "Repuestos";
                default: return stat;
            }
        }
    }
}
