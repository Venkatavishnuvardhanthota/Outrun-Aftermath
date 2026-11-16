using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using OutrunAftermath.Client.Runtime.Character;
using OutrunAftermath.Client.Runtime.Combat;
using OutrunAftermath.Client.Runtime.CameraSystem;
using OutrunAftermath.Client.Runtime.Abilities;

namespace OutrunAftermath.Client.Runtime.UI
{
    /// <summary>
    /// Tactical in-game HUD for OUTRUN: AFTERMATH.
    /// Provides dynamic crosshair spread, hitmarkers, health/armor gauges, ammo counter,
    /// compass bearing, downed state alerts, operator kits, ability cooldowns, and tactical objectives.
    /// </summary>
    public sealed class TacticalHUD : MonoBehaviour
    {
        private PlayerHealth _health;
        private WeaponSystem _weapons;
        private CameraRig _cameraRig;
        private OperatorAbilitySystem _abilitySystem;

        // UI Canvas and Root Panels
        private Canvas _canvas;
        private RectTransform _crosshairRoot;
        private Image[] _crosshairTicks; // 0: Top, 1: Bottom, 2: Left, 3: Right
        private Image _centerDot;
        private RectTransform _hitMarkerRoot;
        private Image[] _hitMarkerLines;
        private float _hitMarkerTimer;

        // Health & Armor & Operator
        private Slider _healthBar;
        private Slider _armorBar;
        private Text _healthText;
        private Text _armorText;
        private Text _operatorBadgeText;

        // Tactical Abilities (Bottom Center)
        private Text _primaryKeyText;
        private Text _primaryNameText;
        private Text _primaryCdText;
        private Text _ultimateKeyText;
        private Text _ultimateNameText;
        private Text _ultimateCdText;
        private Slider _ultimateBar;

        // Objective Tracker (Top Center)
        private GameObject _objectivePanel;
        private Text _objectiveTitleText;
        private Text _objectiveStatusText;
        private Slider _objectiveSlider;

        // Vehicle Telemetry & Crew Panel
        private GameObject _vehiclePanel;
        private Text _vehicleSpeedText;
        private Text _vehicleHealthText;
        private Slider _vehicleNitroBar;

        // Weapon & Ammo
        private Text _weaponNameText;
        private Text _ammoText;
        private Text _fireModeText;
        private Slider _reloadBar;

        // Tactical Status & Compass
        private Text _compassText;
        private Text _perspectiveBadge;
        private Text _networkBadge;
        private Text _missionText;
        private Text _killFeedText;
        private readonly System.Collections.Generic.List<string> _feedLines = new System.Collections.Generic.List<string>();
        private GameObject _controlsPanel;
        private bool _controlsVisible = true;

        // Downed Overlay
        private GameObject _downedOverlay;
        private Text _downedTimerText;
        private Slider _reviveProgressSlider;

        public void Initialize(PlayerHealth health, WeaponSystem weapons, CameraRig cameraRig)
        {
            _health = health;
            _weapons = weapons;
            _cameraRig = cameraRig;

            BuildTacticalUI();

            // Wire up callbacks
            if (_health != null)
            {
                _health.OnHealthArmorChanged += UpdateHealthArmorDisplay;
                _health.OnDowned += HandleDowned;
                _health.OnRevived += HandleRevived;
                _health.OnEliminated += HandleEliminated;
            }

            if (_weapons != null)
            {
                _weapons.OnAmmoChanged += UpdateAmmoDisplay;
                _weapons.OnWeaponSwitched += UpdateWeaponDisplay;
                _weapons.OnHitConfirmed += TriggerHitMarker;
            }

            UpdateHealthArmorDisplay(_health.CurrentHealth, _health.CurrentArmor);
            if (_weapons.CurrentWeapon != null)
            {
                UpdateWeaponDisplay(_weapons.CurrentWeapon);
            }
        }

