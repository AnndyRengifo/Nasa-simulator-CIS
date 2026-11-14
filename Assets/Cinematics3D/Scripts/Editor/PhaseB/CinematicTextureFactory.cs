using UnityEngine;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>
    /// Texturas procedurales (PNG en Textures/Generated). Son provisionales y sustituibles:
    /// basta con reemplazar el PNG manteniendo el nombre, o reasignar la textura en el material.
    /// </summary>
    internal static class CinematicTextureFactory
    {
        static readonly CinematicBuildKit.TextureSpec Tiled = new CinematicBuildKit.TextureSpec { Wrap = TextureWrapMode.Repeat, MaxSize = 1024, Mipmaps = true };
        static readonly CinematicBuildKit.TextureSpec Clamped = new CinematicBuildKit.TextureSpec { Wrap = TextureWrapMode.Clamp, MaxSize = 1024, Mipmaps = true };

        public static void BuildAll(BuildContext ctx)
        {
            CinematicBuildKit.GetTexture(ctx, "T_Starfield", 2048, 1024, () => Starfield(2048, 1024),
                new CinematicBuildKit.TextureSpec { Wrap = TextureWrapMode.Clamp, MaxSize = 2048, Mipmaps = false });
            CinematicBuildKit.GetTexture(ctx, "T_Mars_Planet", 1024, 512, () => MarsPlanet(1024, 512), Clamped);
            CinematicBuildKit.GetTexture(ctx, "T_Earth_Planet", 512, 256, () => EarthPlanet(512, 256), Clamped);
            CinematicBuildKit.GetTexture(ctx, "T_Sun_Surface", 256, 256, () => SunSurface(256), Tiled);
            CinematicBuildKit.GetTexture(ctx, "T_Mars_Ground", 1024, 1024, () => MarsGround(1024), Tiled);
            CinematicBuildKit.GetTexture(ctx, "T_Base_Panel", 256, 256, () => Panel(256), Tiled);
            CinematicBuildKit.GetTexture(ctx, "T_Solar_Cells", 256, 256, () => SolarCells(256), Tiled);
            CinematicBuildKit.GetTexture(ctx, "T_Hazard_Stripes", 128, 32, () => Hazard(128, 32), Tiled);
            CinematicBuildKit.GetTexture(ctx, "T_Screen_Data", 256, 128, () => ScreenData(256, 128, 11), Clamped);
            CinematicBuildKit.GetTexture(ctx, "T_Screen_Alert", 256, 128, () => ScreenAlert(256, 128), Clamped);
            CinematicBuildKit.GetTexture(ctx, "T_Screen_Mission", 512, 256, () => ScreenMission(512, 256), Clamped);
            CinematicBuildKit.GetTexture(ctx, "T_Window_MarsView", 512, 256, () => WindowView(512, 256), Clamped);
            CinematicBuildKit.GetTexture(ctx, "T_FX_SoftDot", 64, 64, () => SoftDot(64),
                new CinematicBuildKit.TextureSpec { Wrap = TextureWrapMode.Clamp, MaxSize = 64, Mipmaps = true, Alpha = true });
        }

        static Color32[] Starfield(int width, int height)
        {
            var pixels = new Color32[width * height];
            var random = new System.Random(42);
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)height;
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)width;
                    float band = Mathf.Exp(-Mathf.Pow(v - 0.5f - 0.12f * Mathf.Sin(u * Mathf.PI * 2f), 2f) / 0.006f);
                    float cloud = band * CinematicBuildKit.Fbm(u * 18f, v * 9f, 4) * 0.16f;
                    var color = new Color(0.010f + cloud * 0.7f, 0.012f + cloud * 0.75f, 0.022f + cloud);
                    pixels[y * width + x] = color;
                }
            }

            for (int i = 0; i < 5200; i++)
            {
                int x = random.Next(width);
                int y = random.Next(height);
                float brightness = Mathf.Pow((float)random.NextDouble(), 5f) * 0.9f + 0.1f;
                float temperature = (float)random.NextDouble();
                var color = Color.Lerp(new Color(0.75f, 0.85f, 1f), new Color(1f, 0.9f, 0.75f), temperature) * brightness;
                color.a = 1f;
                Plot(pixels, width, height, x, y, color);
                if (brightness > 0.75f)
                {
                    var halo = color * 0.35f;
                    halo.a = 1f;
                    Plot(pixels, width, height, x + 1, y, halo);
                    Plot(pixels, width, height, x - 1, y, halo);
                    Plot(pixels, width, height, x, y + 1, halo);
                    Plot(pixels, width, height, x, y - 1, halo);
                }
            }
            return pixels;
        }

        static void Plot(Color32[] pixels, int width, int height, int x, int y, Color color)
        {
            x = (x % width + width) % width;
            if (y < 0 || y >= height)
                return;
            Color current = pixels[y * width + x];
            pixels[y * width + x] = new Color(Mathf.Max(current.r, color.r), Mathf.Max(current.g, color.g), Mathf.Max(current.b, color.b), 1f);
        }

        static Color32[] MarsPlanet(int width, int height)
        {
            var dark = CinematicBuildKit.Hex("#5E2F1D");
            var mid = CinematicBuildKit.Hex("#B8693F");
            var light = CinematicBuildKit.Hex("#E3A372");
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)height;
                float latitude = (v - 0.5f) * 180f;
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)width;
                    float angle = u * Mathf.PI * 2f;
                    float nx = Mathf.Cos(angle) * 2.2f + 10f, nz = Mathf.Sin(angle) * 2.2f + 10f;
                    float n = CinematicBuildKit.Fbm(nx + v * 3f, nz + v * 5f, 6);
                    var color = n < 0.5f ? Color.Lerp(dark, mid, Mathf.InverseLerp(0.3f, 0.5f, n)) : Color.Lerp(mid, light, Mathf.InverseLerp(0.5f, 0.68f, n));
                    float canyon = Mathf.Exp(-Mathf.Pow((v - 0.47f - 0.02f * Mathf.Sin(u * 20f)) * 60f, 2f)) * (u > 0.55f && u < 0.75f ? 1f : 0f);
                    color = Color.Lerp(color, dark * 0.8f, canyon * 0.7f);
                    float cap = Mathf.InverseLerp(68f + n * 8f, 80f, Mathf.Abs(latitude));
                    color = Color.Lerp(color, new Color(0.93f, 0.9f, 0.88f), cap);
                    color.a = 1f;
                    pixels[y * width + x] = color;
                }
            }
            return pixels;
        }

        static Color32[] EarthPlanet(int width, int height)
        {
            var ocean = CinematicBuildKit.Hex("#1B4F8A");
            var deep = CinematicBuildKit.Hex("#0E2F5A");
            var land = CinematicBuildKit.Hex("#4E7D3A");
            var desert = CinematicBuildKit.Hex("#B59A62");
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)height;
                float latitude = Mathf.Abs((v - 0.5f) * 180f);
                for (int x = 0; x < width; x++)
                {
                    float angle = x / (float)width * Mathf.PI * 2f;
                    float nx = Mathf.Cos(angle) * 1.8f + 30f, nz = Mathf.Sin(angle) * 1.8f + 30f;
                    float n = CinematicBuildKit.Fbm(nx + v * 4f, nz + v * 4f, 5);
                    Color color = n > 0.53f ? Color.Lerp(land, desert, Mathf.InverseLerp(0.6f, 0.75f, n) * (1f - latitude / 90f)) : Color.Lerp(deep, ocean, n / 0.53f);
                    float clouds = Mathf.InverseLerp(0.55f, 0.75f, CinematicBuildKit.Fbm(nx * 2f + 5f, nz * 2f + v * 9f, 4));
                    color = Color.Lerp(color, Color.white, clouds * 0.75f + Mathf.InverseLerp(72f, 82f, latitude));
                    color.a = 1f;
                    pixels[y * width + x] = color;
                }
            }
            return pixels;
        }

        static Color32[] SunSurface(int size)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = CinematicBuildKit.Fbm(x * 0.05f, y * 0.05f, 5);
                    var color = Color.Lerp(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.95f, 0.7f), Mathf.InverseLerp(0.35f, 0.7f, n));
                    color.a = 1f;
                    pixels[y * size + x] = color;
                }
            }
            return pixels;
        }

        static Color32[] MarsGround(int size)
        {
            var dark = CinematicBuildKit.Hex("#7A3E24");
            var mid = CinematicBuildKit.Hex("#B8693F");
            var dust = CinematicBuildKit.Hex("#D9965E");
            var random = new System.Random(7);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = CinematicBuildKit.Fbm(x * 0.012f, y * 0.012f, 6);
                    float fine = CinematicBuildKit.Fbm(x * 0.15f + 50f, y * 0.15f + 50f, 2);
                    var color = Color.Lerp(dark, mid, Mathf.InverseLerp(0.3f, 0.55f, n));
                    color = Color.Lerp(color, dust, Mathf.InverseLerp(0.55f, 0.75f, n) * 0.8f);
                    color *= 0.85f + fine * 0.3f;
                    if (random.NextDouble() < 0.004)
                        color *= 0.55f;
                    color.a = 1f;
                    pixels[y * size + x] = color;
                }
            }
            return pixels;
        }

        static Color32[] Panel(int size)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float shade = 0.94f + (CinematicBuildKit.Fbm(x * 0.05f, y * 0.05f, 2) - 0.5f) * 0.05f;
                    bool seam = x % 128 < 2 || y % 128 < 2;
                    bool rivet = (x % 128 > 8 && x % 128 < 12 || x % 128 > 116 && x % 128 < 120) && (y % 128 > 8 && y % 128 < 12 || y % 128 > 116 && y % 128 < 120);
                    float value = seam ? 0.62f : rivet ? 0.75f : shade;
                    pixels[y * size + x] = new Color(value, value, value, 1f);
                }
            }
            return pixels;
        }

        static Color32[] SolarCells(int size)
        {
            var pixels = new Color32[size * size];
            var cell = CinematicBuildKit.Hex("#14284A");
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool line = x % 32 < 2 || y % 32 < 2;
                    var color = line ? new Color(0.72f, 0.76f, 0.8f) : cell * (0.9f + 0.2f * (y % 32) / 32f);
                    color.a = 1f;
                    pixels[y * size + x] = color;
                }
            }
            return pixels;
        }

        static Color32[] Hazard(int width, int height)
        {
            var pixels = new Color32[width * height];
            var orange = CinematicBuildKit.Hex("#D88B56");
            var graphite = CinematicBuildKit.Hex("#22262D");
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = ((x + y) / 16) % 2 == 0 ? orange : graphite;
            return pixels;
        }

        static Color32[] ScreenData(int width, int height, int seed)
        {
            var pixels = Fill(width, height, CinematicBuildKit.Hex("#0B1622"));
            var cyan = CinematicBuildKit.Hex("#53C7D8");
            var green = CinematicBuildKit.Hex("#65C69A");
            var random = new System.Random(seed);

            Frame(pixels, width, height, cyan * 0.6f);
            for (int i = 0; i < 6; i++)
            {
                int y = height - 12 - i * 9;
                int length = 20 + random.Next(70);
                Rect(pixels, width, height, 8, y, length, 3, i % 2 == 0 ? cyan * 0.8f : green * 0.8f);
            }
            for (int i = 0; i < 8; i++)
            {
                int bar = 10 + random.Next(40);
                Rect(pixels, width, height, 140 + i * 13, 10, 8, bar, green);
            }
            int previous = height / 2;
            for (int x = 8; x < 120; x++)
            {
                int y = Mathf.Clamp(previous + random.Next(-2, 3), 12, 48);
                Rect(pixels, width, height, x, y, 1, 2, cyan);
                previous = y;
            }
            return pixels;
        }

        static Color32[] ScreenAlert(int width, int height)
        {
            var pixels = Fill(width, height, CinematicBuildKit.Hex("#2A0E10"));
            var red = CinematicBuildKit.Hex("#DB6262");
            Frame(pixels, width, height, red);
            for (int y = 0; y < 14; y++)
                for (int x = 0; x < width; x++)
                    if (((x + y) / 7) % 2 == 0)
                        pixels[y * width + x] = red;
            for (int y = 0; y < 50; y++)
            {
                int half = y / 2;
                Rect(pixels, width, height, width / 2 - half, 40 + 50 - y, half * 2, 1, red);
            }
            Rect(pixels, width, height, width / 2 - 2, 62, 4, 18, Color.black);
            Rect(pixels, width, height, width / 2 - 2, 52, 4, 5, Color.black);
            return pixels;
        }

        static Color32[] ScreenMission(int width, int height)
        {
            var pixels = Fill(width, height, CinematicBuildKit.Hex("#0B1622"));
            var cyan = CinematicBuildKit.Hex("#53C7D8");
            var orange = CinematicBuildKit.Hex("#D88B56");
            var blue = CinematicBuildKit.Hex("#3D7DD8");
            Frame(pixels, width, height, cyan * 0.6f);
            Disc(pixels, width, height, 90, 128, 38, blue);
            Disc(pixels, width, height, 420, 140, 26, orange);
            for (int i = 0; i <= 60; i++)
            {
                float t = i / 60f;
                int x = Mathf.RoundToInt(Mathf.Lerp(128, 394, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(128, 140, t) + Mathf.Sin(t * Mathf.PI) * 70f);
                if (i % 3 != 0)
                    Rect(pixels, width, height, x, y, 2, 2, cyan);
            }
            for (int i = 0; i < 4; i++)
                Rect(pixels, width, height, 20, 24 + i * 10, 120 + i * 30, 4, cyan * 0.7f);
            return pixels;
        }

        static Color32[] WindowView(int width, int height)
        {
            var pixels = new Color32[width * height];
            var skyTop = CinematicBuildKit.Hex("#C89A74");
            var skyLow = CinematicBuildKit.Hex("#E7B88C");
            var ground = CinematicBuildKit.Hex("#8A4A2C");
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)width;
                    float horizon = 0.38f + (CinematicBuildKit.Fbm(u * 6f, 2f, 4) - 0.5f) * 0.18f;
                    float v = y / (float)height;
                    Color color = v < horizon
                        ? ground * (0.75f + 0.3f * CinematicBuildKit.Fbm(u * 30f, v * 30f, 3))
                        : Color.Lerp(skyLow, skyTop, Mathf.InverseLerp(horizon, 1f, v));
                    color.a = 1f;
                    pixels[y * width + x] = color;
                }
            }
            return pixels;
        }

        static Color32[] SoftDot(int size)
        {
            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float a = Mathf.Clamp01(1f - d);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            }
            return pixels;
        }

        static Color32[] Fill(int width, int height, Color color)
        {
            var pixels = new Color32[width * height];
            Color32 c = color;
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = c;
            return pixels;
        }

        static void Rect(Color32[] pixels, int width, int height, int x0, int y0, int w, int h, Color color)
        {
            color.a = 1f;
            Color32 c = color;
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(height, y0 + h); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(width, x0 + w); x++)
                    pixels[y * width + x] = c;
        }

        static void Frame(Color32[] pixels, int width, int height, Color color)
        {
            Rect(pixels, width, height, 0, 0, width, 2, color);
            Rect(pixels, width, height, 0, height - 2, width, 2, color);
            Rect(pixels, width, height, 0, 0, 2, height, color);
            Rect(pixels, width, height, width - 2, 0, 2, height, color);
        }

        static void Disc(Color32[] pixels, int width, int height, int cx, int cy, int radius, Color color)
        {
            color.a = 1f;
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    if (x < 0 || y < 0 || x >= width || y >= height)
                        continue;
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / radius;
                    if (d <= 1f)
                        pixels[y * width + x] = color * (1.05f - d * 0.45f);
                }
            }
        }
    }
}
