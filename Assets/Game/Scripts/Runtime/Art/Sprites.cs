using System.Collections.Generic;
using UnityEngine;

namespace Sol3.Game.Art
{
    public enum Facing
    {
        Down = 0,
        Up = 1,
        Side = 2,
    }

    public enum Icon
    {
        Energy, Oxygen, Water, Food, Health, Radiation, Sun, Moon, Pause, Sound, Mute, Book, Check, Alert, Wrench, Flask, Signal, Shield, Leaf, Battery,
    }

    /// <summary>
    /// Gráficos originales del juego generados por código (sin recursos de terceros): astronautas animados,
    /// suelos, paredes, máquinas, decoración, iconos y piezas de interfaz. Se generan una vez y se cachean.
    /// </summary>
    public static class Sprites
    {
        public const float PixelsPerUnit = 64f;
        static readonly Dictionary<string, Sprite> s_Cache = new Dictionary<string, Sprite>();

        static Sprite Make(string key, Paint paint, Vector2 pivot, bool tiled = false, Vector4 border = default)
        {
            var texture = paint.ToTexture(FilterMode.Bilinear, tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp);
            texture.name = key;
            var sprite = Sprite.Create(texture, new Rect(0, 0, paint.Width, paint.Height), pivot, PixelsPerUnit, 0,
                SpriteMeshType.FullRect, border);
            sprite.name = key;
            s_Cache[key] = sprite;
            return sprite;
        }

        static bool Cached(string key, out Sprite sprite) => s_Cache.TryGetValue(key, out sprite);

        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        // ================================================================== Personajes

        /// <summary>Astronauta en vista superior (3/4). frame 0 = quieto, 1-4 = caminar.</summary>
        public static Sprite Astronaut(Facing facing, int frame, Color accent)
        {
            string key = $"astro_{facing}_{frame}_{ColorUtility.ToHtmlStringRGB(accent)}";
            if (Cached(key, out var cached))
                return cached;

            var p = new Paint(48, 56);
            float swing = frame == 0 ? 0f : Mathf.Sin((frame - 1) * Mathf.PI * 0.5f) * 3f;
            var suit = Palette.SoftWhite;
            var shadow = Palette.SoftWhite.Shade(-0.22f);
            var graphite = Palette.Graphite;

            p.Ellipse(24, 7, 13, 4.5f, new Color(0, 0, 0, 0.28f));

            if (facing == Facing.Side)
            {
                p.RoundRect(17 + swing, 4, 8, 9, 3, graphite);
                p.RoundRect(23 - swing, 4, 8, 9, 3, graphite.Shade(0.15f));
                p.RoundRect(26, 15, 8, 16, 3, graphite);                       // mochila (detrás)
                p.RoundRect(14, 11, 17, 21, 7, shadow);
                p.RoundRect(15, 12, 15, 19, 6, suit);
                p.Rect(15, 19, 15, 3, accent);
                p.RoundRect(15 - swing * 0.6f, 14, 6, 13, 3, suit.Shade(-0.1f)); // brazo
                p.Circle(22, 36, 10, shadow);
                p.Circle(22, 37, 9.3f, suit);
                p.Ellipse(16.5f, 36, 4.6f, 5.2f, Palette.Ink);
                p.Ellipse(15.6f, 37.6f, 1.6f, 1.4f, Palette.Cyan.WithAlpha(0.7f));
                p.Rect(19, 44.5f, 6, 1.6f, accent);
            }
            else
            {
                bool up = facing == Facing.Up;
                p.RoundRect(16, 4 + swing, 7, 9, 3, graphite);
                p.RoundRect(25, 4 - swing, 7, 9, 3, graphite);
                p.RoundRect(11, 14 - swing * 0.7f, 6, 14, 3, suit.Shade(-0.12f));
                p.RoundRect(31, 14 + swing * 0.7f, 6, 14, 3, suit.Shade(-0.12f));
                p.RoundRect(14, 11, 20, 21, 8, shadow);
                p.RoundRect(15, 12, 18, 19, 7, suit);
                if (up)
                {
                    p.RoundRect(16.5f, 13, 15, 16, 4, graphite);
                    p.Rect(16.5f, 24, 15, 2.5f, accent);
                }
                else
                {
                    p.Rect(15, 19, 18, 3, accent);
                    p.RoundRect(20, 23, 8, 5, 1.5f, graphite);
                    p.Circle(22, 25.5f, 0.9f, Palette.Cyan);
                    p.Circle(26, 25.5f, 0.9f, Palette.Green);
                }
                p.Circle(24, 37, 10, shadow);
                p.Circle(24, 38, 9.3f, suit);
                if (up)
                {
                    p.Rect(20, 44, 8, 1.6f, accent);
                }
                else
                {
                    p.Ellipse(24, 36.5f, 6.2f, 4.8f, Palette.Ink);
                    p.Ellipse(21.8f, 38, 1.8f, 1.3f, Palette.Cyan.WithAlpha(0.6f));
                }
            }
            return Make(key, p, new Vector2(0.5f, 0.12f));
        }

