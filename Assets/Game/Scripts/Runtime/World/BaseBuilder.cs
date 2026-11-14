using System.Collections.Generic;
using Sol3.Game.Art;
using Sol3.Game.Audio;
using Sol3.Game.Player;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sol3.Game.World
{
    public static class Sorting
    {
        public const int Ground = -30000;
        public const int Floor = -20000;
        public const int Wall = -15000;
        public const int Overlay = 20000;

        public static int ForY(float y) => 10000 - Mathf.RoundToInt(y * 100f);
    }

    /// <summary>
    /// Construye la base a partir de BaseLayout: terreno marciano, suelos por habitación, paredes con
    /// colisiones, puertas automáticas, luces 2D, rótulos, decoración y máquinas interactivas.
    /// </summary>
    public sealed class BaseBuilder : MonoBehaviour
    {
        static Material s_Lit, s_Unlit;

        public readonly List<Machine> Machines = new List<Machine>();
        public readonly Dictionary<RoomId, Light2D> RoomLights = new Dictionary<RoomId, Light2D>();
        public Light2D GlobalLight { get; private set; }
        readonly List<Door> m_Doors = new List<Door>();

        public static Material Lit => s_Lit != null ? s_Lit : s_Lit = LoadMaterial("Materials/SOL3_SpriteLit", "Universal Render Pipeline/2D/Sprite-Lit-Default");
        public static Material Unlit => s_Unlit != null ? s_Unlit : s_Unlit = LoadMaterial("Materials/SOL3_SpriteUnlit", "Universal Render Pipeline/2D/Sprite-Unlit-Default");

        /// <summary>Los materiales viven en Resources para que sus shaders se incluyan siempre en la build.</summary>
        static Material LoadMaterial(string resource, string shader)
        {
            var material = Resources.Load<Material>(resource);
            if (material != null)
                return material;
            var found = Shader.Find(shader);
            return found != null ? new Material(found) : new Material(Shader.Find("Sprites/Default"));
        }

        public static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, Vector2 position, int order, bool lit = true, Color? tint = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = lit ? Lit : Unlit;
            renderer.sortingOrder = order;
            if (tint.HasValue)
                renderer.color = tint.Value;
            return renderer;
        }

        static SpriteRenderer Tiled(Transform parent, string name, Sprite sprite, Rect rect, int order, Color? tint = null)
        {
            var renderer = Sprite(parent, name, sprite, rect.center, order, true, tint);
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = rect.size;
            return renderer;
        }

        public void Build()
        {
            var grid = BaseLayout.FloorGrid();
            var ground = Tiled(transform, "MarsGround", Sprites.MarsGround(), new Rect(-30, -24, BaseLayout.Width + 60, BaseLayout.Height + 48), Sorting.Ground);
            ground.size = new Vector2(BaseLayout.Width + 60, BaseLayout.Height + 48);

            var floors = new GameObject("Floors").transform;
            floors.SetParent(transform, false);
            foreach (var room in BaseLayout.Rooms)
            {
                Tiled(floors, room.Name, Sprites.Floor(room.Floor, room.FloorStyle, (int)room.Id), new Rect(room.Rect.x, room.Rect.y, room.Rect.width, room.Rect.height), Sorting.Floor);
                BuildRoomDetails(room);
            }
            foreach (var corridor in BaseLayout.Corridors)
            {
                var sprite = Sprites.Floor(Palette.Hex("#26323F"), 2, 9);
                var r = Tiled(floors, "Corridor", sprite, new Rect(corridor.x, corridor.y, corridor.width, corridor.height), Sorting.Floor + 1);
                if (corridor.height > corridor.width)
                    r.transform.localRotation = Quaternion.identity;
            }

            BuildWalls(grid);
            BuildDoors();
            BuildLights();
            BuildMachines();
        }

        void BuildWalls(bool[,] grid)
        {
            var walls = new GameObject("Walls").transform;
            walls.SetParent(transform, false);
            var collider = walls.gameObject;
            var wallSprite = Sprites.Wall();
            for (int y = -1; y <= BaseLayout.Height; y++)
            {
                int runStart = int.MinValue;
                for (int x = -1; x <= BaseLayout.Width + 1; x++)
                {
                    bool wall = IsWall(grid, x, y);
                    if (wall && runStart == int.MinValue)
                        runStart = x;
                    if ((!wall || x == BaseLayout.Width + 1) && runStart != int.MinValue)
                    {
                        int end = wall ? x + 1 : x;
                        var rect = new Rect(runStart, y, end - runStart, 1);
                        Tiled(walls, "Wall", wallSprite, rect, Sorting.Wall);
                        var box = collider.AddComponent<BoxCollider2D>();
                        box.offset = rect.center;
                        box.size = rect.size;
                        runStart = int.MinValue;
                    }
                }
            }
        }

        static bool IsWall(bool[,] grid, int x, int y)
        {
            if (Floor(grid, x, y))
                return false;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (Floor(grid, x + dx, y + dy))
                        return true;
            return false;
        }

        static bool Floor(bool[,] grid, int x, int y) =>
            x >= 0 && y >= 0 && x < BaseLayout.Width && y < BaseLayout.Height && grid[x, y];

        void BuildDoors()
        {
            var root = new GameObject("Doors").transform;
            root.SetParent(transform, false);
            foreach (var c in BaseLayout.Corridors)
            {
                if (c.width >= c.height)
                {
                    AddDoor(root, new Vector2(c.xMin + 0.1f, c.y + c.height * 0.5f), true, c.height);
                    AddDoor(root, new Vector2(c.xMax - 0.1f, c.y + c.height * 0.5f), true, c.height);
                }
                else
                {
                    AddDoor(root, new Vector2(c.x + c.width * 0.5f, c.yMin + 0.05f), false, c.width);
                    AddDoor(root, new Vector2(c.x + c.width * 0.5f, c.yMax - 0.05f), false, c.width);
                }
            }
        }

        void AddDoor(Transform root, Vector2 position, bool vertical, int span)
        {
            var go = new GameObject("Door");
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            var door = go.AddComponent<Door>();
            door.Build(vertical, span);
            m_Doors.Add(door);
        }

        void BuildLights()
        {
            var lights = new GameObject("Lights").transform;
            lights.SetParent(transform, false);
            var global = new GameObject("GlobalLight");
            global.transform.SetParent(lights, false);
            GlobalLight = global.AddComponent<Light2D>();
            GlobalLight.lightType = Light2D.LightType.Global;
            GlobalLight.intensity = 0.9f;
            GlobalLight.color = Color.white;

            foreach (var room in BaseLayout.Rooms)
            {
                var go = new GameObject("Light_" + room.Id);
                go.transform.SetParent(lights, false);
                go.transform.localPosition = room.Center;
                var light = go.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = room.Light;
                light.intensity = 0.55f;
                light.pointLightOuterRadius = 7.5f;
                light.pointLightInnerRadius = 2.5f;
                light.falloffIntensity = 0.6f;
                RoomLights[room.Id] = light;
            }
        }

        void BuildRoomDetails(RoomDef room)
        {
            var parent = new GameObject("Room_" + room.Id).transform;
            parent.SetParent(transform, false);

            // Rótulo pintado en el suelo
            var label = new GameObject("Label");
            label.transform.SetParent(parent, false);
            label.transform.localPosition = room.At(room.Rect.width * 0.5f, 0.55f);
            var text = label.AddComponent<TextMeshPro>();
            text.text = room.Name;
            text.fontSize = 3.2f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Palette.SoftWhite.WithAlpha(0.35f);
            text.rectTransform.sizeDelta = new Vector2(room.Rect.width, 1);
            text.sortingOrder = Sorting.Floor + 5;

            switch (room.Id)
            {
                case RoomId.Control:
                    for (int i = 0; i < 3; i++)
                        Sprite(parent, "WallScreen", Sprites.ScreenGlow(70, 22), room.At(1.6f + i * 0.0f + i * 0.0f, 6.65f) + new Vector2(i * 1.3f, 0), Sorting.Wall + 2, false, Palette.Cyan.WithAlpha(0.8f));
                    Sprite(parent, "Plant", Sprites.Plants(2), room.At(10.2f, 1.0f), Sorting.ForY(room.Rect.y + 1f));
                    break;
                case RoomId.LifeSupport:
                    for (int i = 0; i < 3; i++)
                        Sprite(parent, "Tank", Sprites.Tank(), room.At(1.0f + i * 0.95f, 1.2f), Sorting.ForY(room.Rect.y + 1.2f));
                    break;
                case RoomId.Greenhouse:
                    for (int i = 0; i < 4; i++)
                        Sprite(parent, "GrowLight", Sprites.Soft(32), room.At(1.8f + i * 2.4f, 6.6f), Sorting.Wall + 2, false, Palette.Hex("#C59BFF").WithAlpha(0.5f));
                    break;
                case RoomId.Lab:
                    Sprite(parent, "Crate", Sprites.Crate(), room.At(9.0f, 1.2f), Sorting.ForY(room.Rect.y + 1.2f));
                    Sprite(parent, "Tank", Sprites.Tank(), room.At(1.2f, 1.3f), Sorting.ForY(room.Rect.y + 1.3f));
                    break;
                case RoomId.Engine:
                    for (int i = 0; i < 3; i++)
                        Sprite(parent, "Crate", Sprites.Crate(), room.At(1.0f + i * 0.8f, 1.1f), Sorting.ForY(room.Rect.y + 1.1f));
                    break;
                case RoomId.Shelter:
                    for (int i = 0; i < 3; i++)
                        Sprite(parent, "Bunk", Sprites.Bunk(), room.At(1.0f + i * 1.15f, 1.0f), Sorting.ForY(room.Rect.y + 1f));
                    Sprite(parent, "Crate", Sprites.Crate(), room.At(9.0f, 1.2f), Sorting.ForY(room.Rect.y + 1.2f));
                    break;
            }
        }

        void BuildMachines()
        {
            var root = new GameObject("Machines").transform;
            root.SetParent(transform, false);
            foreach (var (kind, roomId, local, index) in BaseLayout.Machines())
            {
                var room = BaseLayout.Room(roomId);
                var go = new GameObject($"{kind}_{index}");
                go.transform.SetParent(root, false);
                go.transform.localPosition = room.At(local.x, local.y);
                var machine = go.AddComponent<Machine>();
                machine.Build(kind, roomId, index);
                Machines.Add(machine);
            }
        }

        void Update()
        {
            var player = PlayerController.Instance;
            if (player == null)
                return;
            foreach (var door in m_Doors)
                door.UpdateDoor(player.transform.position, CrewMember.NearAny(door.transform.position));
        }
    }

    /// <summary>Puerta corredera que se abre al acercarse alguien.</summary>
    public sealed class Door : MonoBehaviour
    {
        Transform m_A, m_B;
        bool m_Vertical;
        float m_Open;
        float m_Span;
        bool m_WasOpen;

        public void Build(bool vertical, int span)
        {
            m_Vertical = vertical;
            m_Span = span;
            m_A = Leaf("A");
            m_B = Leaf("B");
            Apply();
        }

        Transform Leaf(string name)
        {
            var renderer = BaseBuilder.Sprite(transform, "Leaf" + name, Sprites.DoorPanel(), Vector2.zero, Sorting.Wall + 3);
            renderer.transform.localScale = m_Vertical ? new Vector3(m_Span * 0.5f, 0.6f, 1f) : new Vector3(m_Span * 0.5f, 0.6f, 1f);
            if (m_Vertical)
                renderer.transform.localRotation = Quaternion.Euler(0, 0, 90);
            return renderer.transform;
        }

        public void UpdateDoor(Vector3 player, bool crewNear)
        {
            bool open = Vector2.Distance(player, transform.position) < 2.2f || crewNear;
            if (open && !m_WasOpen && Vector2.Distance(player, transform.position) < 3f)
                GameAudio.Instance?.Play(Sfx.Door, 0.6f);
            m_WasOpen = open;
            m_Open = Mathf.MoveTowards(m_Open, open ? 1f : 0f, Time.deltaTime * 3.5f);
            Apply();
        }

        void Apply()
        {
            float offset = m_Span * 0.25f + m_Open * m_Span * 0.25f;
            var axis = m_Vertical ? Vector3.up : Vector3.right;
            m_A.localPosition = -axis * offset;
            m_B.localPosition = axis * offset;
            float visible = 1f - m_Open * 0.85f;
            m_A.localScale = new Vector3(m_Span * 0.5f * visible, 0.6f, 1f);
            m_B.localScale = new Vector3(m_Span * 0.5f * visible, 0.6f, 1f);
        }
    }
}
