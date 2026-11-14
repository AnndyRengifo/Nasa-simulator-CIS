using System;
using System.Collections.Generic;
using Sol3.Game.Art;
using Sol3.Game.Audio;
using Sol3.Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sol3.Game.Minigames
{
    public enum MinigameKind { Circuits, Oxygen, Water, Comms }
    public enum MinigameResult { Success, Failure, Cancelled }

    /// <summary>
    /// Base de los minijuegos de reparación. Se abren en una ventana modal con instrucciones, temporizador
    /// y botón de abandonar. "Difícil" se usa cuando no quedan repuestos (más elementos o menos tiempo).
    /// </summary>
    public abstract class Minigame : MonoBehaviour
    {
        protected RectTransform Area;
        protected bool Hard;
        protected System.Random Rng;
        Action<MinigameResult> m_Done;
        TextMeshProUGUI m_Timer, m_Result;
        Window m_Window;
        float m_TimeLeft, m_EndDelay = -1f;
        MinigameResult m_Outcome;
        protected bool Finished => m_EndDelay >= 0f;
        protected abstract float TimeLimit { get; }
        protected abstract void BuildArea();
        protected abstract void Step(float dt);

        public static string TitleOf(MinigameKind kind)
        {
            switch (kind)
            {
                case MinigameKind.Circuits: return "Reparación de circuitos";
                case MinigameKind.Oxygen: return "Calibración de oxígeno";
                case MinigameKind.Water: return "Reciclaje de agua";
                default: return "Alineación de comunicaciones";
            }
        }

        public static string InstructionsOf(MinigameKind kind, bool hard)
        {
            switch (kind)
            {
                case MinigameKind.Circuits:
                    return "Arrastra cada cable desde la izquierda hasta el conector del mismo color y símbolo. Tres errores provocan un cortocircuito." +
                           (hard ? " <color=#E2B45A>Sin repuestos: hay un cable más y menos tiempo.</color>" : "");
                case MinigameKind.Oxygen:
                    return "Mantén pulsado PRESURIZAR (o las teclas 1, 2 y 3) para subir cada válvula. Mantén las tres dentro de la franja verde hasta llenar la barra.";
                case MinigameKind.Water:
                    return "Toca las tuberías para girarlas y conecta el depósito de entrada (izquierda) con la salida (derecha).";
                default:
                    return "Ajusta la frecuencia y la orientación de la antena hasta que tu señal (ámbar) coincida con la de la Tierra (cian) y se mantenga estable.";
            }
        }

        public static void Open(WindowManager windows, MinigameKind kind, bool hard, int seed, Action<MinigameResult> done)
        {
            windows.Open(TitleOf(kind), new Vector2(880f, 620f), w =>
            {
                var hint = UIKit.Text(w.Body, InstructionsOf(kind, hard), 18f, UIKit.Muted);
                UIKit.Size(hint, -1f, 52f);
                var area = UIKit.Panel(w.Body, Palette.Ink.WithAlpha(0.7f), "Area", 2f);
                UIKit.Size(area, -1f, 372f);
                var footer = UIKit.Row(w.Body, 12f, 50f);
                Minigame game;
                switch (kind)
                {
                    case MinigameKind.Circuits: game = area.gameObject.AddComponent<CircuitsGame>(); break;
                    case MinigameKind.Oxygen: game = area.gameObject.AddComponent<OxygenGame>(); break;
                    case MinigameKind.Water: game = area.gameObject.AddComponent<WaterGame>(); break;
                    default: game = area.gameObject.AddComponent<CommsGame>(); break;
                }
                game.m_Window = w;
                game.m_Done = done;
                game.Hard = hard;
                game.Rng = new System.Random(seed);
                game.Area = area.rectTransform;
                UIKit.Icon(footer, Icon.Wrench, Palette.Cyan, 26f);
                game.m_Timer = UIKit.Text(footer, "", 22f, Palette.SoftWhite, TextAlignmentOptions.Left, FontStyles.Bold);
                UIKit.Size(game.m_Timer, 520f, 44f, 1f);
                UIKit.Size(UIKit.Button(footer, "Abandonar", () => game.Finish(MinigameResult.Cancelled), Palette.Steel, 18f, 46f), 170f, 46f);
                game.m_TimeLeft = game.TimeLimit;
                game.m_Result = UIKit.Text(area.transform, "", 44f, Palette.SoftWhite, TextAlignmentOptions.Center, FontStyles.Bold, "Result");
                game.m_Result.rectTransform.Stretch();
                game.BuildArea();
                game.m_Result.transform.SetAsLastSibling();
            }, false, Icon.Wrench, Palette.Amber);
        }

        protected void Finish(MinigameResult result)
        {
            if (Finished)
                return;
            m_Outcome = result;
            m_EndDelay = result == MinigameResult.Cancelled ? 0f : 1.3f;
            if (result == MinigameResult.Success)
            {
                GameAudio.Instance?.Play(Sfx.Repair);
                m_Result.text = "<color=#65C69A>¡SISTEMA RESTABLECIDO!</color>";
            }
            else if (result == MinigameResult.Failure)
            {
                GameAudio.Instance?.Play(Sfx.Fail);
                m_Result.text = "<color=#DB6262>REPARACIÓN FALLIDA</color>";
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (Finished)
            {
                m_EndDelay -= dt;
                if (m_EndDelay <= 0f && m_Window.IsOpen)
                {
                    m_Window.Close();
                    m_Done?.Invoke(m_Outcome);
                }
                return;
            }
            m_TimeLeft -= dt;
            m_Timer.text = $"Tiempo: {Mathf.CeilToInt(Mathf.Max(0f, m_TimeLeft))} s" + StatusText();
            m_Timer.color = m_TimeLeft < 8f ? Palette.Alert : Palette.SoftWhite;
            if (m_TimeLeft <= 0f)
            {
                Finish(MinigameResult.Failure);
                return;
            }
            Step(dt);
        }

        protected virtual string StatusText() => string.Empty;

        protected Vector2 LocalPoint(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Area, e.position, e.pressEventCamera, out var local);
            return local;
        }

        protected static Image Line(RectTransform parent, Color color, float thickness)
        {
            var image = UIKit.Plain(parent, color, "Wire");
            image.raycastTarget = false;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(0f, thickness);
            return image;
        }

        protected static void SetLine(Image line, Vector2 from, Vector2 to)
        {
            var rt = line.rectTransform;
            var d = to - from;
            rt.anchoredPosition = from;
            rt.sizeDelta = new Vector2(d.magnitude, rt.sizeDelta.y);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        protected static RectTransform Node(RectTransform parent, Vector2 position, Vector2 size, Color color, string name = "Node")
        {
            var image = UIKit.Panel(parent, color, name, 2f);
            var rt = image.rectTransform;
            rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            return rt;
        }
    }

    // ====================================================================== Circuitos

    public sealed class CircuitsGame : Minigame, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        static readonly Color[] Colors = { Palette.Alert, Palette.Amber, Palette.Green, Palette.Cyan, Palette.Hex("#B48CF0") };
        static readonly Icon[] Symbols = { Icon.Energy, Icon.Water, Icon.Leaf, Icon.Oxygen, Icon.Signal };

        readonly List<Vector2> m_Left = new List<Vector2>();
        readonly List<Vector2> m_Right = new List<Vector2>();
        readonly List<int> m_RightIds = new List<int>();
        readonly bool[] m_Connected = new bool[5];
        int m_Count, m_Dragging = -1, m_Mistakes, m_Done;
        Image m_DragLine;

        protected override float TimeLimit => Hard ? 30f : 38f;
        protected override string StatusText() => $"   ·   Cables {m_Done}/{m_Count}   ·   Errores {m_Mistakes}/3";

        protected override void BuildArea()
        {
            m_Count = Hard ? 5 : 4;
            for (int i = 0; i < m_Count; i++) m_RightIds.Add(i);
            for (int i = m_RightIds.Count - 1; i > 0; i--)
            {
                int j = Rng.Next(i + 1);
                (m_RightIds[i], m_RightIds[j]) = (m_RightIds[j], m_RightIds[i]);
            }
            float spacing = 300f / m_Count;
            for (int i = 0; i < m_Count; i++)
            {
                float y = 150f - spacing * (i + 0.5f) - 0f;
                var left = new Vector2(-330f, y);
                var right = new Vector2(330f, y);
                m_Left.Add(left);
                m_Right.Add(right);
                Terminal(left, i, true);
                Terminal(right, m_RightIds[i], false);
            }
            var board = UIKit.Text(Area, "PLACA DE DISTRIBUCIÓN", 16f, Palette.Steel.Shade(0.35f), TextAlignmentOptions.Center, FontStyles.Bold);
            board.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(400f, 24f));
            m_DragLine = Line(Area, Color.white, 10f);
            m_DragLine.enabled = false;
        }

        void Terminal(Vector2 position, int id, bool left)
        {
            var node = Node(Area, position, new Vector2(96f, 52f), Palette.Steel);
            UIKit.Outline(node, Colors[id].WithAlpha(0.9f), 2f);
            var icon = UIKit.Icon(node, Symbols[id], Colors[id], 30f);
            icon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(left ? -16f : 16f, 0f), new Vector2(30f, 30f));
            var plug = UIKit.Panel(node, Colors[id], "Plug", 4f);
            plug.raycastTarget = false;
            plug.rectTransform.Place(new Vector2(left ? 1f : 0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (Finished) return;
            var p = LocalPoint(e);
            for (int i = 0; i < m_Count; i++)
            {
                if (!m_Connected[i] && Vector2.Distance(p, m_Left[i]) < 60f)
                {
                    m_Dragging = i;
                    m_DragLine.enabled = true;
                    m_DragLine.color = Colors[i];
                    SetLine(m_DragLine, m_Left[i] + new Vector2(48f, 0f), p);
                    return;
                }
            }
        }

        public void OnDrag(PointerEventData e)
        {
            if (m_Dragging >= 0)
                SetLine(m_DragLine, m_Left[m_Dragging] + new Vector2(48f, 0f), LocalPoint(e));
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (m_Dragging < 0 || Finished) return;
            var p = LocalPoint(e);
            int id = m_Dragging;
            m_Dragging = -1;
            m_DragLine.enabled = false;
            for (int slot = 0; slot < m_Count; slot++)
            {
                if (Vector2.Distance(p, m_Right[slot]) > 70f)
                    continue;
                if (m_RightIds[slot] == id)
                {
                    var wire = Line(Area, Colors[id], 10f);
                    SetLine(wire, m_Left[id] + new Vector2(48f, 0f), m_Right[slot] - new Vector2(48f, 0f));
                    m_Connected[id] = true;
                    m_Done++;
                    GameAudio.Instance?.Play(Sfx.Pickup, 0.7f);
                    if (m_Done == m_Count)
                        Finish(MinigameResult.Success);
                }
                else
                {
                    m_Mistakes++;
                    GameAudio.Instance?.Play(Sfx.Spark);
                    if (m_Mistakes >= 3)
                        Finish(MinigameResult.Failure);
                }
                return;
            }
        }

        protected override void Step(float dt) { }
    }

    // ====================================================================== Oxígeno

    public sealed class OxygenGame : Minigame
    {
        sealed class Valve
        {
            public float Level, Center, Speed, Phase;
            public RectTransform Fill, Zone;
            public Image FillImage;
            public HoldButton Button;
        }

        readonly List<Valve> m_Valves = new List<Valve>();
        Bar m_Progress;
        float m_Value;
        const float GaugeHeight = 230f;
        const float ZoneSize = 0.24f;

        protected override float TimeLimit => Hard ? 32f : 40f;
        protected override string StatusText() => $"   ·   Estabilidad {Mathf.RoundToInt(m_Value * 100f)} %";

        protected override void BuildArea()
        {
            for (int i = 0; i < 3; i++)
            {
                float x = -220f + i * 220f;
                var gauge = Node(Area, new Vector2(x, 40f), new Vector2(84f, GaugeHeight), Palette.Graphite, "Gauge");
                var zone = UIKit.Plain(gauge, Palette.Green.WithAlpha(0.35f), "Zone").rectTransform;
                zone.anchorMin = new Vector2(0f, 0f); zone.anchorMax = new Vector2(1f, 0f); zone.pivot = new Vector2(0.5f, 0.5f);
                zone.sizeDelta = new Vector2(0f, GaugeHeight * ZoneSize);
                var fill = UIKit.Panel(gauge, Palette.Cyan, "Fill", 4f);
                fill.raycastTarget = false;
                var frt = fill.rectTransform;
                frt.anchorMin = new Vector2(0.25f, 0f); frt.anchorMax = new Vector2(0.75f, 0.5f);
                frt.offsetMin = new Vector2(0f, 6f); frt.offsetMax = Vector2.zero;
                var label = UIKit.Text(Area, $"VÁLVULA {i + 1}", 16f, UIKit.Muted, TextAlignmentOptions.Center, FontStyles.Bold);
                label.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 172f), new Vector2(160f, 24f));
                var button = UIKit.Button(Area, "PRESURIZAR", null, Palette.Steel, 16f, 50f);
                ((RectTransform)button.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -128f), new Vector2(170f, 50f));
                var hold = button.gameObject.AddComponent<HoldButton>();
                m_Valves.Add(new Valve
                {
                    Level = 0.2f + (float)Rng.NextDouble() * 0.2f,
                    Center = 0.45f + (float)Rng.NextDouble() * 0.25f,
                    Speed = (Hard ? 0.16f : 0.11f) + (float)Rng.NextDouble() * 0.06f,
                    Phase = (float)Rng.NextDouble() * 6f,
                    Fill = frt, FillImage = fill, Zone = zone, Button = hold,
                });
            }
            m_Progress = UIKit.Bar(Area, Palette.Green, 18f);
            ((RectTransform)m_Progress.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(640f, 18f));
            m_Progress.Set(0f);
        }

        protected override void Step(float dt)
        {
            var k = Keyboard.current;
            bool allIn = true;
            for (int i = 0; i < m_Valves.Count; i++)
            {
                var v = m_Valves[i];
                bool key = k != null && (i == 0 ? k.digit1Key.isPressed || k.numpad1Key.isPressed : i == 1 ? k.digit2Key.isPressed || k.numpad2Key.isPressed : k.digit3Key.isPressed || k.numpad3Key.isPressed);
                v.Phase += dt;
                v.Level += (v.Button.Held || key ? 0.42f : -v.Speed) * dt;
                v.Level = Mathf.Clamp01(v.Level);
                float center = Mathf.Clamp(v.Center + Mathf.Sin(v.Phase * (Hard ? 0.9f : 0.6f)) * 0.15f, ZoneSize, 1f - ZoneSize * 0.5f);
                bool inZone = Mathf.Abs(v.Level - center) <= ZoneSize * 0.5f;
                allIn &= inZone;
                v.Zone.anchoredPosition = new Vector2(0f, center * GaugeHeight);
                v.Fill.anchorMax = new Vector2(0.75f, Mathf.Max(0.03f, v.Level));
                v.FillImage.color = inZone ? Palette.Green : v.Level > center ? Palette.Alert : Palette.Cyan;
            }
            m_Value = Mathf.Clamp01(m_Value + (allIn ? dt / 5f : -dt / 12f));
            m_Progress.Set(m_Value);
            if (m_Value >= 1f)
                Finish(MinigameResult.Success);
        }
    }

    // ====================================================================== Agua

    public sealed class WaterGame : Minigame
    {
        const int N = 1, E = 2, S = 4, W = 8;
        int m_Cols, m_Rows, m_Start, m_End;
        int[,] m_Base, m_Rot;
        RectTransform[,] m_Pipes;
        List<Image>[,] m_Arms;
        float[,] m_Angle;
        bool[,] m_Wet;
        const float Tile = 74f;

        protected override float TimeLimit => Hard ? 38f : 48f;

        static int Rotate(int mask, int times)
        {
            for (int i = 0; i < (times & 3); i++)
                mask = ((mask << 1) | (mask >> 3)) & 15;
            return mask;
        }

        protected override void BuildArea()
        {
            m_Cols = Hard ? 6 : 5;
            m_Rows = 4;
            m_Base = new int[m_Cols, m_Rows];
            m_Rot = new int[m_Cols, m_Rows];
            m_Pipes = new RectTransform[m_Cols, m_Rows];
            m_Arms = new List<Image>[m_Cols, m_Rows];
            m_Angle = new float[m_Cols, m_Rows];
            m_Wet = new bool[m_Cols, m_Rows];

            // Camino garantizado de izquierda a derecha
            m_Start = Rng.Next(m_Rows);
            m_End = Rng.Next(m_Rows);
            int row = m_Start;
            var path = new List<Vector2Int>();
            for (int c = 0; c < m_Cols; c++)
            {
                int target = c == m_Cols - 1 ? m_End : Rng.Next(m_Rows);
                path.Add(new Vector2Int(c, row));
                while (row != target)
                {
                    row += Math.Sign(target - row);
                    path.Add(new Vector2Int(c, row));
                }
            }
            m_Base[path[0].x, path[0].y] |= W;
            m_Base[path[path.Count - 1].x, path[path.Count - 1].y] |= E;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                var a = path[i];
                var b = path[i + 1];
                if (b.x > a.x) { m_Base[a.x, a.y] |= E; m_Base[b.x, b.y] |= W; }
                else if (b.y > a.y) { m_Base[a.x, a.y] |= N; m_Base[b.x, b.y] |= S; }
                else { m_Base[a.x, a.y] |= S; m_Base[b.x, b.y] |= N; }
            }
            int[] fillers = { N | S, N | E, N | E | S, N | S, E | S };
            for (int x = 0; x < m_Cols; x++)
                for (int y = 0; y < m_Rows; y++)
                {
                    if (m_Base[x, y] == 0)
                        m_Base[x, y] = fillers[Rng.Next(fillers.Length)];
                    m_Rot[x, y] = Rng.Next(4);
                }

            float width = m_Cols * Tile, height = m_Rows * Tile;
            for (int x = 0; x < m_Cols; x++)
                for (int y = 0; y < m_Rows; y++)
                {
                    var pos = new Vector2(-width * 0.5f + (x + 0.5f) * Tile, -height * 0.5f + (y + 0.5f) * Tile + 6f);
                    var tile = Node(Area, pos, new Vector2(Tile - 6f, Tile - 6f), Palette.Graphite, "Tile");
                    int cx = x, cy = y;
                    var button = tile.gameObject.AddComponent<Button>();
                    button.onClick.AddListener(() => Turn(cx, cy));
                    var pipe = UIKit.Rect(tile, "Pipe");
                    pipe.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Tile, Tile));
                    var arms = new List<Image>();
                    for (int bit = 0; bit < 4; bit++)
                    {
                        int dir = 1 << bit;
                        if ((m_Base[x, y] & dir) == 0) continue;
                        var arm = UIKit.Plain(pipe, Palette.Steel, "Arm");
                        arm.raycastTarget = false;
                        var rt = arm.rectTransform;
                        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                        bool vertical = dir == N || dir == S;
                        rt.sizeDelta = vertical ? new Vector2(18f, Tile * 0.5f) : new Vector2(Tile * 0.5f, 18f);
                        rt.anchoredPosition = dir == N ? new Vector2(0, Tile * 0.25f) : dir == S ? new Vector2(0, -Tile * 0.25f) : dir == E ? new Vector2(Tile * 0.25f, 0) : new Vector2(-Tile * 0.25f, 0);
                        arms.Add(arm);
                    }
                    var hub = UIKit.Panel(pipe, Palette.Steel, "Hub", 3f);
                    hub.raycastTarget = false;
                    hub.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
                    arms.Add(hub);
                    m_Pipes[x, y] = pipe;
                    m_Arms[x, y] = arms;
                    m_Angle[x, y] = -90f * m_Rot[x, y];
                    pipe.localRotation = Quaternion.Euler(0, 0, m_Angle[x, y]);
                }

            Port(new Vector2(-width * 0.5f - 46f, -height * 0.5f + (m_Start + 0.5f) * Tile + 6f), "ENTRADA");
            Port(new Vector2(width * 0.5f + 46f, -height * 0.5f + (m_End + 0.5f) * Tile + 6f), "SALIDA");
            Flow();
            if (m_Wet[m_Cols - 1, m_End] && (Rotate(m_Base[m_Cols - 1, m_End], m_Rot[m_Cols - 1, m_End]) & E) != 0)
            {
                m_Rot[0, m_Start] = (m_Rot[0, m_Start] + 1) & 3;
                m_Angle[0, m_Start] = -90f * m_Rot[0, m_Start];
                Flow();
            }
        }

        void Port(Vector2 position, string label)
        {
            var node = Node(Area, position, new Vector2(60f, 60f), Palette.Hex("#2C5C8A"), label);
            var icon = UIKit.Icon(node, Icon.Water, Palette.Cyan, 34f);
            icon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            var text = UIKit.Text(Area, label, 13f, UIKit.Muted, TextAlignmentOptions.Center, FontStyles.Bold);
            text.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2(0f, -44f), new Vector2(90f, 20f));
        }

        void Turn(int x, int y)
        {
            if (Finished) return;
            m_Rot[x, y] = (m_Rot[x, y] + 1) & 3;
            m_Angle[x, y] -= 90f;
            GameAudio.Instance?.Play(Sfx.Click, 0.5f);
            Flow();
        }

        void Flow()
        {
            Array.Clear(m_Wet, 0, m_Wet.Length);
            var queue = new Queue<Vector2Int>();
            if ((Rotate(m_Base[0, m_Start], m_Rot[0, m_Start]) & W) != 0)
            {
                m_Wet[0, m_Start] = true;
                queue.Enqueue(new Vector2Int(0, m_Start));
            }
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                int mask = Rotate(m_Base[c.x, c.y], m_Rot[c.x, c.y]);
                TryFlow(queue, mask, c, N, 0, 1, S);
                TryFlow(queue, mask, c, S, 0, -1, N);
                TryFlow(queue, mask, c, E, 1, 0, W);
                TryFlow(queue, mask, c, W, -1, 0, E);
            }
            for (int x = 0; x < m_Cols; x++)
                for (int y = 0; y < m_Rows; y++)
                    foreach (var arm in m_Arms[x, y])
                        arm.color = m_Wet[x, y] ? Palette.Cyan : Palette.Steel.Shade(0.15f);
            if (m_Wet[m_Cols - 1, m_End] && (Rotate(m_Base[m_Cols - 1, m_End], m_Rot[m_Cols - 1, m_End]) & E) != 0 && Area != null && m_Started)
                Finish(MinigameResult.Success);
        }

        bool m_Started;

        void TryFlow(Queue<Vector2Int> queue, int mask, Vector2Int c, int dir, int dx, int dy, int opposite)
        {
            if ((mask & dir) == 0) return;
            int nx = c.x + dx, ny = c.y + dy;
            if (nx < 0 || ny < 0 || nx >= m_Cols || ny >= m_Rows || m_Wet[nx, ny]) return;
            if ((Rotate(m_Base[nx, ny], m_Rot[nx, ny]) & opposite) == 0) return;
            m_Wet[nx, ny] = true;
            queue.Enqueue(new Vector2Int(nx, ny));
        }

        protected override void Step(float dt)
        {
            m_Started = true;
            for (int x = 0; x < m_Cols; x++)
                for (int y = 0; y < m_Rows; y++)
                {
                    var current = m_Pipes[x, y].localEulerAngles.z;
                    float z = Mathf.MoveTowardsAngle(current, m_Angle[x, y], dt * 720f);
                    m_Pipes[x, y].localRotation = Quaternion.Euler(0, 0, z);
                }
        }
    }

    // ====================================================================== Comunicaciones

    public sealed class CommsGame : Minigame
    {
        const int Points = 64;
        readonly Image[] m_Target = new Image[Points];
        readonly Image[] m_Signal = new Image[Points];
        SimpleSlider m_Frequency, m_Angle;
        float m_TargetF, m_TargetP, m_Lock, m_Quality, m_Time;
        Bar m_LockBar;
        const float Width = 720f, Amp = 62f;

        protected override float TimeLimit => Hard ? 34f : 42f;
        protected override string StatusText() => $"   ·   Calidad de señal {Mathf.RoundToInt(m_Quality * 100f)} %";

        protected override void BuildArea()
        {
            m_TargetF = 0.2f + (float)Rng.NextDouble() * 0.6f;
            m_TargetP = (float)Rng.NextDouble();
            var scope = Node(Area, new Vector2(0f, 82f), new Vector2(Width + 30f, 180f), Palette.Hex("#0A1A24"), "Scope");
            for (int i = 0; i < 5; i++)
            {
                var grid = UIKit.Plain(scope, Palette.Cyan.WithAlpha(0.08f), "Grid");
                grid.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -72f + i * 36f), new Vector2(Width, 2f));
            }
            for (int i = 0; i < Points; i++)
            {
                m_Target[i] = Dot(scope, Palette.Cyan.WithAlpha(0.8f), 9f);
                m_Signal[i] = Dot(scope, Palette.Amber, 7f);
            }
            var fl = UIKit.Text(Area, "FRECUENCIA", 15f, UIKit.Muted, TextAlignmentOptions.Left, FontStyles.Bold);
            fl.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110f, -30f), new Vector2(560f, 22f));
            m_Frequency = SimpleSlider.Create(Area, Palette.Amber, 40f);
            ((RectTransform)m_Frequency.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(Width, 40f));
            var al = UIKit.Text(Area, "ORIENTACIÓN DE LA ANTENA", 15f, UIKit.Muted, TextAlignmentOptions.Left, FontStyles.Bold);
            al.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110f, -98f), new Vector2(560f, 22f));
            m_Angle = SimpleSlider.Create(Area, Palette.Amber, 40f);
            ((RectTransform)m_Angle.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -128f), new Vector2(Width, 40f));
            m_Frequency.SetValue(Mathf.Repeat(m_TargetF + 0.35f + (float)Rng.NextDouble() * 0.2f, 1f));
            m_Angle.SetValue(Mathf.Repeat(m_TargetP + 0.3f + (float)Rng.NextDouble() * 0.3f, 1f));
            m_LockBar = UIKit.Bar(Area, Palette.Green, 14f);
            ((RectTransform)m_LockBar.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(Width, 14f));
            m_LockBar.Set(0f);
        }

        static Image Dot(RectTransform parent, Color color, float size)
        {
            var image = UIKit.Plain(parent, color, "Dot");
            image.sprite = Sprites.Dot();
            image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            image.rectTransform.sizeDelta = new Vector2(size, size);
            return image;
        }

        static float Wave(float x, float f, float p) => Mathf.Sin(2f * Mathf.PI * ((1f + f * 4f) * x + p));

        protected override void Step(float dt)
        {
            m_Time += dt;
            float df = Mathf.Abs(m_Frequency.Value - m_TargetF);
            float dp = Mathf.Abs(m_Angle.Value - m_TargetP);
            dp = Mathf.Min(dp, 1f - dp);
            m_Quality = Mathf.Clamp01(1f - df * 5f - dp * 4f);
            float noise = (1f - m_Quality) * 0.45f;
            for (int i = 0; i < Points; i++)
            {
                float x = i / (float)(Points - 1);
                float px = -Width * 0.5f + x * Width;
                m_Target[i].rectTransform.anchoredPosition = new Vector2(px, Wave(x, m_TargetF, m_TargetP) * Amp);
                float n = (Mathf.PerlinNoise(i * 0.7f, m_Time * 6f) - 0.5f) * 2f * noise;
                m_Signal[i].rectTransform.anchoredPosition = new Vector2(px, (Wave(x, m_Frequency.Value, m_Angle.Value) * (0.75f + 0.25f * m_Quality) + n) * Amp);
            }
            m_Lock = m_Quality > 0.86f ? m_Lock + dt / 1.6f : Mathf.Max(0f, m_Lock - dt);
            m_LockBar.Set(m_Lock);
            if (m_Lock >= 1f)
                Finish(MinigameResult.Success);
        }
    }
}