        // ================================================================== Escenario

        public static Sprite Floor(Color baseColor, int style, int seed)
        {
            string key = $"floor_{ColorUtility.ToHtmlStringRGB(baseColor)}_{style}";
            if (Cached(key, out var cached))
                return cached;
            var p = new Paint(64, 64, baseColor);
            p.Noise(0.06f, seed, 0.3f);
            var seam = baseColor.Shade(-0.35f);
            var hi = baseColor.Shade(0.12f);
            switch (style)
            {
                case 0: // placas metálicas
                    p.Rect(0, 31, 64, 2, seam);
                    p.Rect(31, 0, 2, 64, seam);
                    p.Rect(0, 33, 64, 1, hi);
                    p.Rect(33, 0, 1, 64, hi);
                    foreach (var c in new[] { new Vector2(5, 5), new Vector2(27, 5), new Vector2(5, 27), new Vector2(27, 27), new Vector2(37, 37), new Vector2(59, 37), new Vector2(37, 59), new Vector2(59, 59) })
                        p.Circle(c.x, c.y, 1.3f, seam);
                    break;
                case 1: // rejilla
                    for (int i = 0; i < 64; i += 8)
                    {
                        p.Rect(i, 0, 2, 64, seam.WithAlpha(0.7f));
                        p.Rect(0, i, 64, 2, seam.WithAlpha(0.7f));
                    }
                    break;
                case 2: // pasillo con guía luminosa
                    p.Rect(0, 0, 64, 3, seam);
                    p.Rect(0, 61, 64, 3, seam);
                    p.Rect(0, 30, 64, 4, Palette.Cyan.WithAlpha(0.18f));
                    p.Rect(0, 31.5f, 64, 1, Palette.Cyan.WithAlpha(0.55f));
                    break;
                case 3: // laboratorio: baldosas claras
                    p.Rect(0, 0, 64, 1, seam.WithAlpha(0.5f));
                    p.Rect(0, 32, 64, 1, seam.WithAlpha(0.5f));
                    p.Rect(0, 0, 1, 64, seam.WithAlpha(0.5f));
                    p.Rect(32, 0, 1, 64, seam.WithAlpha(0.5f));
                    break;
            }
            return Make(key, p, Center, true);
        }

        public static Sprite Wall()
        {
            if (Cached("wall", out var cached))
                return cached;
            var p = new Paint(64, 64, Palette.Hex("#152230"));
            p.Noise(0.05f, 3, 0.4f);
            p.Rect(0, 0, 64, 6, Palette.Hex("#0D1620"));
            p.Rect(0, 58, 64, 6, Palette.Steel.Shade(0.08f));
            p.Rect(0, 56, 64, 2, Palette.Cyan.WithAlpha(0.25f));
            p.Rect(0, 20, 64, 1, Palette.Steel.WithAlpha(0.6f));
            p.Rect(0, 42, 64, 1, Palette.Steel.WithAlpha(0.6f));
            return Make("wall", p, Center, true);
        }

