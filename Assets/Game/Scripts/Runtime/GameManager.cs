using System;
using System.Collections.Generic;
using Sol3.Cinematics;
using Sol3.Game.Art;
using Sol3.Game.Audio;
using Sol3.Game.Data;
using Sol3.Game.Player;
using Sol3.Game.Sim;
using Sol3.Game.UI;
using Sol3.Game.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Sol3.Game
{
    /// <summary>
    /// Punto de entrada del juego. Crea el mundo, la interfaz y el audio; avanza la simulación;
    /// traduce los mensajes de la simulación en avisos, ventanas, sonidos y cinemáticas; y gestiona
    /// el flujo menú → cinemática → partida → final. Implementa el puente con el sistema de cinemáticas.
    /// </summary>
    public sealed partial class GameManager : MonoBehaviour, ICinematicGameBridge
    {
        enum Flow { Menu, Playing, Cinematic, Ended }

        [Tooltip("Catálogo de cinemáticas (Assets/Cinematics3D/Config/CinematicCatalog.asset).")]
        [SerializeField] CinematicCatalog cinematicCatalog;
        [Tooltip("Reproducir las cinemáticas 3D en los momentos clave.")]
        [SerializeField] bool playCinematics = true;
        [Tooltip("Semilla fija (0 = aleatoria).")]
        [SerializeField] int fixedSeed;

        public static GameManager Instance { get; private set; }

        Flow m_Flow = Flow.Menu;
        ColonySimulation m_Sim;
        BaseBuilder m_Base;
        PlayerController m_Player;
        readonly List<CrewMember> m_Crew = new List<CrewMember>();
        Atmosphere m_Atmosphere;
        Camera m_Camera;
        CameraFollow m_Follow;
        RectTransform m_HudLayer, m_WindowLayer, m_MenuLayer;
        Hud m_Hud;
        WindowManager m_Windows;
        TouchControls m_Touch;
        readonly HashSet<string> m_ShownCinematics = new HashSet<string>();
        readonly Queue<Action> m_Queue = new Queue<Action>();
        int m_Seed;
        int m_TutorialStep;
        float m_TutorialTimer;
        float m_EarthDelayMinutes;

        public ColonySimulation Simulation => m_Sim;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            GameData.Load();
            GameAudio.Create();
        }

        void Start()
        {
            SetupCamera();
            m_Base = new GameObject("BaseAurora").AddComponent<BaseBuilder>();
            m_Base.Build();
            m_Atmosphere = m_Base.gameObject.AddComponent<Atmosphere>();
            m_Atmosphere.Init(m_Base, m_Camera);
            var control = BaseLayout.Room(RoomId.Control);
            m_Player = PlayerController.Create(control.At(5.5f, 2.5f), m_Base.Machines.ToArray());
            m_Player.Interacted += OnInteract;
            m_Follow.Target = m_Player.transform;
            m_Follow.SnapToTarget();
            var crewRoot = new GameObject("Crew").transform;
            m_Crew.Add(CrewMember.Create(crewRoot, "Cmdte. Ríos", Palette.Cyan, new[] { RoomId.Control, RoomId.Lab, RoomId.Engine }, 1));
            m_Crew.Add(CrewMember.Create(crewRoot, "Ing. Okafor", Palette.Amber, new[] { RoomId.Engine, RoomId.LifeSupport, RoomId.Control }, 2));
            m_Crew.Add(CrewMember.Create(crewRoot, "Dra. Lindqvist", Palette.Green, new[] { RoomId.Greenhouse, RoomId.Lab, RoomId.Shelter }, 3));

            BuildCanvas();
            SetupCinematics();
            m_Sim = NewSimulation();
            ShowMainMenu();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (CinematicManager.Instance != null && ReferenceEquals(CinematicManager.Instance.Bridge, this))
                CinematicManager.Instance.Bridge = null;
        }

        // ================================================================== Preparación

        void SetupCamera()
        {
            m_Camera = Camera.main;
            if (m_Camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                m_Camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            m_Camera.orthographic = true;
            m_Camera.clearFlags = CameraClearFlags.SolidColor;
            m_Camera.backgroundColor = Palette.Hex("#0B0F18");
            if (CinematicLayers.Exists)
                m_Camera.cullingMask &= ~CinematicLayers.Mask;
            var data = m_Camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null)
                data = m_Camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.SetRenderer(0);
            data.renderPostProcessing = false;
            m_Follow = m_Camera.GetComponent<CameraFollow>();
            if (m_Follow == null)
                m_Follow = m_Camera.gameObject.AddComponent<CameraFollow>();
        }

        void BuildCanvas()
        {
            var canvasGo = new GameObject("GameCanvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.AddComponent<InputSystemUIInputModule>();
            }

            m_HudLayer = UIKit.Rect(root, "HUD").Stretch();
            m_Hud = m_HudLayer.gameObject.AddComponent<Hud>();
            m_Hud.Build(m_HudLayer);
            m_Hud.OnPause = OpenPause;
            m_Hud.OnArchive = OpenArchive;
            m_Hud.OnSound = ToggleSound;
            m_Touch = m_HudLayer.gameObject.AddComponent<TouchControls>();
            m_Touch.Build(m_HudLayer);

            m_MenuLayer = UIKit.Rect(root, "Menu").Stretch();
            m_WindowLayer = UIKit.Rect(root, "Windows").Stretch();
            m_Windows = m_WindowLayer.gameObject.AddComponent<WindowManager>();
            m_Windows.Init(m_WindowLayer);
        }

        void SetupCinematics()
        {
            var manager = CinematicManager.Instance;
            if (manager == null && cinematicCatalog != null)
                manager = CinematicManager.Create(cinematicCatalog, this);
            if (manager != null)
                manager.Bridge = this;
        }

        ColonySimulation NewSimulation()
        {
            m_Seed = fixedSeed != 0 ? fixedSeed : Environment.TickCount & 0x7fffffff;
            var rng = new System.Random(m_Seed);
            m_EarthDelayMinutes = 5f + (float)rng.NextDouble() * 15f;
            return new ColonySimulation(GameData.Config, GameData.Events, m_Seed);
        }

        // ================================================================== Flujo

        public void StartNewGame()
        {
            m_Windows.CloseAll();
            m_Queue.Clear();
            ClearMenu();
            m_Sim = NewSimulation();
            m_ShownCinematics.Clear();
            m_TutorialStep = 0;
            m_TutorialTimer = 2f;
            m_Player.Teleport(BaseLayout.Room(RoomId.Control).At(5.5f, 2.5f));
            m_Follow.SnapToTarget();
            GameAudio.Instance.SetNight(false);
            m_Flow = Flow.Playing;
            PlayCinematic(CinematicIds.Intro, () =>
            {
                Toast("AURA: Bienvenida a Base Aurora. Soy tu asistente de la colonia. Ve al Centro de control y usa el terminal.", Palette.Cyan, Icon.Signal, 9f);
                GameAudio.Instance.Play(Sfx.Day);
            });
        }

        void Update()
        {
            HandleBackKey();
            bool modal = m_Windows.AnyOpen;
            bool playing = m_Flow == Flow.Playing;

            if (playing && !modal)
            {
                float dt = Time.deltaTime;
                m_Sim.Tick(dt);
                foreach (var crew in m_Crew)
                    crew.Tick(m_Sim, dt);
                ProcessMessages();
                Tutorial(dt);
            }
            else if (playing)
            {
                ProcessMessages();
            }

            if (playing && !m_Windows.AnyOpen && m_Queue.Count > 0)
                m_Queue.Dequeue()?.Invoke();

            m_Player.InputEnabled = playing && !m_Windows.AnyOpen;
            if (!m_Player.InputEnabled)
                PlayerController.TouchMove = Vector2.zero;
            m_Touch.SetVisible(playing && !m_Windows.AnyOpen);

            if (m_Sim != null)
            {
                m_Atmosphere.Tick(m_Sim, Time.unscaledDeltaTime);
                foreach (var machine in m_Base.Machines)
                    machine.Refresh(m_Sim);
                if (m_Flow != Flow.Menu)
                {
                    var alert = CurrentAlert(out var color);
                    m_Hud.Tick(m_Sim, m_Player, m_Base.Machines, alert, color);
                }
                GameAudio.Instance.SetNight(m_Sim.State.Phase == DayPhase.Night);
            }
        }

        void HandleBackKey()
        {
            var k = Keyboard.current;
            bool back = k != null && (k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame && m_Flow == Flow.Playing && !m_Windows.AnyOpen);
            if (!back)
                return;
            if (m_Windows.CloseTop())
                return;
            if (m_Flow == Flow.Playing && !m_Windows.AnyOpen)
                OpenPause();
        }

        string CurrentAlert(out Color color)
        {
            var s = m_Sim.State;
            color = Palette.Alert;
            var storm = m_Sim.Events.CurrentStorm;
            if (storm != null && storm.Stage == EventStage.Active && !s.ShelterActive)
                return "ACTIVIDAD SOLAR: ¡lleva a la tripulación al Refugio!";
            if (storm != null && storm.Stage == EventStage.Warning)
            {
                color = Palette.Amber;
                return $"Aviso de actividad solar · llegada estimada en {Mathf.CeilToInt(storm.Timer)} s";
            }
            if (storm != null && storm.Stage == EventStage.Active)
            {
                color = Palette.Amber;
                return $"Refugio activo · actividad solar durante {Mathf.CeilToInt(storm.Timer)} s";
            }
            if (s.Blackout)
                return "APAGÓN: reduce el consumo hasta que vuelva la energía";
            foreach (var system in s.Systems.Values)
            {
                if (system.Broken)
                    return $"AVERÍA: {SystemNames.Title(system.Id)} · {RoomOf(system.Id)}";
            }
            if (s.Oxygen < 20f) return "Oxígeno crítico";
            if (s.Energy < 15f) { color = Palette.Amber; return "Batería muy baja"; }
            return null;
        }

        public static string RoomOf(SystemId id)
        {
            switch (id)
            {
                case SystemId.Solar:
                case SystemId.Batteries: return "Sala de máquinas";
                case SystemId.LifeSupport:
                case SystemId.Recycler: return "Soporte vital";
                case SystemId.Greenhouse: return "Invernadero";
                case SystemId.Lab: return "Laboratorio";
                case SystemId.Comms: return "Centro de control";
                default: return "Refugio";
            }
        }

        // ================================================================== Mensajes de la simulación

        void ProcessMessages()
        {
            if (m_Sim.Messages.Count == 0)
                return;
            var messages = m_Sim.Messages.ToArray();
            m_Sim.Messages.Clear();
            foreach (var m in messages)
                Handle(m);
        }

        void Handle(SimMessage m)
        {
            var s = m_Sim.State;
            switch (m.Kind)
            {
                case SimMessageKind.SolStarted:
                    GameAudio.Instance.Play(Sfx.Day);
                    if (s.Sol == 3)
                        PlayCinematic(CinematicIds.Sol3Start, () => EarthMessage("Último sol de la misión. Completad la investigación y enviad el informe antes del final."));
                    else
                        EarthMessage(s.Sol == 2
                            ? "Buen trabajo en el primer sol. Hoy necesitamos avances en la investigación. Atentos a la actividad solar."
                            : "Comienza la misión.");
                    break;
                case SimMessageKind.NightStarted:
                    GameAudio.Instance.Play(Sfx.Night);
                    if (s.Sol == 1)
                        PlayCinematic(CinematicIds.Sol1Night, () => Toast("AURA: " + m.Text + " Vigila la batería: hay que llegar al amanecer.", Palette.Hex("#9AA8E0"), Icon.Moon));
                    else
                        Toast("AURA: " + m.Text, Palette.Hex("#9AA8E0"), Icon.Moon);
                    break;
                case SimMessageKind.DayStarted:
                    GameAudio.Instance.Play(Sfx.Day);
                    Toast("AURA: " + m.Text, Palette.Amber, Icon.Sun);
                    break;
                case SimMessageKind.Blackout:
                    GameAudio.Instance.Play(Sfx.Alarm);
                    m_Atmosphere.PulseAlarm(4f);
                    m_Follow.Shake(1f);
                    Toast("AURA: " + m.Text, Palette.Alert, Icon.Battery);
                    break;
                case SimMessageKind.PowerRestored:
                    GameAudio.Instance.Play(Sfx.Success);
                    Toast("AURA: " + m.Text, Palette.Green, Icon.Energy);
                    break;
                case SimMessageKind.EventWarning:
                    GameAudio.Instance.Play(Sfx.Warning);
                    var warned = FindEvent(m.Id);
                    if (warned != null && warned.kind == "storm")
                        PlayCinematic(CinematicIds.SolarAlert, () => m_Queue.Enqueue(() => OpenEventWindow(warned, true)));
                    else
                        Toast("AURA: " + m.Text, Palette.Amber, Icon.Alert);
                    break;
                case SimMessageKind.EventStarted:
                    GameAudio.Instance.Play(Sfx.Alarm);
                    m_Atmosphere.PulseAlarm(3f);
                    m_Follow.Shake(0.6f);
                    var started = FindEvent(m.Id);
                    if (started != null)
                        m_Queue.Enqueue(() => OpenEventWindow(started, false));
                    break;
                case SimMessageKind.DecisionRequired:
                    GameAudio.Instance.Play(Sfx.Warning);
                    m_Queue.Enqueue(OpenDecisionWindow);
                    break;
                case SimMessageKind.StormStarted:
                    GameAudio.Instance.Play(Sfx.Alarm);
                    m_Atmosphere.PulseAlarm(5f);
                    m_Follow.Shake(0.8f);
                    Toast("AURA: La actividad solar ha llegado. " + (s.ShelterActive ? "El refugio está activo." : "¡Activa el protocolo en el Refugio!"), Palette.Alert, Icon.Radiation, 8f);
                    break;
                case SimMessageKind.StormEnded:
                    GameAudio.Instance.Play(Sfx.Success);
                    Toast("AURA: " + m.Text + " Puedes desactivar el refugio.", Palette.Green, Icon.Shield, 8f);
                    break;
                case SimMessageKind.EventResolved:
                    GameAudio.Instance.Play(Sfx.Success);
                    Toast(m.Text, Palette.Green, Icon.Check);
                    break;
                case SimMessageKind.CropReady:
                    GameAudio.Instance.Play(Sfx.Pickup);
                    Toast("AURA: " + m.Text, Palette.Green, Icon.Leaf);
                    break;
                case SimMessageKind.CropWithered:
                    GameAudio.Instance.Play(Sfx.Fail);
                    Toast("AURA: " + m.Text, Palette.Amber, Icon.Leaf);
                    break;
                case SimMessageKind.ExperimentReady:
                    GameAudio.Instance.Play(Sfx.Pickup);
                    Toast("AURA: " + m.Text, Palette.Cyan, Icon.Flask);
                    break;
                case SimMessageKind.ReportDelivered:
                    GameAudio.Instance.Play(Sfx.Success);
                    EarthMessage("Informe científico recibido. Excelente trabajo. Os enviamos instrucciones para 2 repuestos adicionales.");
                    break;
                case SimMessageKind.ReportLost:
                    GameAudio.Instance.Play(Sfx.Fail);
                    Toast("AURA: " + m.Text, Palette.Alert, Icon.Signal, 8f);
                    break;
                case SimMessageKind.LowResource:
                    GameAudio.Instance.Play(Sfx.Warning);
                    Toast("AURA: " + m.Text, Palette.Amber, IconFor(m.Id));
                    break;
                case SimMessageKind.MissionEnded:
                    EndMission();
                    break;
                default:
                    if (!string.IsNullOrEmpty(m.Text))
                        Toast("AURA: " + m.Text, Palette.Cyan, Icon.Signal);
                    break;
            }
        }

        static Icon IconFor(string stat)
        {
            switch (stat)
            {
                case "energy": return Icon.Battery;
                case "oxygen": return Icon.Oxygen;
                case "water": return Icon.Water;
                case "food": return Icon.Food;
                case "radiation": return Icon.Radiation;
                default: return Icon.Alert;
            }
        }

        EventDef FindEvent(string id)
        {
            foreach (var def in m_Sim.Events.Definitions)
                if (def.id == id)
                    return def;
            return null;
        }

        void EarthMessage(string text)
        {
            Toast($"<b>Control Tierra</b> <color=#9FB3C4>(enviado hace {m_EarthDelayMinutes:0} min)</color>\n{text}", Palette.Hex("#8FB8FF"), Icon.Signal, 9f);
            GameAudio.Instance.Play(Sfx.Transmit, 0.6f);
        }

        public void Toast(string text, Color color, Icon icon, float seconds = 6.5f) => m_Hud.Toast(text, color, icon, seconds);

        // ================================================================== Tutorial (Sol 1: aprendizaje)

        void Tutorial(float dt)
        {
            var s = m_Sim.State;
            if (s.Sol != 1 || m_TutorialStep > 4)
                return;
            m_TutorialTimer -= dt;
            if (m_TutorialTimer > 0f)
                return;
            switch (m_TutorialStep)
            {
                case 0:
                    if (s.VisitedControl)
                    {
                        Toast("AURA: Muévete con WASD o las flechas. Las máquinas con luz ámbar necesitan atención; las rojas están averiadas.", Palette.Cyan, Icon.Signal, 8f);
                        Advance(14f);
                    }
                    break;
                case 1:
                    if (s.Repairs > 0)
                    {
                        Toast("AURA: Reparación completada. Ahora ve al Invernadero y planta un cultivo para asegurar comida.", Palette.Cyan, Icon.Leaf, 8f);
                        Advance(10f);
                    }
                    break;
                case 2:
                    bool planted = false;
                    foreach (var c in s.Crops) planted |= c.Planted;
                    if (planted || s.Harvests > 0)
                    {
                        Toast("AURA: Empieza una investigación en el Laboratorio. Consume energía: mejor de día, cuando producen los paneles.", Palette.Cyan, Icon.Flask, 8f);
                        Advance(15f);
                    }
                    break;
                case 3:
                    Toast("AURA: Antes de la noche, apaga lo que no necesites desde el terminal del Centro de control para ahorrar batería.", Palette.Cyan, Icon.Battery, 8f);
                    Advance(20f);
                    break;
                case 4:
                    Toast("AURA: Consulta el Archivo científico (icono del libro) para ver qué datos son reales y cuáles simulados.", Palette.Cyan, Icon.Book, 8f);
                    Advance(0f);
                    break;
            }
        }

        void Advance(float wait)
        {
            m_TutorialStep++;
            m_TutorialTimer = wait;
        }

        // ================================================================== Final

        void EndMission()
        {
            if (m_Flow == Flow.Ended)
                return;
            m_Flow = Flow.Ended;
            m_Windows.CloseAll();
            m_Queue.Clear();
            var outcome = m_Sim.State.Outcome;
            string cinematic = outcome == MissionOutcome.Success ? CinematicIds.EndingSuccess
                : outcome == MissionOutcome.Partial ? CinematicIds.EndingPartial : CinematicIds.EndingFailure;
            GameAudio.Instance.Play(outcome == MissionOutcome.Failure ? Sfx.Fail : Sfx.Success);
            PlayCinematic(cinematic, ShowEndScreen, true);
        }

        // ================================================================== Cinemáticas (ICinematicGameBridge)

        void PlayCinematic(string id, Action after, bool allowWhenEnded = false)
        {
            var manager = CinematicManager.Instance;
            if (!playCinematics || manager == null || manager.IsPlaying || m_ShownCinematics.Contains(id))
            {
                after?.Invoke();
                return;
            }
            var previous = m_Flow;
            m_Flow = Flow.Cinematic;
            bool started = manager.Play(id, result =>
            {
                m_Flow = previous;
                after?.Invoke();
            });
            if (!started)
            {
                m_Flow = previous;
                after?.Invoke();
            }
        }

        public void OnCinematicStarting(string cinematicId)
        {
            m_HudLayer.gameObject.SetActive(false);
            m_WindowLayer.gameObject.SetActive(false);
            m_MenuLayer.gameObject.SetActive(false);
            m_Player.InputEnabled = false;
            PlayerController.TouchMove = Vector2.zero;
        }

        public void OnCinematicFinished(CinematicResult result)
        {
            m_HudLayer.gameObject.SetActive(true);
            m_WindowLayer.gameObject.SetActive(true);
            m_MenuLayer.gameObject.SetActive(true);
            m_ShownCinematics.Add(result.CinematicId);
        }

        public bool HasCinematicBeenShown(string cinematicId) => m_ShownCinematics.Contains(cinematicId);
        public void MarkCinematicShown(string cinematicId) => m_ShownCinematics.Add(cinematicId);
    }
}
