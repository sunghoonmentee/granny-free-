using System;
using Granny.Core;
using Granny.Gameplay.Interaction;
using UnityEngine;

namespace Granny.Gameplay.Player
{
    /// <summary>
    /// One item. That is the whole inventory.
    ///
    /// It is the rule the original is built on, and it is what turns the house
    /// into a logistics problem: the hammer is in the attic, the door is on the
    /// ground floor, and you cannot carry the wirecutters at the same time. Every
    /// tool is a separate trip, and every trip is another chance to be heard.
    ///
    /// The belt of pocketed items this replaced is preserved on the
    /// `backup/belt-inventory` branch.
    /// </summary>
    public sealed class PlayerInventory : MonoBehaviour
    {
        [Header("Dropping")]
        [SerializeField] Transform dropOrigin;
        [SerializeField, Min(0f)] float dropDistance = 0.7f;
        [SerializeField, Min(0f)] float throwSpeed = 9f;
        [SerializeField, Min(0f)] float throwSpin = 4f;

        [Header("Wiring")]
        [SerializeField] PlayerInputReader input;

        /// <summary>Where dropped items appear. Falls back to the player's own transform.</summary>
        Transform DropOrigin
        {
            get
            {
                if (dropOrigin != null) return dropOrigin;

                var cam = GetComponentInChildren<Camera>();
                dropOrigin = cam != null ? cam.transform : transform;
                return dropOrigin;
            }
        }

        /// <summary>
        /// Tags with a meaning the inventory itself has to know about. Everything
        /// else is just a string a lock compares against.
        /// </summary>
        public const string DartTag = "dart";

        public const string WeaponTag = "weapon";

        /// <summary>What is in the player's hands, or null.</summary>
        public ItemDefinition Held { get; private set; }

        public bool IsEmptyHanded => Held == null;

        /// <summary>
        /// Darts, which are the one thing that does not take up the hand.
        ///
        /// The crossbow is useless without them and they are useless without it,
        /// so making a player choose between carrying the weapon and carrying the
        /// ammunition is not a decision, it is a refusal. Everything else still
        /// obeys the one-hand rule.
        /// </summary>
        public int Darts { get; private set; }

        public bool HasWeapon => Held != null && Held.HasTag(WeaponTag);

        /// <summary>Raised whenever the hand changes, for the HUD.</summary>
        public event Action Changed;

        void Awake()
        {
            if (input == null) input = GetComponentInChildren<PlayerInputReader>();
        }

        void OnEnable()
        {
            if (input == null) return;
            input.Dropped += DropHeld;
            input.Threw += ThrowHeld;
        }

        void OnDisable()
        {
            if (input == null) return;
            input.Dropped -= DropHeld;
            input.Threw -= ThrowHeld;
        }

        /// <summary>
        /// Takes an item into the hand. Anything already held is put down rather
        /// than refused, so picking something up never silently does nothing — and
        /// it is put down where the new item was, so the two simply swap places
        /// instead of one being flung across the room.
        /// </summary>
        public bool TryTake(ItemDefinition item, Vector3? swapPosition = null)
        {
            if (item == null) return false;

            // Darts go in the pocket, not the hand.
            if (item.HasTag(DartTag))
            {
                Darts++;
                Changed?.Invoke();
                return true;
            }

            if (Held != null)
                SpawnInWorld(Held, swapPosition ?? DropPoint(), thrown: false);

            Held = item;
            Changed?.Invoke();
            return true;
        }

        public void DropHeld()
        {
            if (Held == null) return;

            SpawnInWorld(Held, DropPoint(), thrown: false);
            Held = null;
            Changed?.Invoke();
        }

        public void ThrowHeld()
        {
            if (Held == null) return;

            // The throw button fires a weapon rather than throwing it away. Both
            // listen to the same input, so without this the first shot would also
            // hurl the crossbow across the room.
            if (Held.HasTag(WeaponTag)) return;

            SpawnInWorld(Held, DropPoint(), thrown: true);
            Held = null;
            Changed?.Invoke();
        }

        /// <summary>
        /// Puts the held item on the floor at a given spot. Used when she catches
        /// the player: what they were carrying stays where they fell.
        /// </summary>
        public void DropHeldAt(Vector3 position)
        {
            if (Held == null) return;

            SpawnInWorld(Held, position, thrown: false);
            Held = null;
            Changed?.Invoke();
        }

        /// <summary>
        /// Removes the item after it has been used on something. Returns false if
        /// it was not actually being carried, so callers cannot conjure uses.
        /// </summary>
        public bool Consume(ItemDefinition item)
        {
            if (item == null || Held != item) return false;

            Held = null;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Spends one dart. Returns false when the quiver is empty, so a weapon
        /// cannot fire shots that were never picked up.
        /// </summary>
        public bool SpendDart()
        {
            if (Darts <= 0) return false;

            Darts--;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Puts darts back, e.g. when a day is restored from a save.</summary>
        public void SetDarts(int count)
        {
            Darts = Mathf.Max(0, count);
            Changed?.Invoke();
        }

        /// <summary>True if the item in hand carries this tag.</summary>
        public bool HasTag(string tag) => FindByTag(tag) != null;

        /// <summary>The held item if it matches the tag, otherwise null.</summary>
        public ItemDefinition FindByTag(string tag) =>
            Held != null && Held.HasTag(tag) ? Held : null;

        Vector3 DropPoint()
        {
            var origin = DropOrigin;
            return origin.position + origin.forward * dropDistance;
        }

        void SpawnInWorld(ItemDefinition item, Vector3 position, bool thrown)
        {
            var pickup = PickupItem.Spawn(item, position, UnityEngine.Random.rotation);
            if (pickup == null) return;

            if (!pickup.TryGetComponent<Rigidbody>(out var body)) return;

            var forward = DropOrigin.forward;

            body.linearVelocity = thrown
                ? forward * throwSpeed
                : forward * 0.4f;

            if (thrown)
                body.angularVelocity = UnityEngine.Random.onUnitSphere * throwSpin;
        }
    }
}
