using UnityEngine;
using OutrunAftermath.Client.Runtime.Input;
using OutrunAftermath.Client.Runtime.CameraSystem;

namespace OutrunAftermath.Client.Runtime.Character
{
    public enum MovementStance
    {
        Standing,
        Crouching,
        Sliding,
        Downed
    }

    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterMotor : MonoBehaviour
    {
        [Header("Movement Speeds (m/s)")]
        [SerializeField] private float walkSpeed = 3.6f;
        [SerializeField] private float runSpeed = 5.8f;
        [SerializeField] private float sprintSpeed = 8.5f;
        [SerializeField] private float crouchSpeed = 2.4f;
        [SerializeField] private float slideInitialSpeed = 9.8f;
        [SerializeField] private float downedSpeed = 1.2f;

        [Header("Physics & Gravity")]
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float jumpHeight = 1.25f;
        [SerializeField] private float groundCheckRadius = 0.28f;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Collider Dimensions")]
        [SerializeField] private float standingHeight = 1.85f;
        [SerializeField] private float crouchHeight = 1.15f;
        [SerializeField] private float downedHeight = 0.55f;
        [SerializeField] private float controllerRadius = 0.38f;

        private CharacterController _controller;
        private CameraRig _cameraRig;
        private MovementStance _stance = MovementStance.Standing;
        private Vector3 _velocity;
        private Vector3 _slideDirection;
        private float _slideTimer;
        private float _verticalVelocity;
        private bool _isGrounded;

        public CharacterController Controller => _controller;
        public MovementStance Stance => _stance;
        public bool IsGrounded => _isGrounded;
        public Vector3 Velocity => _velocity;
        public float CurrentSpeed => new Vector2(_velocity.x, _velocity.z).magnitude;

        public void Teleport(Vector3 targetPosition)
        {
            if (_controller != null) _controller.enabled = false;
            transform.position = targetPosition;
            _velocity = Vector3.zero;
            _verticalVelocity = 0f;
            if (_controller != null) _controller.enabled = true;
        }

        public void Initialize(CameraRig cameraRig)
        {
            _controller = GetComponent<CharacterController>();
            _controller.radius = controllerRadius;
            _controller.height = standingHeight;
            _controller.center = new Vector3(0f, standingHeight * 0.5f, 0f);
            _controller.stepOffset = 0.35f;
            _controller.slopeLimit = 45f;

            _cameraRig = cameraRig;
        }

        public void ProcessMovement(PlayerInputState input, bool canMove)
        {
            if (_controller == null || _cameraRig == null) return;

            // Ground check
            Vector3 spherePos = transform.position + Vector3.up * (groundCheckRadius - 0.05f);
            _isGrounded = Physics.CheckSphere(spherePos, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);

            if (_isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f; // Small downward force to stay grounded on slopes
            }

            // Stance State Machine
            if (_stance == MovementStance.Downed)
            {
                // Locked in downed crawl
            }
            else if (input.Crouch)
            {
                // If running or sprinting and hits crouch -> Slide!
                if ((input.Sprint || CurrentSpeed > runSpeed * 0.9f) && _stance != MovementStance.Sliding && _isGrounded)
                {
                    _stance = MovementStance.Sliding;
                    _slideTimer = 0.85f;
                    _slideDirection = transform.forward;
                }
                else if (_stance != MovementStance.Sliding)
                {
                    _stance = MovementStance.Crouching;
                }
            }
            else
            {
                if (_stance == MovementStance.Sliding)
                {
                    _slideTimer -= Time.deltaTime;
                    if (_slideTimer <= 0f)
                    {
                        _stance = MovementStance.Standing;
                    }
                }
                else
                {
                    _stance = MovementStance.Standing;
                }
            }

            // Update collider height based on stance
            UpdateColliderHeight();

            // Calculate movement vector
            Vector3 moveDir = Vector3.zero;

            if (canMove)
            {
                if (_stance == MovementStance.Sliding)
                {
                    float slideSpeed = Mathf.Lerp(crouchSpeed, slideInitialSpeed, _slideTimer / 0.85f);
                    moveDir = _slideDirection * slideSpeed;
                }
                else
                {
                    // Calculate target speed
                    float targetSpeed = runSpeed;

                    if (_stance == MovementStance.Downed)
                    {
                        targetSpeed = downedSpeed;
                    }
                    else if (_stance == MovementStance.Crouching)
                    {
                        targetSpeed = crouchSpeed;
                    }
                    else if (input.Sprint && input.Move.y > 0.1f)
                    {
                        targetSpeed = sprintSpeed;
                    }
                    else if (input.Move.sqrMagnitude < 0.2f)
                    {
                        targetSpeed = 0f;
                    }
                    else if (input.Move.sqrMagnitude < 0.5f)
                    {
                        targetSpeed = walkSpeed;
                    }

                    // Forward and right directions aligned to camera yaw
                    Vector3 camForward = Vector3.ProjectOnPlane(_cameraRig.transform.forward, Vector3.up).normalized;
                    Vector3 camRight = Vector3.ProjectOnPlane(_cameraRig.transform.right, Vector3.up).normalized;

                    Vector3 desiredDirection = (camRight * input.Move.x + camForward * input.Move.y).normalized;
                    moveDir = desiredDirection * targetSpeed;
                }

                // Jump
                if (input.Jump && _isGrounded && _stance == MovementStance.Standing)
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }

            // Apply gravity
            _verticalVelocity += gravity * Time.deltaTime;
            _verticalVelocity = Mathf.Max(_verticalVelocity, -30f);

            // Combine horizontal motion with vertical motion
            Vector3 finalMotion = moveDir;
            finalMotion.y = _verticalVelocity;

            _controller.Move(finalMotion * Time.deltaTime);
            _velocity = _controller.velocity;

            // Rotate character body towards aim yaw
            transform.rotation = Quaternion.Euler(0f, _cameraRig.Yaw, 0f);

            // Inform camera rig of stance
            _cameraRig.SetCrouching(_stance == MovementStance.Crouching || _stance == MovementStance.Sliding);
            _cameraRig.SetDowned(_stance == MovementStance.Downed);
        }

        public void SetDownedStance(bool downed)
        {
            _stance = downed ? MovementStance.Downed : MovementStance.Standing;
            UpdateColliderHeight();
        }

        private void UpdateColliderHeight()
        {
            float targetHeight = standingHeight;
            if (_stance == MovementStance.Downed)
            {
                targetHeight = downedHeight;
            }
            else if (_stance == MovementStance.Crouching || _stance == MovementStance.Sliding)
            {
                targetHeight = crouchHeight;
            }

            float newHeight = Mathf.Lerp(_controller.height, targetHeight, Time.deltaTime * 12f);
            _controller.height = newHeight;
            _controller.center = new Vector3(0f, newHeight * 0.5f, 0f);
        }
    }
}
