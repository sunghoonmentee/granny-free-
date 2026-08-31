using Granny.Core;
using UnityEngine;

namespace Granny.Gameplay.Interaction
{
    /// <summary>
    /// A swinging door.
    ///
    /// Doors are the loudest thing in the house that the player uses constantly,
    /// which is what makes them interesting: easing one open is quiet, and being
    /// chased through the same door is not. Granny slams them, which is both a
    /// noise and a warning.
    /// </summary>
    public sealed class HingeDoor : MonoBehaviour, IInteractable
    {
        [Header("Swing")]
        [Tooltip("Leaf that rotates. Usually a child of the frame.")]
        [SerializeField] Transform leaf;
        [SerializeField] float openAngle = 95f;
        [SerializeField, Min(1f)] float openSpeed = 220f;
        [SerializeField, Min(1f)] float slamSpeed = 900f;

        [Header("Locking")]
        [SerializeField] bool locked;
        [Tooltip("Tag of an item that unlocks this, e.g. 'key.bedroom'. Blank means no key exists.")]
        [SerializeField] string unlockTag = "";
        [Tooltip("Whether unlocking uses the item up.")]
        [SerializeField] bool consumeKey = true;

        [Header("Noise")]
        [SerializeField, Min(0f)] float openNoiseRadius = 6f;
        [SerializeField, Min(0f)] float slamNoiseRadius = 22f;
        [SerializeField, Min(0f)] float lockedRattleRadius = 4f;

        float closedYaw;
        float targetAngle;
        float currentAngle;
        float speed;

        public bool IsOpen => targetAngle != 0f;
        public bool IsLocked => locked;

        public Transform Transform => transform;

        public string Prompt
        {
            get
            {
                if (locked)
                    return string.IsNullOrEmpty(unlockTag) ? "Locked" : "Unlock";
                return IsOpen ? "Close" : "Open";
            }
        }

        void Awake()
        {
            if (leaf == null) leaf = transform;
            closedYaw = leaf.localEulerAngles.y;
            speed = openSpeed;
            gameObject.layer = GameLayers.Door;
        }

        void Update()
        {
            if (Mathf.Approximately(currentAngle, targetAngle)) return;

            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, speed * Time.deltaTime);
            leaf.localRotation = Quaternion.Euler(0f, closedYaw + currentAngle, 0f);

            if (Mathf.Approximately(currentAngle, targetAngle))
                speed = openSpeed;
        }

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor)
        {
            if (locked)
            {
                TryUnlock(interactor);
                return;
            }

            SetOpen(!IsOpen, interactor);
        }

        void TryUnlock(GameObject interactor)
        {
            if (string.IsNullOrEmpty(unlockTag))
            {
                // Nothing opens this from here. Rattling it is a small noise and
                // tells the player the door is real, not scenery.
                NoiseBus.Emit(transform.position, lockedRattleRadius, NoiseKind.Door, gameObject);
                return;
            }

            var inventory = interactor.GetComponentInChildren<Player.PlayerInventory>();
            var key = inventory != null ? inventory.FindByTag(unlockTag) : null;

            if (key == null)
            {
                NoiseBus.Emit(transform.position, lockedRattleRadius, NoiseKind.Door, gameObject);
                return;
            }

            locked = false;
            if (consumeKey && key.ConsumedOnUse) inventory.Consume(key);

            SetOpen(true, interactor);
        }

        /// <summary>Opens or closes at normal speed, with the matching noise.</summary>
        public void SetOpen(bool open, GameObject source = null)
        {
            if (locked && open) return;

            // Swing away from whoever opened it, so nobody walks through the leaf.
            if (open && source != null)
            {
                var side = Vector3.Dot(transform.forward, source.transform.position - transform.position);
                targetAngle = side > 0f ? -Mathf.Abs(openAngle) : Mathf.Abs(openAngle);
            }
            else
            {
                targetAngle = open ? Mathf.Abs(openAngle) : 0f;
            }

            speed = openSpeed;
            NoiseBus.Emit(transform.position, openNoiseRadius, NoiseKind.Door, gameObject);
        }

        /// <summary>Throws the door open hard. Loud, and meant to be heard rooms away.</summary>
        public void Slam(GameObject source = null)
        {
            locked = false;
            targetAngle = Mathf.Abs(openAngle);

            if (source != null)
            {
                var side = Vector3.Dot(transform.forward, source.transform.position - transform.position);
                if (side > 0f) targetAngle = -targetAngle;
            }

            speed = slamSpeed;
            NoiseBus.Emit(transform.position, slamNoiseRadius, NoiseKind.Door, gameObject);
        }

        public void SetLocked(bool value) => locked = value;
    }
}
