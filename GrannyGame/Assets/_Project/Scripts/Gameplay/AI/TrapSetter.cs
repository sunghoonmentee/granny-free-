using System.Collections.Generic;
using System.Linq;
using Granny.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Granny.Gameplay.AI
{
    /// <summary>
    /// Her answer to searching a room and finding nobody in it.
    ///
    /// She leaves a trap on the way out, at the nearest place the player cannot
    /// avoid. That turns a successful hiding place into a debt: the noise that
    /// drew her there has made the route back expensive. Over a day the house
    /// slowly fills with consequences of the player's own mistakes.
    ///
    /// Never more than a few at once — a house wallpapered in bear traps stops
    /// being a threat and becomes a maze.
    /// </summary>
    public sealed class TrapSetter : MonoBehaviour
    {
        [Header("What she lays")]
        [SerializeField] GameObject trapPrefab;

        [Header("Where")]
        [Tooltip("How far she will walk off the search spot to use a proper choke point.")]
        [SerializeField, Min(1f)] float chokePointReach = 9f;

        [Tooltip("Traps are not stacked on each other.")]
        [SerializeField, Min(0.5f)] float minimumSpacing = 2.5f;

        [Header("Limit")]
        [Tooltip("Used when no difficulty profile is set.")]
        [SerializeField, Min(1)] int fallbackLimit = 3;

        readonly List<BearTrap> laid = new();
        readonly Dictionary<BearTrap, TrapSpot> spotOf = new();

        /// <summary>Her traps, oldest first.</summary>
        public IReadOnlyList<BearTrap> Laid => laid;

        public int Limit { get; set; }

        void Awake()
        {
            if (Limit <= 0) Limit = fallbackLimit;
        }

        /// <summary>
        /// Puts a trap down near <paramref name="around"/>, on a choke point if
        /// there is a free one within reach and at her own feet otherwise.
        /// </summary>
        /// <returns>The trap, or null if there was nowhere to put one.</returns>
        public BearTrap Lay(Vector3 around)
        {
            if (trapPrefab == null) return null;

            var spot = ChooseSpot(around, out var position);
            if (!position.HasValue) return null;

            var instance = Instantiate(trapPrefab, position.Value, Quaternion.identity);
            instance.SetActive(true);

            var trap = instance.GetComponent<BearTrap>();
            if (trap == null)
            {
                Destroy(instance);
                return null;
            }

            trap.name = $"BearTrap_Laid_{laid.Count}";
            laid.Add(trap);

            if (spot != null)
            {
                spot.IsTaken = true;
                spotOf[trap] = spot;
            }

            // The sound of her setting it. Audible, so a player paying attention
            // knows roughly where it went — and tagged as hers, so she does not
            // walk back to investigate her own handiwork.
            NoiseBus.Emit(position.Value, NoiseKind.ToolWork, gameObject);

            TrimToLimit();
            return trap;
        }

        /// <summary>Takes every trap of hers back up. Used when a day ends.</summary>
        public void ClearAll()
        {
            foreach (var trap in laid.ToList())
                Remove(trap);

            laid.Clear();
        }

        /// <summary>
        /// The oldest trap goes when a new one would exceed the limit, so the
        /// house never accumulates more than the player can be expected to track.
        /// </summary>
        void TrimToLimit()
        {
            while (laid.Count > Mathf.Max(1, Limit))
            {
                var oldest = laid[0];
                laid.RemoveAt(0);
                Remove(oldest);
            }
        }

        void Remove(BearTrap trap)
        {
            if (trap == null) return;

            if (spotOf.TryGetValue(trap, out var spot))
            {
                if (spot != null) spot.IsTaken = false;
                spotOf.Remove(trap);
            }

            Destroy(trap.gameObject);
        }

        /// <summary>
        /// Prefers a free choke point within reach; falls back to the search spot
        /// itself. Either way the result is nudged onto walkable floor, because a
        /// trap resting inside a wall catches nobody.
        /// </summary>
        /// <returns>The spot it claimed, or null when it fell back to her feet.</returns>
        TrapSpot ChooseSpot(Vector3 around, out Vector3? position)
        {
            var best = AllSpots()
                .Where(s => !s.IsTaken)
                .Where(s => Vector3.Distance(s.transform.position, around) <= chokePointReach)
                .Where(s => !IsCrowded(s.transform.position))
                .OrderByDescending(s => s.IsChokePoint)
                .ThenBy(s => Vector3.Distance(s.transform.position, around))
                .FirstOrDefault();

            var wanted = best != null ? best.transform.position : around;

            if (best == null && IsCrowded(wanted))
            {
                position = null;
                return null;
            }

            position = SnapToFloor(wanted);
            return best;
        }

        IEnumerable<TrapSpot> AllSpots() =>
            FindObjectsByType<TrapSpot>(FindObjectsSortMode.None);

        bool IsCrowded(Vector3 position) =>
            laid.Any(t => t != null &&
                          Vector3.Distance(t.transform.position, position) < minimumSpacing);

        /// <summary>
        /// Nudges the chosen point onto walkable floor where there is a NavMesh to
        /// nudge it onto, and leaves it alone where there is not.
        ///
        /// It used to give up and lay nothing when the sample missed. That made
        /// the whole behaviour depend on a baked NavMesh being present, which is
        /// both untestable and a silent failure in a scene that is still being
        /// built: she would search, find nobody, and leave nothing behind.
        /// </summary>
        static Vector3 SnapToFloor(Vector3 wanted) =>
            NavMesh.SamplePosition(wanted, out var hit, 2f, NavMesh.AllAreas)
                ? hit.position
                : wanted;
    }
}
