using System.Collections.Generic;
using System.Linq;
using Granny.Core;
using Granny.Gameplay.Player;
using UnityEngine;

namespace Granny.Gameplay.Interaction
{
    /// <summary>
    /// The workbench where the crossbow gets put together.
    ///
    /// Three parts, one hand: it is three separate trips across the house, and
    /// each trip is another chance to be heard. That is the whole point of
    /// making the weapon a build rather than a pickup — the thing that stops her
    /// has to cost several of the runs she is most likely to interrupt.
    /// </summary>
    public sealed class WeaponBench : MonoBehaviour, IInteractable
    {
        [Header("Recipe")]
        [Tooltip("Tags that have to be delivered, one trip each.")]
        [SerializeField] List<string> requiredTags = new() { "bow_stock", "bow_limb", "bow_cord" };

        [Tooltip("Handed over once the last part is in.")]
        [SerializeField] ItemDefinition weapon;

        [Tooltip("Handed over with it, so the first shot is possible.")]
        [SerializeField] ItemDefinition dart;
        [SerializeField, Min(0)] int dartsSupplied = 3;

        readonly HashSet<string> delivered = new();

        public bool IsComplete => requiredTags.All(delivered.Contains);

        /// <summary>True once the finished weapon has actually been handed over.</summary>
        public bool HasBeenCollected { get; private set; }

        public IReadOnlyCollection<string> Delivered => delivered;

        public int PartsRemaining => requiredTags.Count(t => !delivered.Contains(t));

        public Transform Transform => transform;

        /// <summary>The last thing it said, so the HUD can explain a refusal.</summary>
        public string LastHint { get; private set; } = "";

        public string Prompt
        {
            get
            {
                if (HasBeenCollected) return "Workbench";
                if (IsComplete) return "Take the crossbow";
                return $"Fit a part ({PartsRemaining} missing)";
            }
        }

        public bool CanInteract(GameObject interactor) => !HasBeenCollected;

        public void Interact(GameObject interactor)
        {
            if (HasBeenCollected) return;

            var inventory = interactor.GetComponentInChildren<PlayerInventory>();
            if (inventory == null) return;

            if (IsComplete)
            {
                Collect(inventory);
                return;
            }

            var held = inventory.Held;
            if (held == null)
            {
                LastHint = "Nothing in hand.";
                return;
            }

            var tag = requiredTags.FirstOrDefault(t => held.HasTag(t) && !delivered.Contains(t));
            if (tag == null)
            {
                LastHint = $"{held.DisplayName} is not part of it.";
                return;
            }

            delivered.Add(tag);
            inventory.Consume(held);
            LastHint = IsComplete ? "That is the last of it." : $"{PartsRemaining} to go.";

            // Fitting a part is work, and work is heard.
            NoiseBus.Emit(transform.position, NoiseKind.ToolWork, gameObject);
        }

        void Collect(PlayerInventory inventory)
        {
            if (weapon == null)
            {
                LastHint = "Nothing to collect.";
                return;
            }

            inventory.TryTake(weapon);

            for (var i = 0; i < dartsSupplied && dart != null; i++)
                inventory.TryTake(dart);

            HasBeenCollected = true;
            LastHint = "Loaded.";
        }

        /// <summary>Puts the bench back to bare parts — used when a run restarts.</summary>
        public void Restore()
        {
            delivered.Clear();
            HasBeenCollected = false;
            LastHint = "";
        }
    }
}
