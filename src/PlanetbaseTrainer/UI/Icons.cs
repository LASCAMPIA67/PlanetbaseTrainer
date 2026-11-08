using UnityEngine;

namespace PlanetbaseTrainer.UI
{
    /// <summary>
    /// Small anti-aliased icons rasterized once at runtime (white, tinted when drawn).
    /// Generated procedurally so the trainer does not depend on font glyph coverage.
    /// </summary>
    internal static class Icons
    {
        private const int Size = 64;
        private const int Supersampling = 4;

        private static Texture2D _star;
        private static Texture2D _starOutline;
        private static Texture2D _close;
        private static Texture2D _search;

        public static Texture2D Star => _star != null ? _star : (_star = Rasterize(InsideStar));

        public static Texture2D StarOutline => _starOutline != null ? _starOutline : (_starOutline = Rasterize(InsideStarOutline));

        public static Texture2D Close => _close != null ? _close : (_close = Rasterize(InsideCross));

        public static Texture2D Search => _search != null ? _search : (_search = Rasterize(InsideMagnifier));

        public static Texture2D ChevronLeft => _chevronLeft != null ? _chevronLeft : (_chevronLeft = Rasterize((x, y) => InsideChevron(-x, y)));

        public static Texture2D ChevronRight => _chevronRight != null ? _chevronRight : (_chevronRight = Rasterize(InsideChevron));

        private static Texture2D _chevronLeft;
        private static Texture2D _chevronRight;

        private delegate bool Shape(float x, float y);

        private static Texture2D Rasterize(Shape inside)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[Size * Size];
            const float step = 1f / Supersampling;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < Supersampling; sy++)
                    {
                        for (int sx = 0; sx < Supersampling; sx++)
                        {
                            // Normalized coordinates in [-1, 1], y pointing up.
                            float px = (x + (sx + 0.5f) * step) / Size * 2f - 1f;
                            float py = (y + (sy + 0.5f) * step) / Size * 2f - 1f;
                            if (inside(px, py))
                                hits++;
                        }
                    }
                    byte alpha = (byte)(255 * hits / (Supersampling * Supersampling));
                    pixels[y * Size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static readonly Vector2[] StarPoints = BuildStar(0.92f, 0.40f);
        private static readonly Vector2[] StarInnerPoints = BuildStar(0.56f, 0.20f);

        private static Vector2[] BuildStar(float outer, float inner)
        {
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? outer : inner;
                points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius - 0.06f);
            }
            return points;
        }

        private static bool InsideStar(float x, float y) => InsidePolygon(StarPoints, x, y);

        private static bool InsideStarOutline(float x, float y) =>
            InsidePolygon(StarPoints, x, y) && !InsidePolygon(StarInnerPoints, x, y);

        private static bool InsideCross(float x, float y)
        {
            const float half = 0.11f;
            const float reach = 0.62f;
            bool d1 = Mathf.Abs(x - y) * 0.7071f < half && Mathf.Abs(x + y) * 0.7071f < reach;
            bool d2 = Mathf.Abs(x + y) * 0.7071f < half && Mathf.Abs(x - y) * 0.7071f < reach;
            return d1 || d2;
        }

        private static bool InsideMagnifier(float x, float y)
        {
            // Ring centered slightly up-left, handle towards bottom-right.
            float cx = x + 0.15f, cy = y - 0.15f;
            float distance = Mathf.Sqrt(cx * cx + cy * cy);
            bool ring = distance < 0.55f && distance > 0.36f;
            float hx = x - 0.45f, hy = y + 0.45f;
            bool handle = Mathf.Abs(hx + hy) * 0.7071f < 0.11f && Mathf.Abs(hx - hy) * 0.7071f < 0.30f;
            return ring || handle;
        }

        /// <summary>"›" chevron made of two thick strokes with rounded ends.</summary>
        private static bool InsideChevron(float x, float y)
        {
            const float thickness = 0.13f;
            var tip = new Vector2(0.28f, 0f);
            return DistanceToSegment(new Vector2(x, y), new Vector2(-0.2f, 0.52f), tip) < thickness ||
                   DistanceToSegment(new Vector2(x, y), new Vector2(-0.2f, -0.52f), tip) < thickness;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        private static bool InsidePolygon(Vector2[] polygon, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