        private void BuildTacticalUI()
        {
            // Canvas
            var canvasGo = new GameObject("TacticalHUD_Canvas");
            canvasGo.transform.SetParent(transform);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                               Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 1. Dynamic Crosshair
            var crosshairGo = new GameObject("CrosshairRoot", typeof(RectTransform));
            crosshairGo.transform.SetParent(canvasGo.transform, false);
            _crosshairRoot = crosshairGo.GetComponent<RectTransform>();
            _crosshairRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _crosshairRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _crosshairRoot.sizeDelta = new Vector2(100f, 100f);

            // Center Dot
            var dotGo = new GameObject("CenterDot", typeof(Image));
            dotGo.transform.SetParent(_crosshairRoot, false);
            _centerDot = dotGo.GetComponent<Image>();
            _centerDot.color = new Color(1f, 1f, 1f, 0.75f);
            dotGo.GetComponent<RectTransform>().sizeDelta = new Vector2(3f, 3f);

            // Crosshair Ticks (Top, Bottom, Left, Right)
            _crosshairTicks = new Image[4];
            Vector2[] tickSizes = { new Vector2(2f, 10f), new Vector2(2f, 10f), new Vector2(10f, 2f), new Vector2(10f, 2f) };
            for (int i = 0; i < 4; i++)
            {
                var tickGo = new GameObject($"Tick_{i}", typeof(Image));
                tickGo.transform.SetParent(_crosshairRoot, false);
                _crosshairTicks[i] = tickGo.GetComponent<Image>();
                _crosshairTicks[i].color = new Color(0.95f, 0.95f, 0.95f, 0.85f);
                tickGo.GetComponent<RectTransform>().sizeDelta = tickSizes[i];
            }

            // Hit Marker Root
            var hitMarkerGo = new GameObject("HitMarkerRoot", typeof(RectTransform));
            hitMarkerGo.transform.SetParent(_crosshairRoot, false);
            _hitMarkerRoot = hitMarkerGo.GetComponent<RectTransform>();
            _hitMarkerRoot.sizeDelta = new Vector2(30f, 30f);

            _hitMarkerLines = new Image[4];
            float[] angles = { 45f, 135f, 225f, 315f };
            for (int i = 0; i < 4; i++)
            {
                var hLine = new GameObject($"HitLine_{i}", typeof(Image));
                hLine.transform.SetParent(_hitMarkerRoot, false);
                _hitMarkerLines[i] = hLine.GetComponent<Image>();
                _hitMarkerLines[i].color = new Color(1f, 0.2f, 0.2f, 0f);
                var rt = hLine.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(2f, 8f);
                rt.localRotation = Quaternion.Euler(0f, 0f, angles[i]);
                rt.localPosition = Quaternion.Euler(0f, 0f, angles[i]) * new Vector3(0f, 9f, 0f);
            }

            // 2. Health & Armor Gauges (Bottom Left)
            var healthPanel = CreatePanel(canvasGo.transform, "HealthPanel", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(320f, 135f), new Color(0.08f, 0.1f, 0.12f, 0.75f));

            // Operator Badge
            _operatorBadgeText = CreateText(healthPanel.transform, "OperatorBadge", "[REX // ASSAULT]", defaultFont, 14, TextAnchor.MiddleLeft, new Vector2(24f, 110f), new Vector2(270f, 20f), new Color(0.95f, 0.6f, 0.2f));

            // Armor Bar
            var armorGo = CreateSlider(healthPanel.transform, "ArmorBar", new Vector2(20f, 65f), new Vector2(280f, 16f), new Color(0.15f, 0.65f, 0.95f));
            _armorBar = armorGo.GetComponent<Slider>();
            _armorText = CreateText(healthPanel.transform, "ArmorText", "ARMOR: 50 / 50", defaultFont, 14, TextAnchor.MiddleLeft, new Vector2(24f, 88f), new Vector2(200f, 20f), Color.cyan);

            // Health Bar
            var hpGo = CreateSlider(healthPanel.transform, "HealthBar", new Vector2(20f, 25f), new Vector2(280f, 22f), new Color(0.85f, 0.22f, 0.2f));
            _healthBar = hpGo.GetComponent<Slider>();
            _healthText = CreateText(healthPanel.transform, "HealthText", "HEALTH: 100 / 100", defaultFont, 15, TextAnchor.MiddleLeft, new Vector2(24f, 48f), new Vector2(200f, 20f), Color.white);

            // 3. Tactical Abilities Panel (Bottom Center)
            var abilityPanel = CreatePanel(canvasGo.transform, "AbilityPanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(360f, 90f), new Color(0.06f, 0.08f, 0.11f, 0.8f));

            // Primary Ability [Q]
            var primaryBox = CreatePanel(abilityPanel.transform, "PrimarySlot", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(165f, 75f), new Color(0.12f, 0.15f, 0.18f, 0.9f));
            _primaryKeyText = CreateText(primaryBox.transform, "Key", "[Q] TACTICAL", defaultFont, 12, TextAnchor.UpperLeft, new Vector2(10f, -6f), new Vector2(145f, 18f), Color.yellow);
            _primaryNameText = CreateText(primaryBox.transform, "Name", "FRAG GRENADE", defaultFont, 13, TextAnchor.MiddleLeft, new Vector2(10f, -4f), new Vector2(145f, 22f), Color.white);
            _primaryCdText = CreateText(primaryBox.transform, "Cooldown", "READY", defaultFont, 14, TextAnchor.LowerRight, new Vector2(-10f, 8f), new Vector2(145f, 22f), Color.green);

            // Ultimate Ability [F]
            var ultBox = CreatePanel(abilityPanel.transform, "UltimateSlot", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(165f, 75f), new Color(0.12f, 0.15f, 0.18f, 0.9f));
            _ultimateKeyText = CreateText(ultBox.transform, "Key", "[F] ULTIMATE", defaultFont, 12, TextAnchor.UpperLeft, new Vector2(10f, -6f), new Vector2(145f, 18f), new Color(0.95f, 0.65f, 0.2f));
            _ultimateNameText = CreateText(ultBox.transform, "Name", "OVERRUN", defaultFont, 13, TextAnchor.MiddleLeft, new Vector2(10f, -4f), new Vector2(145f, 22f), Color.white);
            _ultimateCdText = CreateText(ultBox.transform, "Cooldown", "READY (100%)", defaultFont, 13, TextAnchor.LowerRight, new Vector2(-10f, 8f), new Vector2(145f, 22f), Color.yellow);
            var uBarGo = CreateSlider(ultBox.transform, "UltBar", new Vector2(10f, 2f), new Vector2(145f, 5f), new Color(1f, 0.75f, 0.15f));
            _ultimateBar = uBarGo.GetComponent<Slider>();

            // 3. Weapon & Ammo Counter (Bottom Right)
            var weaponPanel = CreatePanel(canvasGo.transform, "WeaponPanel", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(300f, 110f), new Color(0.08f, 0.1f, 0.12f, 0.75f));
            _weaponNameText = CreateText(weaponPanel.transform, "WeaponName", "VANGUARD-4", defaultFont, 16, TextAnchor.MiddleRight, new Vector2(-20f, 75f), new Vector2(260f, 24f), Color.white);
            _ammoText = CreateText(weaponPanel.transform, "AmmoText", "30 / 120", defaultFont, 32, TextAnchor.MiddleRight, new Vector2(-20f, 35f), new Vector2(200f, 40f), new Color(1f, 0.85f, 0.25f));
            _fireModeText = CreateText(weaponPanel.transform, "FireMode", "[AUTO]", defaultFont, 14, TextAnchor.MiddleLeft, new Vector2(20f, 35f), new Vector2(80f, 24f), Color.gray);

            // Reload Bar
            var reloadGo = CreateSlider(weaponPanel.transform, "ReloadBar", new Vector2(20f, 10f), new Vector2(260f, 8f), new Color(1f, 0.8f, 0.2f));
            _reloadBar = reloadGo.GetComponent<Slider>();
            _reloadBar.gameObject.SetActive(false);

            // 3B. Vehicle Telemetry Panel (Bottom Right above Weapon Panel)
            _vehiclePanel = CreatePanel(canvasGo.transform, "VehiclePanel", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 160f), new Vector2(300f, 95f), new Color(0.08f, 0.1f, 0.14f, 0.85f));
            _vehicleSpeedText = CreateText(_vehiclePanel.transform, "VehSpeed", "0 KM/H", defaultFont, 24, TextAnchor.MiddleRight, new Vector2(-20f, 25f), new Vector2(140f, 32f), new Color(0.2f, 0.9f, 1f));
            _vehicleHealthText = CreateText(_vehiclePanel.transform, "VehHealth", "HULL: 900 / 900\nENG: 220 | TIRE: 120", defaultFont, 11, TextAnchor.MiddleLeft, new Vector2(15f, 25f), new Vector2(160f, 30f), Color.white);
            var nitroBarGo = CreateSlider(_vehiclePanel.transform, "VehNitro", new Vector2(15f, 10f), new Vector2(270f, 8f), new Color(1f, 0.45f, 0.1f));
            _vehicleNitroBar = nitroBarGo.GetComponent<Slider>();
            _vehiclePanel.SetActive(false);

            // 4. Compass & Mission Header (Top Center)
            var topPanel = CreatePanel(canvasGo.transform, "TopHeader", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(500f, 60f), new Color(0.06f, 0.08f, 0.1f, 0.65f));
            _compassText = CreateText(topPanel.transform, "CompassText", "000° N", defaultFont, 18, TextAnchor.MiddleCenter, new Vector2(0f, 15f), new Vector2(300f, 24f), Color.white);
            _missionText = CreateText(topPanel.transform, "MissionText", "SECTOR: DEAD CITY | RECON & COMBAT EXTRACTION", defaultFont, 13, TextAnchor.MiddleCenter, new Vector2(0f, -12f), new Vector2(480f, 20f), new Color(0.7f, 0.85f, 0.9f));

            // 4B. Tactical Objective Tracker (Top Center below Header)
            _objectivePanel = CreatePanel(canvasGo.transform, "ObjectiveTracker", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(440f, 34f), new Color(0.08f, 0.1f, 0.14f, 0.85f));
            _objectiveTitleText = CreateText(_objectivePanel.transform, "ObjTitle", "DATA UPLINK", defaultFont, 12, TextAnchor.MiddleLeft, new Vector2(14f, 0f), new Vector2(120f, 24f), Color.cyan);
            _objectiveStatusText = CreateText(_objectivePanel.transform, "ObjStatus", "0% (INACTIVE)", defaultFont, 12, TextAnchor.MiddleRight, new Vector2(-14f, 0f), new Vector2(150f, 24f), Color.white);
            var objBarGo = CreateSlider(_objectivePanel.transform, "ObjSlider", new Vector2(130f, 11f), new Vector2(140f, 12f), new Color(0.15f, 0.85f, 0.95f));
            _objectiveSlider = objBarGo.GetComponent<Slider>();
            _objectiveSlider.value = 0f;

            // Perspective badge (Top Left)
            _perspectiveBadge = CreateText(canvasGo.transform, "PerspectiveBadge", "[FIRST PERSON : T TO TOGGLE]", defaultFont, 14, TextAnchor.MiddleLeft, new Vector2(180f, -30f), new Vector2(300f, 25f), Color.yellow);
            var pRect = _perspectiveBadge.GetComponent<RectTransform>();
            pRect.anchorMin = new Vector2(0f, 1f);
            pRect.anchorMax = new Vector2(0f, 1f);

            // Network & Faction Badge (Top Left below Perspective)
            _networkBadge = CreateText(canvasGo.transform, "NetworkBadge", "[OFFLINE PRACTICE]", defaultFont, 13, TextAnchor.MiddleLeft, new Vector2(180f, -56f), new Vector2(320f, 24f), Color.cyan);
            var nRect = _networkBadge.GetComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0f, 1f);
            nRect.anchorMax = new Vector2(0f, 1f);

