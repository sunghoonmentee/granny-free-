using System;
using Granny.Core;
using UnityEngine;

namespace Granny.Gameplay.Interaction
{
    /// <summary>
    /// A wardrobe or the gap under a bed.
    ///
    /// Hiding is not safety, it is a gamble: it hides you from sight, it does not
    /// stop her searching, and it takes a moment to get back out. The occupant is
    /// parked at <see cref="viewpoint"/> and their motor is disabled, so being
    /// found has nothing to do with collision.
    /// </summary>
    public sealed class HidingSpot : MonoBehaviour, IInteractable
    {
        [Header("Placement")]
        [Tooltip("Where the player's eyes sit while hidden.")]
        [SerializeField] Transform viewpoint;
        [Tooltip("Where the player is put back down on leaving.")]
        [SerializeField] Transform exitPoint;

        [Header("Doors")]
        [Tooltip("Optional leaf that swings shut behind the player.")]
        [SerializeField] HingeDoor door;

        GameObject occupant;

        public bool IsOccupied => occupant != null;

        /// <summary>The hidden player, or null. Read by Granny's perception.</summary>
        public GameObject Occupant => occupant;

        /// <summary>Raised when someone enters or leaves, with the current occupant (null on exit).</summary>
        public event Action<GameObject> OccupancyChanged;

        public Transform Transform => transform;

        public string Prompt => IsOccupied ? "Get out" : "Hide";

        void Awake()
        {
            if (viewpoint == null) viewpoint = transform;
            if (exitPoint == null) exitPoint = transform;
            gameObject.layer = GameLayers.HidingSpot;
        }

        public bool CanInteract(GameObject interactor)
        {
            // Occupied spots are only interactable by whoever is inside, or there
            // would be a "Hide" prompt on a wardrobe you are already standing in.
            return !IsOccupied || occupant == interactor;
        }

        public void Interact(GameObject interactor)
        {
            if (IsOccupied) Exit();
            else Enter(interactor);
        }

        void Enter(GameObject who)
        {
            var motor = who.GetComponent<Player.PlayerMotor>();
            if (motor == null) return;

            occupant = who;
            motor.Warp(viewpoint.position, viewpoint.eulerAngles.y);
            motor.enabled = false;

            if (door != null) door.SetOpen(false, who);

            NoiseBus.Emit(transform.position, NoiseKind.Hiding, gameObject);
            OccupancyChanged?.Invoke(occupant);
        }

        void Exit()
        {
            var who = occupant;
            occupant = null;

            if (who != null && who.TryGetComponent<Player.PlayerMotor>(out var motor))
            {
                motor.enabled = true;
                motor.Warp(exitPoint.position, exitPoint.eulerAngles.y);
            }

            if (door != null) door.SetOpen(true, who);

            NoiseBus.Emit(transform.position, NoiseKind.Hiding, gameObject);
            OccupancyChanged?.Invoke(null);
        }

        /// <summary>
        /// Turfs the occupant out — used when Granny opens the wardrobe and finds
        /// them, before the catch is resolved.
        /// </summary>
        public void ForceExit()
        {
            if (IsOccupied) Exit();
        }
    }
}
