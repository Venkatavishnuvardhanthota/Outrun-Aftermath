using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using OutrunAftermath.Client.Runtime.Settings;

namespace OutrunAftermath.Client.Runtime.Input
{
    /// <summary>
    /// Dual-zone Virtual Touch Controls for OUTRUN: AFTERMATH on Android, iOS, and Touch devices.
    /// Provides dynamic floating movement joystick, touch swipe look zone, tactile combat buttons,
    /// and dedicated vehicle driving controls.
    /// </summary>
    public sealed class VirtualTouchControls : MonoBehaviour
    {
        public static VirtualTouchControls Instance { get; private set; }

        private Canvas _canvas;
        private GameObject _controlsRoot;

        // Joystick Visuals & State
        private RectTransform _joystickBase;
        private RectTransform _joystickThumb;
        private int _joystickTouchId = -1;
        private Vector2 _joystickCenter;
        private Vector2 _virtualMove;

        // Look Drag State
        private int _lookTouchId = -1;
        private Vector2 _lastLookTouchPos;
        private Vector2 _virtualLook;

        // On-screen Buttons State
        private bool _btnFireHeld;
        private bool _btnFireTapped;
        private bool _btnAimHeld;
        private bool _btnJumpTapped;
        private bool _btnCrouchHeld;
        private bool _btnSprintHeld;
        private bool _btnReloadTapped;
        private bool _btnInteractHeld;
        private bool _btnAbilityPrimaryTapped;
        private bool _btnAbilityUltimateTapped;
        private bool _btnTogglePerspectiveTapped;
        private int _btnWeaponSlotTapped = 0;

        // Vehicle Mode State
        private bool _isVehicleMode;
        private float _vehicleSteer;
        private float _vehicleThrottle;
        private bool _vehicleHandbrake;
        private bool _vehicleNitro;

        private const float JoystickRadius = 65f;
        private const float LookSensitivityTouch = 0.12f;

