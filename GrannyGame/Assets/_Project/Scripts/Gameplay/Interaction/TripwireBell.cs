using System;
using Granny.Core;
using Granny.Gameplay.Player;
using UnityEngine;

namespace Granny.Gameplay.Interaction
{
    /// <summary>
    /// A wire across a doorway with a bell on the end of it.
    ///
    /// Unlike a creaky board, crouching does not help: the wire is at shin
    /// height. It rings once and then hangs slack for the rest of the day, so it
    /// is a toll for not having looked, not a wall. Finding it in advance and
    /// stepping over it is the skill it teaches.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class TripwireBell : MonoBehaviour
    {
        [Header("Appearance")]
        [Tooltip("The taut wire, hidden once it has been tripped.")]
        [SerializeField] GameObject wireVisual;

        /// <summary>True once it has rung, until the next morning.</summary>
        public bool HasRung { get; private set; }

        /// <summary>Raised when it rings, for audio and the HUD to pick up.</summary>
        public event Action Rang;

        /// <summary>
        /// Rings the bell if it is still set.
        /// </summary>
        /// <returns>True when this call was the one that rang it.</returns>
        public bool Trip(GameObject tripper)
        {
            if (HasRung) return false;

            HasRung = true;
            if (wireVisual != null) wireVisual.SetActive(false);

            NoiseBus.Emit(transform.position, NoiseKind.Bell, tripper);
            Rang?.Invoke();
            return true;
        }

        /// <summary>Sets the wire again, at the start of a new day.</summary>
        public void Rearm()
        {
            HasRung = false;
            if (wireVisual != null) wireVisual.SetActive(true);
        }

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerMotor>() == null) return;
            Trip(other.gameObject);
        }
    }
}
