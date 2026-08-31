using System;
using System.Linq;
using Granny.Core;
using UnityEngine;

namespace Granny.Gameplay.Interaction
{
    /// <summary>
    /// The front door, and the win condition.
    ///
    /// It stays shut until every <see cref="LockStage"/> on it is cleared. Until
    /// then it still responds — telling the player exactly what is still holding
    /// it — because a door that does nothing reads as scenery, and the point of
    /// the whole game is that this one is the way out.
    /// </summary>
    public sealed class EscapeDoor : MonoBehaviour, IInteractable
    {
        [Header("Fastenings")]
        [Tooltip("Every stage that must be cleared. Collected from children if empty.")]
        [SerializeField] LockStage[] stages = Array.Empty<LockStage>();

        [Header("Opening")]
        [SerializeField] HingeDoor leaf;
        [SerializeField, Min(0f)] float openNoiseRadius = 25f;

        public bool IsUnlocked => stages.All(s => s == null || s.IsCleared);

        public int StagesRemaining => stages.Count(s => s != null && !s.IsCleared);

        /// <summary>Raised once, when the player steps through.</summary>
        public event Action Escaped;

        bool used;

        public Transform Transform => transform;

        public string Prompt
        {
            get
            {
                if (used) return string.Empty;
                if (IsUnlocked) return "Leave";

                var remaining = StagesRemaining;
                return remaining == 1 ? "One fastening left" : $"{remaining} fastenings left";
            }
        }

        void Awake()
        {
            if (stages.Length == 0)
                stages = GetComponentsInChildren<LockStage>(includeInactive: true);

            if (leaf == null) leaf = GetComponentInChildren<HingeDoor>();
            gameObject.layer = GameLayers.Interactable;
        }

        public bool CanInteract(GameObject interactor) => !used;

        public void Interact(GameObject interactor)
        {
            if (used) return;

            if (!IsUnlocked)
            {
                // Rattling it is a small noise, and a reminder that she can hear
                // you trying.
                NoiseBus.Emit(transform.position, 6f, NoiseKind.Door, gameObject);
                return;
            }

            used = true;

            if (leaf != null) leaf.SetOpen(true, interactor);
            NoiseBus.Emit(transform.position, openNoiseRadius, NoiseKind.Door, gameObject);

            Escaped?.Invoke();

            var director = FindAnyObjectByType<GameDirector>();
            if (director != null) director.ReportEscape();
        }

        /// <summary>Re-fastens everything — used when restarting a run.</summary>
        public void Restore()
        {
            used = false;
            foreach (var stage in stages)
                if (stage != null) stage.Restore();
        }
    }
}