        public bool IsActive => _controlsRoot != null && _controlsRoot.activeSelf;

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
                BuildTouchUI();
                UpdateVisibility();
            }
        }

        private void Start()
        {
            if (SettingsDirector.Instance != null)
            {
                SettingsDirector.Instance.OnSettingsChanged += UpdateVisibility;
            }
        }

        private void OnDestroy()
        {
            if (SettingsDirector.Instance != null)
            {
                SettingsDirector.Instance.OnSettingsChanged -= UpdateVisibility;
            }
        }

        public void SetVehicleMode(bool inVehicle)
        {
            _isVehicleMode = inVehicle;
        }

        private void UpdateVisibility()
        {
            bool shouldEnable = false;
            if (SettingsDirector.Instance != null)
            {
                shouldEnable = SettingsDirector.Instance.ShouldEnableTouchControls();
            }
            else
            {
                shouldEnable = Application.isMobilePlatform || UnityEngine.InputSystem.Touchscreen.current != null;
            }

            if (_controlsRoot != null)
            {
                _controlsRoot.SetActive(shouldEnable);
            }
        }

        private void Update()
        {
            if (!IsActive) return;

            ProcessTouchScreenInput();

            // Assemble virtual input frame
            var input = new PlayerInputState();

            if (_isVehicleMode)
            {
                input.Move = new Vector2(_vehicleSteer, _vehicleThrottle);
                input.Sprint = _vehicleNitro;
                input.Crouch = _vehicleHandbrake;
                input.Interact = _btnInteractHeld;
            }
            else
            {
                input.Move = _virtualMove;
                input.Sprint = _btnSprintHeld;
                input.Crouch = _btnCrouchHeld;
                input.Jump = _btnJumpTapped;
                input.Interact = _btnInteractHeld;
            }

            input.Look = _virtualLook;
            input.Fire = _btnFireTapped;
            input.FireHeld = _btnFireHeld;
            input.Aim = _btnAimHeld;
            input.Reload = _btnReloadTapped;
            input.AbilityPrimary = _btnAbilityPrimaryTapped;
            input.AbilityUltimate = _btnAbilityUltimateTapped;
            input.TogglePerspective = _btnTogglePerspectiveTapped;
            input.WeaponSlot = _btnWeaponSlotTapped;

            // Inject into InputManager
            if (InputManager.Instance != null)
            {
                InputManager.Instance.InjectVirtualInput(input);
            }

            // Reset single-frame taps
            _btnFireTapped = false;
            _btnJumpTapped = false;
            _btnReloadTapped = false;
            _btnAbilityPrimaryTapped = false;
            _btnAbilityUltimateTapped = false;
            _btnTogglePerspectiveTapped = false;
            _btnWeaponSlotTapped = 0;
            _virtualLook = Vector2.zero;
        }

        private void ProcessTouchScreenInput()
        {
            var ts = UnityEngine.InputSystem.Touchscreen.current;
            if (ts == null) return;

            float screenW = Screen.width;
            float screenH = Screen.height;

            for (int i = 0; i < ts.touches.Count; i++)
            {
                var touch = ts.touches[i];
                if (!touch.isInProgress)
                {
                    if (touch.touchId.ReadValue() == _joystickTouchId)
                    {
                        ResetJoystick();
                    }
                    else if (touch.touchId.ReadValue() == _lookTouchId)
                    {
                        _lookTouchId = -1;
                    }
                    continue;
                }

                int id = touch.touchId.ReadValue();
                Vector2 pos = touch.position.ReadValue();
                Vector2 delta = touch.delta.ReadValue();

                // 1. Left Half = Virtual Movement Joystick
                if (pos.x < screenW * 0.42f && pos.y < screenH * 0.65f)
                {
                    if (_joystickTouchId == -1 || _joystickTouchId == id)
                    {
                        if (_joystickTouchId == -1)
                        {
                            _joystickTouchId = id;
                            _joystickCenter = pos;
                            _joystickBase.position = pos;
                            _joystickBase.gameObject.SetActive(true);
                        }

                        Vector2 offset = pos - _joystickCenter;
                        Vector2 clampedOffset = Vector2.ClampMagnitude(offset, JoystickRadius);
                        _joystickThumb.anchoredPosition = clampedOffset;
                        _virtualMove = clampedOffset / JoystickRadius;
                    }
                }
                // 2. Right Half = Drag to Look (if not touching dedicated button)
                else if (pos.x >= screenW * 0.45f && pos.y > screenH * 0.25f)
                {
                    if (_lookTouchId == -1)
                    {
                        _lookTouchId = id;
                        _lastLookTouchPos = pos;
                    }
                    else if (_lookTouchId == id)
                    {
                        _virtualLook = delta * LookSensitivityTouch;
                        _lastLookTouchPos = pos;
                    }
                }
            }
        }

        private void ResetJoystick()
        {
            _joystickTouchId = -1;
            _virtualMove = Vector2.zero;
            if (_joystickThumb != null) _joystickThumb.anchoredPosition = Vector2.zero;
            if (_joystickBase != null)
            {
                _joystickBase.anchoredPosition = new Vector2(160f, 160f);
            }
        }

        // ==========================================
        // UI CONSTRUCTION (Procedural uGUI)
        // ==========================================

        private void BuildTouchUI()
        {
            var canvasGo = new GameObject("VirtualTouchCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 95; // Just below settings modal

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _controlsRoot = new GameObject("TouchControlsRoot", typeof(RectTransform));
            _controlsRoot.transform.SetParent(_canvas.transform, false);
            var rootRt = _controlsRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.sizeDelta = Vector2.zero;

            // 1. Virtual Joystick
            var joyBaseGo = CreateImagePanel(_controlsRoot.transform, "JoystickBase", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(180f, 180f), new Vector2(180f, 180f), new Color(0.2f, 0.25f, 0.3f, 0.35f));
            _joystickBase = joyBaseGo.GetComponent<RectTransform>();

            var joyThumbGo = CreateImagePanel(joyBaseGo.transform, "JoystickThumb", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(75f, 75f), new Color(0.9f, 0.95f, 1.0f, 0.7f));
            _joystickThumb = joyThumbGo.GetComponent<RectTransform>();

            // 2. Right-side Combat Action Buttons
            // [FIRE] Button
            CreateTouchButton(_controlsRoot.transform, "Btn_Fire", "FIRE", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-160f, 160f), new Vector2(130f, 130f), new Color(0.85f, 0.2f, 0.15f, 0.75f),
                onDown: () => { _btnFireHeld = true; _btnFireTapped = true; },
                onUp: () => { _btnFireHeld = false; });

            // [AIM / ADS] Button
            CreateTouchButton(_controlsRoot.transform, "Btn_Aim", "AIM", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-160f, 310f), new Vector2(90f, 90f), new Color(0.2f, 0.65f, 0.85f, 0.75f),
                onDown: () => { _btnAimHeld = !_btnAimHeld; }, // Toggle aim
                onUp: null);

            // [JUMP] Button
            CreateTouchButton(_controlsRoot.transform, "Btn_Jump", "JUMP", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-300f, 110f), new Vector2(90f, 90f), new Color(0.35f, 0.4f, 0.45f, 0.75f),
                onDown: () => { _btnJumpTapped = true; },
                onUp: null);

            // [CROUCH] Button
            CreateTouchButton(_controlsRoot.transform, "Btn_Crouch", "CROUCH", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-300f, 220f), new Vector2(90f, 90f), new Color(0.35f, 0.4f, 0.45f, 0.75f),
                onDown: () => { _btnCrouchHeld = !_btnCrouchHeld; }, // Toggle crouch
                onUp: null);

            // [RELOAD] Button
            CreateTouchButton(_controlsRoot.transform, "Btn_Reload", "RELOAD", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-280f, 330f), new Vector2(85f, 85f), new Color(0.9f, 0.7f, 0.15f, 0.75f),
                onDown: () => { _btnReloadTapped = true; },
                onUp: null);

            // [INTERACT / REVIVE] Button
            CreateTouchButton(_controlsRoot.transform, "Btn_Interact", "ACTION", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 180f), new Vector2(120f, 75f), new Color(0.15f, 0.75f, 0.45f, 0.8f),
                onDown: () => { _btnInteractHeld = true; },
                onUp: () => { _btnInteractHeld = false; });

            // [ABILITY 1 (Q)] Button
            CreateTouchButton(_controlsRoot.transform, "Btn_Ability1", "SKILL [Q]", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-120f, 95f), new Vector2(110f, 70f), new Color(0.2f, 0.5f, 0.85f, 0.8f),
                onDown: () => { _btnAbilityPrimaryTapped = true; },
                onUp: null);

            // [ULTIMATE (F)] Button
            CreateTouchButton(_controlsRoot.transform, "Btn_Ultimate", "ULT [F]", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(120f, 95f), new Vector2(110f, 70f), new Color(0.85f, 0.5f, 0.1f, 0.8f),
                onDown: () => { _btnAbilityUltimateTapped = true; },
                onUp: null);

            // [PERSPECTIVE] Button (Top Left)
            CreateTouchButton(_controlsRoot.transform, "Btn_View", "VIEW [T]", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(95f, -85f), new Vector2(95f, 50f), new Color(0.3f, 0.35f, 0.4f, 0.75f),
                onDown: () => { _btnTogglePerspectiveTapped = true; },
                onUp: null);

            // [SPRINT] Button (Left center)
            CreateTouchButton(_controlsRoot.transform, "Btn_Sprint", "SPRINT", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(95f, 320f), new Vector2(85f, 65f), new Color(0.35f, 0.55f, 0.45f, 0.75f),
                onDown: () => { _btnSprintHeld = !_btnSprintHeld; },
                onUp: null);

            // [WEAPON 1 / 2] Swap Buttons
            CreateTouchButton(_controlsRoot.transform, "Btn_Wep1", "PRIMARY", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-220f, -85f), new Vector2(100f, 50f), new Color(0.25f, 0.28f, 0.32f, 0.75f),
                onDown: () => { _btnWeaponSlotTapped = 1; },
                onUp: null);

            CreateTouchButton(_controlsRoot.transform, "Btn_Wep2", "SECONDARY", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-110f, -85f), new Vector2(100f, 50f), new Color(0.25f, 0.28f, 0.32f, 0.75f),
                onDown: () => { _btnWeaponSlotTapped = 2; },
                onUp: null);
        }

        private GameObject CreateImagePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color color)
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

        private GameObject CreateTouchButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color color, Action onDown, Action onUp)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(EventTrigger));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;

            // Label
            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            var txt = textGo.GetComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 15;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;

            // Event Trigger for PointerDown / PointerUp
            var trigger = go.GetComponent<EventTrigger>();
            if (onDown != null)
            {
                var entryDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                entryDown.callback.AddListener((_) =>
                {
                    img.color = new Color(color.r * 1.3f, color.g * 1.3f, color.b * 1.3f, 0.95f);
                    onDown();
                });
                trigger.triggers.Add(entryDown);
            }

            var entryUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            entryUp.callback.AddListener((_) =>
            {
                img.color = color;
                onUp?.Invoke();
            });
            trigger.triggers.Add(entryUp);

            return go;
        }
    }
}