            // Kill / Event Feed (Top Left below Network Badge)
            _killFeedText = CreateText(canvasGo.transform, "KillFeed", "", defaultFont, 13, TextAnchor.UpperLeft, new Vector2(210f, -150f), new Vector2(380f, 140f), new Color(0.95f, 0.95f, 0.95f, 0.9f));
            var kfRect = _killFeedText.GetComponent<RectTransform>();
            kfRect.anchorMin = new Vector2(0f, 1f);
            kfRect.anchorMax = new Vector2(0f, 1f);

            // 5. Controls Helper (Top Right)
            _controlsPanel = CreatePanel(canvasGo.transform, "ControlsPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(280f, 230f), new Color(0.05f, 0.06f, 0.08f, 0.85f));
            CreateText(_controlsPanel.transform, "ControlsTitle", "TACTICAL CONTROLS (F1/H)", defaultFont, 14, TextAnchor.UpperCenter, new Vector2(0f, -10f), new Vector2(260f, 20f), Color.cyan);
            string controls = "WASD - Move / Drive Vehicle\nSHIFT - Sprint / Nitro | C - Crouch/Slide\nSPACE - Jump / Handbrake Drift\nLEFT CLICK - Fire / Mounted Turret\nQ - Tactical Ability | F - Ultimate Ability\nT - Toggle Perspective (FPS / TPS)\n1-4 - Switch Seat | X - Dismount\nE - Interact / Revive / Board / Hack\nESC/F2 - Settings & Sensitivity";
            CreateText(_controlsPanel.transform, "ControlsBody", controls, defaultFont, 12, TextAnchor.UpperLeft, new Vector2(15f, -35f), new Vector2(250f, 185f), Color.white);

            // 5B. Settings Button (Top Right above Controls)
            var settingsBtnGo = new GameObject("Btn_SettingsMenu", typeof(RectTransform), typeof(Image), typeof(Button));
            settingsBtnGo.transform.SetParent(canvasGo.transform, false);
            var sBtnRt = settingsBtnGo.GetComponent<RectTransform>();
            sBtnRt.anchorMin = new Vector2(1f, 1f);
            sBtnRt.anchorMax = new Vector2(1f, 1f);
            sBtnRt.anchoredPosition = new Vector2(-160f, -18f);
            sBtnRt.sizeDelta = new Vector2(120f, 26f);
            settingsBtnGo.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.28f, 0.85f);
            var sBtn = settingsBtnGo.GetComponent<Button>();
            sBtn.onClick.AddListener(() =>
            {
                if (SettingsModalUI.Instance != null)
                {
                    SettingsModalUI.Instance.ToggleOpen();
                }
            });
            CreateText(settingsBtnGo.transform, "SettingsLabel", "SETTINGS [F2]", defaultFont, 11, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(120f, 26f), Color.white);

