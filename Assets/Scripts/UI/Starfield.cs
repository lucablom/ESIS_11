using UnityEngine;

namespace VastSpace.UI
{
    /// <summary>Generates a simple star background texture at runtime (placeholder for real art).</summary>
    public static class Starfield
    {
        public static Texture2D Create(int width, int height, int stars, int seed)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Starfield",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[width * height];
            var top = new Color(0.03f, 0.07f, 0.13f);
            var bottom = new Color(0.01f, 0.03f, 0.07f);
            for (int y = 0; y < height; y++)
            {
                Color32 c = Color.Lerp(bottom, top, y / (float)height);
                for (int x = 0; x < width; x++) pixels[y * width + x] = c;
            }

            var rng = new System.Random(seed);

            // A few soft nebula blotches.
            for (int n = 0; n < 6; n++)
            {
                int cx = rng.Next(width), cy = rng.Next(height), r = rng.Next(120, 320);
                var tint = new Color(0.08f + (float)rng.NextDouble() * 0.06f, 0.1f, 0.2f + (float)rng.NextDouble() * 0.1f);
                for (int y = Mathf.Max(0, cy - r); y < Mathf.Min(height, cy + r); y += 1)
                for (int x = Mathf.Max(0, cx - r); x < Mathf.Min(width, cx + r); x += 1)
                {
                    float d = ((x - cx) * (x - cx) + (y - cy) * (y - cy)) / (float)(r * r);
                    if (d >= 1f) continue;
                    int i = y * width + x;
                    pixels[i] = Color.Lerp(pixels[i], tint, (1f - d) * 0.12f);
                }
            }

            for (int s = 0; s < stars; s++)
            {
                int x = rng.Next(width), y = rng.Next(height);
                float b = 0.35f + (float)rng.NextDouble() * 0.65f;
                var star = new Color(b * 0.85f, b * 0.92f, b);
                Plot(pixels, width, height, x, y, star, 1f);
                if (rng.NextDouble() < 0.12)
                {
                    // Bigger star with a small glow.
                    Plot(pixels, width, height, x + 1, y, star, 0.5f);
                    Plot(pixels, width, height, x - 1, y, star, 0.5f);
                    Plot(pixels, width, height, x, y + 1, star, 0.5f);
                    Plot(pixels, width, height, x, y - 1, star, 0.5f);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static void Plot(Color32[] pixels, int w, int h, int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = y * w + x;
            pixels[i] = Color.Lerp(pixels[i], c, a);
        }
    }
}