        public static Sprite MarsGround()
        {
            if (Cached("mars", out var cached))
                return cached;
            var p = new Paint(128, 128, Palette.Hex("#A65D36"));
            p.Noise(0.16f, 9, 0.06f);
            p.Noise(0.08f, 4, 0.25f);
            var rng = new System.Random(5);
            for (int i = 0; i < 26; i++)
            {
                float x = rng.Next(128), y = rng.Next(128), r = 1.5f + (float)rng.NextDouble() * 3.5f;
                p.Ellipse(x + 1, y - 1, r, r * 0.7f, new Color(0, 0, 0, 0.18f));
                p.Ellipse(x, y, r, r * 0.75f, Palette.Hex("#7A3F25"));
                p.Ellipse(x - r * 0.25f, y + r * 0.2f, r * 0.45f, r * 0.3f, Palette.Hex("#C27A4B"));
            }
            return Make("mars", p, Center, true);
        }

        public static Sprite DoorPanel()
        {
            if (Cached("door", out var cached))
                return cached;
            var p = new Paint(64, 32);
            p.RoundRect(0, 4, 64, 24, 3, Palette.Steel.Shade(-0.1f));
            p.Rect(0, 14, 64, 4, Palette.Graphite);
            p.Rect(54, 6, 3, 20, Palette.Cyan.WithAlpha(0.8f));
            for (int i = 4; i < 50; i += 10)
                p.Line(i, 6, i + 6, 12, 2.5f, Palette.Mars.WithAlpha(0.6f));
            return Make("door", p, Center);
        }

        // ================================================================== Máquinas y decoración

        public static Sprite Console(int width)
        {
            string key = "console_" + width;
            if (Cached(key, out var cached))
                return cached;
            var p = new Paint(width, 72);
            p.RoundRect(2, 2, width - 4, 30, 6, Palette.Graphite);
            p.RoundRect(4, 4, width - 8, 26, 5, Palette.Steel.Shade(-0.15f));
            for (int x = 10; x < width - 10; x += 10)
                p.RoundRect(x, 9, 6, 4, 1, Palette.Steel.Shade(0.2f));
            p.RoundRect(6, 30, width - 12, 40, 6, Palette.Hex("#0B141E"));
            p.Rect(8, 32, width - 16, 2, Palette.Steel);
            return Make(key, p, new Vector2(0.5f, 0.15f));
        }

        public static Sprite ScreenGlow(int width, int height)
        {
            string key = $"screen_{width}_{height}";
            if (Cached(key, out var cached))
                return cached;
            var p = new Paint(width, height);
            p.RoundRect(0, 0, width, height, 4, Color.white.WithAlpha(0.95f));
            for (int y = 4; y < height - 2; y += 5)
                p.Rect(4, y, (width - 8) * (0.4f + 0.5f * Mathf.PerlinNoise(y * 0.3f, width)), 1.6f, Palette.SpaceBlue.WithAlpha(0.35f));
            return Make(key, p, Center);
        }

        public static Sprite OxygenGenerator()
        {
            if (Cached("o2", out var cached))
                return cached;
            var p = new Paint(112, 96);
            p.Ellipse(56, 12, 52, 9, new Color(0, 0, 0, 0.25f));
            p.RoundRect(10, 12, 92, 40, 8, Palette.Steel.Shade(-0.1f));
            foreach (float x in new[] { 28f, 84f })
            {
                p.Circle(x, 52, 22, Palette.SoftWhite.Shade(-0.2f));
                p.Circle(x, 54, 20, Palette.SoftWhite);
                p.Circle(x, 54, 9, Palette.Cyan.Shade(-0.2f));
                p.Ring(x, 54, 14, 2, Palette.Steel);
            }
            p.RoundRect(46, 20, 20, 22, 4, Palette.Graphite);
            p.Ring(56, 31, 6, 2, Palette.Green);
            p.Line(56, 31, 59, 35, 1.6f, Palette.SoftWhite);
            return Make("o2", p, new Vector2(0.5f, 0.12f));
        }

