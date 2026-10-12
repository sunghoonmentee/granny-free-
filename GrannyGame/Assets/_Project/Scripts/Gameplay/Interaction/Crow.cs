using System;
using Granny.Core;
using Granny.Gameplay.Player;
using UnityEngine;

namespace Granny.Gameplay.Interaction
{
    /// <summary>
    /// A bird on a perch that shouts when you come near it.
    ///
    /// It is not a trap and it is not a puzzle. It is a *place* — a few square
    /// metres of the house that cost something to walk through, and which you
    /// learn to go round. That makes it the cheapest way to put a price on a room
    /// that is otherwise free, which is what the attic and the way to the garage
    /// both needed.
    ///
    /// Crouching does not help, and nor does going slowly. The bird is looking at
    /// you, not listening — every other noise rule in this house is about what she
    /// can hear, and one hazard that works the other way stops the player reading
    /// "quiet" as "safe" everywhere.
    ///
    /// It goes quiet for a while after a cry rather than once a day like the
    /// tripwire, so the same bird can catch you twice in one night, and so a
    /// player who is willing to wait can still get past it.
    /// </summary>
    public sealed class Crow : MonoBehaviour
    {
        [Header("Watching")]
        [Tooltip("How close is too close.")]
        [SerializeField, Min(0.5f)] float alarmRange = 3.6f;

        [Tooltip("How long it sulks after a cry before it will do it again.")]
        [SerializeField, Min(1f)] float quietSeconds = 55f;

        [Header("Parts")]
        [SerializeField] Transform wingL;
        [SerializeField] Transform wingR;

        float nextCry;
        float flap;
        PlayerMotor player;

        /// <summary>True when it would cry if the player walked up to it now.</summary>
        public bool IsWatching => Time.time >= nextCry;

        /// <summary>How close the player has to get.</summary>
        public float AlarmRange => alarmRange;

        /// <summary>Raised when it cries, for audio and the HUD.</summary>
        public event Action Cried;

        void Update()
        {
            Flap();

            if (!IsWatching) return;

            if (player == null)
            {
                player = FindAnyObjectByType<PlayerMotor>();
                if (player == null) return;
            }

            var gap = player.transform.position - transform.position;
            if (gap.sqrMagnitude > alarmRange * alarmRange) return;

            Cry(player.gameObject);
        }

        /// <summary>
        /// Sets it off, whoever is responsible.
        /// </summary>
        /// <returns>True if it actually cried, false if it was still sulking.</returns>
        public bool Cry(GameObject disturbedBy)
        {
            if (!IsWatching) return false;

            nextCry = Time.time + quietSeconds;
            flap = 1f;

            NoiseBus.Emit(transform.position, NoiseKind.Crow, disturbedBy);
            Cried?.Invoke();
            return true;
        }

        /// <summary>Makes it watchful again — used by the day change and by tests.</summary>
        public void Rearm() => nextCry = 0f;

        /// <summary>
        /// The wings, beating hard and then folding. Cheap, and it is the only
        /// thing that tells a player who heard a noise behind them which of the
        /// shapes in the room made it.
        /// </summary>
        void Flap()
        {
            if (flap <= 0f) return;

            flap = Mathf.Max(0f, flap - Time.deltaTime * 1.6f);

            var beat = Mathf.Sin(Time.time * 26f) * 42f * flap;

            if (wingL != null) wingL.localRotation = Quaternion.Euler(0f, 0f, beat);
            if (wingR != null) wingR.localRotation = Quaternion.Euler(0f, 0f, -beat);
        }
    }
}
