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

        /// <summary>
        /// The fastenings, resolved on first use rather than in Awake. Edit-mode
        /// tooling and tests inspect this door without ever running the
        /// MonoBehaviour lifecycle, and a door that reports "nothing holding it"
        /// until Awake fires is a dangerous thing to get wrong.
        /// </summary>
        LockStage[] Stages
        {
            get
            {
                if (stages == null || stages.Length == 0)
                    stages = GetComponentsInChildren<LockStage>(includeInactive: true);

                return stages;
            }
        }

        public bool IsUnlocked => Stages.All(s => s == null || s.IsCleared);

        public int StagesRemaining => Stages.Count(s => s != null && !s.IsCleared);

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
            _ = Stages;
            if (leaf == null) leaf = GetComponentInChildren<HingeDoor>();
            gameObject.layer = GameLayers.Interactable;
        }

        public bool CanInteract(GameObject interactor) => !used;

        public void Interact(GameObject interactor)
        {
            if (used) return;

            if (!IsUnlocked)
            {
                // Rattling a door she nailed shut is not something she comes for -
                // but it tells the player the door is real and still fastened.
                NoiseBus.Emit(transform.position, NoiseKind.LockedRattle, gameObject);
                return;
            }

            used = true;

            if (leaf != null) leaf.SetOpen(true, interactor);

            // The front door coming open is the loudest thing in the house. It no
            // longer matters to her - the run is over - but it should be heard.
            NoiseBus.Emit(transform.position, NoiseKind.DoorSlam, gameObject);

            Escaped?.Invoke();

            var director = FindAnyObjectByType<GameDirector>();
            if (director != null) director.ReportEscape();
        }

        /// <summary>Re-fastens everything — used when restarting a run.</summary>
        public void Restore()
        {
            used = false;
            foreach (var stage in Stages)
                if (stage != null) stage.Restore();
        }
    }
}