        public static Sprite WaterRecycler()
        {
            if (Cached("water", out var cached))
                return cached;
            var p = new Paint(112, 96);
            p.Ellipse(56, 12, 52, 9, new Color(0, 0, 0, 0.25f));
            p.RoundRect(6, 14, 100, 30, 8, Palette.Steel.Shade(-0.1f));
            p.Circle(80, 56, 26, Palette.Steel.Shade(0.1f));
            p.Circle(80, 58, 23, Palette.Hex("#2C6C8C"));
            p.Circle(80, 58, 15, Palette.Cyan.WithAlpha(0.65f));
            p.Line(14, 30, 54, 30, 7, Palette.SoftWhite.Shade(-0.25f));
            p.Line(54, 30, 54, 58, 7, Palette.SoftWhite.Shade(-0.25f));
            p.Line(54, 58, 58, 58, 7, Palette.SoftWhite.Shade(-0.25f));
            p.RoundRect(14, 40, 26, 30, 5, Palette.SoftWhite.Shade(-0.1f));
            p.Rect(18, 46, 18, 3, Palette.Cyan);
            return Make("water", p, new Vector2(0.5f, 0.12f));
        }

        public static Sprite CropBed()
        {
            if (Cached("bed", out var cached))
                return cached;
            var p = new Paint(96, 64);
            p.Ellipse(48, 8, 46, 7, new Color(0, 0, 0, 0.22f));
            p.RoundRect(2, 6, 92, 54, 7, Palette.SoftWhite.Shade(-0.35f));
            p.RoundRect(7, 11, 82, 44, 5, Palette.Hex("#4A3226"));
            p.Noise(0.08f, 21, 0.4f);
            for (int x = 14; x < 86; x += 12)
                p.Rect(x, 13, 2, 40, Palette.Hex("#3B261C"));
            return Make("bed", p, new Vector2(0.5f, 0.1f));
        }

        public static Sprite Plants(int stage)
        {
            string key = "plants_" + stage;
            if (Cached(key, out var cached))
                return cached;
            var p = new Paint(96, 64);
            var leaf = Palette.Green.Shade(-0.15f);
            var rng = new System.Random(stage + 3);
            for (int x = 18; x < 86; x += 12)
                for (int y = 20; y < 52; y += 11)
                {
                    float size = stage == 1 ? 2.5f : stage == 2 ? 4.5f : 6f;
                    float jitter = (float)rng.NextDouble() * 2f;
                    p.Ellipse(x - size * 0.6f, y + jitter, size, size * 0.6f, leaf);
                    p.Ellipse(x + size * 0.6f, y + jitter, size, size * 0.6f, leaf.Shade(0.15f));
                    if (stage >= 3)
                        p.Circle(x, y + jitter + 1, 2.2f, Palette.Alert.Shade(0.1f));
                }
            return Make(key, p, new Vector2(0.5f, 0.1f));
        }

