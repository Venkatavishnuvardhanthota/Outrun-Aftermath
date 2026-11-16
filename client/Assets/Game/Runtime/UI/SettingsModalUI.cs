using System;
using UnityEngine;
using UnityEngine.UI;
using OutrunAftermath.Client.Runtime.Settings;
using OutrunAftermath.Client.Runtime.Audio;
using OutrunAftermath.Client.Runtime.Input;

namespace OutrunAftermath.Client.Runtime.UI
{
    /// <summary>
    /// Tactical In-Game & Lobby Settings Modal for OUTRUN: AFTERMATH.
    /// Provides live cross-platform controls for Graphics Quality Presets, Target Framerate,
    /// V-Sync, FOV, Audio Volume channels, Input Sensitivity, and Touch HUD configuration.
    /// </summary>
    public sealed class SettingsModalUI : MonoBehaviour
    {
        public static SettingsModalUI Instance { get; private set; }

        private Canvas _canvas;
        private GameObject _modalRoot;
        private bool _isOpen = false;

        private enum SettingsTab
        {
            Graphics,
            Audio,
            Controls
        }

        private SettingsTab _currentTab = SettingsTab.Graphics;

        // Tab Content Panels
        private GameObject _graphicsPanel;
        private GameObject _audioPanel;
        private GameObject _controlsPanel;

