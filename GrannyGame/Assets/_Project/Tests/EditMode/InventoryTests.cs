using Granny.Core;
using Granny.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;

namespace Granny.Tests
{
    /// <summary>
    /// The hand/belt split is what makes tool trips costly, so these pin the rules
    /// that decide where an item lands and what it displaces.
    /// </summary>
    public class InventoryTests
    {
        GameObject host;
        PlayerInventory inventory;

        static ItemDefinition MakeItem(string id, ItemCarry carry, params string[] tags)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var so = new UnityEditor.SerializedObject(item);

            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("carry").enumValueIndex = (int)carry;

            var tagProp = so.FindProperty("tags");
            tagProp.arraySize = tags.Length;
            for (var i = 0; i < tags.Length; i++)
                tagProp.GetArrayElementAtIndex(i).stringValue = tags[i];

            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("InventoryHost");
            inventory = host.AddComponent<PlayerInventory>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        [Test]
        public void HeldItemGoesToTheHand()
        {
            var hammer = MakeItem("hammer", ItemCarry.Held, "pry");

            Assert.IsTrue(inventory.TryTake(hammer));
            Assert.AreSame(hammer, inventory.Held);
        }

        [Test]
        public void PocketedItemGoesToTheFirstFreeSlot()
        {
            var key = MakeItem("key", ItemCarry.Pocketed, "unlock");

            Assert.IsTrue(inventory.TryTake(key));
            Assert.IsNull(inventory.Held, "A pocketed item must not occupy the hands.");
            Assert.AreSame(key, inventory.Pockets[0]);
        }

        [Test]
        public void PocketsFillUpAndThenRefuse()
        {
            for (var i = 0; i < inventory.PocketCapacity; i++)
                Assert.IsTrue(inventory.TryTake(MakeItem($"small{i}", ItemCarry.Pocketed)));

            Assert.IsFalse(
                inventory.TryTake(MakeItem("overflow", ItemCarry.Pocketed)),
                "A full belt must refuse rather than silently discard.");
        }

        [Test]
        public void TakingASecondToolReplacesTheFirst()
        {
            var hammer = MakeItem("hammer", ItemCarry.Held, "pry");
            var cutters = MakeItem("cutters", ItemCarry.Held, "cut");

            inventory.TryTake(hammer);
            inventory.TryTake(cutters);

            Assert.AreSame(cutters, inventory.Held, "Carrying two tools at once would remove the cost of choosing.");
        }

        [Test]
        public void EquipSlotMovesAPocketedItemIntoTheHand()
        {
            var key = MakeItem("key", ItemCarry.Pocketed, "unlock");
            inventory.TryTake(key);

            inventory.EquipSlot(1);

            Assert.AreSame(key, inventory.Held);
            Assert.IsNull(inventory.Pockets[0]);
        }

        [Test]
        public void EquippingWhileHoldingAPocketedItemSwapsThem()
        {
            var key = MakeItem("key", ItemCarry.Pocketed, "unlock");
            var battery = MakeItem("battery", ItemCarry.Pocketed, "power");

            inventory.TryTake(key);       // slot 0
            inventory.TryTake(battery);   // slot 1
            inventory.EquipSlot(1);       // key to hand

            inventory.EquipSlot(2);       // battery to hand, key back to slot 1

            Assert.AreSame(battery, inventory.Held);
            Assert.AreSame(key, inventory.Pockets[1]);
        }

        [Test]
        public void EquippingAnEmptySlotDoesNothing()
        {
            var hammer = MakeItem("hammer", ItemCarry.Held);
            inventory.TryTake(hammer);

            inventory.EquipSlot(3);

            Assert.AreSame(hammer, inventory.Held);
        }

        [Test]
        public void FindByTagPrefersTheItemInHand()
        {
            var pocketKey = MakeItem("spare", ItemCarry.Pocketed, "unlock");
            var handKey = MakeItem("master", ItemCarry.Held, "unlock");

            inventory.TryTake(pocketKey);
            inventory.TryTake(handKey);

            Assert.AreSame(handKey, inventory.FindByTag("unlock"));
        }

        [Test]
        public void TagMatchingIsCaseInsensitiveAndMissesAreNull()
        {
            var key = MakeItem("key", ItemCarry.Pocketed, "Key.Front");
            inventory.TryTake(key);

            Assert.IsTrue(inventory.HasTag("key.front"));
            Assert.IsFalse(inventory.HasTag("cut"));
            Assert.IsNull(inventory.FindByTag("cut"));
        }

        [Test]
        public void ConsumeRemovesFromHandOrBeltAndRefusesUncarriedItems()
        {
            var held = MakeItem("hammer", ItemCarry.Held);
            var pocketed = MakeItem("key", ItemCarry.Pocketed);
            var stranger = MakeItem("stranger", ItemCarry.Pocketed);

            inventory.TryTake(held);
            inventory.TryTake(pocketed);

            Assert.IsTrue(inventory.Consume(held));
            Assert.IsNull(inventory.Held);

            Assert.IsTrue(inventory.Consume(pocketed));
            Assert.IsNull(inventory.Pockets[0]);

            Assert.IsFalse(inventory.Consume(stranger), "Consuming something never carried must fail.");
        }

        [Test]
        public void ChangedFiresOnEveryMutation()
        {
            var fired = 0;
            inventory.Changed += () => fired++;

            inventory.TryTake(MakeItem("key", ItemCarry.Pocketed));
            inventory.EquipSlot(1);

            Assert.AreEqual(2, fired);
        }

        [Test]
        public void TakingNullIsRefused()
        {
            Assert.IsFalse(inventory.TryTake(null));
        }
    }
}