        public static Sprite LabBench()
        {
            if (Cached("lab", out var cached))
                return cached;
            var p = new Paint(160, 72);
            p.Ellipse(80, 8, 76, 7, new Color(0, 0, 0, 0.22f));
            p.RoundRect(4, 8, 152, 44, 6, Palette.SoftWhite.Shade(-0.12f));
            p.RoundRect(6, 10, 148, 40, 5, Palette.SoftWhite);
            p.RoundRect(20, 26, 28, 34, 5, Palette.Graphite);       // microscopio
            p.Circle(34, 56, 6, Palette.Steel);
            p.Ring(34, 40, 6, 2, Palette.Cyan);
            for (int i = 0; i < 6; i++)                             // muestras
            {
                var c = i % 3 == 0 ? Palette.Green : i % 3 == 1 ? Palette.Cyan : Palette.Mars;
                p.RoundRect(70 + i * 9, 24, 6, 20, 3, Palette.SoftWhite.Shade(-0.3f));
                p.RoundRect(70.5f + i * 9, 25, 5, 10, 2.5f, c);
            }
            p.RoundRect(128, 20, 20, 26, 3, Palette.Hex("#0B141E"));
            return Make("lab", p, new Vector2(0.5f, 0.1f));
        }

        public static Sprite BatteryRack()
        {
            if (Cached("battery", out var cached))
                return cached;
            var p = new Paint(160, 72);
            p.Ellipse(80, 8, 76, 7, new Color(0, 0, 0, 0.25f));
            p.RoundRect(4, 6, 152, 62, 6, Palette.Graphite);
            for (int i = 0; i < 6; i++)
            {
                p.RoundRect(12 + i * 24, 12, 18, 50, 4, Palette.Steel.Shade(-0.25f));
                p.Rect(15 + i * 24, 58, 12, 2, Palette.Mars);
            }
            return Make("battery", p, new Vector2(0.5f, 0.1f));
        }

        public static Sprite PowerPanel()
        {
            if (Cached("power", out var cached))
                return cached;
            var p = new Paint(96, 80);
            p.Ellipse(48, 8, 44, 7, new Color(0, 0, 0, 0.25f));
            p.RoundRect(6, 6, 84, 70, 6, Palette.Steel.Shade(-0.2f));
            p.RoundRect(12, 40, 72, 30, 4, Palette.Hex("#0B141E"));
            for (int i = 0; i < 4; i++)
                p.RoundRect(16 + i * 18, 14, 12, 18, 2, Palette.Graphite);
            p.Line(18, 55, 30, 62, 2, Palette.Amber);
            p.Line(30, 62, 44, 48, 2, Palette.Amber);
            p.Line(44, 48, 60, 58, 2, Palette.Amber);
            return Make("power", p, new Vector2(0.5f, 0.1f));
        }

        public static Sprite CommsConsole()
        {
            if (Cached("comms", out var cached))
                return cached;
            var p = new Paint(112, 88);
            p.Ellipse(56, 8, 52, 7, new Color(0, 0, 0, 0.22f));
            p.RoundRect(6, 6, 100, 32, 6, Palette.Graphite);
            p.RoundRect(14, 34, 84, 50, 6, Palette.Hex("#0B141E"));
            p.Ring(56, 58, 16, 3, Palette.Cyan.WithAlpha(0.8f));
            p.Ring(56, 58, 9, 2, Palette.Cyan.WithAlpha(0.5f));
            p.Line(56, 58, 70, 72, 3, Palette.SoftWhite);
            p.Circle(56, 58, 3, Palette.SoftWhite);
            return Make("comms", p, new Vector2(0.5f, 0.1f));
        }

        public static Sprite ShelterConsole()
        {
            if (Cached("shelterc", out var cached))
                return cached;
            var p = new Paint(112, 80);
            p.Ellipse(56, 8, 52, 7, new Color(0, 0, 0, 0.22f));
            p.RoundRect(6, 6, 100, 68, 8, Palette.Steel.Shade(-0.25f));
            p.Circle(56, 42, 22, Palette.Amber.Shade(-0.1f));
            DrawTrefoil(p, 56, 42, 18, Palette.Graphite);
            return Make("shelterc", p, new Vector2(0.5f, 0.1f));
        }