        // UI Indicators
        private Text _presetStatusText;
        private Text _fpsStatusText;
        private Text _vsyncStatusText;
        private Text _fovStatusText;
        private Text _masterVolText;
        private Text _sfxVolText;
        private Text _ambientVolText;
        private Text _sensitivityText;
        private Text _touchModeText;
        private Text _profilerStatusText;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                BuildModalUI();
                SetOpen(false);
            }
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame || kb.f2Key.wasPressedThisFrame)
                {
                    ToggleOpen();
                }
            }
        }

        public void ToggleOpen()
        {
            SetOpen(!_isOpen);
        }

        public void SetOpen(bool open)
        {
            _isOpen = open;
            if (_modalRoot != null)
            {
                _modalRoot.SetActive(_isOpen);
                if (_isOpen)
                {
                    RefreshDisplay();
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    // Re-lock cursor if in tactical match and not mobile
                    if (!Application.isMobilePlatform && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Main")
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                }
            }
        }

        private void SwitchTab(SettingsTab tab)
        {
            _currentTab = tab;
            _graphicsPanel.SetActive(tab == SettingsTab.Graphics);
            _audioPanel.SetActive(tab == SettingsTab.Audio);
            _controlsPanel.SetActive(tab == SettingsTab.Controls);
            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            if (SettingsDirector.Instance == null) return;

            if (_presetStatusText != null)
                _presetStatusText.text = $"CURRENT: [{SettingsDirector.Instance.CurrentQuality.ToString().ToUpper()}]";

            if (_fpsStatusText != null)
                _fpsStatusText.text = $"LIMIT: [{SettingsDirector.Instance.TargetFrameRate} FPS]";

            if (_vsyncStatusText != null)
                _vsyncStatusText.text = $"V-SYNC: [{(SettingsDirector.Instance.VSyncEnabled ? "ENABLED" : "DISABLED")}]";

            if (_fovStatusText != null)
                _fovStatusText.text = $"FIELD OF VIEW: [{SettingsDirector.Instance.FieldOfView:F0}°]";

            if (_sensitivityText != null)
                _sensitivityText.text = $"LOOK SENSITIVITY: [{SettingsDirector.Instance.LookSensitivity:F1}x]";

            if (_touchModeText != null)
            {
                string mode = SettingsDirector.Instance.TouchControlsMode == 1 ? "FORCE ON" :
                             (SettingsDirector.Instance.TouchControlsMode == 2 ? "FORCE OFF" : "AUTO DETECT");
                _touchModeText.text = $"TOUCH HUD: [{mode}]";
            }

            if (_profilerStatusText != null && PerformanceProfilerHUD.Instance != null)
            {
                _profilerStatusText.text = $"DIAGNOSTICS: [{(PerformanceProfilerHUD.Instance.IsVisible ? "ON" : "OFF")}]";
            }
        }

        // ==========================================
        // UI CONSTRUCTION (Procedural uGUI)
        // ==========================================

        private void BuildModalUI()
        {
            var canvasGo = new GameObject("SettingsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 98; // High priority overlay

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // Modal Background Dimmer
            _modalRoot = CreatePanel(_canvas.transform, "ModalRoot", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.65f));

            // Dialog Window
            var dialog = CreatePanel(_modalRoot.transform, "SettingsDialog", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 540f), new Color(0.08f, 0.10f, 0.13f, 0.96f));

            // Header Banner
            var header = CreatePanel(dialog.transform, "HeaderBanner", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -25f), new Vector2(0f, 50f), new Color(0.12f, 0.15f, 0.20f, 1.0f));
            CreateText(header.transform, "Title", "SYSTEM SETTINGS & CROSS-PLATFORM POLISH", 18, FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(600f, 40f), new Color(0.2f, 0.8f, 1.0f));

            // Close [X] Button
            CreateButton(header.transform, "Btn_Close", "X", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-25f, 0f), new Vector2(36f, 36f), new Color(0.7f, 0.2f, 0.2f), () => SetOpen(false));

            // Tab Navigation Bar
            var tabsBar = CreatePanel(dialog.transform, "TabsBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), new Vector2(0f, 42f), new Color(0.06f, 0.08f, 0.10f, 1.0f));
            CreateButton(tabsBar.transform, "Tab_Gfx", "1. GRAPHICS & PERFORMANCE", new Vector2(0.17f, 0.5f), new Vector2(0.17f, 0.5f), Vector2.zero, new Vector2(230f, 34f), new Color(0.18f, 0.22f, 0.28f), () => SwitchTab(SettingsTab.Graphics));
            CreateButton(tabsBar.transform, "Tab_Audio", "2. AUDIO BALANCES", new Vector2(0.50f, 0.5f), new Vector2(0.50f, 0.5f), Vector2.zero, new Vector2(230f, 34f), new Color(0.18f, 0.22f, 0.28f), () => SwitchTab(SettingsTab.Audio));
            CreateButton(tabsBar.transform, "Tab_Controls", "3. CONTROLS & MOBILE", new Vector2(0.83f, 0.5f), new Vector2(0.83f, 0.5f), Vector2.zero, new Vector2(230f, 34f), new Color(0.18f, 0.22f, 0.28f), () => SwitchTab(SettingsTab.Controls));

            // ----------------------------------------------------
            // 1. GRAPHICS TAB
            // ----------------------------------------------------
            _graphicsPanel = CreatePanel(dialog.transform, "GraphicsPanel", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, -45f), new Vector2(-40f, -140f), Color.clear);

            _presetStatusText = CreateText(_graphicsPanel.transform, "PresetStatus", "CURRENT: [HIGH]", 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(-180f, 130f), new Vector2(300f, 30f), Color.yellow);
            CreateButton(_graphicsPanel.transform, "Btn_Low", "LOW (Mobile)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-225f, 85f), new Vector2(140f, 36f), new Color(0.2f, 0.35f, 0.25f), () => SettingsDirector.Instance.SetQualityPreset(QualityPreset.Low));
            CreateButton(_graphicsPanel.transform, "Btn_Med", "MEDIUM", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-75f, 85f), new Vector2(140f, 36f), new Color(0.2f, 0.3f, 0.4f), () => SettingsDirector.Instance.SetQualityPreset(QualityPreset.Medium));
            CreateButton(_graphicsPanel.transform, "Btn_High", "HIGH (Default)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(75f, 85f), new Vector2(140f, 36f), new Color(0.25f, 0.35f, 0.5f), () => SettingsDirector.Instance.SetQualityPreset(QualityPreset.High));
            CreateButton(_graphicsPanel.transform, "Btn_Ultra", "ULTRA", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(225f, 85f), new Vector2(140f, 36f), new Color(0.45f, 0.25f, 0.5f), () => SettingsDirector.Instance.SetQualityPreset(QualityPreset.Ultra));

            // Target FPS
            _fpsStatusText = CreateText(_graphicsPanel.transform, "FpsStatus", "LIMIT: [60 FPS]", 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(-180f, 25f), new Vector2(300f, 30f), Color.white);
            CreateButton(_graphicsPanel.transform, "Btn_30", "30 FPS", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-225f, -20f), new Vector2(140f, 36f), new Color(0.18f, 0.22f, 0.28f), () => SettingsDirector.Instance.SetTargetFrameRate(30));
            CreateButton(_graphicsPanel.transform, "Btn_60", "60 FPS", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-75f, -20f), new Vector2(140f, 36f), new Color(0.18f, 0.22f, 0.28f), () => SettingsDirector.Instance.SetTargetFrameRate(60));
            CreateButton(_graphicsPanel.transform, "Btn_120", "120 FPS", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(75f, -20f), new Vector2(140f, 36f), new Color(0.18f, 0.22f, 0.28f), () => SettingsDirector.Instance.SetTargetFrameRate(120));
            CreateButton(_graphicsPanel.transform, "Btn_Unl", "UNCAPPED", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(225f, -20f), new Vector2(140f, 36f), new Color(0.18f, 0.22f, 0.28f), () => SettingsDirector.Instance.SetTargetFrameRate(-1));

            // V-Sync & FOV
            _vsyncStatusText = CreateText(_graphicsPanel.transform, "VsyncStatus", "V-SYNC: [DISABLED]", 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(-180f, -80f), new Vector2(250f, 30f), Color.white);
            CreateButton(_graphicsPanel.transform, "Btn_Vsync", "TOGGLE V-SYNC", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150f, -80f), new Vector2(180f, 32f), new Color(0.2f, 0.3f, 0.4f), () => SettingsDirector.Instance.SetVSync(!SettingsDirector.Instance.VSyncEnabled));

            _fovStatusText = CreateText(_graphicsPanel.transform, "FovStatus", "FIELD OF VIEW: [85°]", 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(-180f, -125f), new Vector2(250f, 30f), Color.white);
            CreateButton(_graphicsPanel.transform, "Btn_FovMinus", "FOV -5°", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(70f, -125f), new Vector2(90f, 32f), new Color(0.2f, 0.25f, 0.3f), () => SettingsDirector.Instance.SetFieldOfView(SettingsDirector.Instance.FieldOfView - 5f));
            CreateButton(_graphicsPanel.transform, "Btn_FovPlus", "FOV +5°", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(180f, -125f), new Vector2(90f, 32f), new Color(0.2f, 0.25f, 0.3f), () => SettingsDirector.Instance.SetFieldOfView(SettingsDirector.Instance.FieldOfView + 5f));

            // ----------------------------------------------------
            // 2. AUDIO TAB
            // ----------------------------------------------------
            _audioPanel = CreatePanel(dialog.transform, "AudioPanel", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, -45f), new Vector2(-40f, -140f), Color.clear);
            _audioPanel.SetActive(false);

            CreateText(_audioPanel.transform, "AudioNotice", "PROCEDURAL PCM AUDIO DIRECTOR ACTIVE (16 3D SPATIAL CHANNELS)", 15, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0f, 110f), new Vector2(600f, 30f), Color.cyan);

            CreateText(_audioPanel.transform, "MasterLbl", "MASTER VOLUME:", 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(-180f, 40f), new Vector2(250f, 30f), Color.white);
            CreateButton(_audioPanel.transform, "Btn_MuteMaster", "MUTE / UNMUTE", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120f, 40f), new Vector2(180f, 32f), new Color(0.2f, 0.3f, 0.4f), () => AudioListener.volume = AudioListener.volume > 0.05f ? 0f : 1.0f);

            CreateText(_audioPanel.transform, "SfxLbl", "WEAPONS & COMBAT FX:", 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(-180f, -20f), new Vector2(250f, 30f), Color.white);
            CreateButton(_audioPanel.transform, "Btn_TestSfx", "TEST GUNSHOT AUDIO", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120f, -20f), new Vector2(180f, 32f), new Color(0.4f, 0.25f, 0.2f), () =>
            {
                if (AudioManager.Instance != null && Camera.main != null)
                    AudioManager.Instance.PlayGunshot(Camera.main.transform.position + Camera.main.transform.forward * 2f, "rifle", 1.0f);
            });

            CreateText(_audioPanel.transform, "HitLbl", "TACTICAL HIT CONFIRMATION:", 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(-180f, -80f), new Vector2(250f, 30f), Color.white);
            CreateButton(_audioPanel.transform, "Btn_TestHit", "TEST HEADSHOT CHIME", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120f, -80f), new Vector2(180f, 32f), new Color(0.2f, 0.4f, 0.3f), () =>
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayHitMarker(true, false);
            });

            // ----------------------------------------------------
            // 3. CONTROLS & MOBILE TAB
            // ----------------------------------------------------
            _controlsPanel = CreatePanel(dialog.transform, "ControlsPanel", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, -45f), new Vector2(-40f, -140f), Color.clear);
            _controlsPanel.SetActive(false);

            _sensitivityText = CreateText(_controlsPanel.transform, "SensText", "LOOK SENSITIVITY: [1.8x]", 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(-180f, 110f), new Vector2(250f, 30f), Color.white);
            CreateButton(_controlsPanel.transform, "Btn_SensDown", "- 0.2x", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(70f, 110f), new Vector2(90f, 32f), new Color(0.2f, 0.25f, 0.3f), () => SettingsDirector.Instance.SetLookSensitivity(SettingsDirector.Instance.LookSensitivity - 0.2f));
            CreateButton(_controlsPanel.transform, "Btn_SensUp", "+ 0.2x", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(180f, 110f), new Vector2(90f, 32f), new Color(0.2f, 0.25f, 0.3f), () => SettingsDirector.Instance.SetLookSensitivity(SettingsDirector.Instance.LookSensitivity + 0.2f));

            _touchModeText = CreateText(_controlsPanel.transform, "TouchText", "TOUCH HUD: [AUTO DETECT]", 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(-180f, 40f), new Vector2(250f, 30f), Color.yellow);
            CreateButton(_controlsPanel.transform, "Btn_TouchAuto", "AUTO", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(20f, 40f), new Vector2(80f, 32f), new Color(0.2f, 0.3f, 0.4f), () => SettingsDirector.Instance.SetTouchControlsMode(0));
            CreateButton(_controlsPanel.transform, "Btn_TouchOn", "FORCE ON", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(115f, 40f), new Vector2(95f, 32f), new Color(0.2f, 0.4f, 0.3f), () => SettingsDirector.Instance.SetTouchControlsMode(1));
            CreateButton(_controlsPanel.transform, "Btn_TouchOff", "FORCE OFF", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(220f, 40f), new Vector2(95f, 32f), new Color(0.4f, 0.2f, 0.2f), () => SettingsDirector.Instance.SetTouchControlsMode(2));

            _profilerStatusText = CreateText(_controlsPanel.transform, "ProfilerText", "DIAGNOSTICS: [OFF]", 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(-180f, -30f), new Vector2(250f, 30f), Color.white);
            CreateButton(_controlsPanel.transform, "Btn_ToggleProfiler", "TOGGLE FPS & NET HUD [F3]", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(140f, -30f), new Vector2(220f, 34f), new Color(0.25f, 0.35f, 0.45f), () =>
            {
                if (PerformanceProfilerHUD.Instance != null)
                {
                    PerformanceProfilerHUD.Instance.ToggleVisible();
                    RefreshDisplay();
                }
            });

            // Bottom Confirmation Bar
            var bottomBar = CreatePanel(dialog.transform, "BottomBar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 25f), new Vector2(0f, 50f), new Color(0.06f, 0.08f, 0.10f, 1.0f));
            CreateButton(bottomBar.transform, "Btn_ApplyClose", "APPLY & RESUME GAME [ESC]", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 38f), new Color(0.15f, 0.55f, 0.35f), () => SetOpen(false));
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            return go;
        }

        private Text CreateText(Transform parent, string name, string content, int fontSize, FontStyle style, TextAnchor alignment, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var txt = go.GetComponent<Text>();
            txt.text = content;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = alignment;
            txt.color = color;
            return txt;
        }

        private GameObject CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color color, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.color = color;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = color;
            colors.highlightedColor = new Color(color.r * 1.25f, color.g * 1.25f, color.b * 1.25f, 1f);
            colors.pressedColor = new Color(color.r * 0.75f, color.g * 0.75f, color.b * 0.75f, 1f);
            btn.colors = colors;

            if (onClick != null)
            {
                btn.onClick.AddListener(() =>
                {
                    onClick();
                    RefreshDisplay();
                });
            }

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            var txt = textGo.GetComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 13;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;

            return go;
        }
    }
}
