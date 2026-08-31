using Granny.Core;
using UnityEngine;

namespace Granny.Gameplay.Player
{
    /// <summary>
    /// Turns the player's stance into audible footsteps.
    ///
    /// This is where the stealth economy is actually paid: sprinting is heard
    /// four times further than crouching, and the interval between steps is what
    /// makes running past an open doorway a decision rather than a reflex.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class FootstepNoise : MonoBehaviour
    {
        [Header("Audible radius (metres)")]
        [SerializeField, Min(0f)] float crouchRadius = 2.5f;
        [SerializeField, Min(0f)] float walkRadius = 7f;
        [SerializeField, Min(0f)] float sprintRadius = 14f;

        [Header("Cadence (seconds between steps)")]
        [SerializeField, Min(0.05f)] float crouchInterval = 0.72f;
        [SerializeField, Min(0.05f)] float walkInterval = 0.5f;
        [SerializeField, Min(0.05f)] float sprintInterval = 0.32f;

        PlayerMotor motor;
        float nextStepTime;

        void Awake() => motor = GetComponent<PlayerMotor>();

        void Update()
        {
            var state = motor.State;

            if (state == MoveState.Idle)
            {
                // Standing still resets the cadence, so the first step after
                // stopping is a full interval away rather than immediate.
                nextStepTime = Time.time + IntervalFor(MoveState.Walking);
                return;
            }

            if (Time.time < nextStepTime) return;

            nextStepTime = Time.time + IntervalFor(state);

            NoiseBus.Emit(
                transform.position,
                RadiusFor(state),
                NoiseKind.Footstep,
                gameObject);
        }

        float RadiusFor(MoveState state) => state switch
        {
            MoveState.Crouching => crouchRadius,
            MoveState.Sprinting => sprintRadius,
            _ => walkRadius,
        };

        float IntervalFor(MoveState state) => state switch
        {
            MoveState.Crouching => crouchInterval,
            MoveState.Sprinting => sprintInterval,
            _ => walkInterval,
        };
    }
}
