using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Playables;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace NientumGallery
{
    /// <summary>
    /// Nientum - Op.ZERO 鉴赏室（Mod 版）。
    /// 界面素材与排版取自自建鉴赏器 NientumPlayer；CG / 立绘 / 音乐仍全部来自游戏本体。
    /// </summary>
    [BepInPlugin(Guid, "Nientum 鉴赏室", "1.2.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.local.nientum.gallery";

        internal static ManualLogSource Log;
        private static Plugin _inst;

        private static readonly Color C_BG = new Color32(0x0D, 0x09, 0x07, 0xFF);
        private static readonly Color C_INK = new Color32(0x30, 0x24, 0x1F, 0xFF);
        private static readonly Color C_PAPER = new Color32(0xE6, 0xDD, 0xD0, 0xFF);
        private static readonly Color C_CREAM = new Color32(0xF4, 0xEC, 0xDF, 0xFF);
        private static readonly Color C_GOLD = new Color32(0xC8, 0xA1, 0x66, 0xFF);
        private static readonly Color C_GOLD_LIGHT = new Color32(0xEC, 0xD6, 0xA9, 0xFF);
        private static readonly Color C_GOLD_DEEP = new Color32(0x9A, 0x71, 0x3A, 0xFF);
        private static readonly Color C_MUTED = new Color32(0xC6, 0xB4, 0x9C, 0xFF);
        private static readonly Color C_BORDER = new Color32(0x97, 0x81, 0x69, 0xFF);

        private TMP_FontAsset _serif;
        private TMP_FontAsset _sans;
        private Sprite _round;
        private Sprite _circleSprite;
        private Image _bgImage;
        private Image _vigImage;
        private Image _dimImage;
        private Sprite _bgSprite, _playlistSprite;

        private GameObject _root;
        private TextMeshProUGUI _bigTitle, _subTitle;
        private TextMeshProUGUI _pager;
        private TextMeshProUGUI _countText;
        private TextMeshProUGUI _nowText;
        private TextMeshProUGUI[] _tabText;
        private RectTransform _tabPill;
        private RectTransform _tabLine;
        private RectTransform _nowPanel;
        private TextMeshProUGUI _nowState;
        private RectTransform _content;
        private CanvasGroup _contentGroup;
        private CanvasGroup _rootGroup, _playerGroup, _viewerGroup;
        private RawImage _filmBanner;
        private RawImage _recordCoverRaw;
        private TextMeshProUGUI _musicCountLabel, _recordIdxText, _recordTitleText, _recordArtistText, _recordMetaText;
        private Image _previewToggleImg;
        private readonly Image[] _previewDurImgs = new Image[3];
        private readonly TextMeshProUGUI[] _previewDurLabels = new TextMeshProUGUI[3];
        private int _prevMusicSel = -1;
        private bool _playerClosing;
        private int _lastRenderedTab = -1;
        private string _lastRenderedFilter;
        private RectTransform _viewer;
        private RectTransform _viewerStage;
        private TextMeshProUGUI _viewerTitle;
        private TextMeshProUGUI _viewerCat, _viewerSize, _viewerIndexLbl, _toast;
        private RectTransform _viewerChrome;
        private RectTransform _immersiveBack;
        private bool _immersive;

        private RectTransform _player;
        private RawImage _playerBg;
        private TextMeshProUGUI _playerChapter, _playerTitle, _playerArtist;
        private TextMeshProUGUI _playerBottomTitle, _playerBottomSub, _playerTime, _playerPlay, _playerVolText;
        private RectTransform _playerProgFill, _playerProgBg, _playerVolFill, _playerVolBg;
        private int _playerIndex = -1;
        private string _playerKey;
        private bool _playerVolDrag;
        private RectTransform _playerBar;
        private RectTransform _playerTop;
        private RectTransform _playerBlurRoot;
        private CanvasGroup _playerBlurGroup;
        private RawImage _playerBlurImage;
        private Texture2D _playerBlurTex;
        private float _playerBlurAlpha;
        private float _playerBlurTarget;
        private float _playerLastMove;
        private Vector2 _playerLastMousePos;
        private TextMeshProUGUI _zoomLbl;
        private string _viewBundlePath;
        private RawImage _bigRaw;
        private RectTransform _cgDismiss;
        private RawImage _viewerTopScrim, _viewerTopFade, _viewerBottomScrim;
        private float _zoom = 1f;
        private Vector2 _pan;
        private Vector2 _lastMouse;
        private float _lastClick;

        private bool _built;
        private bool _visible;
        private int _tab;
        private int _page;
        private string _filter = "all";

        private readonly List<Click> _click = new List<Click>();
        private readonly List<RectTransform> _hoverRects = new List<RectTransform>();
        private RectTransform _hoverCard;
        private Vector2 _hoverCardBase;
        private readonly Dictionary<RectTransform, Image> _cardShadows = new Dictionary<RectTransform, Image>();
        private readonly Dictionary<RectTransform, GameObject> _cardHoverBorders = new Dictionary<RectTransform, GameObject>();
        private readonly Dictionary<RectTransform, GameObject> _cardGlows = new Dictionary<RectTransform, GameObject>();
        private readonly Dictionary<RectTransform, GameObject> _musicHoverBorders = new Dictionary<RectTransform, GameObject>();
        private readonly List<RectTransform> _paperFrames = new List<RectTransform>();
        private Sprite _roundOutlineThin, _roundOutlineMid, _roundOutlineThick;
        private Sprite _tachieBackSprite;
        private RectTransform _musicListRoot;
        private readonly Dictionary<string, Texture2D> _thumbs = new Dictionary<string, Texture2D>();
        private readonly HashSet<string> _storyBusy = new HashSet<string>();
        private AssetBundle _viewBundle;
        private Texture2D _viewTex;
        private GameObject _cgInstance;
        private AsyncOperationHandle<GameObject> _cgHandle;
        private int _viewerIndex = -1;
        private int _viewerToken;
        private string _playingKey;
        private string _playingTitle;
        private bool _playingFull;
        private bool _fullPaused;
        private string _previewKey, _pendingKey, _previewError;
        private float _previewEndAt;
        private int _previewSeconds = 20;
        private FMOD.Studio.EventInstance _musicInst;
        private readonly List<FMOD.Studio.EventInstance> _allMusicInsts = new List<FMOD.Studio.EventInstance>();

        private int _musicSel;
        private string _selectedMusicKey;
        private int _musicCatStart;
        private RectTransform _musicTabRoot;
        private Image _catPill, _catLine;
        private TextMeshProUGUI _catPrev, _catNext;
        private readonly List<TextMeshProUGUI> _musicTabLabels = new List<TextMeshProUGUI>();
        private int _musicCatCount;
        private float _musicCatScroll;
        private bool _catDrag;
        private float _catDragStartX, _catDragStartScroll;
        private bool _catDragMoved;
        private int _catPressIndex = -1;
        private RawImage _record;
        private float _lpAngle;
        private float _lpSpeed;
        private TextMeshProUGUI _musicStatus;
        private TextMeshProUGUI _activeChipText;
        private RectTransform _guideRoot;
        private TextMeshProUGUI _previewToggleText;
        private TextMeshProUGUI _backGallery;
        private RectTransform _backGalleryPanel;
        private Sprite _ringSprite;
        private Sprite _radialSprite;
        private Sprite _originalSprite;
        private RectTransform _nowProgFill, _nowProgBg;
        private float _nowProgWidth = 588f;
        private CanvasScaler _scaler;
        private int _lastScreenW, _lastScreenH;
        private float _nonCgResScale = 1f;
        private Sprite _cardPaper, _paperCard, _warmPanel, _goldPanel;
        private float _lastCardClickAt;
        private int _lastCardIndex = -1;

        private bool _bgmDucked;
        private float _bgmSavedVolume = 1f;
        private IntPtr _bgmHandle;
        private float _bgmNextCheck;
        private bool _bgmLogPending;

        private static string BundleRoot
        {
            get { return Path.Combine(Path.Combine(Application.dataPath, "StreamingAssets"), Path.Combine("aa", "StandaloneWindows64")); }
        }

        private static string AssetRoot
        {
            get { return Path.Combine(Paths.PluginPath, "NientumGalleryAssets"); }
        }

        // ------------------------------------------------------------------ 生命周期

        private void Awake()
        {
            Log = Logger;
            _inst = this;
            _autoOpen = Config.Bind("Debug", "AutoOpen", false, "标题界面加载后自动打开鉴赏室（调试用）。");
            _startTab = Config.Bind("Debug", "StartTab", -1, "打开后直接切到指定页签 0-5，-1 不切换。");
            _startPage = Config.Bind("Debug", "StartPage", 0, "打开后直接翻到指定页（从 0 起）。");
            _startViewer = Config.Bind("Debug", "StartViewer", -1, "打开后直接进入第 N 个条目的查看器，-1 不进入。");
            _captureAll = Config.Bind("Debug", "CaptureAllThumbs", false, "生成全部谢幕 CG 缩略图到 _cnwork 下（一次性工具）。");
            LoadFonts();
            try
            {
                var harmony = new Harmony(Guid);
                harmony.PatchAll(typeof(TitleMenuPatch));
                Logger.LogInfo("主菜单入口补丁已挂载。");
                harmony.PatchAll(typeof(PlayerVizPatch));
                Logger.LogInfo("播放器可视化补丁已挂载（脚灯/光环）。");
            }
            catch (Exception e) { Logger.LogWarning("主菜单/可视化补丁失败: " + e); }
            Logger.LogInfo("鉴赏室已加载：F7 打开。");
        }

        private void LoadFonts()
        {
            string serifPath = Path.Combine(Path.Combine(AssetRoot, "fonts"), "NotoSerifSC-VF.ttf");
            string sansPath = Path.Combine(Path.Combine(AssetRoot, "fonts"), "NotoSansSC-VF.ttf");
            try
            {
                if (File.Exists(serifPath))
                    _serif = TMP_FontAsset.CreateFontAsset(serifPath, 0, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048);
            }
            catch (Exception e) { Logger.LogWarning("衬线字体加载失败: " + e.Message); }
            try
            {
                if (File.Exists(sansPath))
                    _sans = TMP_FontAsset.CreateFontAsset(sansPath, 0, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048);
            }
            catch (Exception e) { Logger.LogWarning("黑体字体加载失败: " + e.Message); }
            if (_serif == null) _serif = TMP_FontAsset.CreateFontAsset(Font.CreateDynamicFontFromOSFont(new[] { "SimSun", "STSong" }, 32));
            if (_sans == null) _sans = TMP_FontAsset.CreateFontAsset(Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei" }, 32));
            Logger.LogInfo("字体就绪：serif=" + (_serif != null) + " sans=" + (_sans != null));
        }

        // ------------------------------------------------------------------ 输入

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;

            if (kb != null && kb.f7Key.wasPressedThisFrame)
            {
                if (!_built) BuildUI();
                Toggle();
                return;
            }
            if (!_visible)
            {
                if (_rootGroup != null && _root != null && _root.activeSelf)
                {
                    float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.08f);
                    _rootGroup.alpha = Mathf.Lerp(_rootGroup.alpha, 0f, k);
                    if (_rootGroup.alpha <= 0.015f) { _rootGroup.alpha = 0f; _root.SetActive(false); }
                }
                return;
            }
            if (_rootGroup != null && _rootGroup.alpha < 0.998f)
            {
                float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.08f);
                _rootGroup.alpha = Mathf.Lerp(_rootGroup.alpha, 1f, k);
                if (_rootGroup.alpha > 0.995f) _rootGroup.alpha = 1f;
            }

            if (Time.unscaledTime >= _bgmNextCheck)
            {
                _bgmNextCheck = Time.unscaledTime + 0.5f;
                DuckGameBgm();
                if (Screen.width != _lastScreenW || Screen.height != _lastScreenH) SyncGameDisplaySettings();
            }

            bool flacOn = FlacAudio.IsPlayingOrPaused;
            if (_musicInst.isValid() || flacOn)
            {
                if (_musicStopping)
                {
                    _musicFade = Mathf.Max(0f, _musicFade - Time.unscaledDeltaTime / Mathf.Max(0.05f, _fadeOutDur));
                    if (_musicInst.isValid()) _musicInst.setVolume(_volume * _musicFade);
                    if (flacOn) FlacAudio.SetVolume(_volume * _musicFade);
                    if (_musicFade <= 0.01f || Time.unscaledTime >= _musicStopAt)
                    {
                        bool resume = _resumeSuspendedAfterStop && string.IsNullOrEmpty(_pendingKey);
                        _resumeSuspendedAfterStop = false;
                        bool hadPending = !string.IsNullOrEmpty(_pendingKey);
                        if (!hadPending) StopMusicImmediate(false);
                        else { StopMusicInstance(); _musicStopping = false; }
                        if (resume) ResumeSuspendedFull();
                    }
                }
                else
                {
                    _musicFade = Mathf.Min(1f, _musicFade + Time.unscaledDeltaTime / Mathf.Max(0.05f, _fadeInDur));
                    if (_musicInst.isValid()) _musicInst.setVolume(_volume * _musicFade);
                    if (flacOn) FlacAudio.SetVolume(_volume * _musicFade);
                }
            }

            bool viewerOpen = _viewer != null && _viewer.gameObject.activeSelf;
            bool playerOpen = _player != null && _player.gameObject.activeSelf;

            if (playerOpen)
            {
                RefreshPlayerProgress();
                if (mouse != null && _playerBar != null)
                {
                    Vector2 mp = mouse.position.ReadValue();
                    if ((mp - _playerLastMousePos).sqrMagnitude > 4f)
                    {
                        _playerLastMousePos = mp;
                        _playerLastMove = Time.unscaledTime;
                        if (!_playerBar.gameObject.activeSelf) _playerBar.gameObject.SetActive(true);
                        if (_playerTop != null) _playerTop.gameObject.SetActive(true);
                    }
                    bool overBar = _playerBar.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(_playerBar, mp, null);
                    bool overTop = _playerTop != null && _playerTop.gameObject.activeSelf && mp.x <= Screen.width * 0.35f && mp.y >= Screen.height * 0.74f;
                    bool lower = mp.y <= Screen.height * 0.22f;
                    bool uiHover = overBar || overTop || lower;
                    if (uiHover) _playerLastMove = Time.unscaledTime;
                    if (!uiHover && Time.unscaledTime - _playerLastMove > 2.6f && !_playerVolDrag && _playerBar.gameObject.activeSelf)
                    {
                        _playerBar.gameObject.SetActive(false);
                        if (_playerTop != null) _playerTop.gameObject.SetActive(false);
                    }
                    _playerBlurTarget = uiHover ? 1f : 0f;
                    _playerBlurAlpha += (_playerBlurTarget - _playerBlurAlpha) * (1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.19f));
                    if (_playerBlurGroup != null) _playerBlurGroup.alpha = _playerBlurAlpha;
                }
                if (mouse != null && _playerVolBg != null)
                {
                    if (mouse.leftButton.wasPressedThisFrame)
                    {
                        Vector2 mp = mouse.position.ReadValue();
                        _playerVolDrag = RectTransformUtility.RectangleContainsScreenPoint(_playerVolBg, mp, null);
                    }
                    if (mouse.leftButton.isPressed && _playerVolDrag)
                    {
                        Vector2 local;
                        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_playerVolBg, mouse.position.ReadValue(), null, out local))
                        {
                            _volume = Mathf.Clamp01(local.x / 86f);
                            if (_musicInst.isValid()) _musicInst.setVolume(_volume * _musicFade);
                            if (FlacAudio.IsPlayingOrPaused) FlacAudio.SetVolume(_volume * _musicFade);
                            if (_playerVolFill != null) _playerVolFill.sizeDelta = new Vector2(86f * _volume, 8f);
                            if (_playerVolText != null) _playerVolText.text = Mathf.RoundToInt(_volume * 100) + "%";
                        }
                    }
                    if (mouse != null && !mouse.leftButton.isPressed) _playerVolDrag = false;
                }
            }

            if (kb != null && kb.escapeKey.wasPressedThisFrame && playerOpen)
            {
                ClosePlayer();
                return;
            }
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (viewerOpen)
                {
                    if (_immersive) ToggleChrome();
                    else CloseViewer();
                }
                else Toggle();
                return;
            }
            if (viewerOpen && kb != null)
            {
                if (kb.iKey.wasPressedThisFrame) { ToggleChrome(); return; }
                if (kb.backspaceKey.wasPressedThisFrame) { if (_immersive) ToggleChrome(); else CloseViewer(); return; }
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) { StepViewer(1); return; }
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) { StepViewer(-1); return; }
                if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) { SetZoom(_zoom * 1.2f); return; }
                if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) { SetZoom(_zoom / 1.2f); return; }
                if (kb.rKey.wasPressedThisFrame || kb.digit0Key.wasPressedThisFrame) { SetZoom(1f); return; }
            }
            if (playerOpen && kb != null)
            {
                if (kb.backspaceKey.wasPressedThisFrame) { ClosePlayer(); return; }
                if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { TogglePlayerPlay(); return; }
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) { StepPlayerTrack(1); return; }
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) { StepPlayerTrack(-1); return; }
                if (kb.upArrowKey.wasPressedThisFrame) { AdjustVolume(0.08f); return; }
                if (kb.downArrowKey.wasPressedThisFrame) { AdjustVolume(-0.08f); return; }
            }

            if (kb != null && !viewerOpen && !playerOpen)
            {
                if (kb.digit1Key.wasPressedThisFrame) { SetTab(0); return; }
                if (kb.digit2Key.wasPressedThisFrame) { SetTab(1); return; }
                if (kb.digit3Key.wasPressedThisFrame) { SetTab(2); return; }
                if (kb.digit4Key.wasPressedThisFrame) { SetTab(3); return; }
                if (kb.digit5Key.wasPressedThisFrame) { SetTab(4); return; }
                if (kb.digit6Key.wasPressedThisFrame) { SetTab(5); return; }
                if (kb.qKey.wasPressedThisFrame) { SetTab((_tab + 5) % 6); return; }
                if (kb.eKey.wasPressedThisFrame) { SetTab((_tab + 1) % 6); return; }

                if (_tab == 5)
                {
                    if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) { MoveMusicSelection(-1); return; }
                    if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) { MoveMusicSelection(1); return; }
                    if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) { StepMusicCategory(1); return; }
                    if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) { StepMusicCategory(-1); return; }
                    if (kb.backspaceKey.wasPressedThisFrame) { SetTab(0); return; }
                    if (kb.spaceKey.wasPressedThisFrame)
                    {
                        TogglePreviewFromButton();
                        return;
                    }
                    if (kb.tabKey.wasPressedThisFrame) { SetTab(0); return; }
                    if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { OpenPlayer(_musicSel); return; }
                }
                else
                {
                    if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) { NextPage(1); return; }
                    if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) { NextPage(-1); return; }
                    if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) { StepGalleryFilter(-1); return; }
                    if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) { StepGalleryFilter(1); return; }
                    if (kb.tabKey.wasPressedThisFrame) { SetTab((_tab + 1) % 6); return; }
                    if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { OpenFirstInTab(); return; }
                }
            }

            if (_viewerGroup != null && viewerOpen && _viewerGroup.alpha < 0.998f)
            {
                float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.08f);
                _viewerGroup.alpha = Mathf.Lerp(_viewerGroup.alpha, 1f, k);
                if (_viewerGroup.alpha > 0.995f) _viewerGroup.alpha = 1f;
                if (_viewerStage != null)
                {
                    float sc = Mathf.Lerp(0.978f, 1f, _viewerGroup.alpha);
                    _viewerStage.localScale = new Vector3(sc, sc, 1f);
                }
            }
            if (_playerGroup != null && _player != null && _player.gameObject.activeSelf)
            {
                float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.08f);
                if (_playerClosing)
                {
                    _playerGroup.alpha = Mathf.Lerp(_playerGroup.alpha, 0f, k);
                    if (_playerGroup.alpha <= 0.015f)
                    {
                        _playerGroup.alpha = 0f;
                        _playerClosing = false;
                        _player.gameObject.SetActive(false);
                        RefreshNowPanel();
                    }
                }
                else if (_playerGroup.alpha < 0.998f)
                {
                    _playerGroup.alpha = Mathf.Lerp(_playerGroup.alpha, 1f, k);
                    if (_playerGroup.alpha > 0.995f) _playerGroup.alpha = 1f;
                }
            }

            if (_visible && !viewerOpen && !playerOpen && mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    if (_tab == 5) MoveMusicSelection(wheel > 0f ? -1 : 1);
                    else NextPage(wheel > 0f ? -1 : 1);
                    return;
                }
            }

            if (_contentGroup != null && _contentGroup.alpha < 0.998f)
            {
                float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.09f);
                _contentGroup.alpha = Mathf.Lerp(_contentGroup.alpha, 1f, k);
                if (_contentGroup.alpha > 0.995f) _contentGroup.alpha = 1f;
                if (_content != null)
                {
                    float sc = Mathf.Lerp(0.988f, 1f, _contentGroup.alpha);
                    _content.localScale = new Vector3(sc, sc, 1f);
                }
            }

            if (_visible && !playerOpen && _tab == 5 && _volBg != null)
            {
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    Vector2 mp = mouse.position.ReadValue();
                    if (RectTransformUtility.RectangleContainsScreenPoint(_volBg, mp, null)) _volDrag = true;
                    else _volDrag = false;
                }
                if (mouse != null && mouse.leftButton.isPressed && _volDrag)
                {
                    Vector2 local;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_volBg, mouse.position.ReadValue(), null, out local))
                    {
                        float f = Mathf.Clamp01((local.x) / 160f);
                        _volume = f;
                        if (_musicInst.isValid()) _musicInst.setVolume(_volume * _musicFade);
                        if (FlacAudio.IsPlayingOrPaused) FlacAudio.SetVolume(_volume * _musicFade);
                        if (_volFill != null) _volFill.sizeDelta = new Vector2(160f * _volume, 4f);
                        if (_volText != null) _volText.text = "音量 " + Mathf.RoundToInt(_volume * 100) + "%";
                    }
                }
                if (mouse != null && !mouse.leftButton.isPressed) _volDrag = false;
            }

            if (_visible && _tab == 5)
            {
                float dt = Time.unscaledDeltaTime;

                if (Mathf.Abs(_musicScroll - _musicScrollTarget) > 0.001f)
                {
                    _musicScroll += (_musicScrollTarget - _musicScroll) * (1f - Mathf.Exp(-dt / 0.17f));
                    if (Mathf.Abs(_musicScroll - _musicScrollTarget) < 0.002f) _musicScroll = _musicScrollTarget;
                    LayoutMusicCards();
                }

                // 唱片只有在试听/正式播放时缓慢转动，停止时平滑减速。
                if (_record != null)
                {
                    float target = ((_musicInst.isValid() || FlacAudio.IsPlayingOrPaused) && _playingKey == _selectedMusicKey && !_fullPaused) ? 30f : 0f;
                    _lpSpeed += (target - _lpSpeed) * (1f - Mathf.Exp(-dt / 0.55f));
                    if (Mathf.Abs(_lpSpeed) > 0.02f)
                    {
                        _lpAngle = (_lpAngle - _lpSpeed * dt) % 360f;
                        _record.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _lpAngle);
                    }
                }

                // 试听倒计时临近结束（进入渐弱区间）或实际停声时平滑淡出停下。
                if (!string.IsNullOrEmpty(_previewKey) && (Time.unscaledTime >= _previewEndAt - _fadeOutDur || !PreviewPlaybackAlive()))
                {
                    Log.LogInfo("music preview stop selected=" + _previewKey + " timer=" + (Time.unscaledTime >= _previewEndAt - _fadeOutDur) + " alive=" + PreviewPlaybackAlive());
                    StopMusic();
                    RefreshTab();
                }
                else if (_musicStatus != null)
                {
                    if (!string.IsNullOrEmpty(_previewKey))
                    {
                        string remain = "正在试听 · " + Mathf.Max(0, Mathf.CeilToInt(_previewEndAt - Time.unscaledTime)).ToString("00") + "s";
                        _musicStatus.text = "试听中 · " + Mathf.Max(0, Mathf.CeilToInt(_previewEndAt - Time.unscaledTime)).ToString("00") + "s";
                        if (_activeChipText != null) _activeChipText.text = remain;
                    }
                    else if (_playingFull && !string.IsNullOrEmpty(_playingKey))
                        _musicStatus.text = "正式播放中";
                    else if (!string.IsNullOrEmpty(_previewError))
                        _musicStatus.text = _previewError;
                    else
                        _musicStatus.text = "试听 " + _previewSeconds + " 秒";
                    if (_previewToggleText != null) _previewToggleText.text = string.IsNullOrEmpty(_previewKey) ? "试听" : "停止";
                }

                if (_musicInst.isValid())
                {
                    try
                    {
                        int pos = 0, len = 0;
                        _musicInst.getTimelinePosition(out pos);
                        FMOD.Studio.EventDescription desc;
                        if (_musicInst.getDescription(out desc) == FMOD.RESULT.OK) desc.getLength(out len);
                        float f = len > 0 ? Mathf.Clamp01((float)pos / len) : 0f;
                        if (_nowProgFill != null) _nowProgFill.sizeDelta = new Vector2(_nowProgWidth * f, 3f);
                        if (_loopMusic && _playingFull && len > 1000 && pos >= len - 220)
                        {
                            _musicInst.setTimelinePosition(0);
                            _musicInst.start();
                        }
                        if (_loopMusic && _playingFull && len <= 0 && _musicFallbackLen > 1f)
                        {
                            if (Time.unscaledTime - _musicStartAt >= _musicFallbackLen)
                            {
                                _musicInst.setTimelinePosition(0);
                                _musicInst.start();
                                _musicStartAt = Time.unscaledTime;
                            }
                        }
                    }
                    catch { }
                }
                else if (FlacAudio.IsPlayingOrPaused)
                {
                    int pos = FlacAudio.PositionMs();
                    int len = FlacAudio.LengthMs();
                    float f = len > 0 ? Mathf.Clamp01((float)pos / len) : 0f;
                    if (_nowProgFill != null) _nowProgFill.sizeDelta = new Vector2(_nowProgWidth * f, 3f);
                }
            }

            if (viewerOpen && mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) SetZoom(_zoom * (wheel > 0 ? 1.16f : 1f / 1.16f));
                if (mouse.leftButton.wasPressedThisFrame) _lastMouse = mouse.position.ReadValue();
                if (mouse.leftButton.isPressed && _bigRaw != null && _zoom > 1.001f)
                {
                    Vector2 cur = mouse.position.ReadValue();
                    if ((cur - _lastMouse).sqrMagnitude > 4f)
                    {
                        _pan += cur - _lastMouse;
                        _lastMouse = cur;
                        ApplyTransform();
                    }
                }
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    float now = Time.unscaledTime;
                    if (now - _lastClick < 0.3f)
                    {
                        if (_zoom > 1.001f) { _zoom = 1f; _pan = Vector2.zero; }
                        else _zoom = 2f;
                        ApplyTransform();
                    }
                    _lastClick = now;
                }
            }

            if (_visible && !viewerOpen && !playerOpen && mouse != null)
            {
                Vector2 mp = mouse.position.ReadValue();
                if (_hoverCard != null && !RectTransformUtility.RectangleContainsScreenPoint(_hoverCard, mp, null))
                {
                    ResetCardHover(_hoverCard);
                    var prev = _hoverCard;
                    _hoverCard = null;
                    if (_tab == 5) LayoutMusicCards();
                    else prev.anchoredPosition = _hoverCardBase;
                }
                if (_hoverCard == null)
                {
                    for (int i = _hoverRects.Count - 1; i >= 0; i--)
                    {
                        var hr = _hoverRects[i];
                        if (hr == null || !hr.gameObject.activeInHierarchy) continue;
                        if (RectTransformUtility.RectangleContainsScreenPoint(hr, mp, null))
                        {
                            _hoverCard = hr;
                            _hoverCardBase = hr.anchoredPosition;
                            SetCardHover(hr);
                            if (_tab == 5) LayoutMusicCards();
                            else hr.anchoredPosition = _hoverCardBase + new Vector2(0f, 5f);
                            break;
                        }
                    }
                }
            }

            if (_visible && _tab == 5 && !viewerOpen && !playerOpen && mouse != null && _musicTabRoot != null)
            {
                Vector2 mp = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    Vector2 local;
                    if (RectTransformUtility.RectangleContainsScreenPoint(_musicTabRoot, mp, null) &&
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(_musicTabRoot, mp, null, out local))
                    {
                        _catDrag = true;
                        _catDragMoved = false;
                        _previewGen++;
                        Log.LogInfo("music tab drag start press=" + _catPressIndex + " scroll=" + _musicCatScroll);
                        _catDragStartX = mp.x;
                        _catDragStartScroll = _musicCatScroll;
                        int firstVisible = Mathf.FloorToInt(_musicCatScroll + 0.001f);
                        int press = Mathf.FloorToInt(local.x / 100f + _musicCatScroll);
                        _catPressIndex = Mathf.Clamp(press, firstVisible, Mathf.Min(firstVisible + 7, _musicCatCount - 1));
                    }
                }
                if (_catDrag)
                {
                    if (mouse.leftButton.isPressed)
                    {
                        float dx = mp.x - _catDragStartX;
                        if (Mathf.Abs(dx) > 3f) _catDragMoved = true;
                        if (_catDragMoved)
                        {
                            float ns = Mathf.Clamp(_catDragStartScroll - dx / 100f, 0f, Mathf.Max(0, _musicCatCount - 8));
                            if (Mathf.Abs(ns - _musicCatScroll) > 0.001f)
                            {
                                _musicCatScroll = ns;
                                LayoutMusicTabs();
                            }
                        }
                    }
                    else
                    {
                        _catDrag = false;
                        if (!_catDragMoved && _catPressIndex >= 0) SelectMusicCategory(_catPressIndex);
                        else
                        {
                            _musicCatStart = Mathf.Clamp(Mathf.RoundToInt(_musicCatScroll), 0, Mathf.Max(0, _musicCatCount - 8));
                            _musicCatScroll = _musicCatStart;
                            LayoutMusicTabs();
                            Log.LogInfo("music tab drag end start=" + _musicCatStart);
                        }
                    }
                }
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                if (viewerOpen && _immersive)
                {
                    ToggleChrome();
                    return;
                }
                Vector2 pos = mouse.position.ReadValue();
                for (int i = _click.Count - 1; i >= 0; i--)
                {
                    var c = _click[i];
                    if (c.Rt == null || !c.Rt.gameObject.activeInHierarchy) continue;
                    if (viewerOpen && !c.Rt.IsChildOf(_viewer)) continue;
                    if (playerOpen && !c.Rt.IsChildOf(_player)) continue;
                    if (RectTransformUtility.RectangleContainsScreenPoint(c.Rt, pos, null))
                    {
                        try { c.Act(); } catch (Exception e) { Log.LogWarning("点击处理异常: " + e.Message); }
                        break;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ 开关

        public static bool GalleryVisible { get { return _inst != null && _inst._visible; } }

        public static void OpenGallery()
        {
            if (_inst == null) return;
            Log.LogInfo("主菜单入口：打开鉴赏室。");
            _inst.ResetToCg();
            _inst.Show();
        }

        private void ResetToCg()
        {
            _tab = 0;
            _page = 0;
            _filter = "all";
            _musicSel = 0;
            _musicCatStart = 0;
            _musicCatScroll = 0f;
            _musicScroll = 0f;
            _musicScrollTarget = 0f;
            _catDrag = false;
            Log.LogInfo("主菜单入口：重置到 CG 页签 0。");
        }

        private void Toggle() { if (_visible) Hide(); else Show(); }

        private void SyncGameDisplaySettings()
        {
            _lastScreenW = Screen.width;
            _lastScreenH = Screen.height;
            _nonCgResScale = _lastScreenH > 0 ? Mathf.Clamp(_lastScreenH / 1080f, 0.5f, 2.0f) : 1f;
            if (_scaler != null) _scaler.dynamicPixelsPerUnit = Mathf.Clamp(_nonCgResScale, 1f, 2f);
            try
            {
                var ucm = UserConfigManager.Instance;
                if (ucm != null)
                {
                    var fCfg = AccessTools.Field(typeof(UserConfigManager), "_configData");
                    var cfg = fCfg != null ? fCfg.GetValue(ucm) as UserConfigData : null;
                    if (cfg != null)
                    {
                        QualitySettings.vSyncCount = cfg.isVSync ? 1 : 0;
                        Application.targetFrameRate = cfg.frameRate > 0 ? cfg.frameRate : -1;
                        QualitySettings.antiAliasing = cfg.antiAliasing;
                    }
                }
            }
            catch (Exception e) { Log.LogWarning("同步游戏显示设置失败: " + e.Message); }
        }

        private void Show()
        {
            if (!_built) BuildUI();
            SyncGameDisplaySettings();
            _visible = true;
            _lastCardIndex = -1;
            _playingFull = false;
            _fullPaused = false;
            _root.SetActive(true);
            if (_rootGroup != null) _rootGroup.alpha = 0f;
            _bgmNextCheck = 0f;
            DuckGameBgm();
            Log.LogInfo("gallery show tab=" + _tab + " bgmDucked=" + _bgmDucked);
            RefreshTab();
        }

        private void Hide()
        {
            _visible = false;
            RestoreGameBgm();
            CloseViewer();
            if (_player != null) { _playerClosing = false; _player.gameObject.SetActive(false); }
            StopMusicImmediate();
            Log.LogInfo("gallery hide bgmRestored=" + !_bgmDucked);
        }

        private void OnDestroy()
        {
            RestoreGameBgm();
        }

        private void DuckGameBgm()
        {
            SoundManager sm = null;
            try { sm = SoundManager.Instance; } catch { }
            if (sm == null) return;
            FMOD.Studio.EventInstance bgm;
            try { bgm = sm.bgm; } catch { return; }
            if (!bgm.isValid())
            {
                if (!_bgmDucked && !_bgmLogPending)
                {
                    _bgmLogPending = true;
                    Log.LogInfo("bgm duck wait：标题 BGM 实例尚未就绪。");
                }
                return;
            }
            IntPtr handle = IntPtr.Zero;
            try { handle = bgm.handle; } catch { }
            if (_bgmDucked && handle == _bgmHandle)
            {
                try
                {
                    float curVol;
                    if (bgm.getVolume(out curVol) == FMOD.RESULT.OK && curVol > 0.005f) bgm.setVolume(0f);
                }
                catch { }
                return;
            }
            float vol = 1f;
            try
            {
                float got;
                if (bgm.getVolume(out got) == FMOD.RESULT.OK) vol = got;
            }
            catch { }
            _bgmSavedVolume = Mathf.Clamp(vol, 0.001f, 2f);
            _bgmDucked = true;
            _bgmHandle = handle;
            _bgmLogPending = false;
            try
            {
                sm.SetBGMVolumeFade(0f, 0.3f);
                Log.LogInfo("bgm duck handle=" + handle + " savedVol=" + _bgmSavedVolume.ToString("0.###"));
            }
            catch (Exception e) { Log.LogWarning("bgm duck 失败: " + e.Message); }
        }

        private void RestoreGameBgm()
        {
            if (!_bgmDucked) return;
            _bgmDucked = false;
            _bgmHandle = IntPtr.Zero;
            SoundManager sm = null;
            try { sm = SoundManager.Instance; } catch { }
            if (sm == null) return;
            try
            {
                var bgm = sm.bgm;
                if (!bgm.isValid())
                {
                    Log.LogInfo("bgm restore skipped：BGM 实例已失效。");
                    return;
                }
                sm.SetBGMVolumeFade(_bgmSavedVolume, 0.3f);
                Log.LogInfo("bgm restore vol=" + _bgmSavedVolume.ToString("0.###"));
            }
            catch (Exception e) { Log.LogWarning("bgm restore 失败: " + e.Message); }
        }

        // ------------------------------------------------------------------ 界面

        private void BuildUI()
        {
            _built = true;
            _round = MakeRoundSprite();
            _circleSprite = MakeCircleSprite(64);
            _ringSprite = MakeRingSprite(128, 5);
            _radialSprite = MakeRadialSprite(256);
            _roundOutlineThin = MakeRoundedOutlineSprite(1.3f);
            _roundOutlineMid = MakeRoundedOutlineSprite(1.8f);
            _roundOutlineThick = MakeRoundedOutlineSprite(2.4f);
            _tachieBackSprite = MakeTachieBackSprite(256);
            _warmPanel = MakeWarmPanelSprite(false);
            _goldPanel = MakeWarmPanelSprite(true);
            var origTex = LoadAssetTexture("ui/label_original.png");
            if (origTex != null) _originalSprite = ToSprite(origTex);
            // 不用游戏里的整张卡片图：它自带黑色边缘和阴影，拉伸后就是用户看到的黑框。
            _paperCard = MakePaperCardSprite();

            _root = new GameObject("NientumGalleryRoot");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            _rootGroup = _root.AddComponent<CanvasGroup>();
            _rootGroup.alpha = 0f;
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = _root.AddComponent<CanvasScaler>();
            _scaler = scaler;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            _root.AddComponent<GraphicRaycaster>();
            SyncGameDisplaySettings();
            Logger.LogInfo("屏幕 " + Screen.width + "×" + Screen.height + " canvas 缩放 " + scaler.scaleFactor);

            var under = NewRaw("Under", _root.transform, Color.white);
            under.texture = SolidTexture(new Color32(0x1C, 0x17, 0x14, 0xFF));
            Fill(under.rectTransform);
            under.rectTransform.offsetMin = new Vector2(-100f, -100f);
            under.rectTransform.offsetMax = new Vector2(100f, 100f);

            var bgTex = LoadAssetTexture("ui/Collection_bg.png");
            var bg = NewImage("Bg", _root.transform, Color.white);
            Fill(bg.rectTransform);
            // Collection_bg 四周有大片透明毛边，裁掉再铺，避免游戏底从纸边透出
            bg.rectTransform.offsetMin = new Vector2(-24f, -24f);
            bg.rectTransform.offsetMax = new Vector2(24f, 24f);
            if (bgTex != null) _bgSprite = ToSpriteCropped(bgTex, 0.11f);
            var playTex = LoadAssetTexture("ui/BG_Playlist.png");
            if (playTex != null) _playlistSprite = ToSprite(playTex);
            bg.sprite = _bgSprite;
            bg.raycastTarget = true;
            _bgImage = bg;

            var dim = NewImage("Dim", _root.transform, new Color(10f / 255f, 7f / 255f, 5f / 255f, 0.52f));
            Fill(dim.rectTransform);
            dim.rectTransform.offsetMin = new Vector2(-100f, -100f);
            dim.rectTransform.offsetMax = new Vector2(100f, 100f);
            _dimImage = dim;

            var vig = NewImage("Vignette", _root.transform, new Color(1f, 1f, 1f, 0.78f));
            Fill(vig.rectTransform);
            vig.rectTransform.offsetMin = new Vector2(-100f, -100f);
            vig.rectTransform.offsetMax = new Vector2(100f, 100f);
            var vigTex = LoadAssetTexture("ui/PZImgVignetting.png");
            if (vigTex != null) vig.sprite = ToSprite(vigTex);
            _vigImage = vig;

            // 顶部胶片条（仅 FREE PLAY 时显示，还原独立版剧院顶栏）
            _filmBanner = NewRaw("FilmBanner", _root.transform, Color.white);
            var filmTex = LoadAssetTexture("ui/film.png");
            if (filmTex != null) _filmBanner.texture = filmTex;
            Top(_filmBanner.rectTransform, 0f, 0f, 1920f, 142f);
            _filmBanner.gameObject.SetActive(false);

            // 顶部用一条暖褐色柔和渐隐，不再铺胶片格和横向色带。
            var head = NewRaw("HeadFade", _root.transform, Color.white);
            head.texture = MakeVerticalGradient(196, true, 216);
            Top(head.rectTransform, -100, 0, 2120, 196);

            var t1 = Txt("Title", _root.transform, "CG GALLERY", 46, C_CREAM, _serif, TextAlignmentOptions.BottomLeft);
            Top(t1.rectTransform, 56, 14, 760, 64);
            var t2 = Txt("Sub", _root.transform, "C G 鉴 赏", 18, C_MUTED, _serif, TextAlignmentOptions.BottomLeft);
            Top(t2.rectTransform, 332, 30, 300, 40);
            _bigTitle = t1; _subTitle = t2;
            var tl = NewImage("TitleLine", _root.transform, new Color(C_GOLD.r, C_GOLD.g, C_GOLD.b, 0.76f));
            Top(tl.rectTransform, 56, 90, 414, 2);
            var backPanel = NewWarmPanel(_root.transform, "BackGalleryPanel", 500, 38, 176, 36);
            _backGalleryPanel = backPanel.rectTransform;
            _backGallery = Txt("BackGallery", backPanel.rectTransform, "‹ 返回 CG 鉴赏", 15, C_GOLD_LIGHT, _sans, TextAlignmentOptions.Center);
            Fill(_backGallery.rectTransform);
            _click.Add(new Click { Rt = backPanel.rectTransform, Act = () => { Log.LogInfo("FreePlay 返回 CG：BackGalleryPanel 点击"); SetTab(0); } });

            string[] tabs = { "谢幕 CG", "CG 原画", "剧情 · 尾声", "立 绘", "差 分", "曲 目" };
            float[] widths = { 168f, 150f, 178f, 118f, 118f, 110f };
            _tabText = new TextMeshProUGUI[6];
            float x = 70f;
            for (int i = 0; i < 6; i++)
            {
                int idx = i;
                var t = Txt("Tab" + i, _root.transform, tabs[i], 20, C_MUTED, _serif, TextAlignmentOptions.Center);
                Top(t.rectTransform, x, 156, widths[i], 44);
                _click.Add(new Click { Rt = t.rectTransform, Act = () => SetTab(idx) });
                _tabText[i] = t;
                x += widths[i] + 46f;
            }
            var pill = NewImage("TabPill", _root.transform, Color.white, _goldPanel);
            Top(pill.rectTransform, 70, 156, 168, 40);
            _tabPill = pill.rectTransform;
            _tabPill.SetSiblingIndex(_tabText[0].transform.GetSiblingIndex());
            var tl2 = NewImage("TabLine", _root.transform, C_GOLD);
            Top(tl2.rectTransform, 70, 199, 168, 2);
            _tabLine = tl2.rectTransform;

            // 底部操作区做成真正可点的按钮，鼠标也能走完整个流程。
            _guideRoot = Rect("GuideRoot", _root.transform);
            Bottom(_guideRoot, 56f, 22f, 1140f, 44f);
            _countText = Txt("Count", _root.transform, "", 15, C_GOLD_LIGHT, _sans, TextAlignmentOptions.Right);
            Top(_countText.rectTransform, 1640, 236, 200, 26);
            _pager = Txt("Pager", _root.transform, "", 15, C_MUTED, _sans, TextAlignmentOptions.Right);
            Bottom(_pager.rectTransform, 1740f, 31f, 120f, 26f);

            var pp = Txt("PagerPrev", _root.transform, "◀", 24, C_CREAM, _sans, TextAlignmentOptions.Center);
            Top(pp.rectTransform, 34, 560, 44, 44);
            _click.Add(new Click { Rt = pp.rectTransform, Act = () => NextPage(-1) });
            var pn = Txt("PagerNext", _root.transform, "▶", 24, C_CREAM, _sans, TextAlignmentOptions.Center);
            Top(pn.rectTransform, 1842, 560, 44, 44);
            _click.Add(new Click { Rt = pn.rectTransform, Act = () => NextPage(1) });

            var panel = NewWarmPanel(_root.transform, "NowPanel", 1232, 1000, 628, 44);
            _nowPanel = panel.rectTransform;
            Bottom(_nowPanel, 1232f, 22f, 628f, 44f);
            _nowText = Txt("Now", panel.rectTransform, "", 15, new Color(240f / 255f, 228f / 255f, 208f / 255f, 1f), _serif, TextAlignmentOptions.Left);
            _nowText.overflowMode = TextOverflowModes.Ellipsis;
            Top(_nowText.rectTransform, 20f, 8f, 368f, 24f);
            _nowState = Txt("NowState", panel.rectTransform, "播放中", 13, new Color(216f / 255f, 178f / 255f, 116f / 255f, 1f), _sans, TextAlignmentOptions.Right);
            Top(_nowState.rectTransform, 396f, 8f, 212f, 24f);
            var npb = NewImage("NowProgBg", panel.rectTransform, new Color(84f / 255f, 72f / 255f, 62f / 255f, 0.75f));
            Top(npb.rectTransform, 20f, 35f, 588f, 3f);
            _nowProgBg = npb.rectTransform;
            var np = NewImage("NowProg", panel.rectTransform, C_GOLD);
            Top(np.rectTransform, 20f, 35f, 0, 3f);
            _nowProgFill = np.rectTransform;
            _click.Add(new Click { Rt = panel.rectTransform, Act = () => { if (_tab == 5) OpenPlayer(_musicSel); } });
            panel.gameObject.SetActive(false);

            _content = Rect("Content", _root.transform);
            Top(_content, 0, 244, 1920, 820);
            _contentGroup = _content.gameObject.AddComponent<CanvasGroup>();
            if (_nowPanel != null) _nowPanel.SetAsLastSibling();

            BuildViewer();
            BuildPlayer();
            RefreshTab();
            _root.SetActive(false);
        }

        private void BuildViewer()
        {
            _viewer = Rect("Viewer", _root.transform);
            Fill(_viewer);
            _viewerGroup = _viewer.gameObject.AddComponent<CanvasGroup>();
            _viewerGroup.alpha = 0f;
            var bg = NewImage("ViewerBg", _viewer, new Color(12f / 255f, 9f / 255f, 6f / 255f, 0.94f));
            Fill(bg.rectTransform);
            bg.raycastTarget = true;

            _cgDismiss = Rect("CgDismiss", _viewer);
            Fill(_cgDismiss);
            _click.Add(new Click { Rt = _cgDismiss, Act = () => { if (_tab == 0) CloseViewer(); } });
            _cgDismiss.gameObject.SetActive(false);

            var topScrim = NewRaw("TopScrim", _viewer, Color.white);
            topScrim.texture = MakeVerticalGradient(200, true, 200);
            var tsr = topScrim.rectTransform;
            tsr.anchorMin = new Vector2(0f, 1f); tsr.anchorMax = new Vector2(1f, 1f);
            tsr.pivot = new Vector2(0.5f, 1f); tsr.sizeDelta = new Vector2(0f, 200f); tsr.anchoredPosition = Vector2.zero;
            var topFade = NewRaw("TopFade", _viewer, new Color(1f, 1f, 1f, 0.35f));
            topFade.texture = SolidTexture(new Color32(0x14, 0x0E, 0x0A, 255));
            Top(topFade.rectTransform, 0, 200, 1920, 34);
            var bottomScrim = NewRaw("BottomScrim", _viewer, Color.white);
            bottomScrim.texture = MakeVerticalGradient(190, false, 190);
            var bsr = bottomScrim.rectTransform;
            bsr.anchorMin = new Vector2(0f, 0f); bsr.anchorMax = new Vector2(1f, 0f);
            bsr.pivot = new Vector2(0.5f, 0f); bsr.sizeDelta = new Vector2(0f, 190f); bsr.anchoredPosition = Vector2.zero;
            _viewerTopScrim = topScrim;
            _viewerTopFade = topFade;
            _viewerBottomScrim = bottomScrim;

            _viewerChrome = Rect("Chrome", _viewer);
            Fill(_viewerChrome);
            var goldLine = NewImage("HeaderLine", _viewerChrome, new Color(198f / 255f, 156f / 255f, 96f / 255f, 0.78f));
            Top(goldLine.rectTransform, 64f, 44f, 2f, 62f);

            _viewerCat = Txt("Cat", _viewerChrome, "", 15, new Color(206f / 255f, 170f / 255f, 118f / 255f, 0.94f), _sans, TextAlignmentOptions.Left);
            _viewerCat.characterSpacing = 8f;
            Top(_viewerCat.rectTransform, 84f, 44f, 600f, 26f);

            _viewerTitle = Txt("ViewerTitle", _viewerChrome, "", 30, new Color(242f / 255f, 232f / 255f, 216f / 255f, 1f), _serif, TextAlignmentOptions.Left);
            _viewerTitle.overflowMode = TextOverflowModes.Overflow;
            Top(_viewerTitle.rectTransform, 84f, 70f, 1080f, 42f);

            _viewerSize = Txt("Size", _viewerChrome, "", 14, new Color(196f / 255f, 178f / 255f, 156f / 255f, 0.80f), _sans, TextAlignmentOptions.Left);
            Top(_viewerSize.rectTransform, 86f, 108f, 600f, 24f);

            var pill = NewImage("IdxPill", _viewerChrome, Color.white, _warmPanel);
            Top(pill.rectTransform, 1654f, 54f, 132f, 36f);
            _viewerIndexLbl = Txt("IdxLbl", pill.rectTransform, "", 16, new Color(226f / 255f, 210f / 255f, 190f / 255f, 1f), _sans, TextAlignmentOptions.Center);
            Fill(_viewerIndexLbl.rectTransform);

            var close = NewImage("Close", _viewerChrome, Color.white, UiSprites.Panel("vw_close", 46, 46, 8f,
                new Color32(42, 32, 24, 232), new Color32(16, 12, 9, 238), new Color32(212, 172, 112, 148), 1.05f, 36));
            close.type = Image.Type.Simple;
            Top(close.rectTransform, 1814f, 50f, 46f, 46f);
            var xa = NewRaw("CloseA", close.rectTransform, C_CREAM);
            xa.texture = SolidTexture(new Color32(0xF2, 0xE4, 0xD0, 0xFF));
            xa.rectTransform.anchorMin = xa.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            xa.rectTransform.sizeDelta = new Vector2(2.4f, 22f);
            xa.rectTransform.anchoredPosition = Vector2.zero;
            xa.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var xb = NewRaw("CloseB", close.rectTransform, C_CREAM);
            xb.texture = SolidTexture(new Color32(0xF2, 0xE4, 0xD0, 0xFF));
            xb.rectTransform.anchorMin = xb.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            xb.rectTransform.sizeDelta = new Vector2(2.4f, 22f);
            xb.rectTransform.anchoredPosition = Vector2.zero;
            xb.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            _click.Add(new Click { Rt = close.rectTransform, Act = CloseViewer });

            _viewerStage = Rect("Stage", _viewer);
            _viewerStage.gameObject.AddComponent<RectMask2D>();
            Top(_viewerStage, 80f, 136f, 1760f, 820f);

            var bar = NewWarmPanel(_viewerChrome, "Toolbar", 60f, 1000f, 1800f, 48f);
            Bottom(bar.rectTransform, 60f, 26f, 1800f, 48f);

            var prevBtn = NewImage("PrevBtn", bar.rectTransform, Color.white, UiSprites.Panel("vw_sq", 48, 34, 6f,
                new Color32(42, 32, 24, 232), new Color32(16, 12, 9, 238), new Color32(212, 172, 112, 148), 1.05f, 36));
            prevBtn.type = Image.Type.Simple;
            Top(prevBtn.rectTransform, 14f, 7f, 48f, 34f);
            var prev = Txt("Prev", prevBtn.rectTransform, "◀", 17, C_CREAM, _sans, TextAlignmentOptions.Center);
            Fill(prev.rectTransform);

            var nextBtn = NewImage("NextBtn", bar.rectTransform, Color.white, UiSprites.Panel("vw_sq", 48, 34, 6f,
                new Color32(42, 32, 24, 232), new Color32(16, 12, 9, 238), new Color32(212, 172, 112, 148), 1.05f, 36));
            nextBtn.type = Image.Type.Simple;
            Top(nextBtn.rectTransform, 70f, 7f, 48f, 34f);
            var next = Txt("Next", nextBtn.rectTransform, "▶", 17, C_CREAM, _sans, TextAlignmentOptions.Center);
            Fill(next.rectTransform);

            var divider = NewImage("BarDivider", bar.rectTransform, new Color(174f / 255f, 140f / 255f, 92f / 255f, 0.30f));
            Top(divider.rectTransform, 142f, 9f, 1f, 30f);

            var hint = Txt("BarHint", bar.rectTransform, "滚轮/+/- 缩放  ·  拖动/R 复位  ·  ←→ 切换  ·  I 无UI  ·  ESC/退格 关闭", 14, new Color(232f / 255f, 218f / 255f, 200f / 255f, 0.95f), _sans, TextAlignmentOptions.Center);
            Top(hint.rectTransform, 170f, 6f, 940f, 36f);

            var zoomOutBtn = NewImage("ZoomOutBtn", bar.rectTransform, Color.white, UiSprites.Panel("vw_z", 38, 34, 6f,
                new Color32(42, 32, 24, 232), new Color32(16, 12, 9, 238), new Color32(212, 172, 112, 148), 1.05f, 36));
            zoomOutBtn.type = Image.Type.Simple;
            Top(zoomOutBtn.rectTransform, 1184f, 7f, 38f, 34f);
            var zoomOut = Txt("ZoomOut", zoomOutBtn.rectTransform, "−", 20, new Color(242f / 255f, 232f / 255f, 216f / 255f, 1f), _sans, TextAlignmentOptions.Center);
            Fill(zoomOut.rectTransform);

            _zoomLbl = Txt("ZoomLbl", bar.rectTransform, "100%", 16, new Color(242f / 255f, 232f / 255f, 216f / 255f, 1f), _sans, TextAlignmentOptions.Center);
            Top(_zoomLbl.rectTransform, 1226f, 6f, 84f, 36f);

            var zoomInBtn = NewImage("ZoomInBtn", bar.rectTransform, Color.white, UiSprites.Panel("vw_z", 38, 34, 6f,
                new Color32(42, 32, 24, 232), new Color32(16, 12, 9, 238), new Color32(212, 172, 112, 148), 1.05f, 36));
            zoomInBtn.type = Image.Type.Simple;
            Top(zoomInBtn.rectTransform, 1314f, 7f, 38f, 34f);
            var zoomIn = Txt("ZoomIn", zoomInBtn.rectTransform, "+", 20, new Color(242f / 255f, 232f / 255f, 216f / 255f, 1f), _sans, TextAlignmentOptions.Center);
            Fill(zoomIn.rectTransform);

            var fullBtn = NewImage("FullBtn", bar.rectTransform, Color.white, UiSprites.Panel("vw_full", 160, 34, 6f,
                new Color32(42, 32, 24, 232), new Color32(16, 12, 9, 238), new Color32(212, 172, 112, 148), 1.05f, 36));
            fullBtn.type = Image.Type.Simple;
            Top(fullBtn.rectTransform, 1612f, 7f, 160f, 34f);
            var full = Txt("Full", fullBtn.rectTransform, "全屏无UI", 15, new Color(242f / 255f, 232f / 255f, 216f / 255f, 1f), _sans, TextAlignmentOptions.Center);
            Fill(full.rectTransform);

            _toast = Txt("Toast", _viewerChrome, "", 15, C_GOLD_LIGHT, _sans, TextAlignmentOptions.Center);
            Top(_toast.rectTransform, 660f, 940f, 600f, 30f);

            _click.Add(new Click { Rt = prevBtn.rectTransform, Act = () => StepViewer(-1) });
            _click.Add(new Click { Rt = nextBtn.rectTransform, Act = () => StepViewer(1) });
            _click.Add(new Click { Rt = zoomInBtn.rectTransform, Act = () => SetZoom(_zoom * 1.2f) });
            _click.Add(new Click { Rt = zoomOutBtn.rectTransform, Act = () => SetZoom(_zoom / 1.2f) });
            _click.Add(new Click { Rt = fullBtn.rectTransform, Act = ToggleChrome });

            _immersiveBack = Rect("ImmersiveBack", _viewer);
            Fill(_immersiveBack);
            var backBtn = NewImage("ImmBackBtn", _immersiveBack, Color.white, _warmPanel);
            Top(backBtn.rectTransform, 36f, 36f, 56f, 56f);
            var arrow = Txt("ImmArrow", backBtn.rectTransform, "‹", 36, C_CREAM, _sans, TextAlignmentOptions.Center);
            Fill(arrow.rectTransform);
            var pill2 = NewImage("ImmPill", _immersiveBack, Color.white, _warmPanel);
            Top(pill2.rectTransform, 100f, 40f, 190f, 48f);
            var backTxt = Txt("ImmBackTxt", pill2.rectTransform, "返回 CG 列表", 17, new Color(236f / 255f, 224f / 255f, 206f / 255f, 0.98f), _sans, TextAlignmentOptions.Left);
            Top(backTxt.rectTransform, 18f, 6f, 170f, 36f);
            _click.Add(new Click { Rt = backBtn.rectTransform, Act = ToggleChrome });
            _click.Add(new Click { Rt = pill2.rectTransform, Act = ToggleChrome });
            _immersiveBack.gameObject.SetActive(false);

            _viewerChrome.SetAsLastSibling();
            _immersiveBack.SetAsLastSibling();
            _viewer.gameObject.SetActive(false);
        }

        private void RevealSource()
        {
            try
            {
                if (string.IsNullOrEmpty(_viewBundlePath))
                {
                    Toast("源文件：游戏 Addressables 资源（当前未加载）");
                    return;
                }
                System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + _viewBundlePath + "\"");
                Toast("源文件：" + Path.GetFileName(_viewBundlePath));
            }
            catch (Exception e) { Toast("无法打开源文件：" + e.Message); }
        }

        private void RefreshPlayerProgress()
        {
            if (_playerProgFill == null || _playerTime == null) return;
            int pos = 0, len = 0;
            if (_musicInst.isValid())
            {
                try
                {
                    _musicInst.getTimelinePosition(out pos);
                    FMOD.Studio.EventDescription d;
                    if (_musicInst.getDescription(out d) == FMOD.RESULT.OK) d.getLength(out len);
                }
                catch { }
            }
            else if (FlacAudio.IsPlayingOrPaused)
            {
                pos = FlacAudio.PositionMs();
                len = FlacAudio.LengthMs();
            }
            if (len <= 0 && _musicFallbackLen > 1f)
            {
                len = Mathf.RoundToInt(_musicFallbackLen * 1000f);
                pos = Mathf.RoundToInt((Time.unscaledTime - _musicStartAt) * 1000f);
                if (_musicInst.isValid())
                {
                    try { int realPos = 0; _musicInst.getTimelinePosition(out realPos); if (realPos > 0) pos = realPos; } catch { }
                }
            }
            pos = Mathf.Clamp(pos, 0, Mathf.Max(0, len));
            float f = len > 0 ? Mathf.Clamp01((float)pos / len) : 0f;
            _playerProgFill.sizeDelta = new Vector2(926f * f, 6f);
            _playerTime.text = (pos / 60000).ToString("00") + ":" + ((pos / 1000) % 60).ToString("00") + " / "
                + (len / 60000).ToString("00") + ":" + ((len / 1000) % 60).ToString("00");
            if (_playerPlay != null)
                _playerPlay.text = (_playingFull && _playingKey == _playerKey && !_fullPaused) ? "■" : "▶";
        }

        private void BuildPlayer()
        {
            _player = Rect("Player", _root.transform);
            Fill(_player);
            _playerGroup = _player.gameObject.AddComponent<CanvasGroup>();
            _playerGroup.alpha = 0f;

            _playerBg = NewRaw("PlayerBg", _player, Color.white);
            Fill(_playerBg.rectTransform);
            _playerBg.uvRect = new Rect(0f, 0f, 1f, 1f);
            var vig = NewRaw("PlayerVignette", _player, new Color(1f, 1f, 1f, 0.66f));
            vig.texture = LoadAssetTexture("ui/PZImgVignetting.png");
            Fill(vig.rectTransform);

            _playerBlurRoot = Rect("PlayerBlur", _player);
            Fill(_playerBlurRoot);
            _playerBlurGroup = _playerBlurRoot.gameObject.AddComponent<CanvasGroup>();
            _playerBlurGroup.alpha = 0f;
            _playerBlurGroup.blocksRaycasts = false;
            _playerBlurImage = NewRaw("PlayerBlurImage", _playerBlurRoot, Color.white);
            Fill(_playerBlurImage.rectTransform);
            _playerBlurImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            var blurDim = NewImage("PlayerBlurDim", _playerBlurRoot, new Color(5f / 255f, 4f / 255f, 4f / 255f, 70f / 255f));
            Fill(blurDim.rectTransform);

            var topGrad = NewRaw("PlayerTopGradient", _player, Color.white);
            topGrad.texture = MakeVerticalGradient(180, true, 150);
            var tgr = topGrad.rectTransform;
            tgr.anchorMin = new Vector2(0f, 1f);
            tgr.anchorMax = new Vector2(1f, 1f);
            tgr.pivot = new Vector2(0.5f, 1f);
            tgr.sizeDelta = new Vector2(0f, 180f);
            tgr.anchoredPosition = Vector2.zero;
            var bottomGrad = NewRaw("PlayerBottomGradient", _player, Color.white);
            bottomGrad.texture = MakeVerticalGradient(200, false, 170);
            var bgr = bottomGrad.rectTransform;
            bgr.anchorMin = new Vector2(0f, 0f);
            bgr.anchorMax = new Vector2(1f, 0f);
            bgr.pivot = new Vector2(0.5f, 0f);
            bgr.sizeDelta = new Vector2(0f, 200f);
            bgr.anchoredPosition = Vector2.zero;

            _playerTop = Rect("PlayerTop", _player);
            Fill(_playerTop);
            var gold = NewImage("PlayerGoldLine", _playerTop, new Color(198f / 255f, 156f / 255f, 96f / 255f, 0.82f));
            Top(gold.rectTransform, 58f, 68f, 2f, 84f);

            var back = Txt("PlayerBack", _playerTop, "‹", 34, C_CREAM, _sans, TextAlignmentOptions.Center);
            Top(back.rectTransform, 67f, 75f, 52f, 52f);
            _click.Add(new Click { Rt = back.rectTransform, Act = ClosePlayer });

            _playerChapter = Txt("PlayerChapter", _playerTop, "", 19, new Color(214f / 255f, 174f / 255f, 118f / 255f, 1f), _serif, TextAlignmentOptions.Left);
            Top(_playerChapter.rectTransform, 122f, 78f, 900f, 30f);
            _playerTitle = Txt("PlayerTitle", _playerTop, "", 32, new Color(240f / 255f, 230f / 255f, 214f / 255f, 1f), _serif, TextAlignmentOptions.Left);
            _playerTitle.overflowMode = TextOverflowModes.Overflow;
            Top(_playerTitle.rectTransform, 122f, 100f, 1100f, 44f);
            _playerArtist = Txt("PlayerArtist", _playerTop, "", 14, new Color(200f / 255f, 182f / 255f, 158f / 255f, 0.85f), _sans, TextAlignmentOptions.Left);
            Top(_playerArtist.rectTransform, 124f, 144f, 900f, 20f);

            var bar = NewWarmPanel(_player, "PlayerBar", 60f, 922f, 1800f, 132f);
            _playerBar = bar.rectTransform;

            _playerBottomTitle = Txt("PlayerBottomTitle", bar.rectTransform, "", 24, new Color(240f / 255f, 230f / 255f, 214f / 255f, 1f), _serif, TextAlignmentOptions.Left);
            _playerBottomTitle.overflowMode = TextOverflowModes.Overflow;
            Top(_playerBottomTitle.rectTransform, 34f, 4f, 900f, 34f);
            _playerBottomSub = Txt("PlayerBottomSub", bar.rectTransform, "", 15, new Color(190f / 255f, 172f / 255f, 150f / 255f, 0.92f), _sans, TextAlignmentOptions.Left);
            Top(_playerBottomSub.rectTransform, 36f, 40f, 1000f, 24f);

            var pbg = NewImage("PlayerProgBg", bar.rectTransform, new Color(84f / 255f, 72f / 255f, 62f / 255f, 0.92f));
            Top(pbg.rectTransform, 34f, 99f, 926f, 6f);
            _playerProgBg = pbg.rectTransform;
            var pf = NewImage("PlayerProg", bar.rectTransform, new Color(212f / 255f, 172f / 255f, 112f / 255f, 1f));
            Top(pf.rectTransform, 34f, 99f, 0f, 6f);
            _playerProgFill = pf.rectTransform;
            _playerTime = Txt("PlayerTime", bar.rectTransform, "00:00 / --:--", 13, new Color(198f / 255f, 182f / 255f, 162f / 255f, 0.90f), _sans, TextAlignmentOptions.Left);
            Top(_playerTime.rectTransform, 34f, 108f, 300f, 20f);
            _click.Add(new Click { Rt = pbg.rectTransform, Act = SeekPlayerFromLastMouse });

            var prev = Txt("PlayerPrev", bar.rectTransform, "◀◀", 18, C_CREAM, _sans, TextAlignmentOptions.Center);
            Top(prev.rectTransform, 1002f, 61f, 46f, 46f);
            _playerPlay = Txt("PlayerPlay", bar.rectTransform, "▶", 24, C_GOLD_LIGHT, _sans, TextAlignmentOptions.Center);
            Top(_playerPlay.rectTransform, 1060f, 54f, 60f, 60f);
            var next = Txt("PlayerNext", bar.rectTransform, "▶▶", 18, C_CREAM, _sans, TextAlignmentOptions.Center);
            Top(next.rectTransform, 1132f, 61f, 46f, 46f);
            var vol = Txt("PlayerVolIcon", bar.rectTransform, "音量", 13, C_CREAM, _sans, TextAlignmentOptions.Center);
            Top(vol.rectTransform, 1196f, 65f, 40f, 40f);
            var fullBtn = Txt("PlayerFull", bar.rectTransform, "全屏", 14, C_CREAM, _sans, TextAlignmentOptions.Center);
            Top(fullBtn.rectTransform, 1692f, 61f, 46f, 46f);

            var vbg = NewImage("PlayerVolBg", bar.rectTransform, new Color(90f / 255f, 76f / 255f, 64f / 255f, 0.92f));
            Top(vbg.rectTransform, 1244f, 81f, 86f, 8f);
            _playerVolBg = vbg.rectTransform;
            var vf = NewImage("PlayerVol", bar.rectTransform, C_GOLD);
            Top(vf.rectTransform, 1244f, 81f, 86f * _volume, 8f);
            _playerVolFill = vf.rectTransform;

            _playerVolText = null;

            _click.Add(new Click { Rt = prev.rectTransform, Act = () => StepPlayerTrack(-1) });
            _click.Add(new Click { Rt = next.rectTransform, Act = () => StepPlayerTrack(1) });
            _click.Add(new Click { Rt = _playerPlay.rectTransform, Act = TogglePlayerPlay });
            _click.Add(new Click { Rt = fullBtn.rectTransform, Act = HidePlayerUi });

            _player.gameObject.SetActive(false);
        }

        private void OpenPlayer(int idx)
        {
            var items = FilteredMusic();
            if (idx < 0 || idx >= items.Count) return;
            _playerIndex = idx;
            SetPlayerTrack(items[idx], true);
            _playerClosing = false;
            _player.gameObject.SetActive(true);
            _player.SetAsLastSibling();
            if (_playerGroup != null) _playerGroup.alpha = 0f;
            _playerLastMove = Time.unscaledTime;
            _playerLastMousePos = UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero;
            if (_playerBar != null) _playerBar.gameObject.SetActive(true);
            if (_playerTop != null) _playerTop.gameObject.SetActive(true);
            _playerBlurAlpha = 0f;
            _playerBlurTarget = 0f;
            if (_playerBlurGroup != null) _playerBlurGroup.alpha = 0f;
        }

        private void SetPlayerTrack(string[] row, bool play)
        {
            string key = TrackId(row);
            string title = row[1];
            _playerKey = key;
            string chapterLabel = row.Length > 3 ? row[3] : "";
            string chapterZh = ChapterZh(chapterLabel);
            _playerChapter.text = chapterLabel;
            _playerTitle.text = ClipText(string.IsNullOrEmpty(chapterZh) ? chapterLabel : chapterZh, 32);
            _playerArtist.text = ClipText(ChapterEn(chapterLabel), 44);
            _playerBottomTitle.text = ClipText(title, 45);
            _playerBottomSub.text = ClipText((row.Length > 3 ? ChapterDisplay(row[3]) + "   ·   " : "") + MusicArtist(title), 60);
            var cover = ChapterCoverHiRes(row.Length > 3 ? row[3] : "");
            if (cover != null)
            {
                _playerBg.texture = cover;
                float ta = (float)cover.width / cover.height;
                float ra = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
                if (ta > ra) { float uw = ra / ta; _playerBg.uvRect = new Rect((1f - uw) * 0.5f, 0f, uw, 1f); }
                else { float uh = ta / ra; _playerBg.uvRect = new Rect(0f, (1f - uh) * 0.5f, 1f, uh); }
                if (_playerBlurTex != null) UnityEngine.Object.Destroy(_playerBlurTex);
                _playerBlurTex = MakeBlurThumb(cover);
                if (_playerBlurImage != null)
                {
                    _playerBlurImage.texture = _playerBlurTex;
                    _playerBlurImage.uvRect = _playerBg.uvRect;
                }
            }
            if (play) StartMusic(key, title, true);
            RefreshPlayerText();
        }

        private void OpenPlayerSource()
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", "\"" + BundleRoot + "\"");
                Toast("游戏音频资源根目录：" + BundleRoot);
            }
            catch (Exception e) { Toast("无法打开音频目录：" + e.Message); }
        }

        private void HidePlayerUi()
        {
            if (_playerBar != null) _playerBar.gameObject.SetActive(false);
            if (_playerTop != null) _playerTop.gameObject.SetActive(false);
            _playerLastMove = Time.unscaledTime - 3f;
        }

        private void ClosePlayer()
        {
            if (_player != null && _player.gameObject.activeSelf)
            {
                if (_playerGroup != null) _playerClosing = true;
                else { _player.gameObject.SetActive(false); RefreshNowPanel(); }
                if (_tab == 5) UpdateMusicSelectionInPlace();
            }
        }

        private void TogglePlayerPlay()
        {
            if (string.IsNullOrEmpty(_playerKey)) return;
            if (_playingFull && _playingKey == _playerKey)
            {
                if (_fullPaused)
                {
                    try { if (_musicInst.isValid()) _musicInst.setPaused(false); } catch { }
                    if (FlacAudio.IsPlayingOrPaused) FlacAudio.SetPaused(false);
                    _fullPaused = false;
                    Log.LogInfo("music full resume " + _playingKey);
                }
                else
                {
                    try { if (_musicInst.isValid()) _musicInst.setPaused(true); } catch { }
                    if (FlacAudio.IsPlayingOrPaused) FlacAudio.SetPaused(true);
                    _fullPaused = true;
                    Log.LogInfo("music full pause " + _playingKey);
                }
                RefreshNowPanel();
                RefreshPlayerProgress();
                UpdateMusicSelectionInPlace();
                return;
            }
            var items = FilteredMusic();
            if (_playerIndex >= 0 && _playerIndex < items.Count) SetPlayerTrack(items[_playerIndex], true);
        }

        private void StepPlayerTrack(int dir)
        {
            var items = FilteredMusic();
            if (items.Count == 0) return;
            int idx = Mathf.Clamp(_playerIndex + dir, 0, items.Count - 1);
            if (idx == _playerIndex) return;
            _playerIndex = idx;
            SetPlayerTrack(items[idx], true);
        }

        private void SeekPlayerFromLastMouse()
        {
            if (_playerProgBg == null) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_playerProgBg, mouse.position.ReadValue(), null, out local)) return;
            float f = Mathf.Clamp01(local.x / Mathf.Max(1f, _playerProgBg.rect.width));
            if (_musicInst.isValid())
            {
                int len = 0;
                try { FMOD.Studio.EventDescription d; if (_musicInst.getDescription(out d) == FMOD.RESULT.OK) d.getLength(out len); } catch { }
                if (len > 0) _musicInst.setTimelinePosition((int)(f * len));
            }
            else if (FlacAudio.IsPlayingOrPaused)
            {
                FlacAudio.SeekFrac(f);
            }
        }

        private void RefreshPlayerText()
        {
            if (_playerPlay != null)
                _playerPlay.text = (_playingFull && _playingKey == _playerKey && !_fullPaused) ? "■" : "▶";
            if (_playerVolText != null)
                _playerVolText.text = Mathf.RoundToInt(_volume * 100) + "%";
            if (_playerVolFill != null)
                _playerVolFill.sizeDelta = new Vector2(86f * _volume, 8f);
        }

        private void Toast(string msg)
        {
            if (_toast != null) { _toast.text = msg; }
            Log.LogInfo(msg);
        }

        private void ToggleChrome()
        {
            _immersive = !_immersive;
            if (_viewerChrome != null) _viewerChrome.gameObject.SetActive(!_immersive);
            if (_immersiveBack != null) _immersiveBack.gameObject.SetActive(false);
            if (_viewerStage != null)
            {
                if (_immersive)
                {
                    _viewerStage.anchorMin = Vector2.zero;
                    _viewerStage.anchorMax = Vector2.one;
                    _viewerStage.offsetMin = Vector2.zero;
                    _viewerStage.offsetMax = Vector2.zero;
                }
                else SetStageForTab();
            }
            _zoom = 1f;
            _pan = Vector2.zero;
            FitStage();
            ApplyTransform();
        }

        private void SetStageForTab()
        {
            if (_viewerStage == null) return;
            if (_tab == 0)
            {
                _viewerStage.anchorMin = Vector2.zero;
                _viewerStage.anchorMax = Vector2.one;
                _viewerStage.offsetMin = Vector2.zero;
                _viewerStage.offsetMax = Vector2.zero;
            }
            else if (_tab == 3 || _tab == 4) Top(_viewerStage, 610f, 100f, 700f, 860f);
            else Top(_viewerStage, 80f, 136f, 1760f, 820f);
        }

        private void FitStage()
        {
            if (_cgInstance != null && _viewerStage != null)
            {
                var crt = _cgInstance.transform as RectTransform;
                if (crt != null)
                {
                    float sw = _viewerStage.rect.width > 1f ? _viewerStage.rect.width : (_immersive ? 1920f : 1760f);
                    float sh = _viewerStage.rect.height > 1f ? _viewerStage.rect.height : (_immersive ? 1080f : 820f);
                    float fit = Mathf.Min(sw / 1920f, sh / 1080f);
                    crt.anchoredPosition = Vector2.zero;
                    crt.localScale = new Vector3(fit, fit, 1f);
                }
            }
            if (_bigRaw == null || _bigRaw.texture == null) return;
            float w = _immersive ? 1920f : (_tab == 3 || _tab == 4 ? 700f : 1760f);
            float h = _immersive ? 1080f : (_tab == 3 || _tab == 4 ? 860f : 820f);
            float s = Mathf.Min(w / _bigRaw.texture.width, h / _bigRaw.texture.height);
            _bigRaw.rectTransform.sizeDelta = new Vector2(_bigRaw.texture.width * s, _bigRaw.texture.height * s);
        }

        private void SetZoom(float z)
        {
            _zoom = Mathf.Clamp(z, 1f, 4f);
            if (_zoom <= 1.001f) _pan = Vector2.zero;
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            if (_bigRaw == null) return;
            var rt = _bigRaw.rectTransform;
            rt.localScale = Vector3.one * _zoom;
            rt.anchoredPosition = _pan;
            if (_zoomLbl != null) _zoomLbl.text = Mathf.RoundToInt(_zoom * 100f) + "%";
        }

        // ------------------------------------------------------------------ 页面

        private void SetTab(int tab)
        {
            if (tab != 5 && (!string.IsNullOrEmpty(_previewKey) || !string.IsNullOrEmpty(_pendingKey))) { _previewGen++; StopMusic(); }
            _tab = tab;
            _page = 0;
            _filter = "all";
            _musicSel = 0;
            _prevMusicSel = -1;
            _musicCatStart = 0;
            _musicScroll = 0f;
            _musicScrollTarget = 0f;
            _musicCatScroll = 0f;
            _catDrag = false;
            if (tab == 5 && ((!_musicInst.isValid() && !FlacAudio.IsPlayingOrPaused) || string.IsNullOrEmpty(_playingKey)))
            {
                _playingFull = false;
                _fullPaused = false;
                _playingKey = null;
                _playingTitle = null;
            }
            RefreshTab();
            if (tab == 5 && (!_playingFull || _fullPaused)) SelectMusic(_musicSel, false, true);
        }

        private void RefreshTab()
        {
            if (!_built) return;
            if (_viewer != null && _viewer.gameObject.activeSelf) CloseViewer();

            bool fadeContent = _tab != _lastRenderedTab || _filter != _lastRenderedFilter;
            _lastRenderedTab = _tab;
            _lastRenderedFilter = _filter;
            if (fadeContent && _contentGroup != null) _contentGroup.alpha = 0f;

            for (int i = 0; i < _tabText.Length; i++) _tabText[i].color = i == _tab ? C_INK : C_MUTED;
            if (_bigTitle != null)
            {
                if (_tab == 5) { _bigTitle.text = "FREE PLAY"; if (_subTitle != null) _subTitle.text = "自 由 选 曲"; }
                else { _bigTitle.text = "CG GALLERY"; if (_subTitle != null) _subTitle.text = "C G 鉴 赏"; }
                if (_subTitle != null)
                {
                    float sx = _bigTitle.rectTransform.anchoredPosition.x + _bigTitle.preferredWidth + 24f;
                    Top(_subTitle.rectTransform, sx, 56, 220, 28);
                }
            }

            if (_bgImage != null) _bgImage.sprite = _tab == 5 && _playlistSprite != null ? _playlistSprite : _bgSprite;
            if (_dimImage != null) _dimImage.color = new Color(10f / 255f, 7f / 255f, 5f / 255f, _tab == 5 ? 0.14f : 0.52f);
            if (_vigImage != null) _vigImage.color = new Color(1f, 1f, 1f, _tab == 5 ? 0.82f : 0.78f);

            BuildGuide();
            if (_nowPanel != null)
            {
                bool music = _tab == 5;
                float nw = music ? 922f : 628f;
                _nowProgWidth = nw - 40f;
                Bottom(_nowPanel, music ? 56f : 1080f, music ? 76f : 22f, nw, 44f);
                if (_nowText != null) Top(_nowText.rectTransform, 20f, 8f, nw - 260f, 24f);
                if (_nowState != null) Top(_nowState.rectTransform, nw - 232f, 8f, 212f, 24f);
                if (_nowProgBg != null) Top(_nowProgBg, 20f, 35f, _nowProgWidth, 3f);
                if (_nowProgFill != null) Top(_nowProgFill, 20f, 35f, _nowProgFill.sizeDelta.x, 3f);
            }
            if (_pager != null) _pager.rectTransform.gameObject.SetActive(_tab != 5);
            if (_countText != null)
            {
                _countText.gameObject.SetActive(_tab != 5);
                Top(_countText.rectTransform, 1640f, 236, 200, 26);
            }
            bool musicTab = _tab == 5;
            if (_filmBanner != null) _filmBanner.gameObject.SetActive(musicTab);
            for (int i = 0; i < _tabText.Length; i++) _tabText[i].gameObject.SetActive(!musicTab);
            if (_tabPill != null) _tabPill.gameObject.SetActive(!musicTab);
            if (_tabLine != null) _tabLine.gameObject.SetActive(!musicTab);
            if (_backGalleryPanel != null)
            {
                _backGalleryPanel.gameObject.SetActive(musicTab);
                if (musicTab) _backGalleryPanel.SetAsLastSibling();
            }
            if (_backGallery != null) _backGallery.gameObject.SetActive(musicTab);

            var trt = _tabText[_tab].rectTransform;
            float w = _tabText[_tab].preferredWidth + 38f;
            _tabPill.sizeDelta = new Vector2(w, 40f);
            _tabPill.anchoredPosition = new Vector2(trt.anchoredPosition.x + trt.sizeDelta.x * 0.5f - w * 0.5f, -156f);
            if (_tabLine != null)
            {
                _tabLine.sizeDelta = new Vector2(Mathf.Max(60f, w - 40f), 2f);
                _tabLine.anchoredPosition = new Vector2(trt.anchoredPosition.x + trt.sizeDelta.x * 0.5f - (w - 40f) * 0.5f, -199f);
            }

            for (int i = _content.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);
            _click.RemoveAll(c => c.Rt == null || c.Rt.IsChildOf(_content));
            _hoverRects.Clear();
            _hoverCard = null;
            _cardShadows.Clear();
            _cardHoverBorders.Clear();
            _cardGlows.Clear();
            _musicHoverBorders.Clear();
            _paperFrames.Clear();
            _musicCards.Clear();
            _activeChipText = null;
            _musicTabRoot = null;
            _musicListRoot = null;
            _musicCountLabel = null;
            _recordCoverRaw = null;
            _recordIdxText = null;
            _recordTitleText = null;
            _recordArtistText = null;
            _recordMetaText = null;
            _previewToggleImg = null;
            for (int i = 0; i < 3; i++) { _previewDurImgs[i] = null; _previewDurLabels[i] = null; }
            _prevMusicSel = -1;
            _catDrag = false;

            if (_tab == 0) RenderCurtain();
            else if (_tab == 1) RenderOriginal();
            else if (_tab == 2) RenderStory();
            else if (_tab == 3) RenderTachie();
            else if (_tab == 4) RenderDiff();
            else RenderMusic();
            for (int i = 0; i < _paperFrames.Count; i++)
                if (_paperFrames[i] != null) _paperFrames[i].SetAsLastSibling();
            UpdateCountText();
            RefreshNowPanel();
        }

        private void BuildGuide()
        {
            if (_guideRoot == null) return;
            _click.RemoveAll(c => c.Rt == null || c.Rt.IsChildOf(_guideRoot));
            for (int i = _guideRoot.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_guideRoot.GetChild(i).gameObject);

            var items = _tab == 5
                ? new (string k, string l, Action a)[]
                {
                    ("ESC", "关闭", Hide),
                    ("Tab", "返回CG", () => SetTab(0)),
                    ("1-6/QE", "页签", () => SetTab((_tab + 1) % 6)),
                    ("↑↓", "选歌", () => MoveMusicSelection(1)),
                    ("←→", "章节", () => StepMusicCategory(1)),
                    ("Enter", "开演", () => OpenPlayer(_musicSel)),
                    ("Space", "试听", TogglePreviewFromButton),
                }
                : new (string k, string l, Action a)[]
                {
                    ("ESC", "关闭", Hide),
                    ("1-6/QE", "页签", () => SetTab((_tab + 1) % 6)),
                    ("↑↓", "筛选", () => StepGalleryFilter(1)),
                    ("←→", "翻页", () => NextPage(1)),
                    ("Enter", "查看", OpenFirstInTab),
                };

            float gap = _tab == 5 ? 10f : 8f;
            float gx = 12f;
            for (int i = 0; i < items.Length; i++)
            {
                if (i > 0) gx += gap;
                gx += MeasureKeycapWidth(items[i].k, items[i].l);
            }
            int barW = Mathf.Clamp(Mathf.RoundToInt(gx + 12f), 320, 1140);
            string skinKey = (_tab == 5 ? "guide_fp_" : "guide_cg_") + barW;
            var bar = NewImage("GuideBg", _guideRoot, Color.white, UiSprites.Panel(skinKey, barW, 44, 7f,
                new Color32(28, 21, 16, 232), new Color32(12, 9, 7, 240), new Color32(204, 166, 108, 128), 1.1f, 34));
            bar.type = Image.Type.Simple;
            Top(bar.rectTransform, 0f, 0f, barW, 44f);

            float px = 12f;
            for (int i = 0; i < items.Length; i++)
            {
                if (i > 0) px += gap;
                px += AddKeycapItem(px, items[i].k, items[i].l, items[i].a);
            }
        }

        private static float MeasureKeycapWidth(string key, string label)
        {
            float kw = Mathf.Max(28f, key.Length * 8.5f + 12f);
            float lw = label.Length * 13f + 6f;
            return kw + lw + 10f;
        }

        private float AddKeycapItem(float x, string key, string label, Action act)
        {
            float kw = Mathf.Max(28f, key.Length * 8.5f + 12f);
            float lw = label.Length * 13f + 6f;
            float total = kw + lw + 10f;
            var hit = Rect("Guide_" + label, _guideRoot);
            Top(hit, x, 4f, total, 36f);

            var cap = NewImage("Cap", hit, Color.white, UiSprites.Panel("kcap_" + Mathf.RoundToInt(kw), Mathf.RoundToInt(kw), 26, 4f,
                new Color32(238, 230, 216, 244), new Color32(194, 182, 164, 240), new Color32(128, 108, 86, 215), 1f, 90));
            cap.type = Image.Type.Simple;
            Top(cap.rectTransform, 0f, 5f, kw, 26f);
            var kt = Txt("K", cap.rectTransform, key, 11f, new Color32(32, 24, 20, 250), _sans, TextAlignmentOptions.Center);
            Fill(kt.rectTransform);

            var lt = Txt("L", hit, label, 13f, new Color32(232, 218, 198, 245), _sans, TextAlignmentOptions.Left);
            Top(lt.rectTransform, kw + 6f, 5f, lw, 26f);
            _click.Add(new Click { Rt = hit, Act = act });
            return total;
        }

        private void TogglePreviewFromButton()
        {
            if (!string.IsNullOrEmpty(_previewKey))
            {
                StopMusic();
                UpdateMusicSelectionInPlace();
                return;
            }
            SelectMusic(_musicSel, false, true);
        }

        private void AdjustVolume(float delta)
        {
            _volume = Mathf.Clamp01(_volume + delta);
            if (_musicInst.isValid()) _musicInst.setVolume(_volume * _musicFade);
            if (FlacAudio.IsPlayingOrPaused) FlacAudio.SetVolume(_volume * _musicFade);
            if (_volFill != null) _volFill.sizeDelta = new Vector2(160f * _volume, 4f);
            if (_volText != null) _volText.text = "音量 " + Mathf.RoundToInt(_volume * 100) + "%";
            if (_playerVolFill != null) _playerVolFill.sizeDelta = new Vector2(86f * _volume, 8f);
            if (_playerVolText != null) _playerVolText.text = Mathf.RoundToInt(_volume * 100) + "%";
        }

        private void StepGalleryFilter(int dir)
        {
            var opts = FilterOptions();
            if (opts.Count <= 1) return;
            int cur = 0;
            for (int i = 0; i < opts.Count; i++) if (opts[i] == _filter) { cur = i; break; }
            int nxt = (cur + dir + opts.Count) % opts.Count;
            _filter = opts[nxt];
            _page = 0;
            RefreshTab();
        }

        private void OpenFirstInTab()
        {
            int idx = _page * PerPage();
            if (_tab == 0) { var list = FilteredCurtain(); if (idx < list.Count) OpenCG(list[idx][0]); }
            else if (_tab == 1) { var list = FilteredOriginal(); if (idx < list.Count) OpenOriginal(idx); }
            else if (_tab == 2) { var list = FilteredStory(); if (idx < list.Count) OpenStory(idx); }
            else if (_tab == 3 || _tab == 4) { var list = CurrentStandingItems(); if (idx < list.Count) OpenStanding(idx); }
        }

        private IEnumerator AutoProbe()
        {
            yield return new WaitForSeconds(4f);
            Show();
            yield return new WaitForSeconds(2f);
            Log.LogInfo("自检：CG 页缩略图 " + _thumbs.Count + " 张");
            _tab = 1; _page = 0; RefreshTab();
            yield return new WaitForSeconds(8f);
            Log.LogInfo("自检：立绘页缩略图 " + _thumbs.Count + " 张");
            _tab = 2; _page = 0; RefreshTab();
            yield return new WaitForSeconds(1f);
            if (GalleryData.Music.Length > 0) ToggleMusic(GalleryData.Music[0][0], GalleryData.Music[0][1]);
            yield return new WaitForSeconds(3f);
            Log.LogInfo("自检：音乐 " + (_playingKey == null ? "失败" : "已播放 " + _playingKey));
            StopMusic();
            _tab = 0; _page = 0; RefreshTab();
            yield return new WaitForSeconds(2f);
            Log.LogInfo("自检结束。");
        }

        private IEnumerator CaptureAllThumbs()
        {
            var dummyGo = new GameObject("NientumThumbDummy", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            var dummy = dummyGo.GetComponent<RawImage>();
            int total = GalleryData.Curtain.Length;
            for (int i = 0; i < total; i++)
            {
                string n = GalleryData.Curtain[i][0];
                yield return StartCoroutine(RenderCurtainThumb(n, dummy, 960f, 540f));
                Log.LogInfo("批量缩略图 " + (i + 1) + "/" + total + "：" + n);
                yield return new WaitForSeconds(0.25f);
            }
            UnityEngine.Object.Destroy(dummyGo);
            Log.LogInfo("批量缩略图完成");
        }

        private IEnumerator OpenForDebug()
        {
            yield return new WaitForSeconds(3.5f);
            Show();
            yield return new WaitForSeconds(1.5f);
            if (_captureAll != null && _captureAll.Value)
            {
                yield return StartCoroutine(CaptureAllThumbs());
                yield break;
            }
            if (_startTab != null && _startTab.Value >= 0 && _startTab.Value < _tabText.Length) { _tab = _startTab.Value; _page = 0; RefreshTab(); }
            if (_startPage != null && _startPage.Value > 0) { _page = _startPage.Value; RefreshTab(); }
            yield return new WaitForSeconds(1.2f);
            if (_startViewer != null && _startViewer.Value >= 0)
            {
                int i = _startViewer.Value;
                if (_tab == 0) { var items = FilteredCurtain(); if (i < items.Count) OpenCG(items[i][0]); }
                else if (_tab == 3) { var items = FilteredStanding(); if (i < items.Count) OpenStanding(i); }
            }
            Log.LogInfo("调试定位完成：tab=" + _tab + " page=" + _page);
        }

        private void RefreshNowPanel()
        {
            if (_nowPanel == null) return;
            bool playingFull = _playingFull && !string.IsNullOrEmpty(_playingKey);
            bool previewing = !playingFull && !string.IsNullOrEmpty(_previewKey);
            bool inFreePlay = _tab == 5 && FilteredMusic().Count > 0;
            bool on = playingFull || previewing || inFreePlay;
            _nowPanel.gameObject.SetActive(on);
            if (!on)
            {
                if (_nowProgFill != null) _nowProgFill.sizeDelta = new Vector2(0f, 3f);
                return;
            }
            if (playingFull)
            {
                _nowText.text = ClipText(_playingTitle ?? _playingKey, 34);
                if (_nowState != null) _nowState.text = _fullPaused ? "已暂停 · 点击回到播放" : "正在播放 · 点击回到播放";
            }
            else if (previewing)
            {
                _nowText.text = ClipText(_playingTitle ?? _previewKey, 34);
                if (_nowState != null) _nowState.text = "正在试听 · 点击进入剧场";
            }
            else
            {
                var items = FilteredMusic();
                int idx = Mathf.Clamp(_musicSel, 0, items.Count - 1);
                _nowText.text = ClipText(items[idx][1], 34);
                if (_nowState != null) _nowState.text = "已选定 · Enter 开演";
                if (_nowProgFill != null) _nowProgFill.sizeDelta = new Vector2(0f, 3f);
            }
        }

        private List<string[]> FilteredCurtain()
        {
            var list = new List<string[]>();
            foreach (var row in GalleryData.Curtain)
            {
                if (_filter != "all" && !row[1].StartsWith(_filter)) continue;
                list.Add(row);
            }
            return list;
        }

        private List<string[]> FilteredDiff()
        {
            var list = new List<string[]>();
            string target = _filter == "莉奥拉" ? "Liora" : (_filter == "阿莱夫" ? "Aleph" : _filter);
            foreach (var row in GalleryData.Diff)
            {
                if (_filter != "all" && row[3] != target && !row[2].StartsWith(_filter)) continue;
                list.Add(row);
            }
            return list;
        }

        private List<string[]> FilteredOriginal()
        {
            var list = new List<string[]>();
            foreach (var row in GalleryData.Original)
            {
                if (_filter != "all")
                {
                    int k = row[2].IndexOf('·');
                    string act = k > 0 ? row[2].Substring(0, k).Trim() : row[2];
                    if (act != _filter) continue;
                }
                list.Add(row);
            }
            return list;
        }

        private List<string> FilterOptions()
        {
            var opts = new List<string> { "all" };
            if (_tab == 0)
            {
                foreach (var row in GalleryData.Curtain)
                {
                    int k = row[1].IndexOf('·');
                    string act = k > 0 ? row[1].Substring(0, k).Trim() : row[1];
                    if (!opts.Contains(act)) opts.Add(act);
                }
            }
            else if (_tab == 1)
            {
                foreach (var row in GalleryData.Original)
                {
                    int k = row[2].IndexOf('·');
                    string act = k > 0 ? row[2].Substring(0, k).Trim() : row[2];
                    if (!opts.Contains(act)) opts.Add(act);
                }
            }
            else if (_tab == 2)
            {
                foreach (var row in GalleryData.Story)
                {
                    int k = row[1].IndexOf('·');
                    string act = k > 0 ? row[1].Substring(0, k).Trim() : row[1];
                    if (!opts.Contains(act)) opts.Add(act);
                }
            }
            else if (_tab == 5)
            {
                foreach (var row in GalleryData.Music)
                {
                    string ch = row.Length > 3 ? row[3] : "";
                    if (ch.Length == 0) continue;
                    if (!opts.Contains(ch)) opts.Add(ch);
                }
            }
            else
            {
                opts.Add("莉奥拉"); opts.Add("阿莱夫");
                if (_tab == 3) opts.Add("爱丽丝");
            }
            return opts;
        }

        private static string MusicChapter(string key)
        {
            if (key.Contains("ActU")) return "外传";
            if (key.Contains("ActT") || key.Contains("Ending")) return "终章";
            for (int i = 0; i <= 9; i++)
                if (key.Contains("Act" + i)) return i == 0 ? "序章" : "第" + "一二三四五六七八九"[i - 1] + "章";
            return "界面";
        }

        private List<string[]> FilteredMusic()
        {
            var list = new List<string[]>();
            foreach (var row in GalleryData.Music)
            {
                if (_filter != "all" && (row.Length <= 3 || row[3] != _filter)) continue;
                list.Add(row);
            }
            return list;
        }

        private List<string[]> FilteredStanding()
        {
            var list = new List<string[]>();
            foreach (var row in GalleryData.Standing)
            {
                if (_filter != "all" && !row[2].StartsWith(_filter)) continue;
                list.Add(row);
            }
            return list;
        }

        private int Count()
        {
            if (_tab == 0) return FilteredCurtain().Count;
            if (_tab == 1) return FilteredOriginal().Count;
            if (_tab == 2) return FilteredStory().Count;
            if (_tab == 3) return FilteredStanding().Count;
            if (_tab == 4) return FilteredDiff().Count;
            return FilteredMusic().Count;
        }

        private int PerPage() { return (_tab == 0 || _tab == 1 || _tab == 2 || _tab == 5) ? 6 : 10; }

        private void UpdateCountText()
        {
            if (_countText != null) _countText.text = Count() + (_tab == 5 ? " 首" : " 张");
        }

        private void RenderEmpty(string msg)
        {
            _page = 0; if (_pager != null) _pager.text = "";
            var rt = PaperCard(620, 180, 680, 300);
            var title = Txt("EmptyTitle", rt, "暂无可用资源", 26, C_INK, _serif, TextAlignmentOptions.Center);
            Top(title.rectTransform, 40, 70, 600, 40);
            var line = NewImage("EmptyLine", rt, new Color(C_GOLD_DEEP.r, C_GOLD_DEEP.g, C_GOLD_DEEP.b, 0.8f));
            Top(line.rectTransform, 250, 120, 180, 2);
            var body = Txt("EmptyBody", rt, msg, 16, new Color(C_INK.r, C_INK.g, C_INK.b, 0.8f), _sans, TextAlignmentOptions.Center);
            Top(body.rectTransform, 50, 140, 580, 110);
        }

        private void NextPage(int dir)
        {
            int pages = Mathf.Max(1, (Count() + PerPage() - 1) / PerPage());
            int next = Mathf.Clamp(_page + dir, 0, pages - 1);
            if (next == _page) return;
            _page = next;
            RefreshTab();
        }

        private void UpdatePager()
        {
            int pages = Mathf.Max(1, (Count() + PerPage() - 1) / PerPage());
            _page = Mathf.Clamp(_page, 0, pages - 1);
            _pager.text = (_page + 1) + " / " + pages;
        }

        // ------------------------------------------------------------------ 谢幕 CG

        private void RenderCurtain()
        {
            BuildFilterChips();
            var items = FilteredCurtain();
            UpdatePager();
            int per = 6, start = _page * per;
            int count = Mathf.Min(per, items.Count - start);
            for (int i = 0; i < count; i++)
            {
                int idx = start + i;
                var row = items[idx];
                int col = i % 3, line = i / 3;
                float left = 90f + col * 600f;
                float top = 42f + line * 358f;

                var rt = PaperCard(left, top, 540f, 344f, true);
                var mark = Txt("Mark", rt, "◆", 54, new Color(C_GOLD_DEEP.r, C_GOLD_DEEP.g, C_GOLD_DEEP.b, 0.45f), _serif, TextAlignmentOptions.Center);
                Top(mark.rectTransform, 0, 110, 540, 90);
                var art = NewRaw("Art", rt, new Color(1f, 1f, 1f, 0f));
                Top(art.rectTransform, 0, 0, 540f, 294f);
                StartCoroutine(RenderCurtainThumb(row[0], art, 540f, 294f));

                var title = Txt("Cap", rt, row[1], 20, C_INK, _serif, TextAlignmentOptions.Left);
                Top(title.rectTransform, 18, 304, 380, 32);
                var tag = Txt("Tag", rt, "CURTAIN CALL", 11, new Color(C_GOLD_DEEP.r, C_GOLD_DEEP.g, C_GOLD_DEEP.b, 1f), _sans, TextAlignmentOptions.Right);
                Top(tag.rectTransform, 390, 303, 132, 18);
                var dim = Txt("DimTxt", rt, "3840 × 2160", 11, new Color(132f / 255f, 110f / 255f, 92f / 255f, 1f), _sans, TextAlignmentOptions.Right);
                Top(dim.rectTransform, 390, 320, 132, 18);

                string n = row[0];
                _click.Add(new Click { Rt = rt, Act = () => OpenCG(n) });
            }
        }

        // ------------------------------------------------------------------ CG 原画

        private void RenderOriginal()
        {
            BuildFilterChips();
            var items = FilteredOriginal();
            UpdatePager();
            int per = 6, start = _page * per;
            int count = Mathf.Min(per, items.Count - start);
            for (int i = 0; i < count; i++)
            {
                int idx = start + i;
                var row = items[idx];
                int col = i % 3, line = i / 3;
                float left = 90f + col * 600f;
                float top = 42f + line * 358f;

                var rt = PaperCard(left, top, 540f, 344f, true);
                var art = NewRaw("Art", rt, Color.white);
                Top(art.rectTransform, 0, 0, 540f, 294f);
                StartCoroutine(FillThumbFromGame(row[0], row[1], art, 540f, 294f, true));

                var title = Txt("Cap", rt, row[2], 20, C_INK, _serif, TextAlignmentOptions.Left);
                Top(title.rectTransform, 18, 304, 380, 32);
                var tag = Txt("Tag", rt, "CG 原画", 11, new Color(C_GOLD_DEEP.r, C_GOLD_DEEP.g, C_GOLD_DEEP.b, 1f), _sans, TextAlignmentOptions.Right);
                Top(tag.rectTransform, 390, 303, 132, 18);
                var dim = Txt("DimTxt", rt, row[3] + " × " + row[4], 11, new Color(132f / 255f, 110f / 255f, 92f / 255f, 1f), _sans, TextAlignmentOptions.Right);
                Top(dim.rectTransform, 360, 320, 162, 18);

                int index = idx;
                _click.Add(new Click { Rt = rt, Act = () => OpenOriginal(index) });
            }
        }

        private void OpenOriginal(int idx)
        {
            var items = FilteredOriginal();
            if (idx < 0 || idx >= items.Count) return;
            var row = items[idx];
            _viewerIndex = idx;
            _viewerCat.text = "CG 原画";
            _viewerTitle.text = row[2];
            _viewerSize.text = row[3] + " × " + row[4];
            _viewerIndexLbl.text = (idx + 1) + " / " + items.Count;
            ClearStage();
            BeginViewer();
            StartCoroutine(LoadViewTexture(row[0], row[1]));
        }

        private void SetViewerScrims(bool on)
        {
            if (_viewerTopScrim != null) _viewerTopScrim.gameObject.SetActive(on);
            if (_viewerTopFade != null) _viewerTopFade.gameObject.SetActive(on);
            if (_viewerBottomScrim != null) _viewerBottomScrim.gameObject.SetActive(on);
        }

        private void BeginViewer()
        {
            _viewer.gameObject.SetActive(true);
            if (_viewerGroup != null) _viewerGroup.alpha = 0f;
            _immersive = false;
            if (_viewerChrome != null) _viewerChrome.gameObject.SetActive(true);
            SetViewerScrims(true);
            if (_cgDismiss != null) _cgDismiss.gameObject.SetActive(false);
            if (_immersiveBack != null) _immersiveBack.gameObject.SetActive(false);
            SetStageForTab();
            if (_viewerChrome != null) _viewerStage.SetSiblingIndex(_viewerChrome.GetSiblingIndex());
            SetViewerScrims(true);
        }

        private RectTransform PaperCard(float left, float top, float w, float h, bool cgArt = false)
        {
            var glowRoot = Rect("CardGlow", _content);
            Top(glowRoot, left - 10f, top - 10f, w + 20f, h + 20f);
            var glowOuter = NewImage("GlowOuter", glowRoot, new Color(214f / 255f, 176f / 255f, 112f / 255f, 18f / 255f), _round);
            Fill(glowOuter.rectTransform);
            var glowInner = NewImage("GlowInner", glowRoot, new Color(214f / 255f, 176f / 255f, 112f / 255f, 26f / 255f), _round);
            Top(glowInner.rectTransform, 5f, 5f, w + 10f, h + 10f);
            glowRoot.gameObject.SetActive(false);

            var shadow = NewImage("CardShadow", _content, new Color(0f, 0f, 0f, 85f / 255f), _round);
            shadow.type = Image.Type.Sliced;
            shadow.pixelsPerUnitMultiplier = 2f;
            Top(shadow.rectTransform, left + 4f, top + 7f, w, h);
            var card = NewImage("Card", _content, Color.white, _paperCard != null ? _paperCard : _round);
            card.type = Image.Type.Sliced;
            Top(card.rectTransform, left, top, w, h);
            var frame = NewImage("CardFrame", card.rectTransform, new Color(158f / 255f, 134f / 255f, 108f / 255f, 1f), _roundOutlineThin);
            Top(frame.rectTransform, 0f, 0f, w, h);
            var hoverBorder = NewImage("CardHoverBorder", card.rectTransform, new Color(226f / 255f, 188f / 255f, 130f / 255f, 1f), _roundOutlineThick);
            Top(hoverBorder.rectTransform, 0f, 0f, w, h);
            hoverBorder.gameObject.SetActive(false);
            _paperFrames.Add(frame.rectTransform);
            if (cgArt)
            {
                var artFrame = Rect("CardArtFrame", card.rectTransform);
                Top(artFrame, 0f, 0f, w, 294f);
                Border(artFrame, w, 294f, new Color(120f / 255f, 100f / 255f, 80f / 255f, 210f / 255f), 1.2f);
                _paperFrames.Add(artFrame);
            }
            _paperFrames.Add(hoverBorder.rectTransform);
            _cardShadows[card.rectTransform] = shadow;
            _cardHoverBorders[card.rectTransform] = hoverBorder.gameObject;
            _cardGlows[card.rectTransform] = glowRoot.gameObject;
            _hoverRects.Add(card.rectTransform);
            return card.rectTransform;
        }

        private void SetCardHover(RectTransform card)
        {
            if (card == null) return;
            Image shadow;
            if (_cardShadows.TryGetValue(card, out shadow) && shadow != null)
                shadow.color = new Color(0f, 0f, 0f, 135f / 255f);
            GameObject hb;
            if (_cardHoverBorders.TryGetValue(card, out hb) && hb != null) hb.SetActive(true);
            GameObject glow;
            if (_cardGlows.TryGetValue(card, out glow) && glow != null) glow.SetActive(true);
            GameObject mhb;
            if (_musicHoverBorders.TryGetValue(card, out mhb) && mhb != null) mhb.SetActive(true);
        }

        private void ResetCardHover(RectTransform card)
        {
            if (card == null) return;
            Image shadow;
            if (_cardShadows.TryGetValue(card, out shadow) && shadow != null)
                shadow.color = new Color(0f, 0f, 0f, 85f / 255f);
            GameObject hb;
            if (_cardHoverBorders.TryGetValue(card, out hb) && hb != null) hb.SetActive(false);
            GameObject glow;
            if (_cardGlows.TryGetValue(card, out glow) && glow != null) glow.SetActive(false);
            GameObject mhb;
            if (_musicHoverBorders.TryGetValue(card, out mhb) && mhb != null) mhb.SetActive(false);
        }

        private void Border(RectTransform rt, float w, float h, Color c, float t)
        {
            var top = NewImage("BorderT", rt, c); Top(top.rectTransform, 0, 0, w, t);
            var bot = NewImage("BorderB", rt, c); Top(bot.rectTransform, 0, h - t, w, t);
            var lef = NewImage("BorderL", rt, c); Top(lef.rectTransform, 0, 0, t, h);
            var rig = NewImage("BorderR", rt, c); Top(rig.rectTransform, w - t, 0, t, h);
        }

        // ------------------------------------------------------------------ 立绘

        private void RenderTachie()
        {
            BuildFilterChips();
            var items = FilteredStanding();
            UpdatePager();
            int per = 10, start = _page * per;
            int count = Mathf.Min(per, items.Count - start);
            for (int i = 0; i < count; i++)
            {
                int idx = start + i;
                var row = items[idx];
                int col = i % 5, line = i / 5;
                float left = 96f + col * 352f;
                float top = 36f + line * 350f;

                var rt = PaperCard(left, top, 320f, 330f);
                var art = NewImage("Art", rt, Color.white, _tachieBackSprite);
                art.type = Image.Type.Simple;
                Top(art.rectTransform, 10, 10, 300, 268);

                var raw = NewRaw("Thumb", art.rectTransform, Color.white);
                var rawRt = raw.rectTransform;
                rawRt.anchorMin = rawRt.anchorMax = new Vector2(0.5f, 0.5f);
                rawRt.pivot = new Vector2(0.5f, 0.5f);
                rawRt.anchoredPosition = Vector2.zero;
                rawRt.sizeDelta = new Vector2(300, 268);

                var title = Txt("Cap", rt, row[2], 17, C_INK, _serif, TextAlignmentOptions.Left);
                Top(title.rectTransform, 16, 298, 288, 26);

                int index = idx;
                _click.Add(new Click { Rt = rt, Act = () => OpenStanding(index) });

                StartCoroutine(FillThumbFromGame(row[0], row[1], raw, 300f, 268f));
            }
        }

        private void BuildFilterChips()
        {
            var opts = FilterOptions();
            float x = 96f;
            for (int i = 0; i < opts.Count; i++)
            {
                string key = opts[i];
                bool active = _filter == key;
                string label = key == "all" ? "全部" : key;
                float w = Mathf.Max(74f, 34f + label.Length * 16f);
                var chip = NewImage("Filter" + i, _content, active ? Color.white : new Color(1f, 1f, 1f, 0.80f), active ? _goldPanel : _warmPanel);
                Top(chip.rectTransform, x, -21, w, 32);
                var t = Txt("Label", chip.rectTransform, label, 14, active ? C_INK : C_CREAM, _sans, TextAlignmentOptions.Center);
                Fill(t.rectTransform);
                _click.Add(new Click { Rt = chip.rectTransform, Act = () => { _filter = key; _page = 0; RefreshTab(); } });
                x += w + 10f;
            }
        }

        private void RenderDiff()
        {
            BuildFilterChips();
            var items = FilteredDiff();
            UpdatePager();
            int per = 10, start = _page * per;
            int count = Mathf.Min(per, items.Count - start);
            for (int i = 0; i < count; i++)
            {
                int idx = start + i;
                var row = items[idx];
                int col = i % 5, line = i / 5;
                float left = 96f + col * 352f;
                float top = 36f + line * 350f;
                var rt = PaperCard(left, top, 320f, 330f);
                var art = NewImage("Art", rt, Color.white, _tachieBackSprite);
                art.type = Image.Type.Simple;
                Top(art.rectTransform, 10, 10, 300, 268);
                var raw = NewRaw("Thumb", art.rectTransform, Color.white);
                var rawRt = raw.rectTransform;
                rawRt.anchorMin = rawRt.anchorMax = new Vector2(0.5f, 0.5f);
                rawRt.pivot = new Vector2(0.5f, 0.5f);
                rawRt.anchoredPosition = Vector2.zero;
                rawRt.sizeDelta = new Vector2(300, 268);
                var title = Txt("Cap", rt, row[2], 17, C_INK, _serif, TextAlignmentOptions.Left);
                Top(title.rectTransform, 16, 296, 288, 28);
                int index = idx;
                _click.Add(new Click { Rt = rt, Act = () => OpenStanding(index) });
                StartCoroutine(FillThumbFromGame(row[0], row[1], raw, 300f, 268f));
            }
        }

        // ------------------------------------------------------------------ 曲目

        private Texture2D _lpTex;
        private float _musicScroll, _musicScrollTarget;
        private float _musicFade = 1f;
        private float _fadeInDur = 0.45f;
        private float _fadeOutDur = 0.35f;
        private int _previewGen;
        private float _musicFallbackLen;
        private float _musicStartAt;
        private int _musicToken;
        private bool _musicStopping;
        private float _musicStopAt;
        private FMOD.Studio.EventInstance _suspendedFullInst;
        private string _suspendedFullKey, _suspendedFullTitle;
        private int _suspendedFullPos;
        private bool _suspendedHasFull;
        private bool _suspendedFullPaused;
        private bool _resumeSuspendedAfterStop;
        private class MusicCardView
        {
            public int Index;
            public RectTransform Root;
            public CanvasGroup Group;
            public float Width;
            public float Height;
        }
        private readonly List<MusicCardView> _musicCards = new List<MusicCardView>();
        private RectTransform _volFill, _volBg;
        private bool _volDrag;
        private bool _loopMusic = true;
        private TextMeshProUGUI _volText;
        private float _volume = 1f;

        private static int ChapterCoverId(string chapter)
        {
            switch (chapter)
            {
                case "序章": return 1;
                case "第一幕": return 2;
                case "第二幕": return 3;
                case "第三幕": return 4;
                case "第四幕": return 5;
                case "第五幕": return 6;
                case "第六幕": return 7;
                case "第七幕": return 8;
                case "第八幕": return 9;
                case "第九幕": return 10;
                case "外传": return 1;
                case "终章": return 1;
                default: return 1;
            }
        }

        private static string ChapterCoverFile(string chapter)
        {
            switch (chapter)
            {
                case "序章": return "cover_act0.png";
                case "第一幕": return "cover_act1.png";
                case "第二幕": return "cover_act2.png";
                case "第三幕": return "cover_act3.png";
                case "第四幕": return "cover_act4.png";
                case "第五幕": return "cover_act5.png";
                case "第六幕": return "cover_act6.png";
                case "第七幕": return "cover_act7.png";
                case "第八幕": return "cover_act8.png";
                case "第九幕": return "cover_act9.png";
                case "外传": return "cover_other.png";
                case "终章": return "cover_ending.png";
                case "其他": return "cover_other.png";
                case "未分类": return "cover_unclassified.png";
                default: return "cover_act0.png";
            }
        }

        private Texture2D ChapterCover(string chapter)
        {
            string key = "cover:" + chapter;
            if (_thumbs.TryGetValue(key, out var cached)) return cached;
            Texture2D tex = LoadAssetTexture("thumbs/covers/" + ChapterCoverFile(chapter));
            if (tex == null)
            {
                if (chapter == "终章") tex = LoadAssetTexture("thumbs/covers/ending.png");
                else tex = LoadAssetTexture("thumbs/cg/cg_" + ChapterCoverId(chapter) + ".png");
            }
            if (tex != null) _thumbs[key] = tex;
            return tex;
        }

        private Texture2D ChapterCoverHiRes(string chapter)
        {
            return ChapterCover(chapter);
        }

        private static string ChapterEn(string label)
        {
            switch (label)
            {
                case "序章": return "A Nameless Story";
                case "第一幕": return "The Wizard of Oz";
                case "第二幕": return "Orpheus and Eurydice";
                case "第三幕": return "Cinderella";
                case "第四幕": return "The Tale of the Bamboo Cutter";
                case "第五幕": return "Arthurian Legend";
                case "第六幕": return "Revelation";
                case "第七幕": return "Alice in Wonderland";
                case "第八幕": return "The Little Match Girl";
                case "第九幕": return "Sleeping Beauty";
                case "终章": return "Finale";
                case "外传": return "Extra";
                case "其他": return "Menu & UI";
                case "未分类": return "Unclassified";
                default: return "";
            }
        }

        private static string FormatDuration(string text)
        {
            float seconds;
            if (!float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out seconds)) return "";
            int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        private static string ChapterZh(string label)
        {
            switch (label)
            {
                case "序章": return "无名氏的故事";
                case "第一幕": return "绿野仙踪";
                case "第二幕": return "奥尔菲斯与尤丽狄茜";
                case "第三幕": return "灰姑娘";
                case "第四幕": return "砍竹子的故事";
                case "第五幕": return "亚瑟王传说";
                case "第六幕": return "启示录";
                case "第七幕": return "爱丽丝梦游仙境";
                case "第八幕": return "卖火柴的小女孩";
                case "第九幕": return "睡美人";
                case "终章": return "终章";
                case "外传": return "外传";
                case "其他": return "界面与菜单";
                default: return "";
            }
        }

        private static string ChapterDisplay(string label)
        {
            string zh = ChapterZh(label);
            return string.IsNullOrEmpty(zh) || zh == label ? label : label + " · " + zh;
        }

        private static float MusicDuration(string title)
        {
            foreach (var row in GalleryData.Music)
            {
                if (row.Length < 5 || row[1] != title) continue;
                float d;
                if (float.TryParse(row[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d))
                    return d;
            }
            return 0f;
        }

        private int ChapterCount()
        {
            var set = new HashSet<string>();
            foreach (var row in GalleryData.Music)
                if (row.Length > 3 && !string.IsNullOrEmpty(row[3])) set.Add(row[3]);
            return set.Count;
        }

        private static string MusicArtist(string title)
        {
            if (title.IndexOf("Dancing Garden", StringComparison.Ordinal) >= 0) return "QueseraGames Co., Ltd./Sennzai";
            if (title.IndexOf("Waltz of the Cinder", StringComparison.Ordinal) >= 0) return "QueseraGames Co., Ltd./Sennzai";
            if (title.IndexOf("Welcome to Wonderstage!", StringComparison.Ordinal) >= 0) return "QueseraGames Co., Ltd./U-ske";
            if (title.IndexOf("Here Comes the Ugly!", StringComparison.Ordinal) >= 0) return "QueseraGames Co., Ltd./U-ske";
            if (title.IndexOf("Guess Whose Story?", StringComparison.Ordinal) >= 0) return "QueseraGames Co., Ltd./U-ske";
            if (title.IndexOf("Need Help?", StringComparison.Ordinal) >= 0) return "QueseraGames Co., Ltd./U-ske";
            if (title.IndexOf("I'll Say It for You!", StringComparison.Ordinal) >= 0) return "QueseraGames Co., Ltd./Nanahira";
            if (title.IndexOf("A Story of a Moment", StringComparison.Ordinal) >= 0) return "QueseraGames Co., Ltd./Sennzai/Nanahira";
            return "QueseraGames Co., Ltd.";
        }

        private void RenderMusic()
        {
            var items = FilteredMusic();
            if (items.Count == 0) { RenderEmpty("游戏音频库中没有可用曲目。"); return; }
            if (_musicSel < 0 || _musicSel >= items.Count) _musicSel = 0;
            if (_lpTex == null) _lpTex = LoadAssetTexture("ui/LP_original.png");
            if (_cardPaper == null)
            {
                var listActive = LoadAssetTexture("ui/List_active.png");
                if (listActive != null) _cardPaper = ToSprite(listActive);
            }
            if (_cardPaper == null) _cardPaper = MakePaperCardSprite();

            BuildMusicTabs();
            _musicListRoot = Rect("MusicListRoot", _content);
            Top(_musicListRoot, 48f, 22f, 980f, 650f);
            _musicListRoot.gameObject.AddComponent<RectMask2D>();

            int n = items.Count;
            var cur = items[_musicSel];
            _selectedMusicKey = TrackId(cur);
            string chapter = cur.Length > 3 ? cur[3] : "";

            var lab = Txt("MusicCount", _content, "曲目  " + (_musicSel + 1).ToString("00") + " / " + n.ToString("00"), 15, new Color(190f / 255f, 168f / 255f, 142f / 255f, 0.90f), _sans, TextAlignmentOptions.Left);
            Top(lab.rectTransform, 70f, 2f, 420f, 26f);
            _musicCountLabel = lab;
            var cnt = Txt("MusicTotal", _content, GalleryData.Music.Length + " 曲 · " + ChapterCount() + " 章", 15, new Color(176f / 255f, 156f / 255f, 136f / 255f, 0.84f), _sans, TextAlignmentOptions.Right);
            Top(cnt.rectTransform, 800f, 2f, 200f, 26f);
            var hline = NewImage("MusicLine", _content, new Color(160f / 255f, 128f / 255f, 88f / 255f, 0.43f));
            Top(hline.rectTransform, 70f, 20f, 930f, 1f);

            _musicCards.Clear();
            _musicScrollTarget = Mathf.Clamp(_musicSel - 2, 0, Mathf.Max(0, n - 6));
            _musicScroll = Mathf.Clamp(_musicScroll, 0f, Mathf.Max(0, n - 6));
            var drawOrder = new List<int>();
            for (int idx = 0; idx < n; idx++) drawOrder.Add(idx);
            drawOrder.Sort((a, b) => Mathf.Abs(b - _musicSel).CompareTo(Mathf.Abs(a - _musicSel)));
            for (int i = 0; i < drawOrder.Count; i++) BuildMusicCard(items, drawOrder[i]);
            LayoutMusicCards();

            RenderRecord(items, chapter, n);
            RenderPreviewBar(items);
            _volBg = null; _volFill = null; _volText = null;
        }

        private void BuildMusicTabs()
        {
            var opts = FilterOptions();
            _musicCatCount = opts.Count;
            int active = 0;
            for (int i = 0; i < opts.Count; i++)
                if (opts[i] == _filter) { active = i; break; }

            const int visible = 8;
            _musicCatStart = Mathf.Clamp(_musicCatStart, 0, Mathf.Max(0, opts.Count - visible));
            _musicCatScroll = _musicCatStart;

            _musicTabLabels.Clear();
            _musicTabRoot = Rect("MusicTabRoot", _content);
            Top(_musicTabRoot, 70f, -84f, 800f, 50f);
            _musicTabRoot.gameObject.AddComponent<RectMask2D>();

            _catPill = NewImage("CatPill", _musicTabRoot, Color.white, _goldPanel);
            Top(_catPill.rectTransform, 0f, 0f, 86f, 38f);
            _catLine = NewImage("CatLine", _musicTabRoot, C_GOLD);
            Top(_catLine.rectTransform, 0f, 0f, 74f, 2f);

            _catPrev = Txt("CatPrev", _content, "◀", 18, C_GOLD_LIGHT, _sans, TextAlignmentOptions.Center);
            Top(_catPrev.rectTransform, 36f, -70f, 28f, 32f);
            _click.Add(new Click { Rt = _catPrev.rectTransform, Act = () => { if (!_catDrag) { _musicCatStart = Mathf.Max(0, _musicCatStart - 1); _musicCatScroll = _musicCatStart; RefreshTab(); } } });
            _catNext = Txt("CatNext", _content, "▶", 18, C_GOLD_LIGHT, _sans, TextAlignmentOptions.Center);
            Top(_catNext.rectTransform, 874f, -70f, 28f, 32f);
            _click.Add(new Click { Rt = _catNext.rectTransform, Act = () => { if (!_catDrag) { _musicCatStart = Mathf.Min(Mathf.Max(0, opts.Count - 8), _musicCatStart + 1); _musicCatScroll = _musicCatStart; RefreshTab(); } } });

            for (int i = 0; i < opts.Count; i++)
            {
                string label = opts[i] == "all" ? "全部" : opts[i];
                var t = Txt("Cat" + i, _musicTabRoot, label, 19f, new Color(176f / 255f, 162f / 255f, 146f / 255f, 0.80f), _serif, TextAlignmentOptions.Center);
                Top(t.rectTransform, 0f, 0f, 90f, 44f);
                _musicTabLabels.Add(t);
            }
            LayoutMusicTabs();
        }

        private void LayoutMusicTabs()
        {
            if (_musicTabRoot == null) return;
            for (int i = 0; i < _musicTabLabels.Count; i++)
            {
                var t = _musicTabLabels[i];
                if (t == null) continue;
                t.rectTransform.anchoredPosition = new Vector2(i * 100f - _musicCatScroll * 100f, 0f);
            }
            // Recompute active from filter.
            var opts = FilterOptions();
            int aidx = 0;
            for (int i = 0; i < opts.Count; i++) if (opts[i] == _filter) { aidx = i; break; }
            for (int i = 0; i < _musicTabLabels.Count; i++)
            {
                var t = _musicTabLabels[i];
                if (t == null) continue;
                bool on = i == aidx;
                t.fontSize = on ? 22f : 19f;
                t.color = on ? C_INK : new Color(176f / 255f, 162f / 255f, 146f / 255f, 0.80f);
            }
            if (_catPill != null)
            {
                float px = aidx * 100f - _musicCatScroll * 100f;
                bool vis = px > -100f && px < 820f;
                _catPill.gameObject.SetActive(vis);
                _catLine.gameObject.SetActive(vis);
                if (vis)
                {
                    _catPill.rectTransform.anchoredPosition = new Vector2(px + 4f, -5f);
                    _catLine.rectTransform.anchoredPosition = new Vector2(px + 10f, -47f);
                }
            }
            if (_catPrev != null) _catPrev.gameObject.SetActive(_musicCatScroll > 0.01f);
            if (_catNext != null) _catNext.gameObject.SetActive(_musicCatScroll < _musicCatCount - 8 - 0.01f);
        }

        private void SelectMusicCategory(int idx)
        {
            var opts = FilterOptions();
            if (idx < 0 || idx >= opts.Count) return;
            string key = opts[idx];
            if (key == _filter) return;
            bool keepPlayingFull = _playingFull && !_fullPaused;
            if (!keepPlayingFull)
            {
                _previewGen++;
                StopMusicImmediate(true);
            }
            _filter = key;
            _musicSel = 0;
            _page = 0;
            _musicScroll = 0f;
            _musicScrollTarget = 0f;
            if (idx < _musicCatStart) _musicCatStart = idx;
            else if (idx >= _musicCatStart + 8) _musicCatStart = idx - 7;
            _musicCatScroll = _musicCatStart;
            Log.LogInfo("music filter=" + key + " count=" + FilteredMusic().Count);
            RefreshTab();
        }

        private static string TrackId(string[] row)
        {
            if (row == null || row.Length == 0) return "";
            return !string.IsNullOrEmpty(row[0]) && row[0] != "—" ? row[0] : (row.Length > 1 ? ("title:" + row[1]) : "");
        }

        private static string ClipText(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text;
            return text.Substring(0, Mathf.Max(1, max - 1)) + "…";
        }

        private void BuildMusicCard(List<string[]> items, int idx)
        {
            var row = items[idx];
            string tid = TrackId(row);
            bool active = idx == _musicSel;
            bool playing = _playingKey == tid;
            bool preview = playing && _previewKey == tid;
            float w = active ? 922f : 900f;
            float h = active ? 148f : 134f;

            var root = Rect("MusicCard" + idx, _musicListRoot != null ? _musicListRoot : _content);
            Top(root, 0f, 0f, w, h);
            var group = root.gameObject.AddComponent<CanvasGroup>();

            if (active)
            {
                var g1 = NewImage("CardGlow1", root, new Color(214f / 255f, 176f / 255f, 112f / 255f, 18f / 255f), _round);
                Top(g1.rectTransform, -11f, -11f, w + 22f, h + 22f);
                var g2 = NewImage("CardGlow2", root, new Color(214f / 255f, 176f / 255f, 112f / 255f, 32f / 255f), _round);
                Top(g2.rectTransform, -5f, -5f, w + 10f, h + 10f);
            }
            var sh = NewImage("CardShadow", root, new Color(0f, 0f, 0f, active ? 105f / 255f : 62f / 255f), _round);
            Top(sh.rectTransform, 5f, 6f, w + 10f, h + 14f);

            var bg = NewImage("Card", root, new Color(1f, 1f, 1f, active ? 1f : 0.90f), _cardPaper);
            bg.type = Image.Type.Simple;
            Top(bg.rectTransform, 0f, 0f, w, h);

            if (active)
            {
                var br = NewImage("CardBorderRect", root, new Color(218f / 255f, 184f / 255f, 126f / 255f, 0.97f), _roundOutlineMid);
                Top(br.rectTransform, 1f, 1f, w - 2f, h - 2f);
                var spine = NewImage("CardSpine", root, new Color(204f / 255f, 160f / 255f, 100f / 255f, 0.98f));
                Top(spine.rectTransform, 3f, 16f, 3f, h - 32f);
            }
            else
            {
                var hb = NewImage("MusicHoverBorder", root, new Color(218f / 255f, 184f / 255f, 126f / 255f, 150f / 255f), _roundOutlineThin);
                Top(hb.rectTransform, 1f, 1f, w - 2f, h - 2f);
                hb.gameObject.SetActive(false);
                _musicHoverBorders[root] = hb.gameObject;
            }

            float thumbTop = (h - 110f) * 0.5f;
            var tr = NewRaw("Thumb", root, new Color32(0x1A, 0x15, 0x12, 235));
            Top(tr.rectTransform, 18f, thumbTop, 196f, 110f);
            var thumb = ChapterCover(row.Length > 3 ? row[3] : "");
            if (thumb != null)
            {
                AssignCover(tr, thumb, 196f, 110f);
                if (!active) tr.color = new Color(1f, 1f, 1f, 0.84f);
            }

            var tb = Rect("ThumbBorder", root);
            Top(tb, 16f, thumbTop - 2f, 200f, 114f);
            Border(tb, 200f, 114f, new Color(145f / 255f, 120f / 255f, 92f / 255f, 190f / 255f), 1f);
            var tib = Rect("ThumbInnerBorder", root);
            Top(tib, 18f, thumbTop, 196f, 110f);
            Border(tib, 196f, 110f, new Color(210f / 255f, 174f / 255f, 116f / 255f, active ? 165f / 255f : 95f / 255f), 1f);
            if (active)
            {
                var mark = NewImage("ThumbMark", root, new Color(228f / 255f, 192f / 255f, 132f / 255f, 0.90f));
                Top(mark.rectTransform, 18f, thumbTop, 26f, 2f);
            }

            float tx = 240f;
            float textW = w - 240f - 176f;
            float textA = active ? 1f : 192f / 255f;
            float cy = h * 0.5f;
            float titleSize = active ? 28f : 24f;
            float artistSize = active ? 17f : 15f;
            float metaSize = active ? 14f : 13f;
            float titleTop = cy - 3f - titleSize * 0.8f;
            float artistTop = cy + 25f - artistSize * 0.8f;
            float metaTop = cy + 48f - metaSize * 0.8f;

            var title = Txt("CardTitle", root, ClipText(row[1], 34), titleSize, new Color(78f / 255f, 62f / 255f, 55f / 255f, textA), _serif, TextAlignmentOptions.Left);
            title.overflowMode = TextOverflowModes.Overflow;
            Top(title.rectTransform, tx, titleTop, textW, active ? 36f : 32f);

            float subA = textA * 0.92f;
            var artist = Txt("CardArtist", root, ClipText(MusicArtist(row[1]), 40), artistSize, new Color(118f / 255f, 98f / 255f, 88f / 255f, subA), _sans, TextAlignmentOptions.Left);
            artist.overflowMode = TextOverflowModes.Overflow;
            Top(artist.rectTransform, tx, artistTop, textW - 10f, 24f);

            string chapter = row.Length > 3 ? row[3] : "";
            string meta = ChapterDisplay(chapter) + "      " + FormatDuration(row.Length > 4 ? row[4] : "");
            var m = Txt("CardMeta", root, meta, metaSize, new Color(150f / 255f, 118f / 255f, 88f / 255f, subA), _sans, TextAlignmentOptions.Left);
            Top(m.rectTransform, tx, metaTop, textW, 24f);

            if (playing && !string.IsNullOrEmpty(_playingKey))
            {
                string label = preview ? "正在试听" : (_playingFull && _fullPaused ? "已暂停" : "正在播放");
                float cw = preview ? 148f : 118f;
                float cx = w - 22f - cw;
                float chipTop = active ? 93f : 53f;
                var chip = NewImage("StateChip", root, new Color32(0x18, 0x12, 0x0E, 228), _round);
                chip.type = Image.Type.Sliced;
                chip.pixelsPerUnitMultiplier = 2f;
                Top(chip.rectTransform, cx, chipTop, cw, 28f);
                Border(chip.rectTransform, cw, 28f, new Color(206f / 255f, 168f / 255f, 108f / 255f, 205f / 255f), 1f);
                for (int b = 0; b < 3; b++)
                {
                    var bar = NewImage("Eq" + b, root, new Color(234f / 255f, 192f / 255f, 126f / 255f, 1f));
                    Top(bar.rectTransform, cx + 10f + b * 5f, chipTop + (b == 1 ? 8f : 11f), 2.4f, b == 1 ? 12f : 7f);
                }
                if (preview)
                    label += " · " + Mathf.Max(0, Mathf.CeilToInt(_previewEndAt - Time.unscaledTime)).ToString("00") + "s";
                var st = Txt("StateText", root, label, 13f, new Color(240f / 255f, 224f / 255f, 200f / 255f, 0.97f), _sans, TextAlignmentOptions.Left);
                Top(st.rectTransform, cx + 28f, chipTop + 5f, cw - 32f, 22f);
                if (preview) _activeChipText = st;
            }

            if (active && _originalSprite != null && row.Length > 5 && row[5] == "true")
            {
                var badge = NewImage("OriginalBadge", root, Color.white, _originalSprite);
                badge.type = Image.Type.Simple;
                Top(badge.rectTransform, w - 122f, h * 0.5f - 44f, 100f, 33f);
            }

            int ci = idx;
            _click.Add(new Click { Rt = bg.rectTransform, Act = () => OnMusicCard(ci) });
            _hoverRects.Add(root);
            _musicCards.Add(new MusicCardView { Index = idx, Root = root, Group = group, Width = w, Height = h });
        }

        private void LayoutMusicCards()
        {
            foreach (var view in _musicCards)
            {
                if (view.Root == null) continue;
                float cy = 98f + (view.Index - _musicScroll) * 100f;
                float t = Mathf.Clamp01((cy - 32f) / 636f);
                float off = 52f * Mathf.Sin(Mathf.PI * t);
                float x = 16f + (900f - view.Width) * 0.5f + off;
                float top = (cy - 24f) - view.Height * 0.5f;
                float lift = view.Root == _hoverCard ? 3f : 0f;
                view.Root.anchoredPosition = new Vector2(x, -top + lift);
                float fade = Mathf.Clamp01(Mathf.Min(cy - 24f, 650f - cy) / 72f);
                view.Group.alpha = fade;
                view.Root.gameObject.SetActive(fade > 0.02f);
            }
        }

        private void RenderRecord(List<string[]> items, string chapter, int n)
        {
            var row = items[_musicSel];
            var r = NewRaw("Record", _content, Color.white);
            r.texture = _lpTex;
            var rrt = r.rectTransform;
            rrt.anchorMin = rrt.anchorMax = new Vector2(0f, 1f);
            rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.anchoredPosition = new Vector2(1580f, -272f);
            rrt.sizeDelta = new Vector2(1000f, 1000f);
            rrt.localRotation = Quaternion.Euler(0f, 0f, _lpAngle);
            _record = r;

            // 封面下面垫一层不透明深色圆，避免唱片金色透到封面上。
            var coverBack = NewImage("RecordCoverBack", rrt, new Color32(0x2A, 0x20, 0x1A, 0xFF), _circleSprite);
            coverBack.type = Image.Type.Simple;
            var brt = coverBack.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(458f, 458f);

            var circleCover = GetCircleCover(chapter, Mathf.RoundToInt(520f * _nonCgResScale));
            var cover = NewRaw("RecordCover", rrt, Color.white);
            var crt = cover.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(450f, 450f);
            if (circleCover != null)
            {
                cover.texture = circleCover;
                cover.uvRect = new Rect(0f, 0f, 1f, 1f);
            }
            _recordCoverRaw = cover;

            var hole = NewImage("RecordHole", rrt, new Color32(0x1C, 0x14, 0x10, 235), _circleSprite);
            hole.type = Image.Type.Simple;
            var hrt = hole.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
            hrt.pivot = new Vector2(0.5f, 0.5f);
            hrt.anchoredPosition = Vector2.zero;
            hrt.sizeDelta = new Vector2(18f, 18f);

            bool originalTrack = row.Length > 5 && row[5] == "true";
            var idxT = Txt("RecordIndex", _content, originalTrack ? "O R I G I N A L" : ("第 " + (_musicSel + 1) + " 曲"), originalTrack ? 13f : 14f, C_GOLD, _sans, TextAlignmentOptions.Center);
            idxT.characterSpacing = originalTrack ? 2.4f : 0f;
            Top(idxT.rectTransform, 1136f, 778f, 800f, 20f);
            _recordIdxText = idxT;
            var title = Txt("RecordTitle", _content, ClipText(row[1], 42), 34, new Color(243f / 255f, 233f / 255f, 218f / 255f, 1f), _serif, TextAlignmentOptions.Center);
            title.overflowMode = TextOverflowModes.Overflow;
            Top(title.rectTransform, 1136f, 798f, 800f, 40f);
            _recordTitleText = title;
            var artist = Txt("RecordArtist", _content, ClipText(MusicArtist(row[1]), 40), 18, new Color(208f / 255f, 190f / 255f, 168f / 255f, 0.95f), _serif, TextAlignmentOptions.Center);
            Top(artist.rectTransform, 1136f, 838f, 800f, 24f);
            _recordArtistText = artist;
            var meta = Txt("RecordMeta", _content, ChapterDisplay(chapter) + "   ·   " + (_musicSel + 1).ToString("00") + " / " + n.ToString("00"), 16, new Color(202f / 255f, 166f / 255f, 120f / 255f, 0.97f), _sans, TextAlignmentOptions.Center);
            Top(meta.rectTransform, 1136f, 862f, 800f, 24f);
            _recordMetaText = meta;

            _click.Add(new Click { Rt = r.rectTransform, Act = OnRecordClick });
        }

        private void RenderPreviewBar(List<string[]> items)
        {
            var row = items[_musicSel];
            var bar = NewWarmPanel(_content, "PreviewBar", 1222f, 890f, 628f, 46f);

            var toggle = NewImage("PreviewToggle", _content, Color.white, UiSprites.Panel("pv_btn", 104, 34, 5f,
                new Color32(42, 32, 24, 232), new Color32(16, 12, 9, 238), new Color32(212, 172, 112, 148), 1.05f, 36));
            toggle.type = Image.Type.Simple;
            Top(toggle.rectTransform, 1234f, 896f, 104f, 34f);
            _previewToggleImg = toggle;
            _previewToggleText = Txt("PreviewToggleLabel", toggle.rectTransform, "试听", 13f, C_CREAM, _sans, TextAlignmentOptions.Center);
            Fill(_previewToggleText.rectTransform);
            _click.Add(new Click { Rt = toggle.rectTransform, Act = TogglePreviewFromButton });

            _musicStatus = Txt("PreviewStatus", _content, "", 13, new Color(206f / 255f, 190f / 255f, 168f / 255f, 0.92f), _sans, TextAlignmentOptions.Left);
            _musicStatus.overflowMode = TextOverflowModes.Ellipsis;
            Top(_musicStatus.rectTransform, 1350f, 897f, 316f, 32f);

            for (int i = 0; i < 3; i++)
            {
                int sec = (i + 1) * 10;
                bool on = _previewSeconds == sec;
                var btn = NewImage("Dur" + i, _content, Color.white, UiSprites.Panel(on ? "dur_on" : "dur_off", 50, 32, 5f,
                    on ? new Color32(246, 222, 178, 250) : new Color32(36, 27, 20, 218),
                    on ? new Color32(168, 126, 70, 250) : new Color32(16, 12, 9, 230),
                    on ? new Color32(255, 240, 206, 235) : new Color32(212, 172, 112, 116), 1.05f, on ? (byte)110 : (byte)20));
                btn.type = Image.Type.Simple;
                Top(btn.rectTransform, 1678f + i * 54f, 897f, 50f, 32f);
                var t = Txt("DurLabel" + i, btn.rectTransform, sec + "s", 13, on ? new Color32(32, 22, 15, 250) : C_CREAM, _sans, TextAlignmentOptions.Center);
                Fill(t.rectTransform);
                _previewDurImgs[i] = btn;
                _previewDurLabels[i] = t;
                int s = sec;
                _click.Add(new Click { Rt = btn.rectTransform, Act = () => { _previewSeconds = s; if (!string.IsNullOrEmpty(_previewKey)) _previewEndAt = Time.unscaledTime + s; UpdateMusicSelectionInPlace(); } });
            }

            if (!string.IsNullOrEmpty(_previewKey))
                _musicStatus.text = "试听中 · " + Mathf.Max(0, Mathf.CeilToInt(_previewEndAt - Time.unscaledTime)).ToString("00") + "s";
            else if (_playingFull && _playingKey == TrackId(row)) _musicStatus.text = "正式播放中";
            else if (!string.IsNullOrEmpty(_previewError)) _musicStatus.text = _previewError;
            else _musicStatus.text = "试听 " + _previewSeconds + " 秒";
            if (_previewToggleText != null) _previewToggleText.text = string.IsNullOrEmpty(_previewKey) ? "试听" : "停止";
        }

        private void OnMusicCard(int idx)
        {
            var items = FilteredMusic();
            if (idx < 0 || idx >= items.Count) return;
            float now = Time.unscaledTime;
            bool dbl = _lastCardIndex == idx && now - _lastCardClickAt < 0.35f;
            _lastCardIndex = idx;
            _lastCardClickAt = now;
            Log.LogInfo("music card selected=" + items[idx][0] + " chapter=" + (items[idx].Length > 3 ? items[idx][3] : ""));
            if (dbl) { _musicSel = idx; OpenPlayer(idx); }
            else SelectMusic(idx, false);
        }

        private void MoveMusicSelection(int dir)
        {
            var items = FilteredMusic();
            if (items.Count == 0) return;
            int idx = Mathf.Clamp(_musicSel + dir, 0, items.Count - 1);
            if (idx == _musicSel) return;
            SelectMusic(idx, false);
        }

        private void StepMusicCategory(int dir)
        {
            var opts = FilterOptions();
            int cur = 0;
            for (int i = 0; i < opts.Count; i++) if (opts[i] == _filter) { cur = i; break; }
            int nxt = Mathf.Clamp(cur + dir, 0, opts.Count - 1);
            if (nxt == cur) return;
            bool keepPlayingFull = _playingFull && !_fullPaused;
            if (!keepPlayingFull)
            {
                _previewGen++;
                StopMusicImmediate(true);
            }
            if (nxt < _musicCatStart) _musicCatStart = nxt;
            else if (nxt >= _musicCatStart + 8) _musicCatStart = nxt - 7;
            _filter = opts[nxt];
            _musicSel = 0;
            _musicScroll = 0f;
            _musicScrollTarget = 0f;
            _musicCatScroll = _musicCatStart;
            _catDrag = false;
            RefreshTab();
            if (!keepPlayingFull) SelectMusic(_musicSel, false);
        }

        private void UpdateMusicSelectionInPlace()
        {
            if (_tab != 5 || _musicListRoot == null || _musicCards.Count == 0) { RefreshTab(); return; }
            var items = FilteredMusic();
            int n = items.Count;
            if (n == 0) return;
            _musicSel = Mathf.Clamp(_musicSel, 0, n - 1);
            _musicScrollTarget = Mathf.Clamp(_musicSel - 2, 0, Mathf.Max(0, n - 6));

            // 仅重建旧选中项与新选中项最多 2 张卡，其余保持不动，滚动无闪断
            var rebuild = new HashSet<int>();
            if (_prevMusicSel >= 0 && _prevMusicSel < n && _prevMusicSel != _musicSel) rebuild.Add(_prevMusicSel);
            rebuild.Add(_musicSel);
            foreach (var v in _musicCards)
            {
                if (v.Root == null) continue;
                bool hasChip = v.Root.Find("StateChip") != null;
                bool shouldPlay = v.Index < n && _playingKey == TrackId(items[v.Index]) && !string.IsNullOrEmpty(_playingKey);
                if (hasChip != shouldPlay) rebuild.Add(v.Index);
            }
            // 也同步刷新正在播放状态变化过的卡片
            for (int i = _musicCards.Count - 1; i >= 0; i--)
            {
                var v = _musicCards[i];
                if (!rebuild.Contains(v.Index)) continue;
                if (v.Root != null)
                {
                    if (_hoverCard == v.Root) _hoverCard = null;
                    _hoverRects.Remove(v.Root);
                    _musicHoverBorders.Remove(v.Root);
                    _click.RemoveAll(c => c.Rt != null && (c.Rt == v.Root || c.Rt.IsChildOf(v.Root)));
                    UnityEngine.Object.Destroy(v.Root.gameObject);
                }
                _musicCards.RemoveAt(i);
            }
            foreach (int ridx in rebuild) BuildMusicCard(items, ridx);
            _musicCards.Sort((a, b) => Mathf.Abs(b.Index - _musicSel).CompareTo(Mathf.Abs(a.Index - _musicSel)));
            for (int i = 0; i < _musicCards.Count; i++)
                if (_musicCards[i].Root != null) _musicCards[i].Root.SetSiblingIndex(i);
            LayoutMusicCards();

            var row = items[_musicSel];
            string chapter = row.Length > 3 ? row[3] : "";
            _selectedMusicKey = TrackId(row);
            if (_musicCountLabel != null) _musicCountLabel.text = "曲目  " + (_musicSel + 1).ToString("00") + " / " + n.ToString("00");
            if (_recordCoverRaw != null)
            {
                var cc = GetCircleCover(chapter, Mathf.RoundToInt(520f * _nonCgResScale));
                if (cc != null) _recordCoverRaw.texture = cc;
            }
            bool originalTrack = row.Length > 5 && row[5] == "true";
            if (_recordIdxText != null)
            {
                _recordIdxText.text = originalTrack ? "O R I G I N A L" : ("第 " + (_musicSel + 1) + " 曲");
                _recordIdxText.fontSize = originalTrack ? 13f : 14f;
                _recordIdxText.characterSpacing = originalTrack ? 2.4f : 0f;
            }
            if (_recordTitleText != null) _recordTitleText.text = ClipText(row[1], 42);
            if (_recordArtistText != null) _recordArtistText.text = ClipText(MusicArtist(row[1]), 40);
            if (_recordMetaText != null) _recordMetaText.text = ChapterDisplay(chapter) + "   ·   " + (_musicSel + 1).ToString("00") + " / " + n.ToString("00");

            for (int i = 0; i < 3; i++)
            {
                if (_previewDurImgs[i] == null) continue;
                bool on = _previewSeconds == (i + 1) * 10;
                _previewDurImgs[i].sprite = UiSprites.Panel(on ? "dur_on" : "dur_off", 50, 32, 5f,
                    on ? new Color32(246, 222, 178, 250) : new Color32(36, 27, 20, 218),
                    on ? new Color32(168, 126, 70, 250) : new Color32(16, 12, 9, 230),
                    on ? new Color32(255, 240, 206, 235) : new Color32(212, 172, 112, 116), 1.05f, on ? (byte)110 : (byte)20);
                if (_previewDurLabels[i] != null)
                    _previewDurLabels[i].color = on ? new Color32(32, 22, 15, 250) : C_CREAM;
            }
            if (_musicStatus != null)
            {
                if (!string.IsNullOrEmpty(_previewKey))
                    _musicStatus.text = "试听中 · " + Mathf.Max(0, Mathf.CeilToInt(_previewEndAt - Time.unscaledTime)).ToString("00") + "s";
                else if (_playingFull && _playingKey == TrackId(row)) _musicStatus.text = "正式播放中";
                else if (!string.IsNullOrEmpty(_previewError)) _musicStatus.text = _previewError;
                else _musicStatus.text = "试听 " + _previewSeconds + " 秒";
            }
            if (_previewToggleText != null) _previewToggleText.text = string.IsNullOrEmpty(_previewKey) ? "试听" : "停止";
            _prevMusicSel = _musicSel;
        }

        private void OnRecordClick()
        {
            var items = FilteredMusic();
            if (items.Count == 0) return;
            if (!string.IsNullOrEmpty(_previewKey)) { StopMusic(); RefreshTab(); return; }
            SelectMusic(_musicSel, false, true);
        }

        private void SelectMusic(int idx, bool full, bool immediatePreview = false)
        {
            var items = FilteredMusic();
            if (idx < 0 || idx >= items.Count) return;
            _prevMusicSel = _musicSel;
            _musicSel = idx;
            string key = TrackId(items[idx]);
            string title = items[idx][1];
            Log.LogInfo("music select idx=" + idx + " selected=" + key + " scheduledGen=" + (_previewGen + 1) + " previewSeconds=" + _previewSeconds + " full=" + full);

            if (full)
            {
                if (_playingFull && _playingKey == key) { StopMusic(); UpdateMusicSelectionInPlace(); return; }
                StartMusic(key, title, true);
                return;
            }

            // 手动播放中的整曲保持优先；只有显式试听（唱片/Space）才暂停并接管。
            if (_playingFull && !_fullPaused && !immediatePreview)
            {
                UpdateMusicSelectionInPlace();
                return;
            }
            if (_playingFull) SuspendFullForPreview();
            if (_previewKey == key && PreviewPlaybackAlive())
            {
                Log.LogInfo("music select same-alive selected=" + key);
                UpdateMusicSelectionInPlace();
                return;
            }

            _previewGen++;
            int gen = _previewGen;
            if (immediatePreview)
                StopMusicImmediate(!_playingFull);
            else if (_musicInst.isValid() || FlacAudio.IsPlayingOrPaused)
                StopMusic();
            else
                StopMusicInstance();
            _resumeSuspendedAfterStop = false;
            _previewError = null;
            _pendingKey = null;
            UpdateMusicSelectionInPlace();
            if (immediatePreview) StartMusic(key, title, false);
            else
            {
                Log.LogInfo("music preview schedule selected=" + key + " scheduledGen=" + gen);
                StartCoroutine(DelayedPreview(idx, key, gen, 0.65f));
            }
        }

        private bool PreviewPlaybackAlive()
        {
            if (!_musicInst.isValid()) return FlacAudio.IsAlive;
            try
            {
                FMOD.Studio.PLAYBACK_STATE st;
                if (_musicInst.getPlaybackState(out st) != FMOD.RESULT.OK) return false;
                return st == FMOD.Studio.PLAYBACK_STATE.PLAYING
                    || st == FMOD.Studio.PLAYBACK_STATE.STARTING
                    || st == FMOD.Studio.PLAYBACK_STATE.SUSTAINING;
            }
            catch { return false; }
        }

        private IEnumerator DelayedPreview(int idx, string key, int gen, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (gen != _previewGen || !_visible || _tab != 5 || _playingFull)
            {
                Log.LogInfo("music preview stale selected=" + key + " scheduledGen=" + gen + " currentGen=" + _previewGen + " full=" + _playingFull);
                yield break;
            }
            var items = FilteredMusic();
            if (_musicSel != idx || idx < 0 || idx >= items.Count || TrackId(items[idx]) != key)
            {
                Log.LogInfo("music preview mismatch selected=" + key + " scheduledGen=" + gen + " sel=" + _musicSel + " idxKey=" + (idx >= 0 && idx < items.Count ? TrackId(items[idx]) : "?"));
                yield break;
            }
            Log.LogInfo("music preview fire selected=" + key + " scheduledGen=" + gen + " actualPreview=" + key);
            StartMusic(key, items[idx][1], false);
        }

        private void StartMusic(string key, string title, bool full)
        {
            _resumeSuspendedAfterStop = false;
            StopMusicImmediate(full);
            _musicFade = 0f;
            _musicStopping = false;
            _pendingKey = key;
            _previewError = null;
            _musicFallbackLen = MusicDuration(title);
            _musicStartAt = Time.unscaledTime;
            _fadeInDur = full ? 0.8f : (_previewSeconds <= 10 ? 1.3f : (_previewSeconds <= 20 ? 1.7f : 2.1f));
            _fadeOutDur = full ? 0.8f : (_previewSeconds <= 10 ? 1.15f : (_previewSeconds <= 20 ? 1.5f : 1.85f));
            _previewGen++;
            _musicToken++;
            Log.LogInfo("music start selected=" + key + " full=" + full + " scheduledGen=" + _previewGen + " token=" + _musicToken);
            StartCoroutine(PlayMusic(key, title, full, _musicToken));
            UpdateMusicSelectionInPlace();
        }

        private void ToggleVolume()
        {
            _volume = _volume >= 0.99f ? 0f : Mathf.Min(1f, _volume + 0.25f);
            if (_musicInst.isValid()) _musicInst.setVolume(_volume * _musicFade);
            if (_volFill != null) _volFill.sizeDelta = new Vector2(160f * _volume, 4f);
            if (_volText != null) _volText.text = "音量 " + Mathf.RoundToInt(_volume * 100) + "%";
            if (_playerVolFill != null) _playerVolFill.sizeDelta = new Vector2(86f * _volume, 8f);
            if (_playerVolText != null) _playerVolText.text = Mathf.RoundToInt(_volume * 100) + "%";
        }

        // ------------------------------------------------------------------ 剧情 · 尾声

        private List<string[]> FilteredStory()
        {
            var list = new List<string[]>();
            foreach (var row in GalleryData.Story)
            {
                if (_filter != "all")
                {
                    int k = row[1].IndexOf('·');
                    string act = k > 0 ? row[1].Substring(0, k).Trim() : row[1];
                    if (act != _filter) continue;
                }
                list.Add(row);
            }
            return list;
        }

        private void RenderStory()
        {
            BuildFilterChips();
            var items = FilteredStory();
            UpdatePager();
            int per = 6, start = _page * per;
            int count = Mathf.Min(per, items.Count - start);
            for (int i = 0; i < count; i++)
            {
                int idx = start + i;
                var row = items[idx];
                int col = i % 3, line = i / 3;
                float left = 90f + col * 600f;
                float top = 42f + line * 358f;
                var rt = PaperCard(left, top, 540f, 344f, true);
                var art = NewRaw("Art", rt, Color.white);
                Top(art.rectTransform, 0, 0, 540f, 294f);
                StartCoroutine(FillStoryThumb(row[2], art, 540f, 294f));
                var title = Txt("Cap", rt, row[1], 20, C_INK, _serif, TextAlignmentOptions.Left);
                Top(title.rectTransform, 18, 304, 380, 32);
                var tag = Txt("Tag", rt, "剧情 · 尾声", 11, new Color(C_GOLD_DEEP.r, C_GOLD_DEEP.g, C_GOLD_DEEP.b, 1f), _sans, TextAlignmentOptions.Right);
                Top(tag.rectTransform, 390, 303, 132, 18);
                var dim = Txt("DimTxt", rt, row[3] + " × " + row[4], 11, new Color(132f / 255f, 110f / 255f, 92f / 255f, 1f), _sans, TextAlignmentOptions.Right);
                Top(dim.rectTransform, 360, 320, 162, 18);
                int index = idx;
                _click.Add(new Click { Rt = rt, Act = () => OpenStory(index) });
            }
        }

        private void OpenStory(int idx)
        {
            var items = FilteredStory();
            if (idx < 0 || idx >= items.Count) return;
            var row = items[idx];
            _viewerIndex = idx;
            _viewerCat.text = "剧情 · 尾声";
            _viewerTitle.text = row[1];
            _viewerSize.text = row[3] + " × " + row[4];
            _viewerIndexLbl.text = (idx + 1) + " / " + items.Count;
            ClearStage();
            BeginViewer();
            StartCoroutine(LoadStoryView(row[2]));
        }

        private IEnumerator LoadStoryView(string group)
        {
            Texture2D tex = null;
            yield return BuildStoryTexture(group, 1920, 1080, t => tex = t);
            if (tex == null || !_viewer.gameObject.activeSelf) yield break;
            var raw = NewRaw("Big", _viewerStage, Color.white);
            _bigRaw = raw;
            _zoom = 1f; _pan = Vector2.zero;
            raw.texture = tex;
            var rt = raw.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            FitStage();
            ApplyTransform();
        }

        private IEnumerator FillStoryThumb(string group, RawImage target, float maxW, float maxH)
        {
            Texture2D tex = null;
            yield return BuildStoryTexture(group, 1080, 588, t => tex = t);
            if (tex != null) AssignCover(target, tex, maxW, maxH);
        }

        private static string StoryPreRenderedFile(string group)
        {
            if (group == "PZImgFcgLioraDeath1") return "liora_death.png";
            if (group == "TitleBackgroundSmalltheater") return "smalltheater.png";
            if (group == "PZImgFcgEndingUniverse") return "ending_universe_game.png";
            if (group == "PZImgFcgEndingUniverseFeet") return "ending_universe_full.png";
            return null;
        }

        private Texture2D LoadStoryPreRendered(string group, int w, int h)
        {
            string file = StoryPreRenderedFile(group);
            if (string.IsNullOrEmpty(file)) return null;
            string key = "storypre:" + group + ":" + w + "x" + h;
            if (_thumbs.TryGetValue(key, out var cached)) return cached;
            string path = Path.Combine(AssetRoot, Path.Combine("thumbs", Path.Combine("story", file)));
            if (!File.Exists(path)) return null;
            Texture2D src = null;
            try
            {
                src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!src.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.Destroy(src); return null; }
                var tex = MakeCoverThumb(src, w, h);
                UnityEngine.Object.Destroy(src);
                _thumbs[key] = tex;
                return tex;
            }
            catch (Exception e)
            {
                Log.LogWarning("预渲染剧情图加载失败 " + file + " : " + e.Message);
                if (src != null) UnityEngine.Object.Destroy(src);
                return null;
            }
        }

        private IEnumerator BuildStoryTexture(string group, int w, int h, System.Action<Texture2D> done)
        {
            string cacheKey = "story:" + group + ":" + w + "x" + h;
            if (_thumbs.TryGetValue(cacheKey, out var cached)) { done(cached); yield break; }
            var pre = LoadStoryPreRendered(group, w, h);
            if (pre != null) { _thumbs[cacheKey] = pre; done(pre); yield break; }
            while (_storyBusy.Contains(cacheKey)) yield return null;
            if (_thumbs.TryGetValue(cacheKey, out var cached2)) { done(cached2); yield break; }
            _storyBusy.Add(cacheKey);
            Color32[] dst = null;
            foreach (var row in GalleryData.StoryLayers)
            {
                if (row[0] != group) continue;
                string assetName = row[1];
                string bundleFile = row[2];
                var req = AssetBundle.LoadFromFileAsync(Path.Combine(BundleRoot, bundleFile));
                yield return req;
                var ab = req.assetBundle;
                if (ab == null) continue;
                var areq = ab.LoadAssetAsync<Texture2D>(assetName);
                yield return areq;
                var src = areq.asset as Texture2D;
                if (src == null)
                {
                    foreach (var full in ab.GetAllAssetNames())
                    {
                        if (!full.EndsWith("/" + assetName + ".png") && Path.GetFileNameWithoutExtension(full) != assetName) continue;
                        var r2 = ab.LoadAssetAsync<Texture2D>(full);
                        yield return r2;
                        src = r2.asset as Texture2D;
                        if (src != null) break;
                    }
                }
                if (src != null)
                {
                    if (dst == null) dst = new Color32[w * h];
                    CompositeStoryLayer(src, w, h, dst, assetName);
                }
                try { ab.Unload(true); } catch { }
                yield return null;
            }
            if (dst == null) { _storyBusy.Remove(cacheKey); yield break; }
            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.SetPixels32(dst);
            outTex.Apply();
            _thumbs[cacheKey] = outTex;
            _storyBusy.Remove(cacheKey);
            done(outTex);
        }

        private static bool StoryPlacement(string asset, out float scale, out float left, out float top)
        {
            scale = 1f; left = 0f; top = 0f;
            if (asset == "PZImgFcgAliceAleph")
            {
                scale = 1.159792f; left = -27.784f; top = 320.376f; return true;
            }
            if (asset == "PZImgFcgAliceAliceExpression3")
            {
                scale = 1.341866f; left = 1669.597f; top = 20.852f; return true;
            }
            return false;
        }

        private static void CompositeStoryLayer(Texture2D src, int w, int h, Color32[] dst, string assetName)
        {
            float scale, left, top;
            bool placed = StoryPlacement(assetName, out scale, out left, out top);
            float k = (float)w / 3840f;
            int lw = placed ? Mathf.Max(1, Mathf.RoundToInt(src.width * scale * k)) : w;
            int lh = placed ? Mathf.Max(1, Mathf.RoundToInt(src.height * scale * k)) : h;
            int ox = placed ? Mathf.RoundToInt(left * k) : 0;
            int oy = placed ? h - Mathf.RoundToInt(top * k) - lh : 0;

            var rt = RenderTexture.GetTemporary(lw, lh, 0, RenderTextureFormat.ARGB32);
            var old = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var tmp = new Texture2D(lw, lh, TextureFormat.RGBA32, false);
            tmp.ReadPixels(new Rect(0, 0, lw, lh), 0, 0);
            tmp.Apply();
            RenderTexture.active = old;
            rt.Release();
            RenderTexture.ReleaseTemporary(rt);

            var px = tmp.GetPixels32();
            for (int y = 0; y < lh; y++)
            {
                int dy = oy + y;
                if (dy < 0 || dy >= h) continue;
                int rowDst = dy * w;
                int rowSrc = y * lw;
                for (int x = 0; x < lw; x++)
                {
                    int dx = ox + x;
                    if (dx < 0 || dx >= w) continue;
                    Color32 p = px[rowSrc + x];
                    int a = p.a;
                    if (a == 0) continue;
                    int di = rowDst + dx;
                    if (a == 255)
                    {
                        dst[di] = p;
                    }
                    else
                    {
                        int ia = 255 - a;
                        dst[di].r = (byte)((p.r * a + dst[di].r * ia) / 255);
                        dst[di].g = (byte)((p.g * a + dst[di].g * ia) / 255);
                        dst[di].b = (byte)((p.b * a + dst[di].b * ia) / 255);
                        dst[di].a = 255;
                    }
                }
            }
            UnityEngine.Object.Destroy(tmp);
        }

        // ------------------------------------------------------------------ 查看器

        private List<string[]> CurrentStandingItems()
        {
            return _tab == 4 ? FilteredDiff() : FilteredStanding();
        }

        private void OpenStanding(int idx)
        {
            var items = CurrentStandingItems();
            if (idx < 0 || idx >= items.Count) return;
            var row = items[idx];
            _viewerIndex = idx;
            _viewerCat.text = _tab == 4 ? "差分" : "立绘";
            _viewerTitle.text = row[2];
            _viewerSize.text = _tab == 4 ? "" : (row.Length > 5 ? (row[4] + " × " + row[5]) : "");
            _viewerIndexLbl.text = (idx + 1) + " / " + items.Count;
            ClearStage();
            BeginViewer();
            StartCoroutine(LoadViewTexture(row[0], row[1]));
        }

        private IEnumerator LoadViewTexture(string assetName, string bundleFile)
        {
            string path = Path.Combine(BundleRoot, bundleFile);
            var req = AssetBundle.LoadFromFileAsync(path);
            yield return req;
            AssetBundle ab = req.assetBundle;
            if (ab == null) { Log.LogWarning("bundle 打开失败 " + bundleFile); yield break; }
            var areq = ab.LoadAssetAsync<Texture2D>(assetName);
            yield return areq;
            Texture2D tex = areq.asset as Texture2D;
            if (tex == null)
            {
                foreach (var full in ab.GetAllAssetNames())
                {
                    if (Path.GetFileNameWithoutExtension(full) != assetName) continue;
                    var r2 = ab.LoadAssetAsync<Texture2D>(full);
                    yield return r2;
                    tex = r2.asset as Texture2D;
                    if (tex != null) break;
                }
            }
            if (tex == null)
            {
                Log.LogWarning("未找到贴图资源：" + assetName);
                try { ab.Unload(true); } catch { }
                yield break;
            }
            if (!_viewer.gameObject.activeSelf) { try { ab.Unload(true); } catch { } yield break; }
            _viewBundle = ab;
            _viewBundlePath = path;
            _viewTex = tex;

            var raw = NewRaw("Big", _viewerStage, Color.white);
            _bigRaw = raw;
            _zoom = 1f; _pan = Vector2.zero;
            raw.texture = tex;
            var rt = raw.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            FitStage();
            ApplyTransform();
        }

        private void StepViewer(int dir)
        {
            if (_viewerIndex < 0) return;
            int nextIndex = _viewerIndex + dir;
            if (_tab == 0)
            {
                var list = FilteredCurtain();
                if (list.Count == 0 || nextIndex < 0 || nextIndex >= list.Count) return;
                _viewerIndex = nextIndex;
                OpenCG(list[_viewerIndex][0]);
                return;
            }
            if (_tab == 1)
            {
                int no = FilteredOriginal().Count;
                if (no == 0 || nextIndex < 0 || nextIndex >= no) return;
                _viewerIndex = nextIndex;
                OpenOriginal(_viewerIndex);
                return;
            }
            if (_tab == 2)
            {
                int no = FilteredStory().Count;
                if (no == 0 || nextIndex < 0 || nextIndex >= no) return;
                _viewerIndex = nextIndex;
                OpenStory(_viewerIndex);
                return;
            }
            int n = CurrentStandingItems().Count;
            if (n == 0 || nextIndex < 0 || nextIndex >= n) return;
            _viewerIndex = nextIndex;
            OpenStanding(_viewerIndex);
        }

        private IEnumerator RevealCgChromeAfter(float delay, int token)
        {
            yield return new WaitForSeconds(delay);
            if (token != _viewerToken || _viewer == null || !_viewer.gameObject.activeSelf || _tab != 0) yield break;
            RestoreCgChrome();
        }

        private void RestoreCgChrome()
        {
            if (_viewerChrome == null || _viewerStage == null) return;
            _viewerStage.SetSiblingIndex(_viewerChrome.GetSiblingIndex());
            if (!_immersive) _viewerChrome.gameObject.SetActive(true);
        }

        private static string CurtainStaticFile(string n)
        {
            switch (n)
            {
                case "1":
                case "12": return "Act0_无名氏的故事.png";
                case "2": return "Act1_绿野仙踪.png";
                case "13": return "Act1_绿野仙踪_二周目.png";
                case "3": return "Act2_奥尔菲斯与尤丽狄茜.png";
                case "14": return "Act2_奥尔菲斯与尤丽狄茜_二周目.png";
                case "4": return "Act3_灰姑娘.png";
                case "15": return "Act3_灰姑娘_二周目.png";
                case "5": return "Act4_砍竹子的故事.png";
                case "16": return "Act4_砍竹子的故事_二周目.png";
                case "6": return "Act5_亚瑟王传说.png";
                case "17": return "Act5_亚瑟王传说_二周目.png";
                case "7": return "Act6_启示录.png";
                case "18": return "Act6_启示录_二周目.png";
                case "8": return "Act7_爱丽丝梦游仙境.png";
                case "19": return "Act7_爱丽丝梦游仙境_二周目.png";
                case "9": return "Act8_卖火柴的小女孩.png";
                case "10": return "Act9_睡美人.png";
                default: return null;
            }
        }

        private void OpenCG(string n)
        {
            ClearStage();
            BeginViewer();
            _viewerCat.text = "谢幕 CG";
            _viewerTitle.text = "谢幕 CG";
            _viewerSize.text = "3840 × 2160";
            _bigRaw = null;
            var clist = FilteredCurtain();
            int cidx = -1;
            for (int ci = 0; ci < clist.Count; ci++) if (clist[ci][0] == n) { cidx = ci; break; }
            _viewerIndex = cidx;
            _viewerIndexLbl.text = cidx >= 0 ? ((cidx + 1) + " / " + clist.Count) : "CG";
            foreach (var row in GalleryData.Curtain)
                if (row[0] == n) { _viewerTitle.text = row[1]; break; }

            string key = "cg4k:" + n;
            Texture2D tex = null;
            if (!_thumbs.TryGetValue(key, out tex) || tex == null)
            {
                string file = CurtainStaticFile(n);
                if (!string.IsNullOrEmpty(file)) tex = LoadAssetTexture("cg_4k/" + file);
                if (tex == null) tex = LoadAssetTexture("thumbs/cg/cg_" + n + ".png");
                if (tex != null) _thumbs[key] = tex;
            }
            if (tex == null) return;
            var raw = NewRaw("Big", _viewerStage, Color.white);
            _bigRaw = raw;
            _zoom = 1f;
            _pan = Vector2.zero;
            raw.texture = tex;
            var rt = raw.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            FitStage();
            ApplyTransform();
        }

        private IEnumerator FreezeCurtainAfter(float delay, int token, GameObject go)
        {
            yield return new WaitForSeconds(delay);
            if (token != _viewerToken || go == null) yield break;
            try
            {
                foreach (var d in go.GetComponentsInChildren<UnityEngine.Playables.PlayableDirector>(true)) d.Pause();
                foreach (var b in go.GetComponentsInChildren<Behaviour>(true))
                    if (b.GetType().Name == "Animator") b.enabled = false;
            }
            catch { }
        }

        // ------------------------------------------------------------------ 音乐

        private void ToggleMusic(string key, string title)
        {
            StartMusic(key, title, true);
        }

        private IEnumerator PlayMusic(string key, string title, bool full, int token)
        {
            string fmodKey = key != null && key.StartsWith("title:", StringComparison.Ordinal) ? "—" : key;
            var sm = SoundManager.Instance;
            if (sm == null)
            {
                FailMusic(key, token, "SoundManager 为空");
                yield break;
            }
            if (fmodKey != "—")
            {
                IEnumerator load = null;
                try { load = sm.LoadBankAsync(fmodKey, true); } catch { }
                if (load != null) yield return StartCoroutine(load);
                if (token != _musicToken || _pendingKey != key)
                {
                    Log.LogInfo("music load stale selected=" + key + " token=" + token + " currentToken=" + _musicToken);
                    yield break;
                }

                string[] prefixes;
                try
                {
                    if (full)
                        prefixes = new[] { sm.GetBgmPrefix(), sm.GetBgmLoopPrefix(), sm.GetFreePlayPrefix() };
                    else
                    {
                        var pm = AccessTools.Method(sm.GetType(), "GetPreveiwPrefix");
                        string pp = pm != null ? (pm.Invoke(sm, null) as string) : null;
                        prefixes = string.IsNullOrEmpty(pp)
                            ? new[] { sm.GetBgmPrefix(), sm.GetBgmLoopPrefix(), sm.GetFreePlayPrefix() }
                            : new[] { sm.GetBgmPrefix(), sm.GetBgmLoopPrefix(), sm.GetFreePlayPrefix(), pp + "preview_" };
                    }
                }
                catch { prefixes = new[] { "event:/MUSIC/" }; }

                foreach (var pre in prefixes)
                {
                    if (string.IsNullOrEmpty(pre)) continue;
                    string evt = pre + fmodKey;
                    try
                    {
                        var inst = FMODUnity.RuntimeManager.CreateInstance(evt);
                        if (!inst.isValid())
                        {
                            Log.LogWarning("music fmod invalid event=" + evt + " selected=" + key + " fmodResult=invalid");
                            continue;
                        }
                        if (token != _musicToken || _pendingKey != key)
                        {
                            inst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                            inst.release();
                            Log.LogInfo("music fmod stale selected=" + key + " token=" + token + " currentToken=" + _musicToken);
                            yield break;
                        }
                        StopAllFmodMusicInsts();
                        FlacAudio.Stop();
                        inst.setVolume(0f);
                        FMOD.RESULT res = inst.start();
                        FMOD.Studio.PLAYBACK_STATE state = FMOD.Studio.PLAYBACK_STATE.STOPPED;
                        try { inst.getPlaybackState(out state); } catch { }
                        Log.LogInfo("music fmod event=" + evt + " selected=" + key + " full=" + full + " actualPreview=" + (full ? "none" : key) + " fmodResult=" + res + " resultInt=" + (int)res + " playbackState=" + state);
                        if (res == FMOD.RESULT.OK && state != FMOD.Studio.PLAYBACK_STATE.STOPPED)
                        {
                            StopAllFmodMusicInsts();
                            FlacAudio.Stop();
                            TrackMusicInst(inst);
                            _musicInst = inst;
                            _musicStopping = false;
                            _musicStartAt = Time.unscaledTime;
                            _playingKey = key;
                            _playingTitle = title;
                            _playingFull = full;
                            if (full) _fullPaused = false;
                            _previewKey = full ? null : key;
                            _previewEndAt = Time.unscaledTime + _previewSeconds;
                            _pendingKey = null;
                            _previewError = null;
                            if (_visible && _tab == 5) UpdateMusicSelectionInPlace();
                            RefreshNowPanel();
                            yield break;
                        }
                        Log.LogWarning("music fmod start rejected event=" + evt + " selected=" + key + " fmodResult=" + res + " playbackState=" + state);
                        try { inst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); } catch { }
                        try { inst.release(); } catch { }
                    }
                    catch (Exception e) { Log.LogWarning("music fmod exception event=" + evt + " : " + e.Message); }
                }

                // 先扫已加载 bank 的事件索引，找真实路径（排除 SFX 事件误匹配）。
                string bankIndexPath = null;
                bool bankIndexReady = false, bankIndexStale = false;
                FMOD.Studio.EventInstance bankInst = default(FMOD.Studio.EventInstance);
                try
                {
                    var studio = FMODUnity.RuntimeManager.StudioSystem;
                    if (studio.isValid())
                    {
                        FMOD.Studio.Bank[] banks;
                        if (studio.getBankList(out banks) == FMOD.RESULT.OK && banks != null)
                        {
                            int eventCount = 0;
                            string fuzzyPath = null;
                            string core = key.StartsWith("PZMus", StringComparison.OrdinalIgnoreCase) ? key.Substring(5) : key;
                            for (int bi = 0; bi < banks.Length && bankIndexPath == null; bi++)
                            {
                                var bank = banks[bi];
                                if (!bank.isValid()) continue;
                                FMOD.Studio.EventDescription[] events;
                                if (bank.getEventList(out events) != FMOD.RESULT.OK || events == null) continue;
                                for (int ei = 0; ei < events.Length; ei++)
                                {
                                    eventCount++;
                                    string p;
                                    if (events[ei].getPath(out p) != FMOD.RESULT.OK || string.IsNullOrEmpty(p)) continue;
                                    if (p.EndsWith("/" + fmodKey, StringComparison.OrdinalIgnoreCase) || p.EndsWith(fmodKey, StringComparison.OrdinalIgnoreCase))
                                    {
                                        bankIndexPath = p;
                                        break;
                                    }
                                    bool isMusicEvt = p.IndexOf("/MUSIC", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("/BGM", StringComparison.OrdinalIgnoreCase) >= 0;
                                    if (fuzzyPath == null && isMusicEvt && core.Length >= 4 && p.IndexOf(core, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        fuzzyPath = p;
                                    }
                                }
                            }
                            if (bankIndexPath == null && fuzzyPath != null)
                            {
                                bankIndexPath = fuzzyPath;
                                Log.LogInfo("music bankIndex fuzzy=" + fuzzyPath + " selected=" + key);
                            }
                            if (bankIndexPath == null)
                                Log.LogInfo("music bankIndex miss selected=" + key + " banks=" + banks.Length + " events=" + eventCount);
                        }
                        else Log.LogInfo("music bankIndex miss selected=" + key + " bankList=unavailable");
                    }
                    else Log.LogInfo("music bankIndex miss selected=" + key + " studio=invalid");
                }
                catch (Exception e) { Log.LogWarning("music bankIndex exception selected=" + key + " : " + e.Message); }

                if (bankIndexPath != null)
                {
                    try
                    {
                        bankInst = FMODUnity.RuntimeManager.CreateInstance(bankIndexPath);
                        if (!bankInst.isValid())
                        {
                            Log.LogWarning("music bankIndex invalid path=" + bankIndexPath + " selected=" + key + " fmodResult=invalid");
                        }
                        else if (token != _musicToken || _pendingKey != key)
                        {
                            bankIndexStale = true;
                            try { bankInst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); } catch { }
                            try { bankInst.release(); } catch { }
                            bankInst = default(FMOD.Studio.EventInstance);
                            Log.LogInfo("music bankIndex stale selected=" + key + " token=" + token + " currentToken=" + _musicToken);
                        }
                        else
                        {
                            StopAllFmodMusicInsts();
                            FlacAudio.Stop();
                            bankInst.setVolume(0f);
                            FMOD.RESULT bankRes = bankInst.start();
                            FMOD.Studio.PLAYBACK_STATE bankState = FMOD.Studio.PLAYBACK_STATE.STOPPED;
                            try { bankInst.getPlaybackState(out bankState); } catch { }
                            Log.LogInfo("music bankIndex path=" + bankIndexPath + " selected=" + key + " full=" + full + " actualPreview=" + (full ? "none" : key) + " fmodResult=" + bankRes + " playbackState=" + bankState);
                            if (bankRes == FMOD.RESULT.OK && bankState != FMOD.Studio.PLAYBACK_STATE.STOPPED)
                            {
                                bankIndexReady = true;
                            }
                            else
                            {
                                Log.LogWarning("music bankIndex rejected path=" + bankIndexPath + " selected=" + key + " fmodResult=" + bankRes + " playbackState=" + bankState);
                                try { bankInst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); } catch { }
                                try { bankInst.release(); } catch { }
                                bankInst = default(FMOD.Studio.EventInstance);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.LogWarning("music bankIndex exception path=" + bankIndexPath + " selected=" + key + " : " + e.Message);
                        try { if (bankInst.isValid()) bankInst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); } catch { }
                        try { if (bankInst.isValid()) bankInst.release(); } catch { }
                        bankInst = default(FMOD.Studio.EventInstance);
                    }
                }

                if (bankIndexStale) yield break;
                if (bankIndexReady && bankInst.isValid())
                {
                    StopAllFmodMusicInsts();
                    FlacAudio.Stop();
                    TrackMusicInst(bankInst);
                    _musicInst = bankInst;
                    _musicStopping = false;
                    _musicStartAt = Time.unscaledTime;
                    _playingKey = key;
                    _playingTitle = title;
                    _playingFull = full;
                    if (full) _fullPaused = false;
                    _previewKey = full ? null : key;
                    _previewEndAt = Time.unscaledTime + _previewSeconds;
                    _pendingKey = null;
                    _previewError = null;
                    if (_visible && _tab == 5) UpdateMusicSelectionInPlace();
                    RefreshNowPanel();
                    yield break;
                }

                // 少数曲目不在常规前缀下：仅允许音乐路径精确匹配。
                FMOD.Studio.EventInstance fbInst = default(FMOD.Studio.EventInstance);
                string fbPath = null;
                try
                {
                    var bgm = sm.bgm;
                    if (bgm.isValid())
                    {
                        FMOD.Studio.EventDescription desc;
                        if (bgm.getDescription(out desc) == FMOD.RESULT.OK)
                        {
                            string path;
                            FMOD.RESULT pathRes = desc.getPath(out path);
                            if (pathRes == FMOD.RESULT.OK && !string.IsNullOrEmpty(path) && path.EndsWith(fmodKey, StringComparison.OrdinalIgnoreCase))
                            {
                                fbPath = path;
                                fbInst = FMODUnity.RuntimeManager.CreateInstance(path);
                            }
                            else
                            {
                                Log.LogInfo("music fallback skip bgmPath=" + (path ?? "?") + " selected=" + key + " pathResult=" + pathRes);
                            }
                        }
                        else Log.LogInfo("music fallback skip selected=" + key + " bgmDesc=failed");
                    }
                    else Log.LogInfo("music fallback skip selected=" + key + " bgm=invalid");
                }
                catch (Exception e) { Log.LogWarning("music fallback desc exception selected=" + key + " : " + e.Message); }

                if (fbInst.isValid())
                {
                    if (token != _musicToken || _pendingKey != key)
                    {
                        try { fbInst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); } catch { }
                        try { fbInst.release(); } catch { }
                        Log.LogInfo("music fallback stale selected=" + key + " token=" + token + " currentToken=" + _musicToken);
                        yield break;
                    }
                    StopAllFmodMusicInsts();
                    FlacAudio.Stop();
                    fbInst.setVolume(0f);
                    FMOD.RESULT fbRes = fbInst.start();
                    FMOD.Studio.PLAYBACK_STATE fbState = FMOD.Studio.PLAYBACK_STATE.STOPPED;
                    try { fbInst.getPlaybackState(out fbState); } catch { }
                    Log.LogInfo("music fallback bgmPath=" + fbPath + " selected=" + key + " full=" + full + " actualPreview=" + (full ? "none" : key) + " fmodResult=" + fbRes + " playbackState=" + fbState);
                    if (fbRes == FMOD.RESULT.OK && fbState != FMOD.Studio.PLAYBACK_STATE.STOPPED)
                    {
                        StopAllFmodMusicInsts();
                        FlacAudio.Stop();
                        TrackMusicInst(fbInst);
                        _musicInst = fbInst;
                        _musicStopping = false;
                        _musicStartAt = Time.unscaledTime;
                        _playingKey = key;
                        _playingTitle = title;
                        _playingFull = full;
                        if (full) _fullPaused = false;
                        _previewKey = full ? null : key;
                        _previewEndAt = Time.unscaledTime + _previewSeconds;
                        _pendingKey = null;
                        _previewError = null;
                        if (_visible && _tab == 5) UpdateMusicSelectionInPlace();
                        RefreshNowPanel();
                        yield break;
                    }
                    Log.LogWarning("music fallback rejected bgmPath=" + fbPath + " selected=" + key + " fmodResult=" + fbRes + " playbackState=" + fbState);
                    try { fbInst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); } catch { }
                    try { fbInst.release(); } catch { }
                }
                else if (!string.IsNullOrEmpty(fbPath))
                {
                    Log.LogWarning("music fallback bgmPath=" + fbPath + " selected=" + key + " fmodResult=invalid");
                }
            }

            // 本地无损 FLAC 回退（用于 Liora's Song / Prelude 及额外原声碟曲目）。
            bool flacOk = false;
            yield return StartCoroutine(FlacAudio.LoadAndPlay(AssetRoot, _root, title, full, ok => flacOk = ok));
            if (token != _musicToken || _pendingKey != key)
            {
                if (flacOk) FlacAudio.Stop();
                Log.LogInfo("music flac stale selected=" + key + " token=" + token + " currentToken=" + _musicToken);
                yield break;
            }
            if (flacOk)
            {
                StopAllFmodMusicInsts();
                FlacAudio.SetVolume(_volume * _musicFade);
                _musicStopping = false;
                _musicStartAt = Time.unscaledTime;
                _playingKey = key;
                _playingTitle = title;
                _playingFull = full;
                if (full) _fullPaused = false;
                _previewKey = full ? null : key;
                _previewEndAt = Time.unscaledTime + _previewSeconds;
                _pendingKey = null;
                _previewError = null;
                Log.LogInfo("music flac ok selected=" + key + " title=" + title + " full=" + full);
                if (_visible && _tab == 5) UpdateMusicSelectionInPlace();
                RefreshNowPanel();
                yield break;
            }

            FailMusic(key, token, "找不到可播放的音源");
        }

        private void FailMusic(string key, int token, string reason)
        {
            if (token != _musicToken || _pendingKey != key) return;
            _pendingKey = null;
            StopMusicInstance();
            _playingKey = null;
            _playingTitle = null;
            _playingFull = false;
            _fullPaused = false;
            _previewKey = null;
            _previewError = "无法播放：" + key + "（" + reason + "）";
            Log.LogWarning("music fail selected=" + key + " actualPreview=none reason=" + reason + " fmodResult=failed");
            if (_suspendedHasFull) ResumeSuspendedFull();
            else { RefreshNowPanel(); if (_visible && _tab == 5) RefreshTab(); }
        }

        private void TrackMusicInst(FMOD.Studio.EventInstance inst)
        {
            if (inst.isValid()) _allMusicInsts.Add(inst);
        }

        private void StopAllFmodMusicInsts()
        {
            IntPtr suspHandle = _suspendedHasFull && _suspendedFullInst.isValid() ? _suspendedFullInst.handle : IntPtr.Zero;
            if (_musicInst.isValid() && _musicInst.handle != suspHandle)
            {
                try { _musicInst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); } catch { }
                try { _musicInst.release(); } catch { }
            }
            for (int i = _allMusicInsts.Count - 1; i >= 0; i--)
            {
                var inst = _allMusicInsts[i];
                if (!inst.isValid()) { _allMusicInsts.RemoveAt(i); continue; }
                if (suspHandle != IntPtr.Zero && inst.handle == suspHandle) continue;
                try { inst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); } catch { }
                try { inst.release(); } catch { }
                _allMusicInsts.RemoveAt(i);
            }
            _musicInst = default(FMOD.Studio.EventInstance);
        }

        private void StopMusicInstance()
        {
            bool valid = _musicInst.isValid() || _allMusicInsts.Count > 0;
            StopAllFmodMusicInsts();
            FlacAudio.Stop();
            if (valid) Log.LogInfo("music instance stop-release");
        }

        private void StopMusicImmediate(bool dropSuspended = true)
        {
            _musicToken++;
            _pendingKey = null;
            StopMusicInstance();
            if (dropSuspended) DropSuspendedFull();
            _musicStopping = false;
            _musicFade = 0f;
            _playingKey = null;
            _playingTitle = null;
            _playingFull = false;
            _fullPaused = false;
            _previewKey = null;
            if (_nowProgFill != null) _nowProgFill.sizeDelta = new Vector2(0f, 3f);
            RefreshNowPanel();
        }

        private void SuspendFullForPreview()
        {
            if (!_playingFull) return;
            if (!_musicInst.isValid())
            {
                FlacAudio.Stop();
                _playingKey = null;
                _playingTitle = null;
                _playingFull = false;
                _fullPaused = false;
                _previewKey = null;
                return;
            }
            int pos = 0;
            try { _musicInst.getTimelinePosition(out pos); } catch { }
            _suspendedFullInst = _musicInst;
            _suspendedFullKey = _playingKey;
            _suspendedFullTitle = _playingTitle;
            _suspendedFullPos = pos;
            _suspendedHasFull = true;
            _suspendedFullPaused = _fullPaused;
            _fullPaused = false;
            Log.LogInfo("music suspend full " + _suspendedFullKey + " pos=" + pos + " paused=" + _suspendedFullPaused);
            try { _suspendedFullInst.setPaused(true); _suspendedFullInst.setVolume(0f); } catch { }
            _musicInst = default(FMOD.Studio.EventInstance);
            _playingKey = null;
            _playingTitle = null;
            _playingFull = false;
            _previewKey = null;
            RefreshNowPanel();
        }

        private void DropSuspendedFull()
        {
            if (_suspendedFullInst.isValid())
            {
                try { _suspendedFullInst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); _suspendedFullInst.release(); } catch { }
            }
            _suspendedFullInst = default(FMOD.Studio.EventInstance);
            _suspendedHasFull = false;
            _suspendedFullPaused = false;
            _resumeSuspendedAfterStop = false;
        }

        private void ResumeSuspendedFull()
        {
            if (!_suspendedHasFull) return;
            var inst = _suspendedFullInst;
            _suspendedHasFull = false;
            _suspendedFullInst = default(FMOD.Studio.EventInstance);
            if (!inst.isValid()) return;
            if (!string.IsNullOrEmpty(_pendingKey) || !string.IsNullOrEmpty(_previewKey) || _musicInst.isValid() || FlacAudio.IsPlayingOrPaused)
            {
                try { inst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); inst.release(); } catch { }
                _allMusicInsts.RemoveAll(x => !x.isValid() || x.handle == inst.handle);
                return;
            }
            _previewGen++;
            _musicToken++;
            _pendingKey = null;
            bool wasPaused = _suspendedFullPaused;
            try { inst.setVolume(_volume); inst.setPaused(wasPaused); } catch { }
            TrackMusicInst(inst);
            _musicInst = inst;
            _playingKey = _suspendedFullKey;
            _playingTitle = _suspendedFullTitle;
            _playingFull = true;
            _fullPaused = wasPaused;
            _previewKey = null;
            _musicFade = 1f;
            _musicStopping = false;
            _musicStartAt = Time.unscaledTime - _suspendedFullPos / 1000f;
            Log.LogInfo("music resume full " + _playingKey + " pos=" + _suspendedFullPos);
            RefreshTab();
            RefreshNowPanel();
        }

        private void StopMusic()
        {
            _musicToken++;
            _pendingKey = null;
            if (!_musicInst.isValid() && _suspendedHasFull)
            {
                ResumeSuspendedFull();
                return;
            }
            if (_musicInst.isValid() || FlacAudio.IsPlayingOrPaused)
            {
                if (!_musicStopping)
                {
                    _musicStopping = true;
                    _resumeSuspendedAfterStop = _suspendedHasFull;
                    _fadeOutDur = string.IsNullOrEmpty(_previewKey) ? 0.8f : (_previewSeconds <= 10 ? 1.15f : (_previewSeconds <= 20 ? 1.5f : 1.85f));
                    _musicStopAt = Time.unscaledTime + _fadeOutDur;
                }
            }
            else
            {
                StopMusicInstance();
                if (_suspendedHasFull) ResumeSuspendedFull();
            }
            _playingKey = null;
            _playingTitle = null;
            _playingFull = false;
            _fullPaused = false;
            _previewKey = null;
            if (_nowProgFill != null) _nowProgFill.sizeDelta = new Vector2(0f, 3f);
            RefreshNowPanel();
        }

        // ------------------------------------------------------------------ 资源

        private Texture2D LoadAssetTexture(string rel)
        {
            string path = Path.Combine(AssetRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                if (!File.Exists(path)) return null;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(File.ReadAllBytes(path))) return tex;
                UnityEngine.Object.Destroy(tex);
            }
            catch (Exception e) { Log.LogWarning("UI 素材加载失败 " + rel + " : " + e.Message); }
            return null;
        }

        private static Sprite ToSprite(Texture2D tex)
        {
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite ToSpriteCropped(Texture2D tex, float inset)
        {
            if (tex == null) return null;
            int x = Mathf.RoundToInt(tex.width * inset);
            int y = Mathf.RoundToInt(tex.height * inset);
            int w = Mathf.Max(2, tex.width - 2 * x);
            int h = Mathf.Max(2, tex.height - 2 * y);
            return Sprite.Create(tex, new Rect(x, y, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private IEnumerator LoadThumbFile(string file, RawImage target, float maxW, float maxH)
        {
            if (_thumbs.TryGetValue(file, out var cached)) { Assign(target, cached, maxW, maxH); yield break; }
            string path = Path.Combine(Path.Combine(AssetRoot, "thumbs"), file);
            if (!File.Exists(path)) yield break;
            Texture2D tex = null;
            try
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.Destroy(tex); tex = null; }
            }
            catch { }
            if (tex != null)
            {
                _thumbs[file] = tex;
                Assign(target, tex, maxW, maxH);
            }
            yield return null;
        }

        private bool _renderingThumb;
        private static ConfigEntry<bool> _autoOpen;
        private static ConfigEntry<int> _startTab, _startPage, _startViewer;
        private static ConfigEntry<bool> _captureAll;

        private IEnumerator RenderCurtainThumb(string n, RawImage target, float maxW, float maxH)
        {
            string key = "cg:" + n;
            if (_thumbs.TryGetValue(key, out var cached)) { AssignCover(target, cached, maxW, maxH); yield break; }

            // 优先用随包附带的、由游戏预制体渲染出的缩略图（离线生成，避免运行时反复截帧）
            string pre = Path.Combine(AssetRoot, Path.Combine("thumbs", Path.Combine("cg", "cg_" + n + ".png")));
            if (File.Exists(pre))
            {
                Texture2D preTex = null;
                try
                {
                    preTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!preTex.LoadImage(File.ReadAllBytes(pre))) { UnityEngine.Object.Destroy(preTex); preTex = null; }
                }
                catch { }
                if (preTex != null)
                {
                    _thumbs[key] = preTex;
                    AssignCover(target, preTex, maxW, maxH);
                    yield break;
                }
            }

            while (_renderingThumb) yield return null;
            _renderingThumb = true;

            var req = Addressables.LoadAssetAsync<GameObject>("curtaincall." + n);
            yield return req;
            if (req.Status != AsyncOperationStatus.Succeeded || req.Result == null)
            {
                if (req.IsValid()) Addressables.Release(req);
                _renderingThumb = false;
                yield break;
            }

            // 与查看器完全同一条渲染路径：预制体挂在画廊 Canvas 下，隐藏其它层，等动画到末帧后截游戏画面。
            bool rootWasActive = _root.activeSelf;
            if (!rootWasActive) _root.SetActive(true);
            CloseViewer();

            var hidden = new List<KeyValuePair<GameObject, bool>>();
            foreach (Transform ch in _root.transform)
            {
                hidden.Add(new KeyValuePair<GameObject, bool>(ch.gameObject, ch.gameObject.activeSelf));
                ch.gameObject.SetActive(false);
            }

            var capRoot = new GameObject("NientumCapRoot", typeof(RectTransform));
            capRoot.transform.SetParent(_root.transform, false);
            capRoot.transform.SetAsLastSibling();
            var capRt = (RectTransform)capRoot.transform;
            Fill(capRt);

            var black = NewRaw("CapBg", capRoot.transform, Color.white);
            black.texture = SolidTexture(new Color32(0, 0, 0, 255));
            Fill(black.rectTransform);

            var rootsBefore = new HashSet<int>();
            foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects()) rootsBefore.Add(r.GetInstanceID());

            var go = UnityEngine.Object.Instantiate(req.Result, capRoot.transform);
            var irt = go.transform as RectTransform;
            if (irt != null)
            {
                irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = Vector2.zero;
                irt.sizeDelta = new Vector2(1920, 1080);
                irt.localScale = Vector3.one;
            }
            // 预制体自带的 Canvas 必须排在画廊 Canvas 之上，否则会被背景/暗角盖住
            foreach (var c in go.GetComponentsInChildren<Canvas>(true))
            {
                c.overrideSorting = true;
                c.sortingOrder = 32001;
            }

            var dir = go.GetComponentInChildren<PlayableDirector>();
            if (dir != null)
            {
                dir.time = 0.0;
                dir.Play();
            }
            // 让过场/红幕动画走完，停在完整 CG 的一帧（直接跳到末帧会变成空白）
            yield return new WaitForSeconds(3.4f);

            // 预制体可能比屏幕大：先按实际屏幕占位缩放，再按它的世界矩形裁剪
            if (irt != null)
            {
                Canvas.ForceUpdateCanvases();
                var cs = new Vector3[4];
                irt.GetWorldCorners(cs);
                float bw = Mathf.Abs(cs[2].x - cs[0].x), bh = Mathf.Abs(cs[2].y - cs[0].y);
                if (bw > 1f && bh > 1f)
                {
                    float f = Mathf.Min(Screen.width * 0.98f / bw, Screen.height * 0.98f / bh);
                    if (f < 0.999f) { irt.localScale *= f; Canvas.ForceUpdateCanvases(); }
                }
                yield return null;
            }

            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return new WaitForEndOfFrame();

            Texture2D shot = null;
            try { shot = ScreenCapture.CaptureScreenshotAsTexture(); } catch (Exception e) { Log.LogWarning("截帧失败 " + n + " : " + e.Message); }

            Texture2D tex = null;
            if (shot != null)
            {
                int sw = shot.width, sh = shot.height;
                float x0 = 0f, y0 = 0f, cw = sw, chh = sh;
                if (irt != null)
                {
                    var cs = new Vector3[4];
                    irt.GetWorldCorners(cs);
                    x0 = Mathf.Clamp(Mathf.Min(cs[0].x, cs[2].x), 0f, sw);
                    y0 = Mathf.Clamp(Mathf.Min(cs[0].y, cs[2].y), 0f, sh);
                    cw = Mathf.Clamp(Mathf.Max(cs[0].x, cs[2].x), 0f, sw) - x0;
                    chh = Mathf.Clamp(Mathf.Max(cs[0].y, cs[2].y), 0f, sh) - y0;
                }
                if (cw < 2f || chh < 2f) { cw = sw; chh = sh; x0 = 0f; y0 = 0f; }
                var rt = RenderTexture.GetTemporary(960, 540, 0, RenderTextureFormat.ARGB32);
                var old = RenderTexture.active;
                Graphics.Blit(shot, rt, new Vector2(cw / sw, chh / sh), new Vector2(x0 / sw, y0 / sh));
                RenderTexture.active = rt;
                tex = new Texture2D(960, 540, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                tex.Apply();
                RenderTexture.active = old;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.Destroy(shot);
            }

            UnityEngine.Object.Destroy(go);
            UnityEngine.Object.Destroy(capRoot);
            // 时间轴可能在场景根下生成 CG/幕布对象，一并清掉，避免盖住画廊
            foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (r == null) continue;
                if (rootsBefore.Contains(r.GetInstanceID())) continue;
                if (r.name.StartsWith("Nientum")) continue;
                UnityEngine.Object.Destroy(r);
            }
            foreach (var kv in hidden)
                if (kv.Key != null) kv.Key.SetActive(kv.Value);
            if (!rootWasActive) _root.SetActive(false);
            Addressables.Release(req);
            yield return null;

            if (tex != null)
            {
                int mean = Mean(tex);
                Log.LogInfo("CG 缩略图 " + n + " 截帧完成，平均亮度 " + mean);
                try
                {
                    string shotDir = Path.Combine(Path.Combine(Application.dataPath, ".."), Path.Combine("_cnwork", Path.Combine("gallery", "shots")));
                    Directory.CreateDirectory(shotDir);
                    File.WriteAllBytes(Path.Combine(shotDir, "thumb_" + n + ".png"), tex.EncodeToPNG());
                }
                catch { }
                _thumbs[key] = tex;
                if (_visible) AssignCover(target, tex, maxW, maxH);
            }
            _renderingThumb = false;
        }

        private static int Mean(Texture2D tex)
        {
            try
            {
                var px = tex.GetPixels32();
                int cnt = 0; long s = 0;
                for (int i = 0; i < px.Length; i += 37) { s += px[i].r; cnt++; }
                return (int)(s / Math.Max(1, cnt));
            }
            catch { return 0; }
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursively(c.gameObject, layer);
        }

        private IEnumerator FillThumbFromGame(string assetName, string bundleFile, RawImage target, float maxW, float maxH, bool cover = false)
        {
            string cacheKey = assetName + (cover ? ":cover" : "");
            if (_thumbs.TryGetValue(cacheKey, out var cached))
            {
                if (cover) AssignCover(target, cached, maxW, maxH); else Assign(target, cached, maxW, maxH);
                yield break;
            }
            string path = Path.Combine(BundleRoot, bundleFile);
            var req = AssetBundle.LoadFromFileAsync(path);
            yield return req;
            var ab = req.assetBundle;
            if (ab == null) { Log.LogWarning("立绘 bundle 打开失败 " + bundleFile); yield break; }
            var areq = ab.LoadAssetAsync<Texture2D>(assetName);
            yield return areq;
            var src = areq.asset as Texture2D;
            if (src == null)
            {
                foreach (var full in ab.GetAllAssetNames())
                {
                    if (Path.GetFileNameWithoutExtension(full) != assetName) continue;
                    var r2 = ab.LoadAssetAsync<Texture2D>(full);
                    yield return r2;
                    src = r2.asset as Texture2D;
                    if (src != null) break;
                }
            }
            if (src != null)
            {
                Texture2D thumb = null;
                try { thumb = cover ? MakeCoverThumb(src, (int)(maxW * 2f), (int)(maxH * 2f)) : MakeThumb(src, (int)(maxW * 2f), (int)(maxH * 2f)); } catch { }
                if (thumb != null)
                {
                    _thumbs[cacheKey] = thumb;
                    if (cover) AssignCover(target, thumb, maxW, maxH); else Assign(target, thumb, maxW, maxH);
                }
            }
            else Log.LogWarning("立绘贴图未找到 " + assetName);
            try { ab.Unload(true); } catch { }
        }

        private static void Assign(RawImage target, Texture2D tex, float maxW, float maxH)
        {
            if (target == null) return;
            target.color = Color.white;
            target.texture = tex;
            target.uvRect = new Rect(0f, 0f, 1f, 1f);
            float s = Mathf.Min(maxW / tex.width, maxH / tex.height);
            target.rectTransform.sizeDelta = new Vector2(tex.width * s, tex.height * s);
        }

        private static void AssignCover(RawImage target, Texture2D tex, float w, float h)
        {
            if (target == null || tex == null) return;
            target.color = Color.white;
            target.texture = tex;
            float ta = (float)tex.width / tex.height;
            float ra = w / h;
            if (ta > ra)
            {
                float uw = ra / ta;
                target.uvRect = new Rect((1f - uw) * 0.5f, 0f, uw, 1f);
            }
            else
            {
                float uh = ta / ra;
                target.uvRect = new Rect(0f, (1f - uh) * 0.5f, 1f, uh);
            }
            target.rectTransform.sizeDelta = new Vector2(w, h);
        }

        private static Texture2D MakeThumb(Texture2D src, int maxW, int maxH)
        {
            float scale = Mathf.Min((float)maxW / src.width, (float)maxH / src.height);
            int w = Mathf.Max(2, Mathf.RoundToInt(src.width * scale));
            int h = Mathf.Max(2, Mathf.RoundToInt(src.height * scale));
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var old = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = old;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        private static Texture2D MakeCoverThumb(Texture2D src, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var old = RenderTexture.active;
            float sa = (float)src.width / src.height;
            float da = (float)w / h;
            Vector2 scale;
            Vector2 offset;
            if (sa > da)
            {
                float uvw = da / sa;
                scale = new Vector2(uvw, 1f);
                offset = new Vector2((1f - uvw) * 0.5f, 0f);
            }
            else
            {
                float uvh = sa / da;
                scale = new Vector2(1f, uvh);
                offset = new Vector2(0f, (1f - uvh) * 0.5f);
            }
            Graphics.Blit(src, rt, scale, offset);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = old;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        private void ClearStage()
        {
            if (_viewerStage == null) return;
            _viewerToken++;
            for (int i = _viewerStage.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_viewerStage.GetChild(i).gameObject);
            _viewTex = null;
            _viewBundlePath = null;
            if (_viewBundle != null) { try { _viewBundle.Unload(true); } catch { } _viewBundle = null; }
            if (_cgInstance != null)
            {
                try { SoundManager.Instance.StopVoice(0f); } catch { }
                StopPrefabAudio(_cgInstance);
                UnityEngine.Object.Destroy(_cgInstance);
                _cgInstance = null;
            }
            if (_cgHandle.IsValid()) { try { Addressables.Release(_cgHandle); } catch { } }
            _cgHandle = default(AsyncOperationHandle<GameObject>);
        }

        private static void StopPrefabAudio(GameObject go)
        {
            try
            {
                foreach (var c in go.GetComponentsInChildren<Component>(true))
                {
                    if (c == null) continue;
                    string tn = c.GetType().Name;
                    if (tn != "AudioSource" && tn.IndexOf("StudioEventEmitter", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    try
                    {
                        var m = c.GetType().GetMethod("Stop", Type.EmptyTypes);
                        if (m != null) m.Invoke(c, null);
                    }
                    catch { }
                }
                var dir = go.GetComponentInChildren<PlayableDirector>();
                if (dir != null) dir.Stop();
            }
            catch { }
        }

        private void CloseViewer()
        {
            if (_viewer == null) return;
            ClearStage();
            _viewerIndex = -1;
            _immersive = false;
            if (_viewerChrome != null) _viewerChrome.gameObject.SetActive(true);
            if (_viewerStage != null && _viewerChrome != null && _tab != 0) _viewerStage.SetSiblingIndex(_viewerChrome.GetSiblingIndex());
            SetViewerScrims(true);
            if (_cgDismiss != null) _cgDismiss.gameObject.SetActive(false);
            if (_immersiveBack != null) _immersiveBack.gameObject.SetActive(false);
            if (_cgDismiss != null) _cgDismiss.gameObject.SetActive(false);
            _viewer.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ 控件

        private static Texture2D SolidTexture(Color32 c)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, c);
            tex.Apply();
            return tex;
        }

        private Sprite MakeRoundSprite()
        {
            const int size = 24, r = 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, r - Mathf.Min(x, size - 1 - x));
                    float dy = Mathf.Max(0, r - Mathf.Min(y, size - 1 - y));
                    bool inside = dx * dx + dy * dy <= r * r;
                    px[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        private static Sprite MakeRoundedOutlineSprite(float thickness)
        {
            const int size = 64, radius = 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float pxf = x + 0.25f + sx * 0.5f;
                            float pyf = y + 0.25f + sy * 0.5f;
                            bool outer = InRoundedRect(pxf, pyf, 0f, 0f, size, size, radius);
                            bool inner = InRoundedRect(pxf, pyf, thickness, thickness,
                                size - 2f * thickness, size - 2f * thickness,
                                Mathf.Max(0.5f, radius - thickness));
                            if (outer && !inner) hits++;
                        }
                    byte a = (byte)(hits * 255 / 4);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            float b = radius + 2f;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static bool InRoundedRect(float x, float y, float left, float top, float w, float h, float rad)
        {
            if (x < left || x > left + w || y < top || y > top + h) return false;
            float cx = Mathf.Clamp(x, left + rad, left + w - rad);
            float cy = Mathf.Clamp(y, top + rad, top + h - rad);
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= rad * rad;
        }

        private static Sprite MakeTachieBackSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            Color32 inner = new Color32(0x46, 0x38, 0x30, 0xFF);
            Color32 outer = new Color32(0x18, 0x13, 0x11, 0xFF);
            float cx = size * 0.5f, cy = size * 0.63f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - cx) / (size * 0.5f);
                    float ny = (y - cy) / (size * 0.5f);
                    float t = Mathf.Clamp01(Mathf.Sqrt(nx * nx + ny * ny) / 1.15f);
                    px[y * size + x] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Lerp(inner.r, outer.r, t)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(inner.g, outer.g, t)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(inner.b, outer.b, t)),
                        255);
                }
            tex.SetPixels32(px);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeWarmPanelSprite(bool gold)
        {
            const int size = 64, r = 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            // 黑曜丝绒 / 香槟古金渐变面板（对标独立鉴赏器 v2）
            Color32 top = gold ? new Color32(0xF6, 0xDF, 0xB4, 0xF8) : new Color32(0x28, 0x1F, 0x18, 0xF2);
            Color32 bottom = gold ? new Color32(0xAC, 0x82, 0x48, 0xF6) : new Color32(0x10, 0x0C, 0x09, 0xF6);
            Color32 edge = gold ? new Color32(0xFF, 0xF0, 0xCE, 0xEB) : new Color32(0xD4, 0xAE, 0x72, 0x94);
            Color32 hi = gold ? new Color32(0xFF, 0xF8, 0xEA, 0x6E) : new Color32(0xFF, 0xF2, 0xD6, 0x30);
            for (int y = 0; y < size; y++)
            {
                float t = (float)y / (size - 1);
                byte cr = (byte)Mathf.RoundToInt(Mathf.Lerp(bottom.r, top.r, t));
                byte cg = (byte)Mathf.RoundToInt(Mathf.Lerp(bottom.g, top.g, t));
                byte cb = (byte)Mathf.RoundToInt(Mathf.Lerp(bottom.b, top.b, t));
                byte ca = (byte)Mathf.RoundToInt(Mathf.Lerp(bottom.a, top.a, t));
                for (int x = 0; x < size; x++)
                {
                    bool outside = false, edgePx = false;
                    if ((x < r || x >= size - r) && (y < r || y >= size - r))
                    {
                        float cx = x < r ? r : size - 1 - r;
                        float cy = y < r ? r : size - 1 - r;
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        if (d > r) outside = true;
                        else edgePx = d >= r - 1f;
                    }
                    else edgePx = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    if (outside) { px[y * size + x] = new Color32(0, 0, 0, 0); continue; }
                    if (y == size - 2 && x >= r && x < size - r && !edgePx) px[y * size + x] = hi;
                    else px[y * size + x] = edgePx ? edge : new Color32(cr, cg, cb, ca);
                }
            }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        private static Sprite MakePaperCardSprite()
        {
            const int size = 96, r = 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            Color32 top = new Color32(0xDE, 0xD4, 0xC6, 0xFF);
            Color32 bottom = new Color32(0xDE, 0xD4, 0xC6, 0xFF);
            Color32 edge = new Color32(0xDE, 0xD4, 0xC6, 0xFF);
            Color32 hi = new Color32(0xDE, 0xD4, 0xC6, 0xFF);
            for (int y = 0; y < size; y++)
            {
                float t = (float)y / (size - 1);
                byte cr = (byte)Mathf.RoundToInt(Mathf.Lerp(bottom.r, top.r, t));
                byte cg = (byte)Mathf.RoundToInt(Mathf.Lerp(bottom.g, top.g, t));
                byte cb = (byte)Mathf.RoundToInt(Mathf.Lerp(bottom.b, top.b, t));
                for (int x = 0; x < size; x++)
                {
                    bool outside = false, edgePx = false;
                    if ((x < r || x >= size - r) && (y < r || y >= size - r))
                    {
                        float cx = x < r ? r : size - 1 - r;
                        float cy = y < r ? r : size - 1 - r;
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        if (d > r) outside = true;
                        else edgePx = d >= r - 1f;
                    }
                    else edgePx = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    if (outside) { px[y * size + x] = new Color32(0, 0, 0, 0); continue; }
                    if (y == size - 2 && x >= r && x < size - r && !edgePx) px[y * size + x] = hi;
                    else px[y * size + x] = edgePx ? edge : new Color32(cr, cg, cb, 0xFF);
                }
            }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        private static Sprite MakeCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            float r = size * 0.5f - 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = d <= r - 1f ? (byte)255 : (d >= r ? (byte)0 : (byte)(255f * (r - d)));
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Texture2D MakeVerticalGradient(int height, bool topDark, byte startAlpha)
        {
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false);
            var px = new Color32[height];
            for (int y = 0; y < height; y++)
            {
                float t = height > 1 ? (float)y / (height - 1) : 1f;
                byte a = (byte)Mathf.RoundToInt(startAlpha * (topDark ? t : (1f - t)));
                px[y] = new Color32(0x2B, 0x21, 0x1A, a);
            }
            tex.SetPixels32(px);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeBlurThumb(Texture2D src)
        {
            int w = 160, h = 90;
            var tex = MakeCoverThumb(src, w, h);
            var px = tex.GetPixels32();
            var tmp = new Color32[px.Length];
            for (int pass = 0; pass < 2; pass++)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int r = 0, g = 0, b = 0, a = 0, n = 0;
                        for (int yy = -1; yy <= 1; yy++)
                            for (int xx = -1; xx <= 1; xx++)
                            {
                                int sx = Mathf.Clamp(x + xx, 0, w - 1);
                                int sy = Mathf.Clamp(y + yy, 0, h - 1);
                                var c = px[sy * w + sx];
                                r += c.r; g += c.g; b += c.b; a += c.a; n++;
                            }
                        tmp[y * w + x] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), (byte)(a / n));
                    }
                var swap = px; px = tmp; tmp = swap;
            }
            tex.SetPixels32(px);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return tex;
        }

        private static Sprite MakeRadialSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            float r = size * 0.5f - 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / r);
                    byte a = (byte)(255f * (1f - d));
                    px[y * size + x] = new Color32(0, 0, 0, a);
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeRingSprite(int size, int thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            float r = size * 0.5f - 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = Mathf.Abs(d - r) <= thickness ? (byte)255 : (byte)0;
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private Texture2D GetCircleCover(string chapter, int size)
        {
            string key = "circlecover:" + chapter + ":" + size;
            if (_thumbs.TryGetValue(key, out var cached)) return cached;
            var src = ChapterCover(chapter);
            if (src == null) return null;
            Texture2D tex = null;
            try
            {
                int ts = Mathf.Clamp(size, 256, 896);
                tex = MakeCoverThumb(src, ts, ts);
                var px = tex.GetPixels32();
                float c = (ts - 1) * 0.5f;
                float r = ts * 0.5f - 0.5f;
                for (int y = 0; y < ts; y++)
                    for (int x = 0; x < ts; x++)
                    {
                        float dx = x - c, dy = y - c;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d >= r) px[y * ts + x].a = 0;
                        else if (d > r - 1.6f) px[y * ts + x].a = (byte)(255f * (r - d) / 1.6f);
                    }
                tex.SetPixels32(px);
                tex.Apply();
                _thumbs[key] = tex;
                return tex;
            }
            catch { if (tex != null) UnityEngine.Object.Destroy(tex); return null; }
        }

        private RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private Image NewWarmPanel(Transform parent, string name, float left, float top, float w, float h, bool gold = false, float alpha = 1f)
        {
            // 不再给每个面板叠黑色投影，避免界面出现一圈圈黑框。
            var img = NewImage(name, parent, new Color(1f, 1f, 1f, alpha), gold ? _goldPanel : _warmPanel);
            Top(img.rectTransform, left, top, w, h);
            return img;
        }

        private Image NewImage(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; }
            return img;
        }

        private RawImage NewRaw(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<RawImage>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private TextMeshProUGUI Txt(string name, Transform parent, string text, float size, Color color, TMP_FontAsset font, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        private static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Top(RectTransform rt, float left, float top, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(left, -top);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void Bottom(RectTransform rt, float left, float bottom, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(left, bottom);
            rt.sizeDelta = new Vector2(w, h);
        }

        private class Click
        {
            public RectTransform Rt;
            public Action Act;
        }

        // ------------------------------------------------------------------ 主菜单入口

        public static void InjectTitleButton(Nientum.TitleUIScene scene)
        {
            try
            {
                if (scene == null || Log == null) return;

                UISelectableButtonSetting setting = null;
                foreach (var c in scene.GetComponentsInChildren<UISelectableButtonSetting>(true))
                    if (c.gameObject.activeInHierarchy) { setting = c; break; }
                if (setting == null)
                {
                    Log.LogWarning("主菜单注入：找不到 UISelectableButtonSetting 模板。");
                    return;
                }

                GameObject template = setting.gameObject;
                RectTransform parent = template.transform.parent as RectTransform;
                if (parent == null)
                {
                    Log.LogWarning("主菜单注入：模板没有 RectTransform 父节点。");
                    return;
                }
                if (parent.Find("UIBtnGallery") != null)
                {
                    Log.LogInfo("主菜单入口已存在，跳过重复注入。");
                    return;
                }

                var go = UnityEngine.Object.Instantiate(template, parent, false);
                go.name = "UIBtnGallery";
                go.SetActive(true);

                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    string tn = mb.GetType().Name;
                    if (tn == "UISelectableButtonSetting") UnityEngine.Object.Destroy(mb);
                    else if (tn.IndexOf("Localiz", StringComparison.OrdinalIgnoreCase) >= 0) mb.enabled = false;
                }
                foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
                {
                    t.text = "CG与音乐";
                    if (t.gameObject.name.EndsWith("Active")) t.gameObject.SetActive(false);
                }
                var oldBase = go.GetComponent<UISelectableBase>();
                var galleryBtn = go.AddComponent<GallerySelectableButton>();
                if (oldBase != null && oldBase != galleryBtn)
                {
                    try
                    {
                        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                        foreach (var f in typeof(UISelectableBase).GetFields(flags))
                            f.SetValue(galleryBtn, f.GetValue(oldBase));
                    }
                    catch (Exception e) { if (Log != null) Log.LogWarning("复制按钮选中样式失败: " + e.Message); }
                    UnityEngine.Object.Destroy(oldBase);
                }

                var btn = go.GetComponent<UnityEngine.UI.Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(OpenGallery);
                    btn.interactable = true;
                }

                var rt = go.GetComponent<RectTransform>();
                var le = go.GetComponent<LayoutElement>();
                if (le == null) le = go.AddComponent<LayoutElement>();
                le.ignoreLayout = true;

                if (_inst != null) _inst.StartCoroutine(PlaceAndWire(scene, parent, rt, go));
                if (_inst != null && ((_autoOpen != null && _autoOpen.Value) || (_startTab != null && _startTab.Value >= 0)))
                    _inst.StartCoroutine(_inst.OpenForDebug());
            }
            catch (Exception e) { if (Log != null) Log.LogWarning("注入主菜单入口失败: " + e); }
        }

        private static void EnsureNav(GameBase.ControlInputNavigationBase nav, GameBase.InputNavigationButtonType button, GameBase.ControlInputNavigationBase target)
        {
            if (nav == null || target == null) return;
            EnsureNavState(nav, button, GameBase.InputNavigationStateType.PRESS_DOWN, target);
            EnsureNavState(nav, button, GameBase.InputNavigationStateType.PRESS_UP, target);
        }

        private static void EnsureNavState(GameBase.ControlInputNavigationBase nav, GameBase.InputNavigationButtonType button, GameBase.InputNavigationStateType state, GameBase.ControlInputNavigationBase target)
        {
            for (int i = 0; i < nav.InputNavigationTargetList.Count; i++)
            {
                if (nav.InputNavigationTargetList[i].ButtonType != button || nav.InputNavigationTargetList[i].StateType != state) continue;
                nav.InputNavigationTargetList[i] = new GameBase.InputNavigationTarget(button, state, target);
                return;
            }
            nav.InputNavigationTargetList.Add(new GameBase.InputNavigationTarget(button, state, target));
        }

        private static IEnumerator PlaceAndWire(Nientum.TitleUIScene scene, RectTransform parent, RectTransform rt, GameObject go)
        {
            yield return null;
            yield return null;

            var siblings = new List<RectTransform>();
            foreach (Transform ch in parent)
            {
                var cr = ch as RectTransform;
                if (cr != null && cr != rt && cr.gameObject.activeSelf) siblings.Add(cr);
            }
            if (siblings.Count > 0)
            {
                siblings.Sort((a, b) => b.anchoredPosition.y.CompareTo(a.anchoredPosition.y));
                float step = 104f;
                if (siblings.Count >= 2)
                {
                    step = Mathf.Abs(siblings[0].anchoredPosition.y - siblings[1].anchoredPosition.y);
                    if (step < 40f) step = 104f;
                }
                float newStep = Mathf.Min(step, 80f);
                float topY = siblings[0].anchoredPosition.y + 26f;
                for (int i = 0; i < siblings.Count; i++)
                    siblings[i].anchoredPosition = new Vector2(siblings[i].anchoredPosition.x, topY - i * newStep);

                var last = siblings[siblings.Count - 1];
                rt.anchorMin = last.anchorMin;
                rt.anchorMax = last.anchorMax;
                rt.pivot = last.pivot;
                rt.anchoredPosition = new Vector2(last.anchoredPosition.x, topY - siblings.Count * newStep);
            }
            rt.localScale = Vector3.one;
            Log.LogInfo("主菜单入口位置=" + rt.anchoredPosition + " 父节点=" + parent.name + " active=" + go.activeInHierarchy + " 兄弟数=" + parent.childCount);

            try
            {
                var fSel = AccessTools.Field(scene.GetType(), "selectables");
                var fRect = AccessTools.Field(scene.GetType(), "selectableRects");
                var fIdx = AccessTools.Field(scene.GetType(), "currentIndex");
                var mSelect = AccessTools.Method(scene.GetType(), "SelectUIButton");
                var list = fSel != null ? fSel.GetValue(scene) as System.Collections.IList : null;
                var rects = fRect != null ? fRect.GetValue(scene) as System.Collections.IList : null;
                if (list == null || rects == null) { Log.LogWarning("标题界面缺少可选列表，键盘选择不可用。"); yield break; }

                var mine = go.GetComponent<UISelectableBase>();
                if (mine != null && !list.Contains(mine)) list.Add(mine);
                if (!rects.Contains(rt)) rects.Add(rt);
                int index = list.IndexOf(mine);

                var myNav = go.GetComponentInChildren<GameBase.ControlInputNavigationBase>(true);
                if (myNav != null)
                {
                    myNav.EventSelected.RemoveAllListeners();
                    myNav.EventSelected.AddListener(() =>
                    {
                        try
                        {
                            if (fIdx != null) fIdx.SetValue(scene, index);
                            if (mSelect != null) mSelect.Invoke(scene, new object[] { index });
                        }
                        catch (Exception e) { Log.LogWarning("选择鉴赏室按钮失败: " + e.Message); }
                    });

                    Log.LogInfo("主菜单键盘导航：LateUpdate 钩子已启用（↑ 进入鉴赏室，Enter 打开）。");
                }
                var keyHook = go.GetComponent<GalleryTitleKeyHook>();
                if (keyHook == null) keyHook = go.AddComponent<GalleryTitleKeyHook>();
                keyHook.Setup(scene, index);
                Log.LogInfo("主菜单入口已加入：列表序号 " + index + "。");
            }
            catch (Exception e) { Log.LogWarning("注册主菜单入口失败: " + e.Message); }
        }
    }

    public class GalleryTitleKeyHook : MonoBehaviour
    {
        private Nientum.TitleUIScene _scene;
        private int _index;
        private int _prevCur = -1;
        private FieldInfo _fIndex, _fPrevent;
        private MethodInfo _mSelect;

        public void Setup(Nientum.TitleUIScene scene, int index)
        {
            _scene = scene;
            _index = index;
            _prevCur = -1;
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _fIndex = typeof(Nientum.TitleUIScene).GetField("currentIndex", flags);
            _fPrevent = typeof(Nientum.TitleUIScene).GetField("isPreventInput", flags);
            _mSelect = typeof(Nientum.TitleUIScene).GetMethod("SelectUIButton", flags);
        }

        private void LateUpdate()
        {
            if (_scene == null || Plugin.GalleryVisible) return;
            try
            {
                if (_fPrevent != null && (bool)_fPrevent.GetValue(_scene)) return;
                int cur = _fIndex != null ? (int)_fIndex.GetValue(_scene) : -1;
                if (_prevCur < 0) _prevCur = cur;
                bool up = KeyDown(UnityEngine.KeyCode.UpArrow, false);
                bool down = KeyDown(UnityEngine.KeyCode.DownArrow, false);
                bool enter = KeyDown(UnityEngine.KeyCode.Return, true);
                int lastGame = Mathf.Max(0, _index - 1);
                if (up && _prevCur == 0)
                {
                    _fIndex.SetValue(_scene, _index);
                    if (_mSelect != null) _mSelect.Invoke(_scene, new object[] { _index });
                    cur = _index;
                }
                else if (down && _prevCur == lastGame)
                {
                    _fIndex.SetValue(_scene, _index);
                    if (_mSelect != null) _mSelect.Invoke(_scene, new object[] { _index });
                    cur = _index;
                }
                else if (down && _prevCur == _index)
                {
                    _fIndex.SetValue(_scene, 0);
                    if (_mSelect != null) _mSelect.Invoke(_scene, new object[] { 0 });
                    cur = 0;
                }
                else if (up && _prevCur == _index)
                {
                    _fIndex.SetValue(_scene, lastGame);
                    if (_mSelect != null) _mSelect.Invoke(_scene, new object[] { lastGame });
                    cur = lastGame;
                }
                else if (enter && cur == _index)
                {
                    Plugin.OpenGallery();
                }
                _prevCur = cur;
            }
            catch { }
        }

        private static bool KeyDown(UnityEngine.KeyCode legacy, bool enter)
        {
            bool down = false;
            try { down = UnityEngine.Input.GetKeyDown(legacy); } catch { }
            if (!down)
            {
                try
                {
                    var kb = UnityEngine.InputSystem.Keyboard.current;
                    if (kb != null)
                    {
                        if (enter) down = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
                        else if (legacy == UnityEngine.KeyCode.UpArrow) down = kb.upArrowKey.wasPressedThisFrame;
                        else if (legacy == UnityEngine.KeyCode.DownArrow) down = kb.downArrowKey.wasPressedThisFrame;
                    }
                }
                catch { }
            }
            return down;
        }
    }

    public class GallerySelectableButton : UISelectableButton
    {
        public override void InvokeAction() { Plugin.OpenGallery(); }

        private void Start()
        {
            var b = GetComponent<UnityEngine.UI.Button>();
            if (b != null)
            {
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(InvokeAction);
                b.interactable = true;
            }
        }
    }

    [HarmonyPatch(typeof(Nientum.TitleUIScene), "InitUI")]
    internal static class TitleMenuPatch
    {
        private static void Postfix(Nientum.TitleUIScene __instance)
        {
            Plugin.InjectTitleButton(__instance);
        }
    }
}