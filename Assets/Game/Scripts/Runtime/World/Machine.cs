using Sol3.Game.Art;
using Sol3.Game.Sim;
using UnityEngine;

namespace Sol3.Game.World
{
    /// <summary>
    /// Máquina interactiva de la base. Muestra su estado (luz de estado, pantalla animada, aviso de avería,
    /// plantas que crecen, nivel de batería...) y define qué sistema controla y qué minijuego lo repara.
    /// </summary>
    public sealed class Machine : MonoBehaviour
    {
        public MachineKind Kind { get; private set; }
        public RoomId Room { get; private set; }
        public int Index { get; private set; }
        public Vector2 InteractionPoint => (Vector2)transform.position + new Vector2(0f, -0.35f);

        SpriteRenderer m_Body, m_Screen, m_Status, m_Warning, m_Plants, m_Fill, m_Highlight;
        float m_Pulse;

        public SystemId? System
        {
            get
            {
                switch (Kind)
                {
                    case MachineKind.BatteryBank: return SystemId.Batteries;
                    case MachineKind.PowerPanel: return SystemId.Solar;
                    case MachineKind.OxygenGenerator: return SystemId.LifeSupport;
                    case MachineKind.WaterRecycler: return SystemId.Recycler;
                    case MachineKind.GreenhouseLights: return SystemId.Greenhouse;
                    case MachineKind.ResearchBench: return SystemId.Lab;
                    case MachineKind.CommsConsole: return SystemId.Comms;
                    case MachineKind.ShelterConsole: return SystemId.Shelter;
                    default: return null;
                }
            }
        }

        public string Title
        {
            get
            {
                switch (Kind)
                {
                    case MachineKind.ControlTerminal: return "Terminal de control";
                    case MachineKind.CommsConsole: return "Consola de comunicaciones";
                    case MachineKind.OxygenGenerator: return "Generador de oxígeno";
                    case MachineKind.WaterRecycler: return "Reciclador de agua";
                    case MachineKind.CropBed: return $"Cultivo {Index + 1}";
                    case MachineKind.GreenhouseLights: return "Panel de iluminación";
                    case MachineKind.ResearchBench: return "Mesa de investigación";
                    case MachineKind.BatteryBank: return "Banco de baterías";
                    case MachineKind.PowerPanel: return "Controlador solar";
                    case MachineKind.ShelterConsole: return "Protocolo de refugio";
                    default: return "Monitor de radiación";
                }
            }
        }

        public void Build(MachineKind kind, RoomId room, int index)
        {
            Kind = kind;
            Room = room;
            Index = index;
            int order = Sorting.ForY(transform.position.y);
            Sprite body;
            Vector2 screenPos = Vector2.zero, screenSize = Vector2.zero, statusPos;
            switch (kind)
            {
                case MachineKind.ControlTerminal: body = Sprites.Console(176); screenPos = new Vector2(0, 0.63f); screenSize = new Vector2(150, 34); statusPos = new Vector2(1.2f, 0.25f); break;
                case MachineKind.CommsConsole: body = Sprites.CommsConsole(); statusPos = new Vector2(0.75f, 0.25f); break;
                case MachineKind.OxygenGenerator: body = Sprites.OxygenGenerator(); statusPos = new Vector2(0f, 0.5f); break;
                case MachineKind.WaterRecycler: body = Sprites.WaterRecycler(); statusPos = new Vector2(-0.5f, 0.35f); break;
                case MachineKind.CropBed: body = Sprites.CropBed(); statusPos = new Vector2(0.65f, 0.1f); break;
                case MachineKind.GreenhouseLights: body = Sprites.PowerPanel(); statusPos = new Vector2(0.55f, 0.25f); break;
                case MachineKind.ResearchBench: body = Sprites.LabBench(); statusPos = new Vector2(1.1f, 0.2f); break;
                case MachineKind.BatteryBank: body = Sprites.BatteryRack(); statusPos = new Vector2(1.15f, 0.2f); break;
                case MachineKind.PowerPanel: body = Sprites.PowerPanel(); statusPos = new Vector2(0.55f, 0.25f); break;
                case MachineKind.ShelterConsole: body = Sprites.ShelterConsole(); statusPos = new Vector2(0.75f, 0.2f); break;
                default: body = Sprites.Console(80); screenPos = new Vector2(0, 0.63f); screenSize = new Vector2(56, 34); statusPos = new Vector2(0.5f, 0.25f); break;
            }

            m_Body = BaseBuilder.Sprite(transform, "Body", body, Vector2.zero, order);
            if (kind != MachineKind.CropBed)
            {
                // Base sólida para que el jugador no atraviese la máquina (los cultivos quedan libres para pasar).
                var box = gameObject.AddComponent<BoxCollider2D>();
                var bounds = body.bounds;
                box.size = new Vector2(bounds.size.x * 0.8f, 0.4f);
                box.offset = new Vector2(bounds.center.x, bounds.min.y + 0.3f);
            }
            if (screenSize != Vector2.zero)
                m_Screen = BaseBuilder.Sprite(transform, "Screen", Sprites.ScreenGlow((int)screenSize.x, (int)screenSize.y), screenPos, order + 1, false, Palette.Cyan);
            if (kind == MachineKind.CropBed)
                m_Plants = BaseBuilder.Sprite(transform, "Plants", Sprites.Plants(1), Vector2.zero, order + 1);
            if (kind == MachineKind.BatteryBank)
            {
                m_Fill = BaseBuilder.Sprite(transform, "Charge", Sprites.Dot(), new Vector2(-1.1f, 0.3f), order + 1, false, Palette.Green);
                m_Fill.drawMode = SpriteDrawMode.Simple;
            }
            m_Status = BaseBuilder.Sprite(transform, "Status", Sprites.Dot(), statusPos, order + 2, false, Palette.Green);
            m_Status.transform.localScale = Vector3.one * 0.9f;
            m_Warning = BaseBuilder.Sprite(transform, "Warning", Sprites.WarningMarker(), new Vector2(0, 1.35f), Sorting.Overlay, false);
            m_Warning.transform.localScale = Vector3.one * 0.8f;
            m_Warning.enabled = false;
            m_Highlight = BaseBuilder.Sprite(transform, "Highlight", Sprites.Soft(64), new Vector2(0, 0.4f), order - 1, false, Palette.Cyan.WithAlpha(0f));
            m_Highlight.transform.localScale = new Vector3(3.2f, 2.2f, 1f);
        }

