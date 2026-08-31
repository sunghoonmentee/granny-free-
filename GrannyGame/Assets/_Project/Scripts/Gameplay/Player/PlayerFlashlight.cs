using UnityEngine;

namespace Granny.Gameplay.Player
{
    /// <summary>
    /// The torch. It is the only reliable light in the house, and it is also a
    /// liability: the battery drains while it is on, and later phases let Granny
    /// notice the beam. Draining it is what forces the player to move in the dark.
    /// </summary>
    public sealed class PlayerFlashlight : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] Light beam;

        [Header("Battery")]
        [Tooltip("Seconds of continuous light on a full battery.")]
        [SerializeField, Min(1f)] float batterySeconds = 240f;
        [Tooltip("Fraction of battery below which the beam starts to flicker.")]
        [SerializeField, Range(0f, 0.5f)] float flickerThreshold = 0.15f;

        [Header("Beam")]
        [SerializeField, Min(0f)] float fullIntensity = 3.2f;
        [SerializeField, Min(0.01f)] float warmupSpeed = 14f;

        float charge = 1f;
        bool on;
        float displayedIntensity;

        /// <summary>0-1 battery remaining, for the HUD.</summary>
        public float BatteryNormalized => charge;

        public bool IsOn => on && charge > 0f;

        void Awake()
        {
            if (input == null) input = GetComponentInParent<PlayerInputReader>();
            if (beam == null) beam = GetComponentInChildren<Light>(includeInactive: true);

            if (beam == null)
            {
                Debug.LogError($"[{nameof(PlayerFlashlight)}] No spot light assigned.", this);
                enabled = false;
                return;
            }

            beam.intensity = 0f;
            beam.enabled = false;
        }

        void OnEnable()
        {
            if (input != null) input.FlashlightToggled += Toggle;
        }

        void OnDisable()
        {
            if (input != null) input.FlashlightToggled -= Toggle;
        }

        void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (on && charge > 0f)
            {
                charge = Mathf.Max(0f, charge - dt / batterySeconds);
                if (charge <= 0f) on = false;
            }

            var target = IsOn ? fullIntensity * DimFactor() : 0f;
            displayedIntensity = Mathf.MoveTowards(
                displayedIntensity, target, fullIntensity * warmupSpeed * dt);

            beam.enabled = displayedIntensity > 0.001f;
            beam.intensity = displayedIntensity;
        }

        /// <summary>
        /// Fades and stutters as the battery dies, so the player gets a warning
        /// instead of being dropped into darkness without notice.
        /// </summary>
        float DimFactor()
        {
            if (charge >= flickerThreshold) return 1f;

            var fade = Mathf.InverseLerp(0f, flickerThreshold, charge);
            var stutter = 0.65f + 0.35f * Mathf.PerlinNoise(Time.time * 11f, 0f);
            return Mathf.Lerp(0.25f, 1f, fade) * stutter;
        }

        public void Toggle() => SetOn(!on);

        public void SetOn(bool value) => on = value && charge > 0f;

        /// <summary>Adds battery charge — from a pickup, or on waking up a new day.</summary>
        public void Recharge(float fraction = 1f) => charge = Mathf.Clamp01(charge + fraction);
    }
}
