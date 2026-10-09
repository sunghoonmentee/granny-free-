using System.Collections;
using Granny.Core;
using Granny.Gameplay.AI;
using Granny.Gameplay.Interaction;
using Granny.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// Three trips to build it, three darts to spend, and a noise every time.
    ///
    /// The weapon has to be worth the nights it costs without ever being the
    /// answer to the house, so these check both halves: that the bench refuses
    /// shortcuts, and that firing is never free.
    /// </summary>
    public class CrossbowTests
    {
        GameObject root;
        GameObject player;
        PlayerInventory inventory;
        Crossbow crossbow;
        WeaponBench bench;

        static void Wire(Object target, string field, Object value)
        {
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
#endif
        }

        static ItemDefinition MakeItem(string id, params string[] tags)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(item);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;

            var tagProp = so.FindProperty("tags");
            tagProp.arraySize = tags.Length;
            for (var i = 0; i < tags.Length; i++)
                tagProp.GetArrayElementAtIndex(i).stringValue = tags[i];

            so.ApplyModifiedPropertiesWithoutUndo();
#endif
            return item;
        }

        ItemDefinition weapon;
        ItemDefinition dart;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("CrossbowTestRoot");

            player = new GameObject("TestPlayer");
            player.transform.SetParent(root.transform, false);
            player.layer = GameLayers.Player;
            inventory = player.AddComponent<PlayerInventory>();
            crossbow = player.AddComponent<Crossbow>();

            weapon = MakeItem("crossbow", PlayerInventory.WeaponTag);
            dart = MakeItem("dart", PlayerInventory.DartTag);
            Wire(crossbow, "dartItem", dart);

            var benchGo = new GameObject("Bench");
            benchGo.transform.SetParent(root.transform, false);
            benchGo.SetActive(false);
            bench = benchGo.AddComponent<WeaponBench>();
            Wire(bench, "weapon", weapon);
            Wire(bench, "dart", dart);
            benchGo.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);

            foreach (var stray in Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None))
                Object.DestroyImmediate(stray.gameObject);
        }

        // ------------------------------------------------------------------
        // The bench
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheBenchWantsThreeDifferentParts()
        {
            yield return null;

            Assert.AreEqual(3, bench.PartsRemaining);
            Assert.IsFalse(bench.IsComplete);

            inventory.TryTake(MakeItem("stock", "bow_stock"));
            bench.Interact(player);

            Assert.AreEqual(2, bench.PartsRemaining);
            Assert.IsTrue(inventory.IsEmptyHanded, "A fitted part stays fitted.");
        }

        [UnityTest]
        public IEnumerator TheSamePartTwiceIsStillTwoPartsShort()
        {
            yield return null;

            inventory.TryTake(MakeItem("stock", "bow_stock"));
            bench.Interact(player);

            inventory.TryTake(MakeItem("stock2", "bow_stock"));
            bench.Interact(player);

            Assert.AreEqual(2, bench.PartsRemaining, "Two stocks do not make a crossbow.");
            Assert.IsNotEmpty(bench.LastHint);
        }

        [UnityTest]
        public IEnumerator TheLastPartHandsOverALoadedCrossbow()
        {
            yield return null;

            foreach (var tag in new[] { "bow_stock", "bow_limb", "bow_cord" })
            {
                inventory.TryTake(MakeItem(tag, tag));
                bench.Interact(player);
            }

            Assert.IsTrue(bench.IsComplete);

            bench.Interact(player);

            Assert.IsTrue(bench.HasBeenCollected);
            Assert.IsTrue(inventory.HasWeapon, "The crossbow goes in the hand.");
            Assert.AreEqual(3, inventory.Darts, "And the darts go in the pocket.");
        }

        // ------------------------------------------------------------------
        // Darts
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator DartsDoNotTakeTheHand()
        {
            yield return null;

            var hammer = MakeItem("hammer", "pry");
            inventory.TryTake(hammer);
            inventory.TryTake(dart);

            Assert.AreEqual(hammer, inventory.Held,
                "Picking up a dart must not cost you the tool you were carrying.");
            Assert.AreEqual(1, inventory.Darts);
        }

        // ------------------------------------------------------------------
        // Firing
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AnEmptyCrossbowDoesNothing()
        {
            yield return null;

            inventory.TryTake(weapon);

            Assert.IsFalse(crossbow.Fire(), "No darts, no shot.");
            Assert.AreEqual(0, inventory.Darts);
        }

        [UnityTest]
        public IEnumerator FiringSpendsADartAndIsHeard()
        {
            yield return null;

            inventory.TryTake(weapon);
            inventory.TryTake(dart);
            inventory.TryTake(dart);

            Noise? heard = null;
            void Listener(Noise n) => heard = n;

            NoiseBus.Heard += Listener;
            try
            {
                Assert.IsTrue(crossbow.Fire());
            }
            finally
            {
                NoiseBus.Heard -= Listener;
            }

            Assert.AreEqual(1, inventory.Darts, "One shot, one dart.");
            Assert.IsTrue(heard.HasValue, "Firing is never the quiet option.");
        }

        [UnityTest]
        public IEnumerator TheThrowButtonDoesNotHurlTheCrossbowAway()
        {
            yield return null;

            inventory.TryTake(weapon);
            inventory.ThrowHeld();

            Assert.AreEqual(weapon, inventory.Held,
                "Both the weapon and the inventory listen to throw; only one may act.");
        }

        [UnityTest]
        public IEnumerator AFiredDartCanBePickedUpAgain()
        {
            yield return null;

            // The real asset, because what is being checked here is that a fired
            // dart becomes a thing in the world — and that needs the world prefab
            // the content builder makes, which a stand-in does not have.
            ItemDefinition realDart = null;
#if UNITY_EDITOR
            realDart = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                "Assets/_Project/Data/Items/Item_dart.asset");
#endif
            Assert.IsNotNull(realDart, "No dart item. Run Granny > Build Content.");
            Assert.IsNotNull(realDart.WorldPrefab, "The dart has nothing to be, lying on the floor.");

            Wire(crossbow, "dartItem", realDart);

            inventory.TryTake(weapon);
            inventory.TryTake(realDart);

            crossbow.Fire();
            yield return null;

            var onTheFloor = Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
            Assert.IsNotEmpty(onTheFloor, "Three darts have to last the night, so they land somewhere.");
        }
    }
}
