using System;
using UnityEngine;
using UnityEngine.UI;

namespace NientumGallery
{
    /// <summary>
    /// 播放器的轻量可视化绘制器。脚灯按独立鉴赏器的 48 根竖线绘制，
    /// 光环按 72 根旋转射线绘制；FMOD 的 C# 封装没有频谱接口，所以
    /// 数据由 Plugin 按播放状态/时间/曲目种子生成。
    /// </summary>
    public class SpectrumGraphic : MaskableGraphic
    {
        public const int BarCount = 48;
        public const int RayCount = 72;

        public readonly float[] Bars = new float[BarCount];
        public float TimeValue;
        public float Bass;
        public float Energy;
        public float Lift;
        public bool Active;
        public int Mode; // 0 脚灯，1 光环，2 关闭
        public bool CardMode;      // 剧场卡牌样式：光环居中于卡牌
        public Vector2 HaloCenter; // 卡牌中心（1920×1080 画布，左上角为原点）

        private static readonly Color32 C_CORE = new Color32(255, 236, 193, 230);
        private static readonly Color32 C_GOLD = new Color32(246, 185, 94, 220);
        private static readonly Color32 C_GOLD_DIM = new Color32(246, 185, 94, 38);
        private static readonly Color32 C_GLOW = new Color32(244, 174, 82, 0);
        private static readonly Color32 C_DARK = new Color32(12, 8, 5, 60);

        public void SetData(float[] bars, float time, float bass, float energy, int mode, bool active, float lift)
        {
            if (bars != null)
            {
                int n = Mathf.Min(bars.Length, Bars.Length);
                for (int i = 0; i < n; i++) Bars[i] = bars[i];
            }
            TimeValue = time;
            Bass = bass;
            Energy = energy;
            Mode = mode;
            Active = active;
            Lift = lift;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Mode == 2) return;
            Rect r = GetPixelAdjustedRect();
            if (Mode == 1) DrawHalo(vh, r);
            else DrawFootlights(vh, r);
        }

        private void DrawFootlights(VertexHelper vh, Rect r)
        {
            float w = r.width;
            float baseY = r.yMin + 2f + Lift;

            // floor glow：从脚灯锚点向上 210px，靠锚点亮、向上渐隐
            QuadGradient(vh,
                new Vector2(r.xMin, baseY), new Vector2(r.xMax, baseY + 210f),
                new Color32(252, 206, 130, 34), new Color32(220, 150, 66, 0));

            float margin = 18f;
            float span = Mathf.Max(1f, w - margin * 2f);
            float step = span / (BarCount - 1);
            for (int i = 0; i < BarCount; i++)
            {
                float v = Mathf.Clamp01(Bars[i]);
                float vv = Mathf.Pow(v, 1.18f);
                float flicker = 0.94f + 0.06f * Mathf.Sin(TimeValue * 19f + i * 1.7f);
                float bh = (14f + 150f * vv) * flicker;
                float x = r.xMin + margin + i * step;

                // outer glow（从锚点向上生长）
                QuadGradient(vh,
                    new Vector2(x - 7f, baseY), new Vector2(x + 7f, baseY + bh),
                    new Color32(246, 185, 94, 30), C_GLOW);
                // hot core
                QuadGradient(vh,
                    new Vector2(x - 1.3f, baseY), new Vector2(x + 1.3f, baseY + bh),
                    new Color32(255, 232, 180, 120), C_CORE);

                // lamp at the floor（亮心在锚点略上方）
                float lamp = 3f + 4f * vv;
                QuadGradient(vh,
                    new Vector2(x - lamp * 2.2f, baseY - lamp * 0.4f),
                    new Vector2(x + lamp * 2.2f, baseY + lamp * 1.8f),
                    new Color32(255, 232, 180, 55), new Color32(244, 174, 82, 0));
            }

            // 尘埃：按时间/能量漂移，大小与亮度随节拍变化。
            for (int i = 0; i < 64; i++)
            {
                float ph = i * 2.399f;
                float speed = 6f + (i % 7) * 2.2f;
                float y = r.yMin + Mathf.Repeat(ph * 53f + TimeValue * speed * (0.55f + Energy * 1.4f), 720f);
                float x = r.xMin + Mathf.Repeat(i * 97.3f + Mathf.Sin(TimeValue * 0.35f + ph) * 40f, w);
                float sz = 1.2f + (i % 3) * 0.8f;
                byte a = (byte)Mathf.Clamp(20f + 90f * Energy, 0f, 150f);
                QuadGradient(vh,
                    new Vector2(x - sz, y - sz), new Vector2(x + sz, y + sz),
                    new Color32(246, 210, 150, a), new Color32(246, 210, 150, a));
                QuadGradient(vh,
                    new Vector2(x - sz * 3.2f, y - sz * 3.2f), new Vector2(x + sz * 3.2f, y + sz * 3.2f),
                    new Color32(246, 210, 150, (byte)(a / 4)), new Color32(246, 210, 150, (byte)(a / 4)));
            }
        }

