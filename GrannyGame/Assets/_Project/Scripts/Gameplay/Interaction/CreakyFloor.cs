using Granny.Core;
using Granny.Gameplay.Player;
using UnityEngine;

namespace Granny.Gameplay.Interaction
{
    /// <summary>
    /// A board that gives under your weight and tells her where you are.
    ///
    /// This is the one exception to "moving is silent". The point is that the
    /// house, not the player, decides when the player is heard: a route you have
    /// not learned yet is dangerous, and the way to cross a board you do know
    /// about is to crouch over it and lose the time instead.
    ///
    /// Which boards are live changes with difficulty, so the same house teaches a
    /// different route each setting — see <see cref="SetArmed"/>.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CreakyFloor : MonoBehaviour
    {
        [Header("Creaking")]
        [Tooltip("Quiet period after a creak, so one crossing is one sound.")]
        [SerializeField, Min(0.05f)] float retriggerSeconds = 1.1f;

        [Tooltip("Standing still on a board does not make it creak.")]
        [SerializeField, Min(0f)] float minimumSpeed = 0.5f;

        [Header("Appearance")]
        [Tooltip("Shown only while the board is live, as a hint once it has fired.")]
        [SerializeField] GameObject armedVisual;

        float nextCreak;

        /// <summary>Whether this board is live this run.</summary>
        public bool IsArmed { get; private set; } = true;

        /// <summary>Turned on and off by difficulty when a run starts.</summary>
        public void SetArmed(bool value)
        {
            IsArmed = value;
            if (armedVisual != null) armedVisual.SetActive(value);
        }

        /// <summary>
        /// Decides whether this crossing makes a sound, and makes it if so.
        /// Split out from the trigger callback so it can be tested without
        /// pushing a CharacterController around a scene.
        /// </summary>
        /// <returns>True when a noise was actually emitted.</returns>
        public bool TryCreak(GameObject walker, float speed, bool crouching)
        {
            if (!IsArmed) return false;
            if (crouching) return false;
            if (speed < minimumSpeed) return false;
            if (Time.time < nextCreak) return false;

            nextCreak = Time.time + retriggerSeconds;
            NoiseBus.Emit(transform.position, NoiseKind.Creak, walker);
            return true;
        }

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerStay(Collider other)
        {
            // Only the player. She walks over her own floorboards all night; if
            // they answered her too, every board would be a beacon pointing at
            // wherever she already is.
            var motor = other.GetComponentInParent<PlayerMotor>();
            if (motor == null) return;

            TryCreak(motor.gameObject, motor.Speed, motor.IsCrouching);
        }
    }
}
