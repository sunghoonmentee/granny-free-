using System;
using Granny.Core;
using UnityEngine;

namespace Granny.Gameplay.Interaction
{
    /// <summary>
    /// One of the fastenings holding the front door shut: a nailed plank, an
    /// alarm cord, a padlock.
    ///
    /// Each stage wants a different tool, and the player can only hold one tool
    /// at a time. That is the whole shape of the endgame — three trips across the
    /// house, each one louder than the last, with the door as the fixed point
    /// Granny learns to watch.
    /// </summary>
    public sealed class LockStage : MonoBehaviour, IInteractable
    {
        [Header("Requirement")]
        [Tooltip("Tag of the tool that removes this, e.g. 'pry', 'cut', 'key.front'.")]
        [SerializeField] string requiredTag = "pry";
        [SerializeField] string verb = "Pry off";
        [SerializeField] string missingToolHint = "It is nailed shut.";

        [Header("Work")]
        [Tooltip("Seconds of holding still to remove it. Long enough to be caught doing it.")]
        [SerializeField, Min(0f)] float workSeconds = 1.6f;
        [SerializeField, Min(0f)] float workNoiseRadius = 18f;

        [Header("Visuals")]
        [Tooltip("Hidden once the stage is cleared.")]
        [SerializeField] GameObject visual;

        float workRemaining;

        public bool IsCleared { get; private set; }

        /// <summary>The most recent reason the player could not proceed, for the HUD.</summary>
        public string LastHint { get; private set; } = string.Empty;

        public event Action<LockStage> Cleared;

        public Transform Transform => transform;

        public string Prompt
        {
            get
            {
                if (IsCleared) return string.Empty;
                if (workRemaining > 0f) return $"{verb}...";
                return verb;
            }
        }

        void Awake()
        {
            workRemaining = workSeconds;
            gameObject.layer = GameLayers.Interactable;
        }

        public bool CanInteract(GameObject interactor) => !IsCleared;

        public void Interact(GameObject interactor)
        {
            if (IsCleared) return;

            var inventory = interactor.GetComponentInChildren<Player.PlayerInventory>();
            var tool = inventory != null ? inventory.FindByTag(requiredTag) : null;

            if (tool == null)
            {
                LastHint = missingToolHint;
                return;
            }

            LastHint = string.Empty;

            // Each press is one swing of the hammer, not an instant solve. The
            // noise goes out every time, so a locked-out player is announcing
            // their position at the door for as long as the job takes.
            NoiseBus.Emit(transform.position, workNoiseRadius, NoiseKind.Breakage, gameObject);

            workRemaining -= 0.5f;
            if (workRemaining > 0f) return;

            if (tool.ConsumedOnUse) inventory.Consume(tool);

            IsCleared = true;
            if (visual != null) visual.SetActive(false);
            Cleared?.Invoke(this);
        }

        /// <summary>
        /// Marks the fastening already removed, without the work or the noise.
        /// Used when resuming a saved run, where the player already paid for it.
        /// </summary>
        public void ForceClear()
        {
            if (IsCleared) return;

            IsCleared = true;
            workRemaining = 0f;
            if (visual != null) visual.SetActive(false);
            Cleared?.Invoke(this);
        }

        /// <summary>Puts the fastening back — used when restarting a run.</summary>
        public void Restore()
        {
            IsCleared = false;
            workRemaining = workSeconds;
            LastHint = string.Empty;
            if (visual != null) visual.SetActive(true);
        }
    }
}
