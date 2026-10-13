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

        [Tooltip("Degrees to swing instead of sliding. Zero slides. For a door on a hinge.")]
        [SerializeField] float swingDegrees;

        [Header("Contents")]
        [Tooltip("Item revealed on first opening. Assigned by the spawn table at level setup.")]
        [SerializeField] ItemDefinition contents;
        [SerializeField] Transform contentsAnchor;

        [Header("Lock")]
        [Tooltip("Tag of the thing that opens it, e.g. 'safecode'. Blank means it is not locked.")]
        [SerializeField] string requiredTag = "";

        [Tooltip("Shown when the player tries it empty-handed.")]
        [SerializeField] string missingHint = "There is a dial on it, and four digits you do not know.";

        Vector3 closedPosition;
        Quaternion closedRotation;
        float openAmount;
        float target;
        bool searched;
        bool unlocked;

        /// <summary>
        /// What opens this, or blank. Public so the spawn table can refuse to put
        /// the key to a thing inside the thing — which is the one arrangement that
        /// makes a run unwinnable, and which a shuffle will produce eventually.
        /// </summary>
        public string RequiredTag => requiredTag;

        /// <summary>True while it still wants something the player has not brought.</summary>
        public bool IsLocked => !string.IsNullOrEmpty(requiredTag) && !unlocked;

        /// <summary>The most recent reason it would not open, for the HUD.</summary>
        public string LastHint { get; private set; } = string.Empty;

        public bool IsOpen => target > 0.5f;

        /// <summary>True once the drawer has been opened at least once.</summary>
        public bool HasBeenSearched => searched;

        public ItemDefinition Contents => contents;

        public Transform Transform => transform;

        public string Prompt =>
            IsLocked ? "Work the dial"
            : IsOpen ? "Close"
            : searched ? "Search again"
            : "Search";

        void Awake()
        {
            if (sliding == null) sliding = transform;
            closedPosition = sliding.localPosition;
            closedRotation = sliding.localRotation;
            slideAxis = slideAxis.sqrMagnitude > 0f ? slideAxis.normalized : Vector3.forward;
            gameObject.layer = GameLayers.Interactable;
        }

        void Update()
        {
            if (Mathf.Approximately(openAmount, target)) return;

            openAmount = Mathf.MoveTowards(openAmount, target, slideSpeed * Time.deltaTime);

            // A drawer slides; a door turns. Both are the same component because
            // both are "a container that takes a moment to look inside", which is
            // the only thing the rest of the game cares about — but a safe whose
            // front pops straight out at you looks like the door has come off
            // rather than opened.
            if (Mathf.Approximately(swingDegrees, 0f))
                sliding.localPosition = closedPosition + slideAxis * (slideDistance * openAmount);
            else
                sliding.localRotation = closedRotation *
                                        Quaternion.Euler(0f, swingDegrees * openAmount, 0f);
        }

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor)
        {
            if (IsLocked && !TryUnlock(interactor)) return;

            target = IsOpen ? 0f : 1f;
            // Reported, but filtered: searching costs time with your back to the
            // door, not noise. See NoiseRules.
            NoiseBus.Emit(transform.position, NoiseKind.Container, gameObject);

            if (!IsOpen || searched) return;

            searched = true;
            Reveal();
        }

        /// <summary>
        /// Opens the lock if the player brought the thing that opens it.
        ///
        /// The code is a note, not a keypad the player types on. A combination
        /// you have to carry to the door costs a trip across the house like
        /// everything else here; one you can read off the screen and remember
        /// costs nothing on the second night.
        /// </summary>
        bool TryUnlock(GameObject interactor)
        {
            var inventory = interactor != null
                ? interactor.GetComponentInChildren<Player.PlayerInventory>()
                : null;

            var key = inventory != null ? inventory.FindByTag(requiredTag) : null;

            if (key == null)
            {
                LastHint = missingHint;

                // Trying it is not free. Standing at a safe turning a dial is the
                // same mistake as rattling a locked door, and makes the same
                // amount of noise.
                NoiseBus.Emit(transform.position, NoiseKind.LockedRattle, gameObject);
                return false;
            }

            LastHint = string.Empty;
            unlocked = true;

            if (key.ConsumedOnUse) inventory.Consume(key);

            NoiseBus.Emit(transform.position, NoiseKind.ToolWork, gameObject);
            return true;
        }

        /// <summary>Opens the lock without the key — for resuming a saved run.</summary>
        public void ForceUnlock() => unlocked = true;

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
