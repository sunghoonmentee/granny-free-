using System;
using Granny.Core;
using UnityEngine;

namespace Granny.Gameplay.Player
{
    /// <summary>
    /// Looks for whatever is under the crosshair and runs it when E is pressed.
    ///
    /// The aim is deliberately forgiving: a thin ray first, then a short sphere
    /// cast. Fumbling for a drawer handle while something is walking up the
    /// corridor behind you should not be the hard part.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Header("Reach")]
        [SerializeField, Min(0.5f)] float range = 2.6f;
        [Tooltip("Radius of the fallback sweep when the thin ray misses.")]
        [SerializeField, Min(0f)] float forgiveness = 0.18f;

        [Header("Wiring")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] Transform eye;

        readonly RaycastHit[] hits = new RaycastHit[8];

        /// <summary>What is currently under the crosshair, or null.</summary>
        public IInteractable Target { get; private set; }

        /// <summary>Raised when the target changes, including to null. For the HUD prompt.</summary>
        public event Action<IInteractable> TargetChanged;

        void Awake()
        {
            if (input == null) input = GetComponentInChildren<PlayerInputReader>();
            if (eye == null)
            {
                var cam = GetComponentInChildren<Camera>();
                eye = cam != null ? cam.transform : transform;
            }
        }

        void OnEnable()
        {
            if (input != null) input.Interacted += TryInteract;
        }

        void OnDisable()
        {
            if (input != null) input.Interacted -= TryInteract;
            SetTarget(null);
        }

        void Update() => SetTarget(FindTarget());

        void SetTarget(IInteractable next)
        {
            if (ReferenceEquals(next, Target)) return;

            Target = next;
            TargetChanged?.Invoke(next);
        }

        IInteractable FindTarget()
        {
            var ray = new Ray(eye.position, eye.forward);

            if (Physics.Raycast(ray, out var hit, range, GameLayers.InteractionMask, QueryTriggerInteraction.Collide))
            {
                var direct = Resolve(hit.collider);
                if (direct != null) return direct;
            }

            // The thin ray missed. Sweep a small sphere and take whichever
            // candidate is closest to the centre of the screen, not merely
            // nearest, so a large prop cannot steal a small handle beside it.
            var count = Physics.SphereCastNonAlloc(
                ray, forgiveness, hits, range, GameLayers.InteractionMask, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            var bestAlignment = -1f;

            for (var i = 0; i < count; i++)
            {
                var candidate = Resolve(hits[i].collider);
                if (candidate == null) continue;

                var toTarget = (candidate.Transform.position - eye.position).normalized;
                var alignment = Vector3.Dot(eye.forward, toTarget);

                if (alignment <= bestAlignment) continue;

                bestAlignment = alignment;
                best = candidate;
            }

            return best;
        }

        IInteractable Resolve(Component collider)
        {
            // Colliders often sit on a child of the object that owns the logic.
            var interactable = collider.GetComponentInParent<IInteractable>();
            if (interactable == null) return null;

            return interactable.CanInteract(gameObject) ? interactable : null;
        }

        void TryInteract()
        {
            if (Target == null) return;
            if (!Target.CanInteract(gameObject)) return;

            Target.Interact(gameObject);

            // The interaction may have destroyed or disabled the target.
            SetTarget(FindTarget());
        }
    }
}
