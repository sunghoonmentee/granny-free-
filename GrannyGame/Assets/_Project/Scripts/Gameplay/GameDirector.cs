using System;
using System.Collections;
using Granny.Core;
using Granny.Gameplay.AI;
using Granny.Gameplay.Player;
using UnityEngine;

namespace Granny.Gameplay
{
    /// <summary>
    /// Runs the house: holds the day clock, wakes the player in bed each morning,
    /// and decides what being caught means.
    ///
    /// Deliberately the only thing that knows about all the parts. Granny raises
    /// "I caught someone"; she does not know what a day is.
    /// </summary>
    public sealed class GameDirector : MonoBehaviour
    {
        [Header("Rules")]
        [SerializeField] DifficultyProfile difficulty;

        [Header("Cast")]
        [SerializeField] PlayerMotor player;
        [SerializeField] GrannyBrain granny;

        [Header("Places")]
        [Tooltip("Where the player wakes up each morning.")]
        [SerializeField] Transform bed;
        [Tooltip("Where Granny starts each morning.")]
        [SerializeField] Transform grannySpawn;

        [Header("Blackout")]
        [Tooltip("Seconds the screen stays dark after being caught.")]
        [SerializeField, Min(0f)] float blackoutSeconds = 2.2f;

        DayCycle days;
        bool resolvingCatch;

        public DayCycle Days => days;
        public DifficultyProfile Difficulty => difficulty;

        /// <summary>Raised while the screen should be black, with 0-1 darkness.</summary>
        public event Action<float> BlackoutChanged;

        /// <summary>Raised with the new day number when the player wakes up.</summary>
        public event Action<int> DayStarted;

        public event Action GameOver;
        public event Action Escaped;

        void Awake()
        {
            days = new DayCycle(difficulty != null ? difficulty.DaysAllowed : 5);

            days.DayStarted += day => DayStarted?.Invoke(day);
            days.GameOver += () => GameOver?.Invoke();
            days.Escaped += () => Escaped?.Invoke();

            if (player == null) player = FindAnyObjectByType<PlayerMotor>();
            if (granny == null) granny = FindAnyObjectByType<GrannyBrain>();
        }

        void OnEnable()
        {
            if (granny != null) granny.Caught += OnCaught;
        }

        void OnDisable()
        {
            if (granny != null) granny.Caught -= OnCaught;
        }

        void Start() => DayStarted?.Invoke(days.Day);

        void OnCaught(GameObject who)
        {
            // Granny can reach the player on the same frame she enters Catch, and
            // again the next; without this the player would lose two days at once.
            if (resolvingCatch || days.IsOver) return;

            resolvingCatch = true;
            StartCoroutine(ResolveCatch());
        }

        IEnumerator ResolveCatch()
        {
            if (player != null) player.enabled = false;

            var elapsed = 0f;
            while (elapsed < blackoutSeconds * 0.4f)
            {
                elapsed += Time.deltaTime;
                BlackoutChanged?.Invoke(Mathf.Clamp01(elapsed / (blackoutSeconds * 0.4f)));
                yield return null;
            }

            BlackoutChanged?.Invoke(1f);

            var wasFinal = days.IsFinalDay;
            days.Caught();

            if (wasFinal)
            {
                // Nothing more to wake up for; the screen stays dark.
                resolvingCatch = false;
                yield break;
            }

            StartNewDay();

            elapsed = 0f;
            while (elapsed < blackoutSeconds * 0.6f)
            {
                elapsed += Time.deltaTime;
                BlackoutChanged?.Invoke(1f - Mathf.Clamp01(elapsed / (blackoutSeconds * 0.6f)));
                yield return null;
            }

            BlackoutChanged?.Invoke(0f);
            if (player != null) player.enabled = true;

            resolvingCatch = false;
        }

        void StartNewDay()
        {
            if (player != null && bed != null)
                player.Warp(bed.position, bed.eulerAngles.y);

            if (granny != null && grannySpawn != null)
                granny.ResetForNewDay(grannySpawn.position);

            foreach (var trap in FindObjectsByType<BearTrap>(FindObjectsSortMode.None))
                trap.Rearm();
        }

        /// <summary>Called by the front door once every lock is off.</summary>
        public void ReportEscape() => days.Escape();
    }
}
