using System;
using UnityEngine;
using OutrunAftermath.Client.Runtime.Audio;
using OutrunAftermath.Client.Runtime.Input;

namespace OutrunAftermath.Client.Runtime.Settings
{
    public enum QualityPreset
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Ultra = 3
    }

    /// <summary>
    /// Master Quality & Platform Optimization Director for OUTRUN: AFTERMATH.
    /// Manages cross-platform graphics presets, dynamic resolution scaling, framerate caps,
    /// shadow budgets, audio balances, and input controls sensitivity with persistent storage.
    /// </summary>
    public sealed class SettingsDirector : MonoBehaviour
    {
        public static SettingsDirector Instance { get; private set; }

        private const string PrefQuality = "outrun_settings_quality";
        private const string PrefFps = "outrun_settings_target_fps";
        private const string PrefVsync = "outrun_settings_vsync";
        private const string PrefFov = "outrun_settings_fov";
        private const string PrefMasterVol = "outrun_settings_vol_master";
        private const string PrefSfxVol = "outrun_settings_vol_sfx";
        private const string PrefAmbientVol = "outrun_settings_vol_ambient";
        private const string PrefSensitivity = "outrun_settings_look_sensitivity";
        private const string PrefTouchControls = "outrun_settings_touch_controls";

        [Header("Current Configuration")]
        [SerializeField] private QualityPreset currentQuality = QualityPreset.High;
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private bool vsyncEnabled = false;
        [SerializeField] private float fieldOfView = 85f;
        [SerializeField] private float lookSensitivity = 1.8f;
        [SerializeField] private int touchControlsMode = 0; // 0 = Auto, 1 = Force On, 2 = Force Off

        public QualityPreset CurrentQuality => currentQuality;
        public int TargetFrameRate => targetFrameRate;
        public bool VSyncEnabled => vsyncEnabled;
        public float FieldOfView => fieldOfView;
        public float LookSensitivity => lookSensitivity;
        public int TouchControlsMode => touchControlsMode;

        public event Action OnSettingsChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSavedSettings();
            ApplySettings();
        }

        public void LoadSavedSettings()
        {
            currentQuality = (QualityPreset)PlayerPrefs.GetInt(PrefQuality, (int)QualityPreset.High);
            targetFrameRate = PlayerPrefs.GetInt(PrefFps, 60);
            vsyncEnabled = PlayerPrefs.GetInt(PrefVsync, 0) == 1;
            fieldOfView = PlayerPrefs.GetFloat(PrefFov, 85f);
            lookSensitivity = PlayerPrefs.GetFloat(PrefSensitivity, 1.8f);
            touchControlsMode = PlayerPrefs.GetInt(PrefTouchControls, 0);

            // On mobile platforms, default to Low or Medium quality with 60 FPS cap
            if (Application.isMobilePlatform && !PlayerPrefs.HasKey(PrefQuality))
            {
                currentQuality = QualityPreset.Medium;
                targetFrameRate = 60;
                touchControlsMode = 1; // Force On
            }
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetInt(PrefQuality, (int)currentQuality);
            PlayerPrefs.SetInt(PrefFps, targetFrameRate);
            PlayerPrefs.SetInt(PrefVsync, vsyncEnabled ? 1 : 0);
            PlayerPrefs.SetFloat(PrefFov, fieldOfView);
            PlayerPrefs.SetFloat(PrefSensitivity, lookSensitivity);
            PlayerPrefs.SetInt(PrefTouchControls, touchControlsMode);
            PlayerPrefs.Save();
        }

        public void SetQualityPreset(QualityPreset preset)
        {
            currentQuality = preset;
            ApplySettings();
            SaveSettings();
            OnSettingsChanged?.Invoke();
        }

        public void SetTargetFrameRate(int fps)
        {
            targetFrameRate = fps;
            Application.targetFrameRate = targetFrameRate;
            SaveSettings();
            OnSettingsChanged?.Invoke();
        }

        public void SetVSync(bool enabled)
        {
            vsyncEnabled = enabled;
            QualitySettings.vSyncCount = vsyncEnabled ? 1 : 0;
            SaveSettings();
            OnSettingsChanged?.Invoke();
        }

        public void SetFieldOfView(float fov)
        {
            fieldOfView = Mathf.Clamp(fov, 65f, 105f);
            if (Camera.main != null)
            {
                Camera.main.fieldOfView = fieldOfView;
            }
            SaveSettings();
            OnSettingsChanged?.Invoke();
        }

        public void SetLookSensitivity(float sensitivity)
        {
            lookSensitivity = Mathf.Clamp(sensitivity, 0.4f, 5.0f);
            if (InputManager.Instance != null)
            {
                InputManager.Instance.SetSensitivity(lookSensitivity);
            }
            SaveSettings();
            OnSettingsChanged?.Invoke();
        }

        public void SetTouchControlsMode(int mode)
        {
            touchControlsMode = Mathf.Clamp(mode, 0, 2);
            SaveSettings();
            OnSettingsChanged?.Invoke();
        }

        public void ApplySettings()
        {
            // 1. Framerate & Sensitivity
            Application.targetFrameRate = targetFrameRate;

            if (InputManager.Instance != null)
            {
                InputManager.Instance.SetSensitivity(lookSensitivity);
            }

            // In headless/batchmode, skip hardware graphics buffer manipulations
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.Log($"[SettingsDirector] Applied Settings (Headless): Preset={currentQuality}, TargetFPS={targetFrameRate}, Sensitivity={lookSensitivity:F1}x");
                return;
            }

            QualitySettings.vSyncCount = vsyncEnabled ? 1 : 0;

            // 2. Preset-specific Graphics Tunings
            switch (currentQuality)
            {
                case QualityPreset.Low: // Mobile & Low-End Target
                    QualitySettings.shadowDistance = 25f;
                    QualitySettings.shadowResolution = ShadowResolution.Low;
                    QualitySettings.shadowCascades = 0;
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                    QualitySettings.lodBias = 0.7f;
                    QualitySettings.maximumLODLevel = 1;
                    QualitySettings.realtimeReflectionProbes = false;
                    ScalableBufferManager.ResizeBuffers(0.70f, 0.70f);
                    break;

                case QualityPreset.Medium: // Balanced Performance
                    QualitySettings.shadowDistance = 50f;
                    QualitySettings.shadowResolution = ShadowResolution.Medium;
                    QualitySettings.shadowCascades = 2;
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                    QualitySettings.lodBias = 1.0f;
                    QualitySettings.maximumLODLevel = 0;
                    QualitySettings.realtimeReflectionProbes = false;
                    ScalableBufferManager.ResizeBuffers(0.85f, 0.85f);
                    break;

                case QualityPreset.High: // Standard High PC
                    QualitySettings.shadowDistance = 85f;
                    QualitySettings.shadowResolution = ShadowResolution.High;
                    QualitySettings.shadowCascades = 4;
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                    QualitySettings.lodBias = 1.3f;
                    QualitySettings.maximumLODLevel = 0;
                    QualitySettings.realtimeReflectionProbes = true;
                    ScalableBufferManager.ResizeBuffers(1.0f, 1.0f);
                    break;

                case QualityPreset.Ultra: // Enthusiast PC
                    QualitySettings.shadowDistance = 140f;
                    QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
                    QualitySettings.shadowCascades = 4;
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                    QualitySettings.lodBias = 2.0f;
                    QualitySettings.maximumLODLevel = 0;
                    QualitySettings.realtimeReflectionProbes = true;
                    ScalableBufferManager.ResizeBuffers(1.0f, 1.0f);
                    break;
            }

            // 3. Apply Camera FOV
            if (Camera.main != null)
            {
                Camera.main.fieldOfView = fieldOfView;
            }

            Debug.Log($"[SettingsDirector] Applied Settings: Preset={currentQuality}, TargetFPS={targetFrameRate}, VSync={vsyncEnabled}, FOV={fieldOfView:F1}, ShadowDist={QualitySettings.shadowDistance}m");
        }

        public bool ShouldEnableTouchControls()
        {
            if (touchControlsMode == 1) return true;  // Force On
            if (touchControlsMode == 2) return false; // Force Off
            return Application.isMobilePlatform || UnityEngine.InputSystem.Touchscreen.current != null;
        }
    }
}
