using UnityEngine;
using OutrunAftermath.Client.Runtime.Input;
using OutrunAftermath.Client.Runtime.CameraSystem;
using OutrunAftermath.Client.Runtime.Character;
using OutrunAftermath.Client.Runtime.Combat;
using OutrunAftermath.Client.Runtime.UI;
using OutrunAftermath.Client.Runtime.Environment;

namespace OutrunAftermath.Client
{
    /// <summary>
    /// Master Game Bootstrap for OUTRUN: AFTERMATH.
    /// Orchestrates game initialization, environment construction, player instantiation,
    /// camera rig binding, weapon systems, and tactical HUD.
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

        private void Awake()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 1;

            InitializeGame();
        }

        private void InitializeGame()
        {
            Debug.Log("[OUTRUN: AFTERMATH] Bootstrapping Phase 1 Core Engine...");

            // 1. Build greybox environment if needed
            if (buildEnvironmentOnStart && GameObject.Find("DeadCity_Sector4") == null)
            {
                DeadCityEnvironment.GenerateEnvironment(transform);
            }

            // 2. Ensure Input Manager
            if (FindAnyObjectByType<InputManager>() == null)
            {
                var inputGo = new GameObject("InputManager");
                inputGo.AddComponent<InputManager>();
            }

            // 3. Spawn and Setup Player
            SetupPlayer();

            // 4. Setup HUD
            SetupHUD();

            Debug.Log("[OUTRUN: AFTERMATH] Phase 1 Core Systems Online. Player Ready.");
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
        }

        private void Update()
        {
            if (InputManager.Instance == null) return;

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
