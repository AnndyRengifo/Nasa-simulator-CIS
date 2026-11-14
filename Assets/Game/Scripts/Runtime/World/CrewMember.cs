using System.Collections.Generic;
using Sol3.Game.Art;
using Sol3.Game.Sim;
using TMPro;
using UnityEngine;

namespace Sol3.Game.World
{
    /// <summary>
    /// Miembro de la tripulación (no jugable). Camina entre salas por la red de puntos de paso,
    /// trabaja un rato en cada una y acude al refugio cuando el protocolo de refugio está activo.
    /// </summary>
    public sealed class CrewMember : MonoBehaviour
    {
        static readonly List<CrewMember> s_All = new List<CrewMember>();

        public string DisplayName { get; private set; }
        SpriteRenderer m_Renderer;
        TextMeshPro m_Label;
        Color m_Accent;
        readonly Queue<Vector2> m_Path = new Queue<Vector2>();
        int m_Waypoint;
        float m_Wait, m_Anim;
        RoomId[] m_Rooms;
        Facing m_Facing = Facing.Down;
        bool m_Right;
        bool m_GoingToShelter;
        System.Random m_Random;

        public static bool NearAny(Vector2 position)
        {
            foreach (var crew in s_All)
                if (Vector2.Distance(crew.transform.position, position) < 1.8f)
                    return true;
            return false;
        }

        public static CrewMember Create(Transform parent, string name, Color accent, RoomId[] rooms, int seed)
        {
            var go = new GameObject("Crew_" + name);
            go.transform.SetParent(parent, false);
            var crew = go.AddComponent<CrewMember>();
            crew.DisplayName = name;
            crew.m_Accent = accent;
            crew.m_Rooms = rooms;
            crew.m_Random = new System.Random(seed);
            crew.m_Waypoint = BaseLayout.WaypointOf(rooms[0]);
            go.transform.position = BaseLayout.Waypoints[crew.m_Waypoint] + new Vector2(seed % 3 - 1, -1f);
            crew.m_Renderer = BaseBuilder.Sprite(go.transform, "Sprite", Sprites.Astronaut(Facing.Down, 0, accent), Vector2.zero, 0);

            var label = new GameObject("Name");
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            crew.m_Label = label.AddComponent<TextMeshPro>();
            crew.m_Label.text = name;
            crew.m_Label.fontSize = 2f;
            crew.m_Label.alignment = TextAlignmentOptions.Center;
            crew.m_Label.color = Palette.SoftWhite.WithAlpha(0.75f);
            crew.m_Label.rectTransform.sizeDelta = new Vector2(4f, 0.6f);
            crew.m_Label.sortingOrder = Sorting.Overlay - 1;
            crew.m_Wait = 2f + seed;
            return crew;
        }

        void OnEnable() => s_All.Add(this);
        void OnDisable() => s_All.Remove(this);

        public void Tick(ColonySimulation sim, float dt)
        {
            bool shelter = sim.State.ShelterActive;
            if (shelter != m_GoingToShelter)
            {
                m_GoingToShelter = shelter;
                m_Path.Clear();
                m_Wait = 0f;
                if (shelter)
                    PathTo(BaseLayout.WaypointOf(RoomId.Shelter));
            }

            bool moving = false;
            if (m_Path.Count > 0)
            {
                var target = m_Path.Peek();
                var pos = (Vector2)transform.position;
                var delta = target - pos;
                float step = (shelter ? 3.2f : 2.1f) * dt;
                if (delta.magnitude <= step)
                {
                    transform.position = target;
                    m_Path.Dequeue();
                }
                else
                {
                    var dir = delta.normalized;
                    transform.position = pos + dir * step;
                    moving = true;
                    if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y)) { m_Facing = Facing.Side; m_Right = dir.x > 0f; }
                    else m_Facing = dir.y > 0f ? Facing.Up : Facing.Down;
                }
            }
            else if (!shelter)
            {
                m_Wait -= dt;
                if (m_Wait <= 0f)
                {
                    var room = m_Rooms[m_Random.Next(m_Rooms.Length)];
                    PathTo(BaseLayout.WaypointOf(room));
                    m_Wait = 6f + (float)m_Random.NextDouble() * 10f;
                    m_Facing = Facing.Up;
                }
            }

            m_Anim = moving ? m_Anim + dt * 7f : 0f;
            m_Renderer.sprite = Sprites.Astronaut(m_Facing, moving ? 1 + (int)m_Anim % 4 : 0, m_Accent);
            m_Renderer.flipX = m_Facing == Facing.Side && m_Right;
            m_Renderer.sortingOrder = Sorting.ForY(transform.position.y);
        }

        /// <summary>Camino más corto (BFS) por la red de puntos de paso, con un pequeño desvío dentro de la sala.</summary>
        void PathTo(int goal)
        {
            var previous = new int[BaseLayout.Waypoints.Length];
            for (int i = 0; i < previous.Length; i++) previous[i] = -2;
            var queue = new Queue<int>();
            queue.Enqueue(m_Waypoint);
            previous[m_Waypoint] = -1;
            while (queue.Count > 0)
            {
                int node = queue.Dequeue();
                if (node == goal)
                    break;
                foreach (int next in BaseLayout.WaypointLinks[node])
                {
                    if (previous[next] != -2)
                        continue;
                    previous[next] = node;
                    queue.Enqueue(next);
                }
            }
            if (previous[goal] == -2)
                return;
            var route = new List<int>();
            for (int n = goal; n != -1; n = previous[n])
                route.Add(n);
            route.Reverse();
            m_Path.Clear();
            // Vuelve primero al punto de paso actual para no atravesar paredes.
            m_Path.Enqueue(BaseLayout.Waypoints[m_Waypoint]);
            for (int i = 1; i < route.Count; i++)
                m_Path.Enqueue(BaseLayout.Waypoints[route[i]]);
            var jitter = new Vector2((float)m_Random.NextDouble() * 3f - 1.5f, (float)m_Random.NextDouble() * 2f - 1.6f);
            m_Path.Enqueue(BaseLayout.Waypoints[goal] + jitter);
            m_Waypoint = goal;
        }
    }
}
