using System;
using System.Collections.Generic;
using Granny.Core;
using Granny.Gameplay.Interaction;
using UnityEngine;

namespace Granny.Gameplay.Player
{
    /// <summary>
    /// One item in the hands, plus a small belt of pocketed items.
    ///
    /// The split is the point: tools are bulky, so carrying the hammer means not
    /// carrying the wirecutters, and every trip back to the toolbox is a trip
    /// past whatever is hunting you. Keys and cogs are small and just accumulate.
    /// </summary>
    public sealed class PlayerInventory : MonoBehaviour
    {
        [Header("Capacity")]
        [SerializeField, Range(1, 8)] int pocketSlots = 5;

        [Header("Dropping")]
        [SerializeField] Transform dropOrigin;
        [SerializeField, Min(0f)] float dropDistance = 0.7f;
        [SerializeField, Min(0f)] float throwSpeed = 9f;
        [SerializeField, Min(0f)] float throwSpin = 4f;

        [Header("Wiring")]
        [SerializeField] PlayerInputReader input;

        ItemDefinition[] pockets;

        /// <summary>
        /// The belt, allocated on first use rather than in Awake. Edit-mode tests
        /// never run the MonoBehaviour lifecycle, and an inventory that only works
        /// once Awake has fired is both harder to test and easy to break by
        /// adding the component at runtime.
        /// </summary>
        ItemDefinition[] Belt => pockets ??= new ItemDefinition[Mathf.Max(1, pocketSlots)];

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

        public ItemDefinition Held { get; private set; }

        /// <summary>Pocketed items, in slot order. Null entries are empty slots.</summary>
        public IReadOnlyList<ItemDefinition> Pockets => Belt;

        public int PocketCapacity => Belt.Length;

        /// <summary>Raised whenever the hand or the belt changes, for the HUD.</summary>
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
            input.SlotSelected += EquipSlot;
        }

        void OnDisable()
        {
            if (input == null) return;
            input.Dropped -= DropHeld;
            input.Threw -= ThrowHeld;
            input.SlotSelected -= EquipSlot;
        }

        /// <summary>
        /// Takes an item into the appropriate place. Held items displace whatever
        /// is already in the hand — dropping it at the player's feet rather than
        /// refusing, so picking something up never silently does nothing.
        /// </summary>
        public bool TryTake(ItemDefinition item)
        {
            if (item == null) return false;

            if (item.Carry == ItemCarry.Pocketed)
            {
                var slot = System.Array.IndexOf(Belt, null);
                if (slot < 0) return false;

                Belt[slot] = item;
                Changed?.Invoke();
                return true;
            }

            if (Held != null) SpawnInWorld(Held, thrown: false);

            Held = item;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Moves a pocketed item into the hand, pocketing or dropping what was there.</summary>
        public void EquipSlot(int oneBasedSlot)
        {
            var index = oneBasedSlot - 1;
            if (index < 0 || index >= Belt.Length) return;

            var wanted = Belt[index];
            if (wanted == null) return;

            var previous = Held;
            Held = wanted;
            Belt[index] = null;

            if (previous != null)
            {
                if (previous.Carry == ItemCarry.Pocketed) Belt[index] = previous;
                else SpawnInWorld(previous, thrown: false);
            }

            Changed?.Invoke();
        }

        public void DropHeld()
        {
            if (Held == null) return;

            SpawnInWorld(Held, thrown: false);
            Held = null;
            Changed?.Invoke();
        }

        public void ThrowHeld()
        {
            if (Held == null) return;

            SpawnInWorld(Held, thrown: true);
            Held = null;
            Changed?.Invoke();
        }

        /// <summary>
        /// Removes an item after it has been used on something. Returns false if
        /// the item was not actually carried, so callers cannot conjure uses.
        /// </summary>
        public bool Consume(ItemDefinition item)
        {
            if (item == null) return false;

            if (Held == item)
            {
                Held = null;
                Changed?.Invoke();
                return true;
            }

            var slot = System.Array.IndexOf(Belt, item);
            if (slot < 0) return false;

            Belt[slot] = null;
            Changed?.Invoke();
            return true;
        }

        /// <summary>True if the hand or the belt holds something with this tag.</summary>
        public bool HasTag(string tag) => FindByTag(tag) != null;

        /// <summary>
        /// The carried item matching a tag, preferring what is already in hand so
        /// the player's explicit choice wins over belt order.
        /// </summary>
        public ItemDefinition FindByTag(string tag)
        {
            if (Held != null && Held.HasTag(tag)) return Held;

            foreach (var item in Belt)
                if (item != null && item.HasTag(tag))
                    return item;

            return null;
        }

        void SpawnInWorld(ItemDefinition item, bool thrown)
        {
            var origin = DropOrigin;
            var forward = origin.forward;
            var position = origin.position + forward * dropDistance;

            var pickup = PickupItem.Spawn(item, position, UnityEngine.Random.rotation);
            if (pickup == null) return;

            if (!pickup.TryGetComponent<Rigidbody>(out var body)) return;

            body.linearVelocity = thrown
                ? forward * throwSpeed
                : forward * 1.2f + Vector3.up * 0.4f;

            if (thrown)
                body.angularVelocity = UnityEngine.Random.onUnitSphere * throwSpin;
        }
    }
}
