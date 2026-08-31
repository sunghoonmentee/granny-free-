using UnityEngine;

namespace Granny.Gameplay.Player
{
    /// <summary>
    /// Mouse look. Yaw turns the whole body so movement always follows the view;
    /// pitch is applied to the camera pivot alone and clamped so the capsule
    /// never tips over.
    /// </summary>
    public sealed class PlayerLook : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] Transform body;
        [SerializeField] Transform cameraPivot;

        [Header("Sensitivity")]
        [Tooltip("Degrees turned per unit of mouse delta.")]
        [SerializeField, Range(0.01f, 1f)] float mouseSensitivity = 0.12f;
        [Tooltip("Degrees per second at full stick deflection.")]
        [SerializeField, Range(30f, 500f)] float stickSensitivity = 180f;
        [SerializeField] bool invertY;

        [Header("Limits")]
        [SerializeField, Range(45f, 89f)] float pitchLimit = 85f;

        float pitch;

        /// <summary>Runtime sensitivity hook for the settings menu.</summary>
        public float MouseSensitivity
        {
            get => mouseSensitivity;
            set => mouseSensitivity = Mathf.Clamp(value, 0.01f, 1f);
        }

        public bool InvertY
        {
            get => invertY;
            set => invertY = value;
        }

        void Awake()
        {
            if (body == null) body = transform;
            if (input == null) input = GetComponentInChildren<PlayerInputReader>();
            if (cameraPivot == null)
                Debug.LogError($"[{nameof(PlayerLook)}] No camera pivot assigned.", this);

            pitch = cameraPivot != null ? NormalizeAngle(cameraPivot.localEulerAngles.x) : 0f;
        }

        void OnEnable() => SetCursorLocked(true);

        void OnDisable() => SetCursorLocked(false);

        void LateUpdate()
        {
            if (input == null || cameraPivot == null) return;

            var look = input.Look;
            if (look.sqrMagnitude <= 0f) return;

            // A mouse reports how far it moved since the last frame, so scaling it
            // by delta time would make sensitivity depend on frame rate. A stick
            // reports a held position, which must be integrated over time.
            var scaled = input.LookIsPointerDelta
                ? look * mouseSensitivity
                : look * (stickSensitivity * Time.deltaTime);

            body.Rotate(Vector3.up, scaled.x, Space.Self);

            pitch += invertY ? scaled.y : -scaled.y;
            pitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        static float NormalizeAngle(float degrees) => degrees > 180f ? degrees - 360f : degrees;
    }
}
