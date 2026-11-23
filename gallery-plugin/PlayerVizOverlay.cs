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
    /// 播放页扩展层：可视化（脚灯/光环）、底栏新版质感（丝绒面板、圆形图标按钮、胶囊切换）、
    /// 播放样式切换（全屏 CG / 剧场卡牌）、进度条圆钮。数据全部由 Plugin 的私有字段反射读取。
    /// </summary>
    public class PlayerVizOverlay : MonoBehaviour
    {
        private Plugin _plugin;
        private RectTransform _player;
        private RectTransform _bar;
        private RectTransform _playerTop;
        private SpectrumGraphic _viz;
        private readonly float[] _bars = new float[SpectrumGraphic.BarCount];
        private float _bass, _energy;
        private int _mode;

        private FieldInfo _playerKeyField;
        private FieldInfo _playingFullField;
        private FieldInfo _fullPausedField;
        private FieldInfo _playingKeyField;
        private FieldInfo _playerLastMoveField;
        private FieldInfo _playerLastMousePosField;
        private FieldInfo _progFillField;

        private TMP_FontAsset _sans;
        private TextMeshProUGUI _playerTimeText, _totalTimeText;
        private Image _playIcon;
        private bool _lastPlayIconState;
        private readonly string[] _names = { "脚灯", "光环", "关闭" };
        private const string PrefKey = "NientumGallery.VizMode";

        // ---------------------------------------------------------------- 胶囊按钮
        private class Pill
        {
            public RectTransform Rt;
            public Image Img;
            public TextMeshProUGUI Label;
            public Action Click;
            public Func<bool> IsActive;
            public int W, H;
            public int State = -1;
            public bool InBar = true;
        }
        private readonly List<Pill> _pills = new List<Pill>();

        // ---------------------------------------------------------------- 圆形图标按钮
        private class Round
        {
            public RectTransform Rt;
            public Image Disc, Ring, Glow;
            public int State = -1;
        }
        private readonly List<Round> _rounds = new List<Round>();
        private RectTransform _knob, _knobGlow;
        private RectTransform _progFill;

        public void Init(Plugin plugin)
        {
            _plugin = plugin;
            _player = AccessTools.Field(typeof(Plugin), "_player").GetValue(plugin) as RectTransform;
            if (_player == null) return;
            _bar = _player.Find("PlayerBar") as RectTransform;
            _playerTop = _player.Find("PlayerTop") as RectTransform;
            _mode = LoadMode();

            _playerKeyField = AccessTools.Field(typeof(Plugin), "_playerKey");
            _playingFullField = AccessTools.Field(typeof(Plugin), "_playingFull");
            _fullPausedField = AccessTools.Field(typeof(Plugin), "_fullPaused");
            _playingKeyField = AccessTools.Field(typeof(Plugin), "_playingKey");
            _playerLastMoveField = AccessTools.Field(typeof(Plugin), "_playerLastMove");
            _playerLastMousePosField = AccessTools.Field(typeof(Plugin), "_playerLastMousePos");
            _progFillField = AccessTools.Field(typeof(Plugin), "_playerProgFill");
            _sans = AccessTools.Field(typeof(Plugin), "_sans").GetValue(plugin) as TMP_FontAsset;
            _progFill = _progFillField != null ? _progFillField.GetValue(plugin) as RectTransform : null;

            var go = new GameObject("PlayerViz", typeof(RectTransform), typeof(CanvasRenderer), typeof(SpectrumGraphic));
            go.transform.SetParent(_player, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _viz = go.GetComponent<SpectrumGraphic>();
            _viz.raycastTarget = false;
            _viz.Mode = _mode;
            _viz.gameObject.SetActive(_mode != 2);
            if (_playerTop != null) rt.SetSiblingIndex(_playerTop.GetSiblingIndex());

            if (_bar != null) BuildBar();
            if (_playerTop != null) BuildTop();
            SetMode(_mode);
        }

        // ---------------------------------------------------------------- 构建

        private void BuildBar()
        {
            // 锚定到底部居中，适配 16:9 (1920x1080) 与 16:10 (2560x1600) 等各种比例
            _bar.anchorMin = new Vector2(0.5f, 0f);
            _bar.anchorMax = new Vector2(0.5f, 0f);
            _bar.pivot = new Vector2(0.5f, 0f);
            _bar.anchoredPosition = new Vector2(0f, 26f);
            _bar.sizeDelta = new Vector2(1800f, 132f);

            // 丝绒面板底：深色渐变 + 金色细边 + 顶部高光
            var barImg = _bar.GetComponent<Image>();
            if (barImg != null)
            {
                barImg.sprite = UiSprites.Panel("bar", 1800, 132, 7f,
                    new Color32(32, 24, 18, 240), new Color32(13, 10, 8, 246),
                    new Color32(214, 176, 116, 120), 1.2f, 44);
                barImg.type = Image.Type.Simple;
                barImg.color = Color.white;
            }

            // 修正标题与副标题纵向对齐，避开分隔线
            var bt = _bar.Find("PlayerBottomTitle")?.GetComponent<TextMeshProUGUI>();
            if (bt != null)
            {
                bt.fontSize = 23f;
                bt.alignment = TextAlignmentOptions.MidlineLeft;
                SetTop(bt.rectTransform, 34f, 6f, 920f, 30f);
            }
            var bs = _bar.Find("PlayerBottomSub")?.GetComponent<TextMeshProUGUI>();
            if (bs != null)
            {
                bs.fontSize = 14f;
                bs.alignment = TextAlignmentOptions.MidlineLeft;
                SetTop(bs.rectTransform, 36f, 43f, 920f, 22f);
            }

            // 当前时间左对齐、总时长右对齐到进度条右端 (34+926=960)
            var pt = _bar.Find("PlayerTime")?.GetComponent<TextMeshProUGUI>();
            if (pt != null)
            {
                _playerTimeText = pt;
                pt.alignment = TextAlignmentOptions.Left;
                SetTop(pt.rectTransform, 34f, 109f, 120f, 20f);
                var tgo = new GameObject("PlayerTotalTime", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                tgo.transform.SetParent(_bar, false);
                _totalTimeText = tgo.GetComponent<TextMeshProUGUI>();
                _totalTimeText.font = pt.font;
                _totalTimeText.fontSize = pt.fontSize;
                _totalTimeText.color = pt.color;
                _totalTimeText.alignment = TextAlignmentOptions.Right;
                _totalTimeText.raycastTarget = false;
                SetTop(_totalTimeText.rectTransform, 840f, 109f, 120f, 20f);
            }

            // 标题下分隔线 + 控制区竖分隔线
            var line = MakeLine("BarTitleRule", 28f, 38f, 1744f, 1f, new Color32(214, 176, 116, 72));
            line.transform.SetSiblingIndex(1);
            var vline = MakeLine("BarCtrlRule", 984f, 24f, 1f, 84f, new Color32(198, 160, 104, 52));
            vline.transform.SetSiblingIndex(2);
            var pillRule = MakeLine("BarPillRule", 1534f, 67f, 1f, 34f, new Color32(198, 160, 104, 58));
            pillRule.transform.SetSiblingIndex(3);

            // 圆形图标按钮（用矢量图标替代文字字符）
            string[] names = { "PlayerPrev", "PlayerPlay", "PlayerNext", "PlayerVolIcon", "PlayerFull" };
            string[] icons = { "prev", "play", "next", "volume", "fullscreen" };
            for (int i = 0; i < names.Length; i++)
            {
                var t = _bar.Find(names[i]) as RectTransform;
                if (t == null) continue;
                var img = AddRound(_bar, t, names[i] == "PlayerPlay", icons[i]);
                if (names[i] == "PlayerPlay") _playIcon = img;
            }

            // 进度条圆钮
            _knobGlow = MakeSprite("KnobGlow", UiSprites.Glow(64), new Color32(236, 198, 138, 150), 26f);
            _knob = MakeSprite("Knob", UiSprites.Disc("knob", 16, new Color32(252, 236, 208, 255), new Color32(236, 208, 164, 255)),
                Color.white, 13f);

            // 视觉切换胶囊（脚灯 / 光环 / 关闭）
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                AddPill(_bar, "VizBtn" + i, 1352f + i * 58f, 69f, 52, 30, _names[i],
                    () => SetMode(index), () => _mode == index);
            }
            // 播放样式胶囊（全屏 / 卡牌）
            string[] sn = { "全屏", "卡牌" };
            for (int i = 0; i < 2; i++)
            {
                bool card = i == 1;
                AddPill(_bar, "StyleBtn" + i, 1546f + i * 60f, 69f, 54, 30, sn[i],
                    () => { var s = PlayerCardStage.Instance; if (s != null) s.SetStyle(card); },
                    () => { var s = PlayerCardStage.Instance; return s != null && s.IsCard == card; });
            }
        }

        private void BuildTop()
        {
            var back = _playerTop.Find("PlayerBack") as RectTransform;
            if (back != null)
            {
                SetTop(back, 62f, 70f, 48f, 48f);
                AddRound(_playerTop, back, false, "down");
            }
            var src = _playerTop.Find("PlayerSrcBtn");
            if (src != null) src.gameObject.SetActive(false);
        }

        private RectTransform MakeLine(string name, float left, float top, float w, float h, Color32 color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_bar, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            SetTop(rt, left, top, w, h);
            return rt;
        }

        private RectTransform MakeSprite(string name, Sprite sprite, Color color, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_bar, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            SetTop(rt, 0f, 0f, size, size);
            return rt;
        }

        private Image AddRound(Transform parent, RectTransform text, bool isPlay, string iconName)
        {
            var r = new Round { Rt = text };
            int idx = text.GetSiblingIndex();
            Image Make(string name, Sprite sp, Color c, float scale)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);
                var img = go.GetComponent<Image>();
                img.sprite = sp;
                img.color = c;
                img.raycastTarget = false;
                var rt = (RectTransform)go.transform;
                rt.anchorMin = text.anchorMin;
                rt.anchorMax = text.anchorMax;
                rt.pivot = new Vector2(0.5f, 0.5f);
                var center = text.anchoredPosition + new Vector2(text.sizeDelta.x * 0.5f, -text.sizeDelta.y * 0.5f);
                rt.anchoredPosition = center;
                rt.sizeDelta = text.sizeDelta * scale;
                return img;
            }
            r.Glow = Make("RoundGlow", UiSprites.Glow(64), new Color32(240, 200, 134, 0), isPlay ? 2.1f : 1.7f);
            r.Disc = Make("RoundDisc", UiSprites.Disc("btn", 64, new Color32(62, 47, 35, 225), new Color32(18, 13, 10, 240)),
                Color.white, 1f);
            r.Ring = Make("RoundRing", UiSprites.Ring(128, 3.2f), new Color32(218, 180, 118, 110), 1f);
            var icon = Make("RoundIcon", UiSprites.Icon(iconName, 48), new Color32(238, 224, 204, 255), 0.54f);
            r.Glow.transform.SetSiblingIndex(idx);
            r.Disc.transform.SetSiblingIndex(idx + 1);
            r.Ring.transform.SetSiblingIndex(idx + 2);
            icon.transform.SetSiblingIndex(idx + 3);
            var tmp = text.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.enabled = false;
            _rounds.Add(r);
            return icon;
        }

        private void AddPill(RectTransform parent, string name, float left, float top, int w, int h,
            string text, Action click, Func<bool> active)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            SetTop(rt, left, top, w, h);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;

            var lgo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            lgo.transform.SetParent(rt, false);
            var lrt = (RectTransform)lgo.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var label = lgo.GetComponent<TextMeshProUGUI>();
            label.font = _sans;
            label.text = text;
            label.fontSize = 13.5f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;

            _pills.Add(new Pill { Rt = rt, Img = img, Label = label, Click = click, IsActive = active, W = w, H = h });
        }

        private static Sprite PillSprite(int state, int w, int h)
        {
            switch (state)
            {
                case 2:
                    return UiSprites.Panel("pill_a", w, h, 5f, new Color32(246, 222, 178, 250),
                        new Color32(168, 126, 70, 250), new Color32(255, 240, 206, 235), 1.15f, 112);
                case 1:
                    return UiSprites.Panel("pill_h", w, h, 5f, new Color32(52, 39, 29, 240),
                        new Color32(18, 13, 10, 245), new Color32(214, 174, 112, 215), 1.05f, 44);
                default:
                    return UiSprites.Panel("pill_n", w, h, 5f, new Color32(36, 27, 20, 218),
                        new Color32(16, 12, 9, 230), new Color32(212, 172, 112, 116), 1.0f, 20);
            }
        }

        private static void SetTop(RectTransform rt, float left, float top, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(left, -top);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static int LoadMode()
        {
            try { return Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, 2); }
            catch { return 0; }
        }

        private static void SaveMode(int mode)
        {
            try { PlayerPrefs.SetInt(PrefKey, mode); PlayerPrefs.Save(); }
            catch { }
        }

        public void SetMode(int mode)
        {
            _mode = Mathf.Clamp(mode, 0, 2);
            SaveMode(_mode);
            if (_viz != null)
            {
                _viz.Mode = _mode;
                _viz.gameObject.SetActive(_mode != 2);
                _viz.SetVerticesDirty();
            }
        }

        // ---------------------------------------------------------------- 每帧

        private static bool Over(RectTransform rt, Vector2 pos)
        {
            return rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, pos, null);
        }

        private void RefreshPillVisual(Pill p, Vector2 mp, bool allowHover)
        {
            bool active = p.IsActive != null && p.IsActive();
            bool hover = allowHover && Over(p.Rt, mp);
            int state = active ? 2 : (hover ? 1 : 0);
            if (state == p.State) return;
            p.State = state;
            p.Img.sprite = PillSprite(state, p.W, p.H);
            if (p.Label != null)
                p.Label.color = state == 2 ? new Color32(32, 22, 15, 250) : new Color32(236, 222, 202, 250);
        }

        private void RefreshVisuals(Vector2 mp)
        {
            bool barOn = _bar != null && _bar.gameObject.activeInHierarchy;
            if (barOn)
            {
                foreach (var p in _pills) RefreshPillVisual(p, mp, true);
                foreach (var r in _rounds)
                {
                    bool hover = Over(r.Rt, new Vector2(mp.x, mp.y));
                    int state = hover ? 1 : 0;
                    if (state == r.State) continue;
                    r.State = state;
                    r.Disc.color = hover ? Color.white : new Color(0.82f, 0.82f, 0.82f, 1f);
                    r.Ring.color = hover ? new Color32(226, 188, 126, 232) : new Color32(218, 180, 118, 110);
                    bool play = r.Rt.name == "PlayerPlay";
                    r.Glow.color = new Color32(240, 200, 134, (byte)(hover ? 52 : (play ? 28 : 0)));
                }
                // 进度条圆钮
                if (_progFill != null && _knob != null)
                {
                    float x = 34f + _progFill.sizeDelta.x;
                    SetTop(_knob, x - 6.5f, 102f - 6.5f, 13f, 13f);
                    SetTop(_knobGlow, x - 13f, 102f - 13f, 26f, 26f);
                }
                // 拆分左右时间显示 ("00:16 / 03:48")
                if (_playerTimeText != null && _totalTimeText != null)
                {
                    string raw = _playerTimeText.text;
                    int slash = raw != null ? raw.IndexOf('/') : -1;
                    if (slash > 0)
                    {
                        _playerTimeText.text = raw.Substring(0, slash).Trim();
                        _totalTimeText.text = raw.Substring(slash + 1).Trim();
                    }
                }
                // 切换播放/暂停矢量图标
                if (_playIcon != null)
                {
                    bool playing = _playingFullField != null && (bool)_playingFullField.GetValue(_plugin);
                    bool paused = _fullPausedField != null && (bool)_fullPausedField.GetValue(_plugin);
                    string pkey = _playingKeyField != null ? _playingKeyField.GetValue(_plugin) as string : null;
                    string ckey = _playerKeyField != null ? _playerKeyField.GetValue(_plugin) as string : null;
                    bool activePlay = playing && pkey == ckey && !paused;
                    if (activePlay != _lastPlayIconState)
                    {
                        _lastPlayIconState = activePlay;
                        _playIcon.sprite = UiSprites.Icon(activePlay ? "pause" : "play", 48);
                    }
                }
            }
        }

        private bool HandlePillClick(Vector2 mp)
        {
            foreach (var p in _pills)
            {
                if (p.Click != null && Over(p.Rt, mp))
                {
                    p.Click();
                    return true;
                }
            }
            return false;
        }

        private void Update()
        {
            if (_viz == null || _player == null || !_player.gameObject.activeInHierarchy) return;

            var mouse = UnityEngine.InputSystem.Mouse.current;
            Vector2 mp = mouse != null ? mouse.position.ReadValue() : new Vector2(-1f, -1f);
            bool pressed = mouse != null && mouse.leftButton.wasPressedThisFrame;

            // 底栏自动隐藏时，首击此前只会唤出底栏、丢掉按钮点击。
            // 这里在底栏 inactive 时直接用按钮矩形命中，命中即生效并重新显示底栏/顶栏。
            bool barOn = _bar != null && _bar.gameObject.activeSelf;
            if (pressed)
            {
                if (!barOn)
                {
                    foreach (var p in _pills)
                    {
                        if (Over(p.Rt, mp))
                        {
                            ShowBar();
                            p.Click();
                            if (Plugin.Log != null) Plugin.Log.LogInfo("viz cold click " + p.Rt.name);
                            return;
                        }
                    }
                }
                else if (HandlePillClick(mp)) { /* 已处理 */ }
            }

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.vKey.wasPressedThisFrame)
            {
                SetMode((_mode + 1) % 3);
            }

            RefreshVisuals(mp);

            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;
            bool playing = _playingFullField != null && (bool)_playingFullField.GetValue(_plugin);
            bool paused = _fullPausedField != null && (bool)_fullPausedField.GetValue(_plugin);
            bool sounded = playing && !paused;
            string key = _playerKeyField != null ? (_playerKeyField.GetValue(_plugin) as string) : null;
            float seed = ((key != null ? key.GetHashCode() : 0) & 0x3FF) * 0.017f;

            float targetBass = sounded ? 0.52f + 0.48f * Mathf.Sin(t * 2.1f + seed) : 0.04f;
            _bass += (targetBass - _bass) * (1f - Mathf.Exp(-dt / 0.09f));
            float targetEnergy = sounded ? 0.78f : 0.08f;
            _energy += (targetEnergy - _energy) * (1f - Mathf.Exp(-dt / 0.16f));

            for (int i = 0; i < SpectrumGraphic.BarCount; i++)
            {
                float n = Mathf.PerlinNoise(t * 0.55f + i * 0.17f + seed, seed * 0.7f + i * 0.11f);
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * (1.2f + 0.5f * Mathf.Sin(seed + i)) + i * 0.55f + seed);
                float v = (0.12f + 0.88f * n) * (0.55f + 0.45f * pulse);
                if (!sounded) v = 0.025f;
                _bars[i] += (v - _bars[i]) * (1f - Mathf.Exp(-dt / 0.11f));
            }

            var stage = PlayerCardStage.Instance;
            _viz.CardMode = stage != null && stage.IsCard;
            if (stage != null) _viz.HaloCenter = stage.CardCenterTop;

            float lift = (_bar != null && _bar.gameObject.activeSelf) ? 180f : 0f;
            _viz.SetData(_bars, t, _bass, _energy, _mode, sounded, lift);
        }

        private void ShowBar()
        {
            if (_bar != null && !_bar.gameObject.activeSelf) _bar.gameObject.SetActive(true);
            if (_playerTop != null && !_playerTop.gameObject.activeSelf) _playerTop.gameObject.SetActive(true);
            try
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (_playerLastMoveField != null) _playerLastMoveField.SetValue(_plugin, Time.unscaledTime);
                if (_playerLastMousePosField != null)
                    _playerLastMousePosField.SetValue(_plugin, mouse != null ? mouse.position.ReadValue() : Vector2.zero);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(Plugin), "BuildPlayer")]
    internal static class PlayerVizPatch
    {
        private static void Postfix(Plugin __instance)
        {
            var player = AccessTools.Field(typeof(Plugin), "_player").GetValue(__instance) as RectTransform;
            if (player == null) return;
            if (player.GetComponent<PlayerCardStage>() == null)
            {
                var stage = player.gameObject.AddComponent<PlayerCardStage>();
                stage.Init(__instance);
            }
            if (player.GetComponent<PlayerVizOverlay>() == null)
            {
                var overlay = player.gameObject.AddComponent<PlayerVizOverlay>();
                overlay.Init(__instance);
            }
        }
    }
}
