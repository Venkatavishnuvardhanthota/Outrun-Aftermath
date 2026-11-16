using UnityEngine;
using OutrunAftermath.Client.Runtime.Input;
using OutrunAftermath.Client.Runtime.CameraSystem;
using OutrunAftermath.Client.Runtime.Character;
using OutrunAftermath.Client.Runtime.Combat;
using OutrunAftermath.Client.Runtime.UI;
using OutrunAftermath.Client.Runtime.Environment;
using OutrunAftermath.Client.Runtime.Abilities;
using OutrunAftermath.Client.Multiplayer;

namespace OutrunAftermath.Client
{
    /// <summary>
    /// Master Game Bootstrap for OUTRUN: AFTERMATH.
    /// Orchestrates environment construction and wires Offline Solo Practice vs
    /// Netcode Multiplayer Session startup.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Startup Configuration")]
        [SerializeField] private bool buildEnvironmentOnStart = true;
        [SerializeField] private CameraPerspective defaultPerspective = CameraPerspective.FirstPerson;
        [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 1f, -15f);

        private GameObject _playerRoot;
        private CharacterMotor _motor;
        private PlayerHealth _health;
        private WeaponSystem _weapons;
        private CameraRig _cameraRig;
        private TacticalHUD _hud;
        private NetworkSessionController _sessionController;

        private void Awake()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 1;

