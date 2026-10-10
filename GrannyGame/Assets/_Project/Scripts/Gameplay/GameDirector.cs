using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>The run's progress, ready to be written or restored.</summary>
        public SaveData Snapshot()
        {
            var data = new SaveData
            {
                difficultyName = difficulty != null ? difficulty.DisplayName : "Normal",
                day = days.Day,
            };

            var inventory = player != null ? player.GetComponentInChildren<PlayerInventory>() : null;
            if (inventory != null)
            {
                data.heldItemId = inventory.Held != null ? inventory.Held.Id : string.Empty;
                data.darts = inventory.Darts;
            }

            // The layout is part of this run, like the day count. Leaving it out
            // meant resuming moved every tool in the house.
            var spawner = FindAnyObjectByType<ItemSpawner>();
            if (spawner != null) data.itemLayout = spawner.Layout;

            foreach (var stage in FindObjectsByType<Interaction.LockStage>(FindObjectsSortMode.None))
                if (stage.IsCleared)
                    data.clearedLockIds.Add(stage.name);

            foreach (var container in FindObjectsByType<Interaction.Drawer>(FindObjectsSortMode.None))
                if (container.HasBeenSearched)
                    data.searchedContainerIds.Add(container.name);

            return data;
        }

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

            // Before Restore, so a resumed run restores progress onto the door
            // this difficulty actually has rather than onto locks it does not.
            foreach (var way in FindObjectsByType<Interaction.EscapeDoor>(FindObjectsSortMode.None))
                if (way.ScaledByDifficulty)
                    way.FitLocks(GameModes.LocksFor(difficulty, way.StagesFitted));

            Restore();
        }

        /// <summary>
        /// Picks up an unfinished run. Only the day count and the progress made on
        /// the door and the drawers come back — where everyone was standing does
        /// not, so quitting mid-chase is not an escape route.
        /// </summary>
        void Restore()
        {
            var save = SaveSystem.Load();
            if (save == null) return;

            days.Restore(save.day);

            var inventory = player != null ? player.GetComponentInChildren<PlayerInventory>() : null;
            if (inventory != null) inventory.SetDarts(save.darts);

            var cleared = new HashSet<string>(save.clearedLockIds);
            foreach (var stage in FindObjectsByType<Interaction.LockStage>(FindObjectsSortMode.None))
                if (cleared.Contains(stage.name))
                    stage.ForceClear();

            var searched = new HashSet<string>(save.searchedContainerIds);
            foreach (var container in FindObjectsByType<Interaction.Drawer>(FindObjectsSortMode.None))
                if (searched.Contains(container.name))
                    container.MarkSearched();

            Debug.Log($"[GameDirector] resumed on day {save.day}");
        }

        void OnEnable()
        {
            if (granny != null) granny.Caught += OnCaught;
        }

        void OnDisable()
        {
            if (granny != null) granny.Caught -= OnCaught;
        }

        void Start()
        {
            ArmTheHouse();
            TellThePlayerWhatDayItIs(days.Day);
            DayStarted?.Invoke(days.Day);
        }

        /// <summary>
        /// Four nights of being caught show on the player, not in a number. The
        /// director is the only thing that knows which morning this is, so it is
        /// the only thing that can say.
        /// </summary>
        void TellThePlayerWhatDayItIs(int day)
        {
            var injury = player != null
                ? player.GetComponentInChildren<PlayerInjury>()
                : FindAnyObjectByType<PlayerInjury>();

            if (injury != null) injury.SetDay(day);
        }

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
                // Nothing more to wake up for; the screen stays dark, and there is
                // no run left to continue.
                SaveSystem.Delete();
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

            // Hers are picked up; the ones built into the house are reset where
            // they stand. A night's worth of traps should not carry over, or by
            // day four the house would be unplayable.
            var setter = FindAnyObjectByType<TrapSetter>();
            if (setter != null) setter.ClearAll();

            foreach (var trap in FindObjectsByType<BearTrap>(FindObjectsSortMode.None))
                trap.Rearm();

            ArmTheHouse();
            TellThePlayerWhatDayItIs(days.Day);

            // The run is written when a day begins and at no other time. Saving on
            // demand would let a player undo every mistake, and the mistakes are
            // what the five days are for.
            SaveSystem.Save(Snapshot());
        }

        /// <summary>
        /// Decides which of the house's floorboards creak, and resets the wires.
        ///
        /// The choice is made from each board's own position, not from a random
        /// number, so a given difficulty always creaks in the same places. A
        /// player who learns a quiet route keeps it; raising the difficulty takes
        /// it away.
        /// </summary>
        void ArmTheHouse()
        {
            foreach (var bell in FindObjectsByType<Interaction.TripwireBell>(FindObjectsSortMode.None))
                bell.Rearm();

            var boards = FindObjectsByType<Interaction.CreakyFloor>(FindObjectsSortMode.None)
                .OrderBy(board => PlacementKey(board.transform.position))
                .ToList();

            var share = difficulty != null ? difficulty.CreakyFloorShare : 0.4f;
            var live = Mathf.RoundToInt(boards.Count * Mathf.Clamp01(share));

            for (var i = 0; i < boards.Count; i++)
                boards[i].SetArmed(i < live);
        }

        /// <summary>A stable, scattered ordering of positions — not a hash of the
        /// object, which would change whenever the scene was rebuilt.</summary>
        static int PlacementKey(Vector3 position)
        {
            var x = Mathf.RoundToInt(position.x * 10f);
            var z = Mathf.RoundToInt(position.z * 10f);
            var y = Mathf.RoundToInt(position.y * 10f);
            return (x * 73856093) ^ (z * 19349663) ^ (y * 83492791);
        }

        /// <summary>Called by the front door once every lock is off.</summary>
        public void ReportEscape()
        {
            days.Escape();

            // A finished run should not offer to be continued.
            SaveSystem.Delete();
        }
    }
}
