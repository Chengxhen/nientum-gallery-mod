using System.Collections.Generic;
using UnityEngine;

namespace NientumGallery
{
    /// <summary>
    /// 播放器新版 UI 用到的程序化精灵（SDF 抗锯齿）：圆角面板、圆角描边、圆盘、圆环、径向光晕。
    /// 所有纹理按 2x 超采样生成，ppu=200 时 1 个 canvas 单位对应 2 个纹素，放大到 4K 也不糊。
    /// </summary>
    internal static class UiSprites
    {
        private const int S = 2;
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        private static float RoundedSd(float px, float py, float w, float h, float r)
        {
            float qx = Mathf.Abs(px - w * 0.5f) - (w * 0.5f - r);
            float qy = Mathf.Abs(py - h * 0.5f) - (h * 0.5f - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static Sprite Finish(Texture2D tex, int w, int h, Vector4 border, string key)
        {
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply(false, false);
            var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f * S, 0,
                SpriteMeshType.FullRect, border);
            _cache[key] = sp;
            return sp;
        }

        /// <summary>纯白圆角矩形（用于 Mask 和阴影，九宫格）。</summary>
        public static Sprite RoundFill(int radius)
        {
            string key = "rf" + radius;
            if (_cache.TryGetValue(key, out var c) && c != null) return c;
            int logical = radius * 2 + 2;
            int n = logical * S;
            float r = radius * S;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float sd = RoundedSd(x + 0.5f, y + 0.5f, n, n, r);
                    byte a = (byte)(Mathf.Clamp01(0.5f - sd) * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            float b = (radius + 1) * S;
            return Finish(tex, n, n, new Vector4(b, b, b, b), key);
        }

        /// <summary>白色圆角描边（九宫格），粗细按 canvas 单位。</summary>
        public static Sprite RoundOutline(int radius, float thickness)
        {
            string key = "ro" + radius + "_" + thickness.ToString("0.0");
            if (_cache.TryGetValue(key, out var c) && c != null) return c;
            int logical = radius * 2 + 2;
            int n = logical * S;
            float r = radius * S;
            float t = thickness * S;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float sd = RoundedSd(x + 0.5f, y + 0.5f, n, n, r);
                    float d = Mathf.Abs(sd + t * 0.5f) - t * 0.5f;
                    byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            float b = (radius + 1) * S;
            return Finish(tex, n, n, new Vector4(b, b, b, b), key);
        }

        /// <summary>
        /// 固定尺寸的渐变面板（不九宫格，按 w×h 精确烘焙）：上→下渐变 + 描边 + 顶部高光线。
        /// </summary>
        public static Sprite Panel(string name, int w, int h, float radius,
            Color32 top, Color32 bottom, Color32 border, float borderW, byte highlight)
        {
            string key = "pn_" + name + w + "x" + h;
            if (_cache.TryGetValue(key, out var c) && c != null) return c;
            int tw = w * S, th = h * S;
            float r = radius * S;
            float bw = borderW * S;
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
            var px = new Color32[tw * th];
            for (int y = 0; y < th; y++)
            {
                // y=0 在纹理底部
                float tv = (float)y / (th - 1);
                Color32 fill = Color32.Lerp(bottom, top, tv);
                for (int x = 0; x < tw; x++)
                {
                    float sd = RoundedSd(x + 0.5f, y + 0.5f, tw, th, r);
                    float cover = Mathf.Clamp01(0.5f - sd);
                    if (cover <= 0f) { px[y * tw + x] = new Color32(0, 0, 0, 0); continue; }
                    float e = Mathf.Clamp01(1f + sd / bw); // 0 内部 → 1 边缘
                    Color col = Color.Lerp(fill, border, e * (border.a / 255f));
                    float alpha = Mathf.Lerp(fill.a / 255f, Mathf.Max(fill.a / 255f, border.a / 255f), e);
                    // 顶部高光
                    float hy = th - 1 - y;
                    if (highlight > 0 && hy >= bw && hy < bw + S * 1.2f && x > r && x < tw - r)
                        col = Color.Lerp(col, new Color(1f, 0.95f, 0.84f), highlight / 255f);
                    col.a = alpha * cover;
                    px[y * tw + x] = col;
                }
            }
            tex.SetPixels32(px);
            return Finish(tex, tw, th, Vector4.zero, key);
        }

        /// <summary>圆盘：中心略亮的暖色径向渐变，边缘抗锯齿。</summary>
        public static Sprite Disc(string name, int size, Color32 center, Color32 edge)
        {
            string key = "dc_" + name + size;
            if (_cache.TryGetValue(key, out var c) && c != null) return c;
            int n = size * S;
            float rad = n * 0.5f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - rad, dy = y + 0.5f - rad;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float cov = Mathf.Clamp01(rad - d);
                    float t = Mathf.Clamp01(d / rad);
                    // 光源偏上
                    float tl = Mathf.Clamp01(Mathf.Sqrt(dx * dx + (dy + rad * 0.28f) * (dy + rad * 0.28f)) / rad);
                    Color32 col = Color32.Lerp(center, edge, Mathf.Sqrt(tl));
                    col.a = (byte)(col.a * cov);
                    px[y * n + x] = col;
                }
            tex.SetPixels32(px);
            return Finish(tex, n, n, Vector4.zero, key);
        }

        /// <summary>白色圆环。</summary>
        public static Sprite Ring(int size, float thickness)
        {
            string key = "rg" + size + "_" + thickness.ToString("0.0");
            if (_cache.TryGetValue(key, out var c) && c != null) return c;
            int n = size * S;
            float rad = n * 0.5f;
            float t = thickness * S;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - rad, dy = y + 0.5f - rad;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float dd = Mathf.Abs(d - (rad - t * 0.5f - 0.5f)) - t * 0.5f;
                    byte a = (byte)(Mathf.Clamp01(0.5f - dd) * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            return Finish(tex, n, n, Vector4.zero, key);
        }

        /// <summary>白色实心圆（可染色）。</summary>
        public static Sprite Dot(int size)
        {
            string key = "dt" + size;
            if (_cache.TryGetValue(key, out var c) && c != null) return c;
            int n = size * S;
            float rad = n * 0.5f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - rad, dy = y + 0.5f - rad;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(rad - d) * 255f));
                }
            tex.SetPixels32(px);
            return Finish(tex, n, n, Vector4.zero, key);
        }

