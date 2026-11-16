using System;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using OutrunAftermath.Client.Runtime.Settings;

namespace OutrunAftermath.Client.Runtime.UI
{
    /// <summary>
    /// Real-time Performance & Network Diagnostic Profiler HUD for OUTRUN: AFTERMATH.
    /// Tracks framerate, frame delta latency, heap allocations, quality preset status,
    /// network RTT ping, and draw statistics.
    /// </summary>
    public sealed class PerformanceProfilerHUD : MonoBehaviour
    {
        public static PerformanceProfilerHUD Instance { get; private set; }

        private Canvas _canvas;
        private GameObject _panel;
        private Text _statsText;

        private float _fpsAccumulator;
        private int _fpsFrames;
        private float _fpsTimeLeft;
        private float _currentFps;
        private float _minFps = 999f;
        private float _maxFps = 0f;
        private float _fpsResetTimer;

        private bool _isVisible = false;

        public bool IsVisible => _isVisible;

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
                BuildUI();
                SetVisible(false);
            }
        }

        private void Update()
        {
            // Toggle shortcut [F3]
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f3Key.wasPressedThisFrame)
            {
                ToggleVisible();
            }

            if (!_isVisible) return;

            // Framerate calculation
            float dt = Time.unscaledDeltaTime;
            _fpsAccumulator += 1f / Mathf.Max(0.0001f, dt);
            _fpsFrames++;
            _fpsTimeLeft -= dt;

            _fpsResetTimer += dt;
            if (_fpsResetTimer > 10f)
            {
                _fpsResetTimer = 0f;
                _minFps = _currentFps;
                _maxFps = _currentFps;
            }

            if (_fpsTimeLeft <= 0f)
            {
                _currentFps = _fpsAccumulator / Mathf.Max(1, _fpsFrames);
                if (_currentFps < _minFps) _minFps = _currentFps;
                if (_currentFps > _maxFps) _maxFps = _currentFps;

                _fpsAccumulator = 0f;
                _fpsFrames = 0;
                _fpsTimeLeft = 0.35f;

                UpdateDisplay(dt);
            }
        }

        public void ToggleVisible()
        {
            SetVisible(!_isVisible);
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_panel != null)
            {
                _panel.SetActive(_isVisible);
            }
        }

        private void UpdateDisplay(float lastDeltaTime)
        {
            if (_statsText == null) return;

            long totalMemMb = GC.GetTotalMemory(false) / (1024 * 1024);
            float frameTimeMs = lastDeltaTime * 1000f;

            string presetStr = SettingsDirector.Instance != null ? SettingsDirector.Instance.CurrentQuality.ToString().ToUpper() : "DEFAULT";
            string netRole = "STANDALONE";
            float rttMs = 0f;
            int connectedCount = 1;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                netRole = NetworkManager.Singleton.IsHost ? "HOST / SERVER" : (NetworkManager.Singleton.IsServer ? "DEDICATED SERVER" : "CLIENT");
                connectedCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
                if (NetworkManager.Singleton.NetworkConfig.NetworkTransport != null)
                {
                    rttMs = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
                }
            }

            Color fpsColor = _currentFps >= 55f ? Color.green : (_currentFps >= 30f ? Color.yellow : Color.red);
            string fpsHex = ColorUtility.ToHtmlStringRGB(fpsColor);

            _statsText.text = $"<b>[OUTRUN: AFTERMATH // DIAGNOSTICS]</b>\n" +
                              $"FPS: <color=#{fpsHex}><b>{_currentFps:F1}</b></color> (MIN: {_minFps:F0} / MAX: {_maxFps:F0})\n" +
                              $"FRAME TIME: {frameTimeMs:F2} ms\n" +
                              $"MEMORY (GC): {totalMemMb} MB\n" +
                              $"QUALITY PRESET: {presetStr}\n" +
                              $"RESOLUTION: {Screen.width}x{Screen.height} ({(Screen.dpi > 0 ? Screen.dpi : 96):F0} DPI)\n" +
                              $"NET ROLE: {netRole}\n" +
                              $"PEERS CONNECTED: {connectedCount}\n" +
                              $"NETWORK RTT: {rttMs:F0} ms\n" +
                              $"TOGGLE: [F3]";
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("ProfilerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 99; // Topmost

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _panel = new GameObject("ProfilerPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(_canvas.transform, false);
            var rt = _panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(15f, -15f);
            rt.sizeDelta = new Vector2(320f, 210f);

            var img = _panel.GetComponent<Image>();
            img.color = new Color(0.06f, 0.08f, 0.1f, 0.85f);

            var textGo = new GameObject("StatsText", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(_panel.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(12f, 10f);
            textRt.offsetMax = new Vector2(-12f, -10f);

            _statsText = textGo.GetComponent<Text>();
            _statsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            _statsText.fontSize = 13;
            _statsText.color = new Color(0.85f, 0.9f, 0.95f, 1f);
            _statsText.lineSpacing = 1.15f;
            _statsText.supportRichText = true;
        }
    }
}
