using Granny.Core;
using UnityEngine;

namespace Granny.Gameplay.Player
{
    /// <summary>
    /// What four nights of being caught leave behind.
    ///
    /// Injuries never take speed away. The original does that and it is the one
    /// rule worth refusing: a player who is slower cannot recover from the
    /// mistake that made them slow, so day four becomes a formality. These cost
    /// aim and composure instead — the camera will not hold still, and by the
    /// fourth morning the edges of everything are red — which makes the house
    /// harder to read without making it impossible to run.
    /// </summary>
    public sealed class PlayerInjury : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] Transform cameraPivot;

        [Header("Unsteadiness")]
        [Tooltip("Degrees of roll at the worst, applied as a slow wander.")]
        [SerializeField, Range(0f, 6f)] float maximumRoll = 2.4f;

        [Tooltip("How quickly the wander moves. Slow is unsettling; fast is seasickness.")]
        [SerializeField, Min(0.05f)] float swaySpeed = 0.7f;

        float day = 1f;
        float roll;

        /// <summary>The day being shown, which is what decides how bad this is.</summary>
        public int Day { get; private set; } = 1;

        /// <summary>0 on the first morning, 1 by the last. Drives the blood at the edges.</summary>
        public float Severity => Mathf.Clamp01((Day - 1) / 4f);

        /// <summary>Degrees the view is currently leaning. Zero on day one.</summary>
        public float Roll => roll;

        /// <summary>Raised when a new morning changes how bad it is, for the HUD.</summary>
        public event System.Action<float> SeverityChanged;

        void Awake()
        {
            if (cameraPivot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraPivot = cam.transform;
            }
        }

        /// <summary>Told by the day cycle which morning this is.</summary>
        public void SetDay(int value)
        {
            Day = Mathf.Max(1, value);
            day = Day;
            SeverityChanged?.Invoke(Severity);
        }

        void LateUpdate()
        {
            if (cameraPivot == null) return;

            // Day one is steady. Everything after it leans, a little more each
            // morning, on a wander slow enough to be felt rather than noticed.
            var amount = maximumRoll * Severity;
            if (amount <= 0f)
            {
                roll = 0f;
                return;
            }

            var wander = Mathf.PerlinNoise(Time.time * swaySpeed, day * 13.7f) * 2f - 1f;
            roll = wander * amount;

            cameraPivot.localRotation *= Quaternion.Euler(0f, 0f, roll);
        }
    }
}
