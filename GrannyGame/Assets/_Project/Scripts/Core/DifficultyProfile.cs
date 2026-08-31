using UnityEngine;

namespace Granny.Core
{
    /// <summary>
    /// Every dial that makes the hunter easier or harder, in one asset.
    ///
    /// Difficulty here is not a damage multiplier — it is how much of the house
    /// she can perceive at once. Turning it up widens her ears and her eyes and
    /// shortens the gap between "she heard something" and "she is in the room".
    /// </summary>
    [CreateAssetMenu(menuName = "Granny/Difficulty Profile", fileName = "Difficulty_")]
    public sealed class DifficultyProfile : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] string displayName = "Normal";
        [SerializeField, Min(1)] int daysAllowed = 5;

        [Header("Movement (m/s)")]
        [SerializeField, Min(0.1f)] float patrolSpeed = 1.5f;
        [SerializeField, Min(0.1f)] float investigateSpeed = 2.3f;
        [SerializeField, Min(0.1f)] float chaseSpeed = 4.1f;

        [Header("Hearing")]
        [Tooltip("Multiplies every noise radius. Above 1 she hears the whole floor.")]
        [SerializeField, Range(0.2f, 3f)] float hearingScale = 1f;
        [Tooltip("Seconds before she reacts to a sound. Higher gives the player a head start.")]
        [SerializeField, Range(0f, 2f)] float reactionDelay = 0.35f;

        [Header("Sight")]
        [SerializeField, Range(2f, 30f)] float sightRange = 13f;
        [SerializeField, Range(20f, 180f)] float sightAngle = 105f;
        [Tooltip("Range within which she notices the player regardless of facing.")]
        [SerializeField, Range(0f, 6f)] float awarenessRange = 2.2f;

        [Header("Persistence")]
        [Tooltip("Seconds she keeps chasing after losing sight.")]
        [SerializeField, Range(0.5f, 20f)] float chaseMemory = 6f;
        [Tooltip("Seconds spent poking around a spot before giving up.")]
        [SerializeField, Range(0.5f, 20f)] float searchDuration = 7f;
        [Tooltip("Chance she opens a hiding place she walks past while searching.")]
        [SerializeField, Range(0f, 1f)] float hidingSpotCheckChance = 0.5f;

        public string DisplayName => displayName;
        public int DaysAllowed => daysAllowed;

        public float PatrolSpeed => patrolSpeed;
        public float InvestigateSpeed => investigateSpeed;
        public float ChaseSpeed => chaseSpeed;

        public float HearingScale => hearingScale;
        public float ReactionDelay => reactionDelay;

        public float SightRange => sightRange;
        public float SightAngle => sightAngle;
        public float AwarenessRange => awarenessRange;

        public float ChaseMemory => chaseMemory;
        public float SearchDuration => searchDuration;
        public float HidingSpotCheckChance => hidingSpotCheckChance;
    }
}