        public static Sprite Bunk()
        {
            if (Cached("bunk", out var cached))
                return cached;
            var p = new Paint(64, 112);
            p.RoundRect(4, 4, 56, 104, 6, Palette.Steel.Shade(-0.1f));
            p.RoundRect(8, 8, 48, 96, 5, Palette.Hex("#355C7D"));
            p.RoundRect(12, 78, 40, 20, 6, Palette.SoftWhite.Shade(-0.1f));
            return Make("bunk", p, new Vector2(0.5f, 0.05f));
        }

        public static Sprite Crate()
        {
            if (Cached("crate", out var cached))
                return cached;
            var p = new Paint(48, 48);
            p.Ellipse(24, 6, 22, 5, new Color(0, 0, 0, 0.25f));
            p.RoundRect(4, 6, 40, 38, 4, Palette.SoftWhite.Shade(-0.18f));
            p.Rect(4, 22, 40, 5, Palette.Mars);
            p.Rect(21, 6, 6, 38, Palette.SoftWhite.Shade(-0.32f));
            return Make("crate", p, new Vector2(0.5f, 0.12f));
        }

        public static Sprite Tank()
        {
            if (Cached("tank", out var cached))
                return cached;
            var p = new Paint(56, 56);
            p.Ellipse(28, 8, 24, 6, new Color(0, 0, 0, 0.25f));
            p.Circle(28, 30, 22, Palette.SoftWhite.Shade(-0.2f));
            p.Circle(28, 31, 20, Palette.SoftWhite.Shade(-0.05f));
            p.Ring(28, 31, 13, 2, Palette.Steel);
            p.Circle(28, 31, 4, Palette.Cyan);
            return Make("tank", p, new Vector2(0.5f, 0.12f));
        }

