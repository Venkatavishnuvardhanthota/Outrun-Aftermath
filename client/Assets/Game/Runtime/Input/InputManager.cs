using UnityEngine;
using UnityEngine.InputSystem;

namespace OutrunAftermath.Client.Runtime.Input
{
    public struct PlayerInputState
    {
        public Vector2 Move;
        public Vector2 Look;
        public bool Sprint;
        public bool Crouch;
        public bool Jump;
        public bool Fire;
        public bool FireHeld;
        public bool Aim;
        public bool Reload;
        public bool Interact;
        public bool TogglePerspective;
        public bool ToggleHelp;
        public int WeaponSlot; // 1, 2, 3 or 0 if none
    }

    /// <summary>
    /// Unified input manager supporting keyboard/mouse, gamepads, and virtual input.
    /// Provides clean separation between hardware events and game simulation.
    /// </summary>
    public sealed class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("Sensitivity Settings")]
        [SerializeField] private float mouseSensitivity = 1.8f;
        [SerializeField] private float gamepadSensitivity = 90f;
        [SerializeField] private bool invertY = false;

        public PlayerInputState Current { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            Current = PollInput();
        }

        private PlayerInputState PollInput()
        {
            var state = new PlayerInputState();

            // 1. Keyboard & Mouse Polling
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;

            Vector2 move = Vector2.zero;
            Vector2 look = Vector2.zero;

            if (kb != null)
            {
                if (kb.wKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed) move.y -= 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;

                state.Sprint = kb.leftShiftKey.isPressed;
                state.Crouch = kb.cKey.isPressed || kb.leftCtrlKey.isPressed;
                state.Jump = kb.spaceKey.wasPressedThisFrame;
                state.Reload = kb.rKey.wasPressedThisFrame;
                state.Interact = kb.eKey.isPressed;
                state.TogglePerspective = kb.tKey.wasPressedThisFrame || kb.vKey.wasPressedThisFrame;
                state.ToggleHelp = kb.f1Key.wasPressedThisFrame || kb.hKey.wasPressedThisFrame;

                if (kb.digit1Key.wasPressedThisFrame) state.WeaponSlot = 1;
                else if (kb.digit2Key.wasPressedThisFrame) state.WeaponSlot = 2;
                else if (kb.digit3Key.wasPressedThisFrame) state.WeaponSlot = 3;
            }

            if (mouse != null)
            {
                Vector2 mouseDelta = mouse.delta.ReadValue();
                look.x += mouseDelta.x * mouseSensitivity * 0.1f;
                look.y += (invertY ? mouseDelta.y : -mouseDelta.y) * mouseSensitivity * 0.1f;

                state.Fire = mouse.leftButton.wasPressedThisFrame;
                state.FireHeld = mouse.leftButton.isPressed;
                state.Aim = mouse.rightButton.isPressed;
            }

            // 2. Gamepad fallback/addition
            if (pad != null)
            {
                Vector2 stickMove = pad.leftStick.ReadValue();
                if (stickMove.sqrMagnitude > 0.04f)
                {
                    move = stickMove;
                }

                Vector2 stickLook = pad.rightStick.ReadValue();
                if (stickLook.sqrMagnitude > 0.04f)
                {
                    look.x += stickLook.x * gamepadSensitivity * Time.deltaTime;
                    look.y += (invertY ? stickLook.y : -stickLook.y) * gamepadSensitivity * Time.deltaTime;
                }

                if (pad.rightTrigger.wasPressedThisFrame) state.Fire = true;
                if (pad.rightTrigger.isPressed) state.FireHeld = true;
                if (pad.leftTrigger.isPressed) state.Aim = true;
                if (pad.buttonSouth.wasPressedThisFrame) state.Jump = true;
                if (pad.buttonEast.wasPressedThisFrame) state.Crouch = true;
                if (pad.buttonWest.wasPressedThisFrame) state.Reload = true;
                if (pad.buttonNorth.wasPressedThisFrame) state.Interact = true;
                if (pad.leftStickButton.isPressed) state.Sprint = true;
                if (pad.rightStickButton.wasPressedThisFrame) state.TogglePerspective = true;
            }

            // Normalise movement input vector
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            state.Move = move;
            state.Look = look;

            return state;
        }

        public void SetSensitivity(float sensitivity)
        {
            mouseSensitivity = Mathf.Clamp(sensitivity, 0.2f, 10f);
        }

        public void SetInvertY(bool invert)
        {
            invertY = invert;
        }
    }
}