            // 6. Downed / Bleed-Out Overlay
            _downedOverlay = CreatePanel(canvasGo.transform, "DownedOverlay", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 180f), new Color(0.4f, 0.05f, 0.05f, 0.85f));
            CreateText(_downedOverlay.transform, "DownedAlert", "WARNING: OPERATOR INCAPACITATED", defaultFont, 24, TextAnchor.MiddleCenter, new Vector2(0f, 45f), new Vector2(460f, 40f), Color.red);
            _downedTimerText = CreateText(_downedOverlay.transform, "DownedTimer", "BLEED-OUT: 30s", defaultFont, 18, TextAnchor.MiddleCenter, new Vector2(0f, 10f), new Vector2(300f, 30f), Color.white);
            var rGo = CreateSlider(_downedOverlay.transform, "ReviveSlider", new Vector2(50f, -40f), new Vector2(400f, 20f), Color.green);
            _reviveProgressSlider = rGo.GetComponent<Slider>();
            CreateText(_downedOverlay.transform, "ReviveHint", "HOLD [E] TO SELF-REVIVE OR WAIT FOR ALLY", defaultFont, 13, TextAnchor.MiddleCenter, new Vector2(0f, -65f), new Vector2(400f, 20f), Color.yellow);
            _downedOverlay.SetActive(false);
        }

        private void Update()
        {
            // Update Dynamic Crosshair Gap based on spread
            if (_crosshairRoot != null && _weapons != null)
            {
                float spreadPixels = Mathf.Clamp(_weapons.CurrentSpread * 9f, 10f, 85f);
                _crosshairTicks[0].rectTransform.localPosition = new Vector3(0f, spreadPixels, 0f);
                _crosshairTicks[1].rectTransform.localPosition = new Vector3(0f, -spreadPixels, 0f);
                _crosshairTicks[2].rectTransform.localPosition = new Vector3(-spreadPixels, 0f, 0f);
                _crosshairTicks[3].rectTransform.localPosition = new Vector3(spreadPixels, 0f, 0f);
            }

            // Update Reload Progress
            if (_weapons != null && _reloadBar != null)
            {
                if (_weapons.IsReloading)
                {
                    _reloadBar.gameObject.SetActive(true);
                    _reloadBar.value = _weapons.ReloadProgress;
                }
                else
                {
                    _reloadBar.gameObject.SetActive(false);
                }
            }

            // Update Hit Marker Fade
            if (_hitMarkerTimer > 0f)
            {
                _hitMarkerTimer -= Time.deltaTime;
                float alpha = Mathf.Clamp01(_hitMarkerTimer / 0.18f);
                foreach (var line in _hitMarkerLines)
                {
                    Color c = line.color;
                    c.a = alpha;
                    line.color = c;
                }
            }

            // Update Compass
            if (_cameraRig != null && _compassText != null)
            {
                float deg = (_cameraRig.Yaw % 360f + 360f) % 360f;
                string dir = "N";
                if (deg >= 22.5f && deg < 67.5f) dir = "NE";
                else if (deg >= 67.5f && deg < 112.5f) dir = "E";
                else if (deg >= 112.5f && deg < 157.5f) dir = "SE";
                else if (deg >= 157.5f && deg < 202.5f) dir = "S";
                else if (deg >= 202.5f && deg < 247.5f) dir = "SW";
                else if (deg >= 247.5f && deg < 292.5f) dir = "W";
                else if (deg >= 292.5f && deg < 337.5f) dir = "NW";

                _compassText.text = $"{Mathf.RoundToInt(deg):000}° {dir}";
                _perspectiveBadge.text = $"[{(_cameraRig.Perspective == CameraPerspective.FirstPerson ? "FPS" : "TPS")} : T TO TOGGLE]";
            }

            // Update Downed overlay timer
            if (_health != null && (_health.State == PlayerLifeState.Downed || _health.State == PlayerLifeState.BeingRevived))
            {
                _downedTimerText.text = $"BLEED-OUT IN: {Mathf.CeilToInt(_health.BleedOutRemaining)}s";
                if (_reviveProgressSlider != null)
                {
                    _reviveProgressSlider.value = _health.ReviveProgress;
                }
            }

            // Update Tactical Abilities HUD
            if (_abilitySystem != null && _abilitySystem.Definition != null)
            {
                if (_operatorBadgeText != null)
                {
                    _operatorBadgeText.text = $"[{_abilitySystem.Definition.Codename} // {_abilitySystem.Definition.Role.ToUpper()}]";
                }

                if (_primaryNameText != null && _abilitySystem.PrimaryAbility != null)
                {
                    _primaryNameText.text = _abilitySystem.PrimaryAbility.Id.ToUpper();
                    float cd = _abilitySystem.ClientPrimaryCd;
                    if (cd > 0.05f)
                    {
                        _primaryCdText.text = $"{cd:F1}s";
                        _primaryCdText.color = Color.red;
                    }
                    else
                    {
                        _primaryCdText.text = "READY";
                        _primaryCdText.color = Color.green;
                    }
                }

                if (_ultimateNameText != null && _abilitySystem.UltimateAbility != null)
                {
                    _ultimateNameText.text = _abilitySystem.UltimateAbility.Id.ToUpper();
                    float ultCd = _abilitySystem.ClientUltimateCd;
                    float charge = _abilitySystem.ClientUltimateCharge;
                    if (_ultimateBar != null) _ultimateBar.value = charge / 100f;

                    if (ultCd > 0.05f)
                    {
                        _ultimateCdText.text = $"{ultCd:F0}s";
                        _ultimateCdText.color = Color.red;
                    }
                    else if (charge >= 100f)
                    {
                        _ultimateCdText.text = "READY (100%)";
                        _ultimateCdText.color = Color.yellow;
                    }
                    else
                    {
                        _ultimateCdText.text = $"{Mathf.FloorToInt(charge)}%";
                        _ultimateCdText.color = Color.cyan;
                    }
                }
            }

            // Toggle help panel
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                (UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame ||
                 UnityEngine.InputSystem.Keyboard.current.hKey.wasPressedThisFrame))
            {
                _controlsVisible = !_controlsVisible;
                if (_controlsPanel != null) _controlsPanel.SetActive(_controlsVisible);
            }

            // Vehicle Telemetry
            var currentVeh = FindLocalVehicle();
            if (currentVeh != null)
            {
                if (_vehiclePanel != null) _vehiclePanel.SetActive(true);
                if (_vehicleSpeedText != null)
                {
                    int kmh = Mathf.RoundToInt(currentVeh.NetSpeedMps.Value * 3.6f);
                    _vehicleSpeedText.text = $"{kmh} KM/H";
                }
                if (_vehicleNitroBar != null)
                {
                    _vehicleNitroBar.value = currentVeh.NetNitroFuel.Value / 100f;
                }
                if (_vehicleHealthText != null)
                {
                    _vehicleHealthText.text = $"HULL: {Mathf.CeilToInt(currentVeh.NetBodyHealth.Value)} / {currentVeh.Definition.BodyHealth}\nENG: {Mathf.CeilToInt(currentVeh.NetEngineHealth.Value)} | TIRE: {Mathf.CeilToInt(currentVeh.NetTireHealth.Value)}";
                }
            }
            else
            {
                if (_vehiclePanel != null) _vehiclePanel.SetActive(false);
            }
        }

        private Runtime.Vehicles.NetworkVehicleController FindLocalVehicle()
        {
            if (Camera.main != null)
            {
                var veh = Camera.main.GetComponentInParent<Runtime.Vehicles.NetworkVehicleController>();
                if (veh != null) return veh;
            }
            var player = Unity.Netcode.NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (player != null)
            {
                return player.GetComponentInParent<Runtime.Vehicles.NetworkVehicleController>();
            }
            return null;
        }

        private void TriggerHitMarker(bool hit, bool headshot)
        {
            _hitMarkerTimer = 0.22f;
            Color markerColor = headshot ? new Color(1f, 0.15f, 0.15f, 1f) : new Color(1f, 1f, 1f, 0.95f);
            foreach (var line in _hitMarkerLines)
            {
                line.color = markerColor;
            }
        }

        public void UpdateHealthArmorDisplay(float health, float armor)
        {
            if (_healthBar != null) _healthBar.value = health / 100f;
            if (_armorBar != null) _armorBar.value = armor / 50f;
            if (_healthText != null) _healthText.text = $"HEALTH: {Mathf.CeilToInt(health)} / 100";
            if (_armorText != null) _armorText.text = $"ARMOR: {Mathf.CeilToInt(armor)} / 50";
        }

        public void UpdateWeaponDisplay(WeaponData weapon)
        {
            if (weapon == null) return;
            if (_weaponNameText != null) _weaponNameText.text = weapon.displayName.ToUpper();
            if (_fireModeText != null) _fireModeText.text = $"[{weapon.fireMode.ToString().ToUpper()}]";
            UpdateAmmoDisplay(weapon.currentMagazine, weapon.reserveAmmo);
        }

        public void UpdateAmmoDisplay(int current, int reserve)
        {
            if (_ammoText != null) _ammoText.text = $"{current} / {reserve}";
        }

        public void SetMissionStatus(string statusText)
        {
            if (_missionText != null) _missionText.text = statusText;
        }

        public void SetNetworkStatus(string badgeText)
        {
            if (_networkBadge != null) _networkBadge.text = badgeText;
        }

        public void BindAbilitySystem(OperatorAbilitySystem abilitySystem)
        {
            _abilitySystem = abilitySystem;
        }

        public void SetOperatorInfo(string codename, string role)
        {
            if (_operatorBadgeText != null)
            {
                _operatorBadgeText.text = $"[{codename.ToUpper()} // {role.ToUpper()}]";
            }
        }

        public void UpdateObjectiveStatus(float progress, bool isContested, bool isHacking, Vector3 terminalPos)
        {
            if (_objectiveSlider != null) _objectiveSlider.value = progress / 100f;
            if (_objectiveStatusText != null)
            {
                if (progress >= 100f)
                {
                    _objectiveStatusText.text = "UPLINK SECURED";
                    _objectiveStatusText.color = Color.green;
                }
                else if (isContested)
                {
                    _objectiveStatusText.text = $"CONTESTED! [{Mathf.FloorToInt(progress)}%]";
                    _objectiveStatusText.color = new Color(1f, 0.4f, 0.1f);
                }
                else if (isHacking)
                {
                    _objectiveStatusText.text = $"TRANSMITTING: {Mathf.FloorToInt(progress)}%";
                    _objectiveStatusText.color = Color.cyan;
                }
                else
                {
                    float dist = Camera.main != null ? Vector3.Distance(Camera.main.transform.position, terminalPos) : 0f;
                    _objectiveStatusText.text = $"{Mathf.FloorToInt(progress)}% ({Mathf.CeilToInt(dist)}m)";
                    _objectiveStatusText.color = Color.white;
                }
            }
        }

        public void TriggerAbilityVisual(string abilityId)
        {
            AddKillFeedMessage($"[ABILITY] {abilityId.ToUpper()} DEPLOYED");
        }

        public void AddKillFeedMessage(string message)
        {
            _feedLines.Add(message);
            if (_feedLines.Count > 5)
            {
                _feedLines.RemoveAt(0);
            }

            if (_killFeedText != null)
            {
                _killFeedText.text = string.Join("\n", _feedLines);
            }
        }

        private void HandleDowned()
        {
            if (_downedOverlay != null) _downedOverlay.SetActive(true);
        }

        private void HandleRevived()
        {
            if (_downedOverlay != null) _downedOverlay.SetActive(false);
        }

        private void HandleEliminated()
        {
            if (_downedOverlay != null) _downedOverlay.SetActive(false);
            if (_healthText != null) _healthText.text = "OPERATOR ELIMINATED - RESPAWNING...";
        }

        // --- uGUI Procedural Helper Utilities ---
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

        private Text CreateText(Transform parent, string name, string content, Font font, int fontSize, TextAnchor alignment, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var txt = go.GetComponent<Text>();
            txt.text = content;
            txt.font = font;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.color = color;
            return txt;
        }

        private GameObject CreateSlider(Transform parent, string name, Vector2 pos, Vector2 size, Color fillColor)
        {
            var sliderGo = new GameObject(name, typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(parent, false);
            var rt = sliderGo.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(sliderGo.transform, false);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.16f, 0.8f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            var faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = Vector2.zero;
            faRt.anchorMax = Vector2.one;
            faRt.sizeDelta = Vector2.zero;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = Vector2.zero;
            fill.GetComponent<Image>().color = fillColor;

            var slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = fillRt;
            slider.targetGraphic = bg.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            return sliderGo;
        }
    }
}