        public void SetHighlighted(bool on) => m_Highlight.color = Palette.Cyan.WithAlpha(on ? 0.22f : 0f);

        /// <summary>Actualiza el aspecto según el estado de la simulación.</summary>
        public void Refresh(ColonySimulation sim)
        {
            var state = sim.State;
            m_Pulse += Time.deltaTime;
            bool broken = System.HasValue && state[System.Value].Broken;
            bool off = System.HasValue && !state.Works(System.Value) && !broken;
            bool attention = false;

            switch (Kind)
            {
                case MachineKind.CropBed:
                    var crop = state.Crops[Index];
                    m_Plants.enabled = crop.Planted;
                    if (crop.Planted)
                        m_Plants.sprite = Sprites.Plants(crop.Ready ? 3 : crop.Progress > 0.45f ? 2 : 1);
                    attention = crop.Ready || !crop.Planted;
                    break;
                case MachineKind.BatteryBank:
                    m_Fill.transform.localScale = new Vector3(Mathf.Max(0.05f, state.Energy / 100f) * 2.2f * 16f / 4f, 0.45f, 1f);
                    m_Fill.transform.localPosition = new Vector2(-1.15f + state.Energy / 100f * 1.1f, 0.3f);
                    m_Fill.color = state.Energy < 20f ? Palette.Alert : state.Energy < 50f ? Palette.Amber : Palette.Green;
                    break;
                case MachineKind.ResearchBench:
                    attention = state.ExperimentReady;
                    break;
                case MachineKind.CommsConsole:
                    attention = sim.CanSendReport;
                    break;
                case MachineKind.ShelterConsole:
                    attention = sim.Events.CurrentStorm != null && !state.ShelterActive;
                    break;
            }

            var statusColor = broken ? Palette.Alert : off ? Palette.Steel.Shade(0.2f) : attention ? Palette.Amber : Palette.Green;
            float blink = broken || attention ? 0.55f + 0.45f * Mathf.Sin(m_Pulse * 6f) : 1f;
            m_Status.color = statusColor.WithAlpha(blink);
            m_Warning.enabled = broken;
            if (broken)
                m_Warning.transform.localPosition = new Vector2(0f, 1.35f + Mathf.Sin(m_Pulse * 3f) * 0.06f);
            if (m_Screen != null)
            {
                var screenColor = broken ? Palette.Alert : state.StormActive ? Palette.Amber : Palette.Cyan;
                m_Screen.color = screenColor.WithAlpha(off ? 0.15f : 0.65f + 0.25f * Mathf.PerlinNoise(m_Pulse * 2f, Index));
            }
            m_Body.color = off ? new Color(0.7f, 0.7f, 0.75f) : Color.white;
        }
    }
}
