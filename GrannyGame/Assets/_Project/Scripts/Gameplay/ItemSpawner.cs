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

        [Header("Determinism")]
        [Tooltip("Non-zero repeats the same layout every run — useful when testing.")]
        [SerializeField] int seed;

        /// <summary>Which container each required item ended up in, for debugging.</summary>
        public IReadOnlyDictionary<string, string> Placements => placements;

        readonly Dictionary<string, string> placements = new();

        void Awake()
        {
            if (containers.Count == 0)
                containers.AddRange(FindObjectsByType<Drawer>(FindObjectsSortMode.None));

            Distribute();
        }

        void Distribute()
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

            var random = seed != 0 ? new System.Random(seed) : new System.Random();
            var shuffled = new List<Drawer>(containers);

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