            InitializeGame();
        }

        private void OnDestroy()
        {
            if (_sessionController != null)
            {
                _sessionController.OnOfflinePracticeRequested -= HandleOfflinePracticeRequested;
            }
        }

        private void InitializeGame()
        {
            Debug.Log("[OUTRUN: AFTERMATH] Bootstrapping Core Engine...");

            // 0. Ensure GameApiClient is initialized
            if (FindAnyObjectByType<Runtime.Backend.GameApiClient>() == null)
            {
                var apiGo = new GameObject("GameApiClient");
                apiGo.AddComponent<Runtime.Backend.GameApiClient>();
            }

            // Phase 7: Ensure AudioManager, AtmosphereDirector, and MapEnvironmentDirector
            if (Runtime.Audio.AudioManager.Instance == null)
            {
                var audioGo = new GameObject("AudioManager");
                audioGo.AddComponent<Runtime.Audio.AudioManager>();
            }
            if (Runtime.Environment.AtmosphereDirector.Instance == null)
            {
                var atmoGo = new GameObject("AtmosphereDirector");
                atmoGo.AddComponent<Runtime.Environment.AtmosphereDirector>();
            }
            if (Runtime.Environment.MapEnvironmentDirector.Instance == null)
            {
                var mapGo = new GameObject("MapEnvironmentDirector");
                mapGo.AddComponent<Runtime.Environment.MapEnvironmentDirector>();
            }

            // 1. Build greybox environment if needed
            if (buildEnvironmentOnStart && GameObject.Find("DeadCity_Sector4") == null)
            {
                Runtime.Environment.MapEnvironmentDirector.Instance.LoadMap("deadcity", Runtime.Environment.WeatherCondition.Clear);
            }

            // 2. Ensure Input Manager
            if (FindAnyObjectByType<InputManager>() == null)
            {
                var inputGo = new GameObject("InputManager");
                inputGo.AddComponent<InputManager>();
            }

            // 3. Defer player spawning to NetworkSessionController if present in scene
            _sessionController = FindAnyObjectByType<NetworkSessionController>();
            if (_sessionController != null)
            {
                _sessionController.OnOfflinePracticeRequested += HandleOfflinePracticeRequested;
                Debug.Log("[OUTRUN: AFTERMATH] NetworkSessionController detected. Awaiting deployment mode...");
            }
            else
            {
                // Fallback for isolated offline scenes without NGO
                HandleOfflinePracticeRequested();
            }
        }

        private void HandleOfflinePracticeRequested()
        {
            if (_playerRoot != null) return;

            SetupPlayer();
            SetupHUD();
            _hud.SetNetworkStatus("[OFFLINE PRACTICE]");
            _hud.SetMissionStatus("DEAD CITY SECTOR 4 — SOLO TRAINING");

            if (FindAnyObjectByType<Runtime.Vehicles.NetworkVehicleController>() == null)
            {
                var vehGo = new GameObject("Practice_WastelandRaptor");
                vehGo.transform.position = new Vector3(4.5f, 0.45f, -10f);
                vehGo.AddComponent<Runtime.Vehicles.NetworkVehicleController>();
            }

            Debug.Log("[OUTRUN: AFTERMATH] Offline Solo Practice Systems Online. Player Ready.");
        }

        private void SetupPlayer()
        {
            _playerRoot = new GameObject("LocalPlayer");
            _playerRoot.transform.position = spawnPosition;
            _playerRoot.transform.rotation = Quaternion.identity;

            // Character Controller & Motor
            var cc = _playerRoot.AddComponent<CharacterController>();
            _motor = _playerRoot.AddComponent<CharacterMotor>();
            _health = _playerRoot.AddComponent<PlayerHealth>();

            // Setup Camera Rig
            var camRigGo = new GameObject("PlayerCameraRig");
            _cameraRig = camRigGo.AddComponent<CameraRig>();
            _cameraRig.Initialize(_playerRoot.transform);

            if (defaultPerspective == CameraPerspective.ThirdPerson)
            {
                _cameraRig.TogglePerspective();
            }

            _motor.Initialize(_cameraRig);

            // Setup Weapon System
            _weapons = _playerRoot.AddComponent<WeaponSystem>();
            _weapons.Initialize(_cameraRig, _motor, _health);

            // Setup Operator Abilities
            var abilitySys = _playerRoot.AddComponent<OperatorAbilitySystem>();
            abilitySys.SetOperator("rex");

            // Wire up health to motor stance
            _health.OnDowned += () =>
            {
                _motor.SetDownedStance(true);
            };

            _health.OnRevived += () =>
            {
                _motor.SetDownedStance(false);
            };

            _health.OnEliminated += () =>
            {
                _motor.SetDownedStance(false);
            };
        }

        private void SetupHUD()
        {
            var hudGo = new GameObject("TacticalHUD");
            _hud = hudGo.AddComponent<TacticalHUD>();
            _hud.Initialize(_health, _weapons, _cameraRig);
            var abilitySys = _playerRoot.GetComponent<OperatorAbilitySystem>();
            if (abilitySys != null)
            {
                _hud.BindAbilitySystem(abilitySys);
            }
        }

        private void Update()
        {
            // Only drive local offline player here; networked owners are driven by NetworkPlayerController.OwnerTick()
            if (_playerRoot == null || InputManager.Instance == null) return;

            var input = InputManager.Instance.Current;

            // Perspective toggle (T or V)
            if (input.TogglePerspective && _cameraRig != null)
            {
                _cameraRig.TogglePerspective();
            }

            // Aim look rotation
            if (_cameraRig != null)
            {
                _cameraRig.AddLookInput(input.Look);
                _cameraRig.SetAiming(input.Aim);
            }

            // Self-revive interaction when downed
            if (_health != null && (_health.State == PlayerLifeState.Downed || _health.State == PlayerLifeState.BeingRevived))
            {
                if (input.Interact)
                {
                    _health.TryProgressRevive(Time.deltaTime);
                }
                else
                {
                    _health.CancelRevive();
                }
            }

            // Process character movement
            if (_motor != null)
            {
                bool canMove = _health == null || _health.State == PlayerLifeState.Alive || _health.State == PlayerLifeState.Downed;
                _motor.ProcessMovement(input, canMove);
            }

            // Process weapon firing, aiming and reloading
            if (_weapons != null)
            {
                _weapons.ProcessWeapons(input);
            }

            // Process Operator Abilities in offline practice
            var ability = _playerRoot.GetComponent<OperatorAbilitySystem>();
            if (ability != null)
            {
                if (input.AbilityPrimary) ability.TryActivatePrimary();
                if (input.AbilityUltimate) ability.TryActivateUltimate();
            }

            // Escape key cursor handling
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }
    }
}
