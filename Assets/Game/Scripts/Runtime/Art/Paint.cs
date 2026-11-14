using UnityEngine;

namespace Sol3.Game.Art
{
    /// <summary>Lienzo de píxeles con primitivas suavizadas para generar los gráficos originales del juego.</summary>
    public sealed class Paint
    {
        public readonly int Width, Height;
        readonly Color[] m_Pixels;

        public Paint(int width, int height, Color background = default)
        {
            Width = width;
            Height = height;
            m_Pixels = new Color[width * height];
            for (int i = 0; i < m_Pixels.Length; i++)
                m_Pixels[i] = background;
        }

        public Color Get(int x, int y) => m_Pixels[Mathf.Clamp(y, 0, Height - 1) * Width + Mathf.Clamp(x, 0, Width - 1)];

        public void Blend(int x, int y, Color color, float coverage = 1f)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return;
            float a = color.a * Mathf.Clamp01(coverage);
            if (a <= 0f)
                return;
            int i = y * Width + x;
            var dst = m_Pixels[i];
            float outA = a + dst.a * (1f - a);
            if (outA <= 0f)
                return;
            m_Pixels[i] = new Color(
                (color.r * a + dst.r * dst.a * (1f - a)) / outA,
                (color.g * a + dst.g * dst.a * (1f - a)) / outA,
                (color.b * a + dst.b * dst.a * (1f - a)) / outA,
                outA);
        }

        public void Rect(float x, float y, float w, float h, Color color)
        {
            for (int py = Mathf.FloorToInt(y); py < Mathf.CeilToInt(y + h); py++)
                for (int px = Mathf.FloorToInt(x); px < Mathf.CeilToInt(x + w); px++)
                {
                    float cx = Mathf.Clamp01(Mathf.Min(px + 1f, x + w) - Mathf.Max(px, x));
                    float cy = Mathf.Clamp01(Mathf.Min(py + 1f, y + h) - Mathf.Max(py, y));
                    Blend(px, py, color, cx * cy);
                }
        }

        /// <summary>Rectángulo con esquinas redondeadas y borde suave.</summary>
        public void RoundRect(float x, float y, float w, float h, float radius, Color color)
        {
            radius = Mathf.Min(radius, Mathf.Min(w, h) * 0.5f);
            for (int py = Mathf.FloorToInt(y) - 1; py <= Mathf.CeilToInt(y + h); py++)
                for (int px = Mathf.FloorToInt(x) - 1; px <= Mathf.CeilToInt(x + w); px++)
                {
                    float qx = Mathf.Abs(px + 0.5f - (x + w * 0.5f)) - (w * 0.5f - radius);
                    float qy = Mathf.Abs(py + 0.5f - (y + h * 0.5f)) - (h * 0.5f - radius);
                    float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    Blend(px, py, color, Mathf.Clamp01(0.5f - d));
                }
        }

        public void Circle(float cx, float cy, float radius, Color color)
        {
            for (int py = Mathf.FloorToInt(cy - radius) - 1; py <= Mathf.CeilToInt(cy + radius); py++)
                for (int px = Mathf.FloorToInt(cx - radius) - 1; px <= Mathf.CeilToInt(cx + radius); px++)
                {
                    float d = Vector2.Distance(new Vector2(px + 0.5f, py + 0.5f), new Vector2(cx, cy)) - radius;
                    Blend(px, py, color, Mathf.Clamp01(0.5f - d));
                }
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color color)
        {
            for (int py = Mathf.FloorToInt(cy - ry) - 1; py <= Mathf.CeilToInt(cy + ry); py++)
                for (int px = Mathf.FloorToInt(cx - rx) - 1; px <= Mathf.CeilToInt(cx + rx); px++)
                {
                    float nx = (px + 0.5f - cx) / rx, ny = (py + 0.5f - cy) / ry;
                    float d = (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * Mathf.Min(rx, ry);
                    Blend(px, py, color, Mathf.Clamp01(0.5f - d));
                }
        }

        public void Ring(float cx, float cy, float radius, float thickness, Color color)
        {
            for (int py = Mathf.FloorToInt(cy - radius - thickness) - 1; py <= Mathf.CeilToInt(cy + radius + thickness); py++)
                for (int px = Mathf.FloorToInt(cx - radius - thickness) - 1; px <= Mathf.CeilToInt(cx + radius + thickness); px++)
                {
                    float d = Mathf.Abs(Vector2.Distance(new Vector2(px + 0.5f, py + 0.5f), new Vector2(cx, cy)) - radius) - thickness * 0.5f;
                    Blend(px, py, color, Mathf.Clamp01(0.5f - d));
                }
        }

        public void Line(float x0, float y0, float x1, float y1, float thickness, Color color)
        {
            var a = new Vector2(x0, y0);
            var b = new Vector2(x1, y1);
            int minX = Mathf.FloorToInt(Mathf.Min(x0, x1) - thickness), maxX = Mathf.CeilToInt(Mathf.Max(x0, x1) + thickness);
            int minY = Mathf.FloorToInt(Mathf.Min(y0, y1) - thickness), maxY = Mathf.CeilToInt(Mathf.Max(y0, y1) + thickness);
            var ab = b - a;
            float len2 = Mathf.Max(0.0001f, ab.sqrMagnitude);
            for (int py = minY; py <= maxY; py++)
                for (int px = minX; px <= maxX; px++)
                {
                    var p = new Vector2(px + 0.5f, py + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float d = Vector2.Distance(p, a + ab * t) - thickness * 0.5f;
                    Blend(px, py, color, Mathf.Clamp01(0.5f - d));
                }
        }

        public void Noise(float amount, int seed, float scale = 0.15f)
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    if (m_Pixels[i].a <= 0f)
                        continue;
                    float n = (Mathf.PerlinNoise(x * scale + seed * 13.1f, y * scale + seed * 7.7f) - 0.5f) * amount;
                    var c = m_Pixels[i];
                    m_Pixels[i] = new Color(Mathf.Clamp01(c.r + n), Mathf.Clamp01(c.g + n), Mathf.Clamp01(c.b + n), c.a);
                }
        }

        public void VerticalGradient(Color bottom, Color top)
        {
            for (int y = 0; y < Height; y++)
            {
                var c = Color.Lerp(bottom, top, y / (float)(Height - 1));
                for (int x = 0; x < Width; x++)
                    m_Pixels[y * Width + x] = c;
            }
        }

        public Texture2D ToTexture(FilterMode filter = FilterMode.Bilinear, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { filterMode = filter, wrapMode = wrap };
            texture.SetPixels(m_Pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
