using Granny.Core;
using UnityEngine;

namespace Granny.Gameplay.Interaction
{
    /// <summary>
    /// A drawer or cabinet that slides open and may be hiding something.
    ///
    /// Searching is the loop's slow half: every drawer is a few seconds standing
    /// still with your back to a doorway, and the noise it makes is the price of
    /// finding out it was empty.
    /// </summary>
    public sealed class Drawer : MonoBehaviour, IInteractable
    {
        [Header("Slide")]
        [Tooltip("The part that moves. Usually a child of the cabinet.")]
        [SerializeField] Transform sliding;
        [Tooltip("Local direction the drawer pulls out along.")]
        [SerializeField] Vector3 slideAxis = Vector3.forward;
        [SerializeField, Min(0f)] float slideDistance = 0.42f;
        [SerializeField, Min(0.05f)] float slideSpeed = 1.4f;

        [Header("Contents")]
        [Tooltip("Item revealed on first opening. Assigned by the spawn table at level setup.")]
        [SerializeField] ItemDefinition contents;
        [SerializeField] Transform contentsAnchor;

        [Header("Noise")]
        [SerializeField, Min(0f)] float noiseRadius = 5.5f;

        Vector3 closedPosition;
        float openAmount;
        float target;
        bool searched;

        public bool IsOpen => target > 0.5f;

        /// <summary>True once the drawer has been opened at least once.</summary>
        public bool HasBeenSearched => searched;

        public ItemDefinition Contents => contents;

        public Transform Transform => transform;

        public string Prompt => IsOpen ? "Close" : searched ? "Search again" : "Search";

        void Awake()
        {
            if (sliding == null) sliding = transform;
            closedPosition = sliding.localPosition;
            slideAxis = slideAxis.sqrMagnitude > 0f ? slideAxis.normalized : Vector3.forward;
            gameObject.layer = GameLayers.Interactable;
        }

        void Update()
        {
            if (Mathf.Approximately(openAmount, target)) return;

            openAmount = Mathf.MoveTowards(openAmount, target, slideSpeed * Time.deltaTime);
            sliding.localPosition = closedPosition + slideAxis * (slideDistance * openAmount);
        }

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor)
        {
            target = IsOpen ? 0f : 1f;
            NoiseBus.Emit(transform.position, noiseRadius, NoiseKind.Machine, gameObject);

            if (!IsOpen || searched) return;

            searched = true;
            Reveal();
        }

        void Reveal()
        {
            if (contents == null) return;

            var anchor = contentsAnchor != null ? contentsAnchor : sliding;
            PickupItem.Spawn(contents, anchor.position, Quaternion.identity);
            contents = null;
        }

        /// <summary>Used by the level's randomised spawn table before play starts.</summary>
        public void SetContents(ItemDefinition item)
        {
            contents = item;
            searched = false;
        }

        /// <summary>
        /// Marks the drawer already gone through, without spilling its contents.
        /// Used when resuming a saved run: the player searched this yesterday, and
        /// finding the same hammer in it twice would break the run.
        /// </summary>
        public void MarkSearched()
        {
            searched = true;
            contents = null;
        }
    }
}
