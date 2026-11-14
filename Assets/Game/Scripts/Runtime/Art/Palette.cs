using UnityEngine;

namespace Sol3.Game.Art
{
    /// <summary>Paleta del proyecto (dirección artística).</summary>
    public static class Palette
    {
        public static readonly Color SpaceBlue = Hex("#18283A");
        public static readonly Color Steel = Hex("#30465D");
        public static readonly Color Cyan = Hex("#53C7D8");
        public static readonly Color SoftWhite = Hex("#E8EFF4");
        public static readonly Color Mars = Hex("#D88B56");
        public static readonly Color Green = Hex("#65C69A");
        public static readonly Color Alert = Hex("#DB6262");
        public static readonly Color Amber = Hex("#E2B45A");
        public static readonly Color Graphite = Hex("#22272E");
        public static readonly Color Ink = Hex("#0E1823");

        public static Color Hex(string html) => ColorUtility.TryParseHtmlString(html, out var c) ? c : Color.magenta;

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

        public static Color Shade(this Color c, float amount) =>
            amount >= 0f ? Color.Lerp(c, Color.white, amount).WithAlpha(c.a) : Color.Lerp(c, Color.black, -amount).WithAlpha(c.a);
    }
}