        /// <summary>白色径向光晕（中心不透明、边缘透明），用于圆点/按钮外晕。</summary>
        public static Sprite Glow(int size)
        {
            string key = "gl" + size;
            if (_cache.TryGetValue(key, out var c) && c != null) return c;
            int n = size;
            float rad = n * 0.5f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - rad, dy = y + 0.5f - rad;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / rad);
                    float a = Mathf.Pow(1f - d, 2.2f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply(false, false);
            var sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            _cache[key] = sp;
            return sp;
        }

        // ------------------------------------------------------------ SDF 矢量图标

        private static float DistSeg(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay;
            float wx = px - ax, wy = py - ay;
            float len2 = vx * vx + vy * vy;
            float t = len2 > 1e-6f ? Mathf.Clamp01((wx * vx + wy * vy) / len2) : 0f;
            float dx = px - (ax + vx * t), dy = py - (ay + vy * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float DistTri(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float e0x = bx - ax, e0y = by - ay, e1x = cx - bx, e1y = cy - by, e2x = ax - cx, e2y = ay - cy;
            float d0 = DistSeg(px, py, ax, ay, bx, by);
            float d1 = DistSeg(px, py, bx, by, cx, cy);
            float d2 = DistSeg(px, py, cx, cy, ax, ay);
            float d = Mathf.Min(d0, Mathf.Min(d1, d2));
            float s0 = e0x * (py - ay) - e0y * (px - ax);
            float s1 = e1x * (py - by) - e1y * (px - bx);
            float s2 = e2x * (py - cy) - e2y * (px - cx);
            bool inside = (s0 >= 0 && s1 >= 0 && s2 >= 0) || (s0 <= 0 && s1 <= 0 && s2 <= 0);
            return inside ? -d : d;
        }

        private static float DistBox(float px, float py, float x0, float y0, float x1, float y1)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
            float hx = (x1 - x0) * 0.5f, hy = (y1 - y0) * 0.5f;
            float dx = Mathf.Abs(px - cx) - hx, dy = Mathf.Abs(py - cy) - hy;
            float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f);
        }

