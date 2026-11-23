using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NientumGallery
{
    /// <summary>
    /// 播放页「剧场卡牌」样式：模糊谢幕 CG 背景 + 居中圆角清晰卡牌 + 章节曲目圆点 + 卡牌下方曲目信息。
    /// 卡牌周期性小幅漂浮；悬停 UI 时整个舞台（含卡牌）柔焦。与独立播放器 v2 的 paint_card_stage 一一对应。
    /// </summary>
    public class PlayerCardStage : MonoBehaviour
    {
        public static PlayerCardStage Instance;
        private const string PrefKey = "NientumGallery.PlayerStyle"; // 1=卡牌 0=全屏

        private const float CardW = 1248f, CardH = 702f, CardCx = 960f, CardCy = 456f;
        private const float CardCenterUp = 540f - CardCy; // 相对画布中心向上的偏移

        public bool IsCard { get; private set; }
        public Vector2 CardCenterTop { get; private set; } = new Vector2(CardCx, CardCy);

        private Plugin _plugin;
        private RectTransform _player;
        private RawImage _bg;
        private RawImage _blurSrc;
        private FieldInfo _fBlurAlpha, _fKey, _fIndex, _fBottomTitle, _fBottomSub, _fBar;
        private MethodInfo _mSetTrack, _mFiltered;
        private TMP_FontAsset _serif, _sans;

        private RawImage _backdrop;
        private Image _backdropDim, _stageAura;
        private RectTransform _group, _float;
        private RawImage _sharp, _soft;
        private CanvasGroup _softGroup, _shadowGroup, _overlayGroup, _infoGroup;
        private Image _softDim;
        private TextMeshProUGUI _title, _titleShadow, _meta;
        private RectTransform _tip;
        private Image _tipBg;
        private TextMeshProUGUI _tipText;

        private Texture _lastCover;
        private Texture2D _softTex;
        private Texture2D _backdropTex;
        private float _infoAlpha = 1f;
        private float _t0;

        private class Dot
        {
            public RectTransform Root, Hit;
            public Image Outer, Inner, Glow;
        }
        private readonly List<Dot> _dots = new List<Dot>();
        private List<string[]> _chapterRows = new List<string[]>();
        private string _dotChapter;
        private int _hoverDot = -1;

        public void Init(Plugin plugin)
        {
            Instance = this;
            _plugin = plugin;
            _t0 = Time.unscaledTime;
            _player = (RectTransform)transform;

            var fBg = AccessTools.Field(typeof(Plugin), "_playerBg");
            var fBlur = AccessTools.Field(typeof(Plugin), "_playerBlurImage");
            _bg = fBg.GetValue(plugin) as RawImage;
            _blurSrc = fBlur.GetValue(plugin) as RawImage;
            _fBlurAlpha = AccessTools.Field(typeof(Plugin), "_playerBlurAlpha");
            _fKey = AccessTools.Field(typeof(Plugin), "_playerKey");
            _fIndex = AccessTools.Field(typeof(Plugin), "_playerIndex");
            _fBottomTitle = AccessTools.Field(typeof(Plugin), "_playerBottomTitle");
            _fBottomSub = AccessTools.Field(typeof(Plugin), "_playerBottomSub");
            _fBar = AccessTools.Field(typeof(Plugin), "_playerBar");
            _mSetTrack = AccessTools.Method(typeof(Plugin), "SetPlayerTrack");
            _mFiltered = AccessTools.Method(typeof(Plugin), "FilteredMusic");
            _serif = AccessTools.Field(typeof(Plugin), "_serif").GetValue(plugin) as TMP_FontAsset;
            _sans = AccessTools.Field(typeof(Plugin), "_sans").GetValue(plugin) as TMP_FontAsset;

            int bgIdx = _bg != null ? _bg.transform.GetSiblingIndex() : 0;
            BuildBackdrop(bgIdx + 1);

            var top = _player.Find("PlayerTop");
            int groupIdx = top != null ? top.GetSiblingIndex() : _player.childCount;
            BuildCardGroup(groupIdx);

            bool card = true;
            try { card = PlayerPrefs.GetInt(PrefKey, 1) != 0; } catch { }
            SetStyle(card, false);
        }

        // ------------------------------------------------------------ 构建

        private RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>以画布中心为锚点放置：cx/cyTop 为 1920×1080 画布内的左上角坐标系中心点。</summary>
        private static void Center(RectTransform rt, float cx, float cyTop, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx - 960f, 540f - cyTop);
            rt.sizeDelta = new Vector2(w, h);
        }

        private Image NewImg(string name, Transform parent, Sprite sprite, Color color, bool sliced)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private RawImage NewRawImg(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RawImage>();
            r.raycastTarget = false;
            return r;
        }

        private TextMeshProUGUI NewText(string name, Transform parent, float size, Color color, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        private void BuildBackdrop(int index)
        {
            _backdrop = NewRawImg("CardBackdrop", _player);
            Stretch(_backdrop.rectTransform);
            _backdrop.transform.SetSiblingIndex(index);
            _backdropDim = NewImg("CardBackdropDim", _player, null, new Color32(8, 6, 5, 120), false);
            Stretch(_backdropDim.rectTransform);
            _backdropDim.transform.SetSiblingIndex(index + 1);
        }

        private void BuildCardGroup(int index)
        {
            _group = NewRect("CardStage", _player);
            Stretch(_group);
            _group.SetSiblingIndex(index);
            _float = NewRect("CardFloat", _group);
            Stretch(_float);

            // 舞台柔和暖金环境辉光（避免四周显得空旷）
            _stageAura = NewImg("StageAura", _float, UiSprites.Glow(128), new Color32(226, 178, 108, 44), false);
            Center(_stageAura.rectTransform, CardCx, CardCy, CardW * 1.48f, CardH * 1.56f);

            // 立体投影（3 层）
            var shadowRoot = NewRect("Shadow", _float);
            Stretch(shadowRoot);
            _shadowGroup = shadowRoot.gameObject.AddComponent<CanvasGroup>();
            AddShadow(shadowRoot, 34, -16, -8, 16, 26, 54);
            AddShadow(shadowRoot, 28, -9, -4, 9, 17, 82);
            AddShadow(shadowRoot, 25, -4, -2, 4, 10, 105);

            // 圆角遮罩 + 清晰/柔焦两层
            var maskImg = NewImg("CardMask", _float, UiSprites.RoundFill(22), Color.white, true);
            Center(maskImg.rectTransform, CardCx, CardCy, CardW, CardH);
            var mask = maskImg.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            _sharp = NewRawImg("CardSharp", maskImg.transform);
            Stretch(_sharp.rectTransform);
            var softRoot = NewRect("CardSoftRoot", maskImg.transform);
            Stretch(softRoot);
            _softGroup = softRoot.gameObject.AddComponent<CanvasGroup>();
            _softGroup.alpha = 0f;
            _soft = NewRawImg("CardSoft", softRoot);
            Stretch(_soft.rectTransform);
            _softDim = NewImg("CardSoftDim", softRoot, null, new Color32(6, 5, 4, 76), false);
            Stretch(_softDim.rectTransform);

            // 金色细框 + 圆点 + 提示
            var overlay = NewRect("CardOverlay", _float);
            Stretch(overlay);
            _overlayGroup = overlay.gameObject.AddComponent<CanvasGroup>();
            var frame1 = NewImg("Frame1", overlay, UiSprites.RoundOutline(22, 1.6f), new Color32(222, 188, 130, 175), true);
            Center(frame1.rectTransform, CardCx, CardCy, CardW, CardH);
            var frame2 = NewImg("Frame2", overlay, UiSprites.RoundOutline(21, 1.0f), new Color32(255, 242, 214, 42), true);
            Center(frame2.rectTransform, CardCx, CardCy, CardW - 3f, CardH - 3f);
            var dotsRoot = NewRect("Dots", overlay);
            Stretch(dotsRoot);

            // 曲目信息（卡牌下方）
            var info = NewRect("CardInfo", _float);
            Stretch(info);
            _infoGroup = info.gameObject.AddComponent<CanvasGroup>();
            float bottom = CardCy + CardH * 0.5f;
            _titleShadow = NewText("TitleShadow", info, 31f, new Color32(0, 0, 0, 175), _serif);
            Center(_titleShadow.rectTransform, CardCx + 1.5f, bottom + 31.5f, 1180f, 44f);
            _title = NewText("Title", info, 31f, new Color32(248, 240, 226, 255), _serif);
            Center(_title.rectTransform, CardCx, bottom + 30f, 1180f, 44f);
            var rule = NewImg("InfoRule", info, null, new Color32(212, 174, 112, 118), false);
            Center(rule.rectTransform, CardCx, bottom + 50f, 180f, 1f);
            _meta = NewText("Meta", info, 15f, new Color32(222, 206, 182, 242), _sans);
            _meta.characterSpacing = 3.2f;
            Center(_meta.rectTransform, CardCx, bottom + 68f, 1180f, 24f);

            // 圆点悬停提示
            _tip = NewRect("DotTip", overlay);
            _tipBg = NewImg("Bg", _tip, UiSprites.Panel("tip", 220, 24, 6f,
                new Color32(26, 20, 15, 235), new Color32(14, 10, 8, 235), new Color32(206, 168, 108, 175), 1f, 0),
                Color.white, false);
            Stretch(_tipBg.rectTransform);
            _tipText = NewText("Text", _tip, 12.5f, new Color32(244, 232, 212, 250), _sans);
            Stretch(_tipText.rectTransform);
            _tip.gameObject.SetActive(false);
            _tipDotsRoot = dotsRoot;
        }

        private RectTransform _tipDotsRoot;

        private void AddShadow(Transform parent, int radius, float dl, float dt, float dr, float db, byte alpha)
        {
            var img = NewImg("S" + radius, parent, UiSprites.RoundFill(radius), new Color32(0, 0, 0, alpha), true);
            float w = CardW + dr - dl;
            float h = CardH + db - dt;
            float cx = CardCx + (dl + dr) * 0.5f;
            float cy = CardCy + (dt + db) * 0.5f;
            Center(img.rectTransform, cx, cy, w, h);
        }

        // ------------------------------------------------------------ 样式切换

        public void SetStyle(bool card, bool save = true)
        {
            IsCard = card;
            if (save)
            {
                try { PlayerPrefs.SetInt(PrefKey, card ? 1 : 0); PlayerPrefs.Save(); } catch { }
            }
            if (_backdrop != null) _backdrop.gameObject.SetActive(card);
            if (_backdropDim != null) _backdropDim.gameObject.SetActive(card);
            if (_group != null) _group.gameObject.SetActive(card);
            if (_bg != null) _bg.enabled = !card;
            _lastCover = null; // 强制刷新一次纹理
        }

        public void ToggleStyle() { SetStyle(!IsCard); }

        // ------------------------------------------------------------ 每帧

        private static float Hash(float a) { return Mathf.Sin(a); }

        private void Update()
        {
            if (_player == null || !_player.gameObject.activeInHierarchy) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.cKey.wasPressedThisFrame) ToggleStyle();
            if (!IsCard) return;

            RefreshCover();
            RefreshDots();

            float t = Time.unscaledTime - _t0;
            float ox = 11.5f * Mathf.Sin(t * 0.62f) + 4f * Mathf.Sin(t * 1.13f + 0.7f);
            float oy = 8f * Mathf.Cos(t * 0.78f) + 3f * Mathf.Sin(t * 1.37f);
            _float.anchoredPosition = new Vector2(ox, -oy);
            CardCenterTop = new Vector2(CardCx + ox, CardCy + oy);

            float blur = 0f;
            try { blur = (float)_fBlurAlpha.GetValue(_plugin); } catch { }
            float stage = 1f - 0.55f * blur;
            _softGroup.alpha = blur;
            _softGroup.gameObject.SetActive(blur > 0.01f);
            _softDim.color = new Color(6f / 255f, 5f / 255f, 4f / 255f, 76f / 255f * blur);
            _backdropDim.color = new Color(8f / 255f, 6f / 255f, 5f / 255f, (52f + 72f * blur) / 255f);
            if (_stageAura != null) _stageAura.color = new Color32(226, 178, 108, (byte)(44 * stage));
            _shadowGroup.alpha = stage;
            _overlayGroup.alpha = stage;

            // 底栏可见时略微柔和曲目信息，保留清晰度同时不喧宾夺主
            var bar = _fBar != null ? _fBar.GetValue(_plugin) as RectTransform : null;
            bool barOn = bar != null && bar.gameObject.activeSelf;
            float want = barOn ? 0.62f : 1f;
            _infoAlpha += (want - _infoAlpha) * (1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.19f));
            _infoGroup.alpha = _infoAlpha * stage;

            RefreshInfo();
            UpdateDotsVisual(t, stage);
            HandleDotInput();
        }

        private void RefreshCover()
        {
            if (_bg == null) return;
            var cover = _bg.texture;
            if (cover == null || cover == _lastCover) return;
            _lastCover = cover;
            float ta = cover.height > 0 ? (float)cover.width / cover.height : 16f / 9f;
            float ca = CardW / CardH;
            Rect cardUv = new Rect(0f, 0f, 1f, 1f);
            if (ta > ca + 0.001f) { float uw = ca / ta; cardUv = new Rect((1f - uw) * 0.5f, 0f, uw, 1f); }
            else if (ta < ca - 0.001f) { float uh = ta / ca; cardUv = new Rect(0f, (1f - uh) * 0.5f, 1f, uh); }
            _sharp.texture = cover;
            _sharp.uvRect = cardUv;
            if (_softTex != null) Destroy(_softTex);
            _softTex = MakeSoft(cover, 384, 216, 3, 2);
            _soft.texture = _softTex != null ? (Texture)_softTex : cover;
            _soft.uvRect = cardUv;
            if (_backdropTex != null) Destroy(_backdropTex);
            _backdropTex = MakeSoft(cover, 1280, 720, 1, 1);
            _backdrop.texture = _backdropTex != null ? (Texture)_backdropTex : (_blurSrc != null && _blurSrc.texture != null ? _blurSrc.texture : cover);
            _backdrop.uvRect = _bg.uvRect;
        }

        private void RefreshInfo()
        {
            var bt = _fBottomTitle != null ? _fBottomTitle.GetValue(_plugin) as TextMeshProUGUI : null;
            var bs = _fBottomSub != null ? _fBottomSub.GetValue(_plugin) as TextMeshProUGUI : null;
            if (bt != null && _title.text != bt.text) { _title.text = bt.text; _titleShadow.text = bt.text; }
            if (bs != null && _meta.text != bs.text) _meta.text = bs.text;
        }

        // ------------------------------------------------------------ 章节圆点

        private string CurrentKey()
        {
            return _fKey != null ? _fKey.GetValue(_plugin) as string : null;
        }

        private void RefreshDots()
        {
            string key = CurrentKey();
            if (key == null) return;
            string chapter = null;
            foreach (var row in GalleryData.Music)
                if (row.Length > 3 && row[0] == key) { chapter = row[3]; break; }
            if (chapter == null) chapter = "";
            if (chapter == _dotChapter && _dots.Count > 0) return;
            _dotChapter = chapter;
            _chapterRows = new List<string[]>();
            foreach (var row in GalleryData.Music)
                if (row.Length > 3 && row[3] == chapter && row[0] != "—") _chapterRows.Add(row);

            foreach (var d in _dots) if (d.Root != null) Destroy(d.Root.gameObject);
            _dots.Clear();
            int n = _chapterRows.Count;
            float gap = n > 12 ? 20f : 24f;
            float span = (n - 1) * gap;
            float x0 = CardCx - span * 0.5f;
            float cy = CardCy - CardH * 0.5f - 38f;
            var dotSprite = UiSprites.Dot(16);
            var glow = UiSprites.Glow(64);
            for (int i = 0; i < n; i++)
            {
                var d = new Dot();
                d.Root = NewRect("Dot" + i, _tipDotsRoot);
                Center(d.Root, x0 + i * gap, cy, 20f, 24f);
                d.Hit = d.Root;
                d.Glow = NewImg("Glow", d.Root, glow, new Color(1f, 0.82f, 0.5f, 0f), false);
                Center(d.Glow.rectTransform, 0, 0, 40f, 40f);
                d.Glow.rectTransform.anchoredPosition = Vector2.zero;
                d.Outer = NewImg("Outer", d.Root, dotSprite, Color.black, false);
                d.Inner = NewImg("Inner", d.Root, dotSprite, Color.white, false);
                d.Glow.rectTransform.anchoredPosition = Vector2.zero;
                d.Outer.rectTransform.anchoredPosition = Vector2.zero;
                d.Inner.rectTransform.anchoredPosition = Vector2.zero;
                _dots.Add(d);
            }
        }

        private static void SizeCenter(RectTransform rt, float size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
        }

        private void UpdateDotsVisual(float t, float stage)
        {
            string key = CurrentKey();
            for (int i = 0; i < _dots.Count; i++)
            {
                var d = _dots[i];
                bool active = _chapterRows[i][0] == key;
                bool hov = i == _hoverDot;
                if (active)
                {
                    float pulse = 0.85f + 0.15f * Mathf.Sin(t * 3f);
                    SizeCenter(d.Glow.rectTransform, 40f * pulse);
                    d.Glow.color = new Color(1f, 0.80f, 0.45f, 0.85f);
                    SizeCenter(d.Outer.rectTransform, 11.6f);
                    d.Outer.color = new Color32(232, 192, 128, 110);
                    SizeCenter(d.Inner.rectTransform, 8.6f);
                    d.Inner.color = new Color32(255, 246, 224, 250);
                }
                else
                {
                    float r = hov ? 4.4f : 3.2f;
                    d.Glow.color = new Color(1f, 0.8f, 0.45f, 0f);
                    SizeCenter(d.Outer.rectTransform, (r + 1.1f) * 2f);
                    d.Outer.color = new Color32(8, 6, 5, 130);
                    SizeCenter(d.Inner.rectTransform, r * 2f);
                    d.Inner.color = new Color32(220, 206, 186, (byte)(hov ? 225 : 132));
                }
            }
        }

        private void HandleDotInput()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            _hoverDot = -1;
            if (mouse != null)
            {
                Vector2 mp = mouse.position.ReadValue();
                for (int i = 0; i < _dots.Count; i++)
                {
                    if (RectTransformUtility.RectangleContainsScreenPoint(_dots[i].Hit, mp, null)) { _hoverDot = i; break; }
                }
                if (_hoverDot >= 0 && mouse.leftButton.wasPressedThisFrame) PlayDot(_hoverDot);
            }

            if (_hoverDot >= 0)
            {
                var row = _chapterRows[_hoverDot];
                string txt = (_hoverDot + 1).ToString("00") + ". " + row[1];
                _tipText.text = txt;
                float tw = Mathf.Clamp(txt.Length * 8.2f + 28f, 90f, 520f);
                var c = _dots[_hoverDot].Root.anchoredPosition;
                _tip.anchorMin = _tip.anchorMax = new Vector2(0.5f, 0.5f);
                _tip.pivot = new Vector2(0.5f, 0.5f);
                _tip.sizeDelta = new Vector2(tw, 24f);
                _tip.anchoredPosition = new Vector2(c.x, c.y + 30f);
                _tip.gameObject.SetActive(true);
            }
            else if (_tip.gameObject.activeSelf) _tip.gameObject.SetActive(false);
        }

        private void PlayDot(int i)
        {
            if (i < 0 || i >= _chapterRows.Count) return;
            var row = _chapterRows[i];
            if (row[0] == CurrentKey()) return;
            try
            {
                var items = _mFiltered.Invoke(_plugin, null) as List<string[]>;
                if (items != null)
                {
                    int idx = items.IndexOf(row);
                    if (idx >= 0) _fIndex.SetValue(_plugin, idx);
                }
                _mSetTrack.Invoke(_plugin, new object[] { row, true });
            }
            catch (Exception e)
            {
                if (Plugin.Log != null) Plugin.Log.LogWarning("卡牌圆点切歌失败：" + e.Message);
            }
        }

        // ------------------------------------------------------------ 工具

        /// <summary>把任意纹理缩成 w×h 并做可分离盒式模糊，用于卡牌柔焦层。</summary>
        private static Texture2D MakeSoft(Texture src, int w, int h, int radius, int passes)
        {
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(src, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply(false, false);
                RenderTexture.active = prev;
                var px = tex.GetPixels32();
                var tmp = new Color32[px.Length];
                for (int p = 0; p < passes; p++)
                {
                    BoxBlur(px, tmp, w, h, radius, true);
                    BoxBlur(tmp, px, w, h, radius, false);
                }
                tex.SetPixels32(px);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                tex.Apply(false, false);
                return tex;
            }
            catch (Exception)
            {
                if (tex != null) Destroy(tex);
                return null;
            }
            finally
            {
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static void BoxBlur(Color32[] src, Color32[] dst, int w, int h, int r, bool horizontal)
        {
            int len = horizontal ? w : h;
            int lines = horizontal ? h : w;
            int win = r * 2 + 1;
            for (int l = 0; l < lines; l++)
            {
                int sr = 0, sg = 0, sb = 0;
                for (int k = -r; k <= r; k++)
                {
                    int ci = Mathf.Clamp(k, 0, len - 1);
                    var c = horizontal ? src[l * w + ci] : src[ci * w + l];
                    sr += c.r; sg += c.g; sb += c.b;
                }
                for (int i = 0; i < len; i++)
                {
                    var o = new Color32((byte)(sr / win), (byte)(sg / win), (byte)(sb / win), 255);
                    if (horizontal) dst[l * w + i] = o; else dst[i * w + l] = o;
                    int addI = Mathf.Clamp(i + r + 1, 0, len - 1);
                    int subI = Mathf.Clamp(i - r, 0, len - 1);
                    var a = horizontal ? src[l * w + addI] : src[addI * w + l];
                    var s = horizontal ? src[l * w + subI] : src[subI * w + l];
                    sr += a.r - s.r; sg += a.g - s.g; sb += a.b - s.b;
                }
            }
        }

        private void OnDestroy()
        {
            if (_softTex != null) Destroy(_softTex);
            if (_backdropTex != null) Destroy(_backdropTex);
            if (Instance == this) Instance = null;
        }
    }
}
