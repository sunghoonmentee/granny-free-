using System.Collections.Generic;
using Granny.Core;
using Granny.Gameplay.Interaction;
using UnityEngine;

namespace Granny.Gameplay
{
    /// <summary>
    /// Decides which container holds which tool, freshly each run.
    ///
    /// This is what stops the game becoming a memorised route. The house stays
    /// the same so the player can learn it; where the hammer is does not, so
    /// learning the house is a skill rather than a walkthrough.
    /// </summary>
    public sealed class ItemSpawner : MonoBehaviour
    {
        [Header("What to hide")]
        [Tooltip("Every tool the run needs. Each is placed in exactly one container.")]
        [SerializeField] List<ItemDefinition> requiredItems = new();

        [Tooltip("Extra flavour items placed in whatever containers are left over.")]
        [SerializeField] List<ItemDefinition> optionalItems = new();

        [Header("Where to hide it")]
        [Tooltip("Containers to draw from. Collected from the scene if empty.")]
        [SerializeField] List<Drawer> containers = new();

        [Header("Layouts")]
        /// <summary>
        /// Five fixed arrangements rather than a fresh shuffle every time.
        ///
        /// Pure randomness means the house can never be learned — every run is
        /// the same amount of searching, and knowing the place buys nothing. A
        /// small set of layouts gives a player something to recognise two rooms
        /// in ("this is the one with the cutters in the attic") while still
        /// making the first drawer a real question. It is also the difference
        /// between being able to test that the run is winnable and hoping.
        /// </summary>
        [Tooltip("One seed per layout. Changing these changes what players have learned.")]
        [SerializeField] int[] layoutSeeds = { 1101, 2203, 3307, 4409, 5501 };

        /// <summary>Which layout this run is using, counting from zero.</summary>
        public int Layout { get; private set; }

        public int LayoutCount => Mathf.Max(1, layoutSeeds.Length);

        /// <summary>Which container each required item ended up in, for debugging.</summary>
        public IReadOnlyDictionary<string, string> Placements => placements;

        readonly Dictionary<string, string> placements = new();

        void Awake()
        {
            if (containers.Count == 0)
                containers.AddRange(FindObjectsByType<Drawer>(FindObjectsSortMode.None));

            // A run that is being resumed brings its layout with it; a new one
            // picks. Re-rolling on resume would move every tool in the house
            // while the player was looking away.
            var save = SaveSystem.Load();
            Apply(save != null ? save.itemLayout : Random.Range(0, LayoutCount));
        }

        /// <summary>
        /// Lays the house out as <paramref name="layout"/> says. Public so the
        /// winnability test can walk all five without starting five runs.
        /// </summary>
        public void Apply(int layout)
        {
            Layout = ((layout % LayoutCount) + LayoutCount) % LayoutCount;
            Distribute(layoutSeeds[Layout]);
        }

        void Distribute(int seed)
        {
            placements.Clear();

            if (containers.Count == 0)
            {
                Debug.LogError($"[{nameof(ItemSpawner)}] No containers to hide anything in.", this);
                return;
            }

            if (containers.Count < requiredItems.Count)
            {
                Debug.LogError(
                    $"[{nameof(ItemSpawner)}] {requiredItems.Count} required items but only " +
                    $"{containers.Count} containers — the run would be unwinnable.", this);
                return;
            }

            // Containers are ordered by name first, so the same seed means the
            // same layout no matter what order the scene happens to report them
            // in. Without this the "fixed" layouts would drift every rebuild.
            var random = new System.Random(seed);
            var shuffled = new List<Drawer>(containers);
            shuffled.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            // Fisher-Yates, so every container is equally likely to hold the tool
            // that unblocks the run rather than the early ones being favoured.
            for (var i = shuffled.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }

            var index = 0;

            foreach (var item in requiredItems)
            {
                if (item == null) continue;

                var container = shuffled[index++];
                container.SetContents(item);
                placements[item.Id] = container.name;
            }

            foreach (var item in optionalItems)
            {
                if (item == null || index >= shuffled.Count) break;
                shuffled[index++].SetContents(item);
            }

            // Anything left over is genuinely empty. Opening a drawer and finding
            // nothing is the cost that makes finding something matter.
            for (; index < shuffled.Count; index++)
                shuffled[index].SetContents(null);
        }
    }
}