        public static Sprite Soft(int size)
        {
            string key = "soft_" + size;
            if (Cached(key, out var cached))
                return cached;
            var p = new Paint(size, size);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    p.Blend(x, y, Color.white, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d));
                }
            return Make(key, p, Center);
        }

        public static Sprite Dot()
        {
            if (Cached("dot", out var cached))
                return cached;
            var p = new Paint(16, 16);
            p.Circle(8, 8, 6.5f, Color.white);
            return Make("dot", p, Center);
        }

        public static Sprite WarningMarker()
        {
            if (Cached("warn", out var cached))
                return cached;
            var p = new Paint(48, 48);
            p.Line(24, 40, 6, 8, 6, Palette.Alert);
            p.Line(24, 40, 42, 8, 6, Palette.Alert);
            p.Line(6, 8, 42, 8, 6, Palette.Alert);
            for (int y = 9; y < 38; y++)
                p.Rect(24 - (38 - y) * 0.58f, y, (38 - y) * 1.16f, 1, Palette.Alert);
            p.RoundRect(22, 18, 4, 14, 2, Color.white);
            p.Circle(24, 13, 2.2f, Color.white);
            return Make("warn", p, new Vector2(0.5f, 0f));
        }

        // ================================================================== Interfaz

        public static Sprite Panel()
        {
            if (Cached("panel", out var cached))
                return cached;
            var p = new Paint(48, 48);
            p.RoundRect(0, 0, 48, 48, 12, Color.white);
            return Make("panel", p, Center, false, new Vector4(16, 16, 16, 16));
        }

        public static Sprite PanelOutline()
        {
            if (Cached("panelo", out var cached))
                return cached;
            var inner = new Paint(48, 48);
            for (int y = 0; y < 48; y++)
                for (int x = 0; x < 48; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - 24f) - 12f, qy = Mathf.Abs(y + 0.5f - 24f) - 12f;
                    float d = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - 12f;
                    float edge = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + 2.5f);
                    inner.Blend(x, y, Color.white, edge);
                }
            return Make("panelo", inner, Center, false, new Vector4(16, 16, 16, 16));
        }

        public static Sprite Icon(Icon icon)
        {
            string key = "icon_" + icon;
            if (Cached(key, out var cached))
                return cached;
            var p = new Paint(64, 64);
            var w = Color.white;
            switch (icon)
            {
                case Art.Icon.Energy:
                    p.Line(36, 58, 22, 30, 7, w); p.Line(22, 30, 40, 32, 7, w); p.Line(40, 32, 26, 6, 7, w);
                    break;
                case Art.Icon.Battery:
                    p.Line(10, 18, 50, 18, 5, w); p.Line(10, 46, 50, 46, 5, w); p.Line(10, 18, 10, 46, 5, w); p.Line(50, 18, 50, 46, 5, w);
                    p.Rect(52, 26, 6, 12, w); p.Rect(16, 24, 18, 16, w);
                    break;
                case Art.Icon.Oxygen:
                    p.Ring(26, 30, 15, 6, w); p.Ring(48, 46, 8, 4, w); p.Circle(46, 16, 5, w);
                    break;
                case Art.Icon.Water:
                    p.Circle(32, 24, 16, w);
                    for (int y = 24; y < 58; y++)
                        p.Rect(32 - (58 - y) * 0.47f, y, (58 - y) * 0.94f, 1, w);
                    break;
                case Art.Icon.Food:
                case Art.Icon.Leaf:
                    p.Ellipse(34, 36, 18, 11, w); p.Line(14, 14, 34, 36, 4, w);
                    break;
                case Art.Icon.Health:
                    p.RoundRect(25, 8, 14, 48, 4, w); p.RoundRect(8, 25, 48, 14, 4, w);
                    break;
                case Art.Icon.Radiation:
                    p.Ring(32, 32, 26, 4, w); DrawTrefoil(p, 32, 32, 20, w);
                    break;
                case Art.Icon.Sun:
                    p.Circle(32, 32, 12, w);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        p.Line(32 + Mathf.Cos(a) * 18, 32 + Mathf.Sin(a) * 18, 32 + Mathf.Cos(a) * 27, 32 + Mathf.Sin(a) * 27, 4, w);
                    }
                    break;
                case Art.Icon.Moon:
                    for (int y = 0; y < 64; y++)
                        for (int x = 0; x < 64; x++)
                        {
                            float inA = Mathf.Clamp01(22.5f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(30, 32)));
                            float outB = Mathf.Clamp01(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(42, 40)) - 17.5f);
                            p.Blend(x, y, w, inA * outB);
                        }
                    break;
                case Art.Icon.Pause:
                    p.RoundRect(16, 12, 11, 40, 3, w); p.RoundRect(37, 12, 11, 40, 3, w);
                    break;
                case Art.Icon.Sound:
                case Art.Icon.Mute:
                    p.Rect(10, 24, 12, 16, w);
                    for (int x = 22; x < 38; x++)
                        p.Rect(x, 24 - (x - 22) * 0.8f, 1, 16 + (x - 22) * 1.6f, w);
                    if (icon == Art.Icon.Sound)
                    {
                        for (int s = 0; s < 10; s++)
                        {
                            float a0 = -0.9f + s * 0.18f, a1 = a0 + 0.18f;
                            p.Line(38 + Mathf.Cos(a0) * 10, 32 + Mathf.Sin(a0) * 10, 38 + Mathf.Cos(a1) * 10, 32 + Mathf.Sin(a1) * 10, 4, w);
                            p.Line(38 + Mathf.Cos(a0) * 19, 32 + Mathf.Sin(a0) * 19, 38 + Mathf.Cos(a1) * 19, 32 + Mathf.Sin(a1) * 19, 4, w);
                        }
                    }
                    else
                    {
                        p.Line(42, 22, 58, 42, 5, w);
                        p.Line(58, 22, 42, 42, 5, w);
                    }
                    break;
                case Art.Icon.Book:
                    p.RoundRect(8, 10, 22, 44, 3, w); p.RoundRect(34, 10, 22, 44, 3, w);
                    break;
                case Art.Icon.Check:
                    p.Line(12, 32, 26, 18, 7, w); p.Line(26, 18, 52, 48, 7, w);
                    break;
                case Art.Icon.Alert:
                    p.RoundRect(27, 22, 10, 34, 4, w); p.Circle(32, 12, 6, w);
                    break;
                case Art.Icon.Wrench:
                    p.Line(16, 16, 44, 44, 9, w); p.Ring(48, 48, 9, 7, w);
                    break;
                case Art.Icon.Flask:
                    p.Rect(26, 34, 12, 22, w);
                    for (int y = 8; y < 36; y++)
                        p.Rect(32 - (40 - y) * 0.6f, y, (40 - y) * 1.2f, 1, w);
                    break;
                case Art.Icon.Signal:
                    p.Circle(16, 16, 6, w); p.Ring(16, 16, 20, 5, w); p.Ring(16, 16, 34, 5, w);
                    break;
                case Art.Icon.Shield:
                    p.RoundRect(14, 22, 36, 34, 8, w);
                    for (int y = 6; y < 24; y++)
                        p.Rect(14 + (24 - y) * 0.9f, y, 36 - (24 - y) * 1.8f, 1, w);
                    break;
            }
            return Make(key, p, Center);
        }

        static void DrawTrefoil(Paint p, float cx, float cy, float r, Color color)
        {
            p.Circle(cx, cy, r * 0.18f, color);
            for (int i = 0; i < 3; i++)
            {
                float a0 = Mathf.PI / 2f + i * Mathf.PI * 2f / 3f - 0.52f;
                for (int s = 0; s <= 12; s++)
                {
                    float a = a0 + s * (1.04f / 12f);
                    p.Line(cx + Mathf.Cos(a) * r * 0.3f, cy + Mathf.Sin(a) * r * 0.3f, cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, 2.2f, color);
                }
            }
        }

        public static Sprite MenuBackground()
        {
            if (Cached("menubg", out var cached))
                return cached;
            var p = new Paint(640, 360);
            p.VerticalGradient(Palette.Hex("#8A4B2C"), Palette.Hex("#1B1F2E"));
            var rng = new System.Random(11);
            for (int i = 0; i < 140; i++)
                p.Circle(rng.Next(640), 200 + rng.Next(160), 0.6f + (float)rng.NextDouble(), Color.white.WithAlpha(0.3f + (float)rng.NextDouble() * 0.5f));
            for (int x = 0; x < 640; x++)
            {
                float h = 120 + Mathf.PerlinNoise(x * 0.008f, 1.3f) * 60f;
                for (int y = 0; y < h; y++)
                    p.Blend(x, y, Color.Lerp(Palette.Hex("#5E2F1D"), Palette.Hex("#B8693F"), y / h), 1f);
                float h2 = 70 + Mathf.PerlinNoise(x * 0.012f, 7.1f) * 40f;
                for (int y = 0; y < h2; y++)
                    p.Blend(x, y, Color.Lerp(Palette.Hex("#3E1F14"), Palette.Hex("#8A4A2C"), y / h2), 1f);
            }
            // Silueta de Base Aurora
            p.Ellipse(420, 96, 46, 30, Palette.Hex("#C9D3DA"));
            p.Rect(374, 66, 92, 30, Palette.Hex("#C9D3DA"));
            p.RoundRect(300, 68, 80, 22, 10, Palette.Hex("#AEB9C2"));
            p.RoundRect(462, 68, 90, 22, 10, Palette.Hex("#AEB9C2"));
            p.Rect(372, 98, 96, 3, Palette.Cyan);
            p.Rect(520, 90, 3, 50, Palette.Hex("#8C97A0"));
            p.Circle(521, 142, 4, Palette.Alert);
            for (int i = 0; i < 5; i++)
                p.Rect(200 + i * 18, 60, 14, 6, Palette.Hex("#22344C"));
            return Make("menubg", p, Center);
        }
    }
}
