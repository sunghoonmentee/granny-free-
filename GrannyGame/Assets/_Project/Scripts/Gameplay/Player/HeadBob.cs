using Granny.Core;
using UnityEngine;

namespace Granny.Gameplay.Player
{
    /// <summary>
    /// Sways the camera as the player walks. Beyond flavour this is readable
    /// feedback: the bob is wide and fast while sprinting and almost absent while
    /// crouched, so peripheral vision tells you how loud you currently are.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class HeadBob : MonoBehaviour
    {
        [Tooltip("Must be a CHILD of the camera pivot, never the pivot itself: " +
                 "PlayerMotor drives the pivot's height for crouching, and the two " +
                 "would fight over the same transform.")]
        [SerializeField] Transform bobTarget;

        [Header("Amplitude (metres)")]
        [SerializeField, Min(0f)] float crouchAmplitude = 0.012f;
        [SerializeField, Min(0f)] float walkAmplitude = 0.035f;
        [SerializeField, Min(0f)] float sprintAmplitude = 0.062f;

        [Header("Frequency (steps per second)")]
        [SerializeField, Min(0f)] float crouchFrequency = 1.5f;
        [SerializeField, Min(0f)] float walkFrequency = 2.1f;
        [SerializeField, Min(0f)] float sprintFrequency = 3.2f;

        [Tooltip("How quickly the bob blends when the stance changes.")]
        [SerializeField, Min(0.01f)] float blendSpeed = 8f;

        [Tooltip("Sideways sway as a fraction of the vertical bob.")]
        [SerializeField, Range(0f, 1f)] float lateralRatio = 0.5f;

        PlayerMotor motor;
        Vector3 restPosition;
        float phase;
        float amplitude;
        float frequency;

        /// <summary>
        /// Phase of the walk cycle, 0-1. The footstep emitter reads this so a step
        /// sound fires exactly when the camera bottoms out.
        /// </summary>
        public float Phase => phase / (Mathf.PI * 2f);

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            if (bobTarget == null)
            {
                Debug.LogError($"[{nameof(HeadBob)}] No bob target assigned.", this);
                enabled = false;
                return;
            }

            restPosition = bobTarget.localPosition;
        }

        void LateUpdate()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;

            var (targetAmplitude, targetFrequency) = TargetsFor(motor.State);

            var blend = 1f - Mathf.Exp(-blendSpeed * dt);
            amplitude = Mathf.Lerp(amplitude, targetAmplitude, blend);
            frequency = Mathf.Lerp(frequency, targetFrequency, blend);

            if (targetAmplitude <= 0f && amplitude < 0.0005f)
            {
                // Settle back to centre rather than freezing mid-step.
                amplitude = 0f;
                phase = 0f;
                bobTarget.localPosition = Vector3.Lerp(bobTarget.localPosition, restPosition, blend);
                return;
            }

            phase += frequency * Mathf.PI * 2f * dt;
            if (phase > Mathf.PI * 2f) phase -= Mathf.PI * 2f;

            var vertical = Mathf.Sin(phase) * amplitude;
            var lateral = Mathf.Cos(phase * 0.5f) * amplitude * lateralRatio;

            bobTarget.localPosition = restPosition + new Vector3(lateral, vertical, 0f);
        }

        (float amplitude, float frequency) TargetsFor(MoveState state) => state switch
        {
            MoveState.Crouching => (crouchAmplitude, crouchFrequency),
            MoveState.Walking => (walkAmplitude, walkFrequency),
            MoveState.Sprinting => (sprintAmplitude, sprintFrequency),
            _ => (0f, walkFrequency),
        };

        /// <summary>
        /// Re-reads the rest position. The motor moves the pivot when the player
        /// crouches, so the bob has to be told where "centred" now is.
        /// </summary>
        public void ResetRestPosition()
        {
            if (bobTarget != null) restPosition = bobTarget.localPosition;
        }
    }
}
