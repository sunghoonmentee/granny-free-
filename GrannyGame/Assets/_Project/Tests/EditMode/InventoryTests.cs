using Granny.Core;
using Granny.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;

namespace Granny.Tests
{
    /// <summary>
    /// One hand, one item. This is the rule that makes the house a logistics
    /// problem, so these pin it: nothing accumulates, and taking something always
    /// costs whatever was already being carried.
    /// </summary>
    public class InventoryTests
    {
        GameObject host;
        PlayerInventory inventory;

        static ItemDefinition MakeItem(string id, params string[] tags)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var so = new UnityEditor.SerializedObject(item);

            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;

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
        public void TakingAnItemPutsItInTheHand()
        {
            var hammer = MakeItem("hammer", "pry");

            Assert.IsTrue(inventory.TryTake(hammer));
            Assert.AreSame(hammer, inventory.Held);
            Assert.IsFalse(inventory.IsEmptyHanded);
        }

        [Test]
        public void StartsEmptyHanded()
        {
            Assert.IsNull(inventory.Held);
            Assert.IsTrue(inventory.IsEmptyHanded);
        }

        [Test]
        public void ASecondItemReplacesTheFirst()
        {
            var hammer = MakeItem("hammer", "pry");
            var cutters = MakeItem("cutters", "cut");

            inventory.TryTake(hammer);
            inventory.TryTake(cutters);

            Assert.AreSame(cutters, inventory.Held,
                "Carrying two things at once would remove the cost of choosing.");
        }

        [Test]
        public void EvenAKeyTakesTheHand()
        {
            // Keys used to go on a belt. They do not any more: fetching the key
            // means putting the hammer down, same as everything else.
            var hammer = MakeItem("hammer", "pry");
            var key = MakeItem("key.front", "key.front");

            inventory.TryTake(hammer);
            inventory.TryTake(key);

            Assert.AreSame(key, inventory.Held);
        }

        [Test]
        public void DroppingEmptiesTheHand()
        {
            inventory.TryTake(MakeItem("hammer"));

            inventory.DropHeld();

            Assert.IsNull(inventory.Held);
        }

        [Test]
        public void ThrowingEmptiesTheHand()
        {
            inventory.TryTake(MakeItem("bottle"));

            inventory.ThrowHeld();

            Assert.IsNull(inventory.Held);
        }

        [Test]
        public void DroppingWithEmptyHandsIsHarmless()
        {
            Assert.DoesNotThrow(() => inventory.DropHeld());
            Assert.DoesNotThrow(() => inventory.ThrowHeld());
            Assert.DoesNotThrow(() => inventory.DropHeldAt(Vector3.zero));
        }

        [Test]
        public void TagMatchingIsCaseInsensitiveAndMissesAreNull()
        {
            inventory.TryTake(MakeItem("key", "Key.Front"));

            Assert.IsTrue(inventory.HasTag("key.front"));
            Assert.IsFalse(inventory.HasTag("cut"));
            Assert.IsNull(inventory.FindByTag("cut"));
        }

        [Test]
        public void ATagOnlyMatchesWhatIsActuallyInHand()
        {
            var key = MakeItem("key", "unlock");
            inventory.TryTake(key);
            inventory.TryTake(MakeItem("hammer", "pry"));

            Assert.IsFalse(inventory.HasTag("unlock"),
                "The key was put down to pick up the hammer; it cannot still open anything.");
        }

        [Test]
        public void ConsumeRemovesFromTheHandAndRefusesUncarriedItems()
        {
            var held = MakeItem("hammer");
            var stranger = MakeItem("stranger");

            inventory.TryTake(held);

            Assert.IsTrue(inventory.Consume(held));
            Assert.IsNull(inventory.Held);
            Assert.IsFalse(inventory.Consume(stranger), "Consuming something never carried must fail.");
        }

        [Test]
        public void ChangedFiresOnEveryMutation()
        {
            var fired = 0;
            inventory.Changed += () => fired++;

            inventory.TryTake(MakeItem("key"));
            inventory.DropHeld();

            Assert.AreEqual(2, fired);
        }

        [Test]
        public void TakingNullIsRefused()
        {
            Assert.IsFalse(inventory.TryTake(null));
        }
    }
}