        private void DrawHalo(VertexHelper vh, Rect r)
        {
            float cx = r.xMin + r.width * 0.5f;
            // reference: screen y=500 from top on a 1080 stage
            float cy = r.yMin + r.height - 500f;
            float radius = 285f;
            if (CardMode)
            {
                // 剧场卡牌：光环以卡牌中心为圆心（随卡牌漂浮），叠在卡牌之上，不会被卡牌遮挡
                cx = r.xMin + HaloCenter.x;
                cy = r.yMax - HaloCenter.y;
                radius = 236f;
            }
            float rot = TimeValue * 3f;

            for (int i = 0; i < RayCount; i++)
            {
                float v = Bars[i % BarCount];
                if ((i & 1) == 1) v *= 0.62f;
                float length = 22f + 92f * Mathf.Pow(Mathf.Clamp01(v), 1.25f);
                float ang = (rot + i * 360f / RayCount) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Vector2 perp = new Vector2(-dir.y, dir.x);
                Vector2 b0 = new Vector2(cx, cy) + dir * radius;
                Vector2 b1 = new Vector2(cx, cy) + dir * (radius + length);

                // shadow
                RayQuad(vh, b0, b1, perp, 5.5f, C_DARK, C_DARK);
                // glow
                RayQuad(vh, b0, b1, perp, 5f,
                    new Color32(226, 180, 112, 24), new Color32(226, 180, 112, 36));
                // core
                RayQuad(vh, b0, b1, perp, 1.4f,
                    new Color32(226, 180, 112, 90), new Color32(246, 214, 160, 235));
            }

            // thin static ring
            AddRing(vh, new Vector2(cx, cy), radius - 4f, 1.4f, new Color32(226, 180, 112, 95));
        }

        private static void RayQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 perp, float width, Color32 ca, Color32 cb)
        {
            Vector2 o = perp * (width * 0.5f);
            Quad(vh, a - o, a + o, b + o, b - o, ca, ca, cb, cb);
        }

        private static void AddRing(VertexHelper vh, Vector2 center, float radius, float width, Color32 color)
        {
            int n = 128;
            float half = width * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n;
                float a1 = (i + 1) * Mathf.PI * 2f / n;
                Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                Vector2 d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                Vector2 p0 = center + d0 * (radius - half);
                Vector2 p1 = center + d0 * (radius + half);
                Vector2 p2 = center + d1 * (radius + half);
                Vector2 p3 = center + d1 * (radius - half);
                Quad(vh, p0, p1, p2, p3, color, color, color, color);
            }
        }

        private static void QuadGradient(VertexHelper vh, Vector2 min, Vector2 max, Color32 bottom, Color32 top)
        {
            Quad(vh,
                new Vector2(min.x, min.y), new Vector2(max.x, min.y),
                new Vector2(max.x, max.y), new Vector2(min.x, max.y),
                bottom, bottom, top, top);
        }

        private static void Quad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
            Color32 c0, Color32 c1, Color32 c2, Color32 c3)
        {
            int i = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = c0; v.position = p0; vh.AddVert(v);
            v.color = c1; v.position = p1; vh.AddVert(v);
            v.color = c2; v.position = p2; vh.AddVert(v);
            v.color = c3; v.position = p3; vh.AddVert(v);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }
}