        private static float DistArc(float px, float py, float cx, float cy, float r, float deg0, float deg1)
        {
            float ang = Mathf.Atan2(py - cy, px - cx) * Mathf.Rad2Deg;
            float d = Mathf.Abs(Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy)) - r);
            if (ang >= deg0 && ang <= deg1) return d;
            float a0 = deg0 * Mathf.Deg2Rad, a1 = deg1 * Mathf.Deg2Rad;
            float da = Mathf.Sqrt(Mathf.Pow(px - (cx + Mathf.Cos(a0) * r), 2) + Mathf.Pow(py - (cy + Mathf.Sin(a0) * r), 2));
            float db = Mathf.Sqrt(Mathf.Pow(px - (cx + Mathf.Cos(a1) * r), 2) + Mathf.Pow(py - (cy + Mathf.Sin(a1) * r), 2));
            return Mathf.Min(da, db);
        }

        /// <summary>生成抗锯齿白色矢量图标（play / pause / prev / next / volume / fullscreen / down）。</summary>
        public static Sprite Icon(string name, int size = 48)
        {
            string key = "ic_" + name + "_" + size;
            if (_cache.TryGetValue(key, out var c) && c != null) return c;
            int n = size * S;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            float sw = n * 0.042f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // 归一化到 [-1, 1]，y 向上
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float sd = 1e6f;
                    if (name == "play")
                    {
                        sd = DistTri(u, v, -0.26f, -0.38f, -0.26f, 0.38f, 0.42f, 0f);
                    }
                    else if (name == "pause")
                    {
                        sd = Mathf.Min(DistBox(u, v, -0.28f, -0.34f, -0.09f, 0.34f),
                                       DistBox(u, v, 0.09f, -0.34f, 0.28f, 0.34f));
                    }
                    else if (name == "next" || name == "prev")
                    {
                        float uu = name == "prev" ? -u : u;
                        float tri = DistTri(uu, v, -0.36f, -0.32f, -0.36f, 0.32f, 0.14f, 0f);
                        float bar = DistBox(uu, v, 0.20f, -0.32f, 0.34f, 0.32f);
                        sd = Mathf.Min(tri, bar);
                    }
                    else if (name == "volume")
                    {
                        float cone = Mathf.Min(
                            DistBox(u, v, -0.46f, -0.15f, -0.22f, 0.15f),
                            DistTri(u, v, -0.26f, -0.15f, -0.26f, 0.15f, -0.02f, 0.38f));
                        cone = Mathf.Min(cone, DistTri(u, v, -0.26f, -0.15f, -0.02f, 0.38f, -0.02f, -0.38f));
                        float a1 = DistArc(u, v, -0.02f, 0f, 0.24f, -52f, 52f) - 0.045f;
                        float a2 = DistArc(u, v, -0.02f, 0f, 0.42f, -50f, 50f) - 0.045f;
                        sd = Mathf.Min(cone, Mathf.Min(a1, a2));
                    }
                    else if (name == "fullscreen")
                    {
                        float d = 1e6f;
                        for (int sx = -1; sx <= 1; sx += 2)
                            for (int sy = -1; sy <= 1; sy += 2)
                            {
                                float cx = sx * 0.38f, cy = sy * 0.38f;
                                d = Mathf.Min(d, DistSeg(u, v, cx, cy, cx - sx * 0.24f, cy) - 0.048f);
                                d = Mathf.Min(d, DistSeg(u, v, cx, cy, cx, cy - sy * 0.24f) - 0.048f);
                            }
                        sd = d;
                    }
                    else if (name == "down")
                    {
                        sd = Mathf.Min(DistSeg(u, v, -0.26f, 0.14f, 0f, -0.14f),
                                       DistSeg(u, v, 0f, -0.14f, 0.26f, 0.14f)) - 0.055f;
                    }
                    float pixelSd = sd * (n * 0.5f);
                    float aa = Mathf.Clamp01(0.5f - pixelSd / Mathf.Max(1f, sw * 0.15f));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(aa * 255f));
                }
            tex.SetPixels32(px);
            return Finish(tex, n, n, Vector4.zero, key);
        }
    }
}
