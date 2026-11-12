using UnityEngine;

namespace OutrunAftermath.Client.Runtime.CameraSystem
{
    public enum CameraPerspective
    {
        FirstPerson,
        ThirdPerson
    }

    /// <summary>
    /// Dual perspective camera rig for OUTRUN: AFTERMATH.
    /// Provides first-person and third-person perspectives with zero competitive advantage
    /// (same hitboxes, same line of sight verification, no wall-peeking exploit).
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [Header("Perspective")]
        [SerializeField] private CameraPerspective currentPerspective = CameraPerspective.FirstPerson;
        [SerializeField] private float perspectiveTransitionSpeed = 12f;

        [Header("First Person Configuration")]
        [SerializeField] private Vector3 fpsStandingOffset = new Vector3(0f, 1.65f, 0f);
        [SerializeField] private Vector3 fpsCrouchOffset = new Vector3(0f, 0.95f, 0f);
        [SerializeField] private Vector3 fpsDownedOffset = new Vector3(0f, 0.35f, 0.15f);

        [Header("Third Person Configuration")]
        [SerializeField] private Vector3 tpsShoulderOffset = new Vector3(0.48f, 0.15f, -2.4f);
        [SerializeField] private float cameraCollisionRadius = 0.22f;
        [SerializeField] private LayerMask cameraOcclusionMask = ~0;

        [Header("FOV & Zoom")]
        [SerializeField] private float normalFov = 75f;
        [SerializeField] private float adsFov = 52f;
        [SerializeField] private float fovTransitionSpeed = 14f;

        [Header("Aim Clamps")]
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        private Camera _cam;
        private Transform _targetTransform;
        private float _yaw;
        private float _pitch;
        private Vector3 _currentCameraLocalPos;
        private Vector3 _recoilOffset;
        private bool _isCrouching;
        private bool _isDowned;
        private bool _isAiming;

        public Camera UnityCamera => _cam;
        public CameraPerspective Perspective => currentPerspective;
        public float Yaw => _yaw;
        public float Pitch => _pitch;

        public void Initialize(Transform target)
        {
            _targetTransform = target;
            _cam = GetComponentInChildren<Camera>();
            if (_cam == null)
            {
                var camGo = new GameObject("PlayerCamera");
                camGo.transform.SetParent(transform);
                camGo.tag = "MainCamera";
                _cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            _cam.nearClipPlane = 0.08f;
            _cam.fieldOfView = normalFov;

            _yaw = _targetTransform.eulerAngles.y;
            _pitch = 0f;
            _currentCameraLocalPos = fpsStandingOffset;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void SetCrouching(bool crouching) => _isCrouching = crouching;
        public void SetDowned(bool downed) => _isDowned = downed;
        public void SetAiming(bool aiming) => _isAiming = aiming;

        public void TogglePerspective()
        {
            currentPerspective = (currentPerspective == CameraPerspective.FirstPerson)
                ? CameraPerspective.ThirdPerson
                : CameraPerspective.FirstPerson;
        }

        public void AddLookInput(Vector2 lookDelta)
        {
            _yaw += lookDelta.x;
            _pitch += lookDelta.y;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }

        public void ApplyRecoilImpulse(float pitchKick, float yawKick)
        {
            _pitch -= pitchKick;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            _yaw += Random.Range(-yawKick, yawKick);
        }

        private void LateUpdate()
        {
            if (_targetTransform == null || _cam == null) return;

            // Follow player position
            transform.position = _targetTransform.position;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            // Determine eye pivot based on stance
            Vector3 eyePivot = fpsStandingOffset;
            if (_isDowned)
            {
                eyePivot = fpsDownedOffset;
            }
            else if (_isCrouching)
            {
                eyePivot = fpsCrouchOffset;
            }

            // Target camera position calculation
            Vector3 targetLocalPos;
            if (currentPerspective == CameraPerspective.FirstPerson)
            {
                targetLocalPos = eyePivot;
            }
            else
            {
                // Third person: over shoulder offset relative to pitch and yaw
                Vector3 worldPivot = transform.position + eyePivot;
                Quaternion pitchRot = Quaternion.Euler(_pitch, _yaw, 0f);
                Vector3 desiredOffset = pitchRot * tpsShoulderOffset;

                // Wall occlusion check (raycast back from eye pivot)
                float desiredDistance = tpsShoulderOffset.magnitude;
                Vector3 rayDirection = desiredOffset.normalized;

                if (Physics.SphereCast(worldPivot, cameraCollisionRadius, rayDirection, out RaycastHit hit, desiredDistance, cameraOcclusionMask, QueryTriggerInteraction.Ignore))
                {
                    float safeDist = Mathf.Max(0.2f, hit.distance - 0.1f);
                    desiredOffset = rayDirection * safeDist;
                }

                targetLocalPos = transform.InverseTransformPoint(worldPivot + desiredOffset);
            }

            // Smooth transition between camera positions
            _currentCameraLocalPos = Vector3.Lerp(_currentCameraLocalPos, targetLocalPos, Time.deltaTime * perspectiveTransitionSpeed);
            _cam.transform.localPosition = _currentCameraLocalPos;
            _cam.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            // Smooth FOV transition for ADS
            float targetFov = _isAiming ? adsFov : normalFov;
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, Time.deltaTime * fovTransitionSpeed);
        }

        public Ray GetAimRay()
        {
            if (_cam == null) return new Ray(transform.position, transform.forward);
            return _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }
    }
}
