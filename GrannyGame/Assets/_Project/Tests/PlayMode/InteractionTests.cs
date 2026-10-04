using System.Collections;
using Granny.Core;
using Granny.Gameplay.Interaction;
using Granny.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// Exercises the interaction loop end to end in a live scene: aim at a thing,
    /// press the button, watch the world change.
    /// </summary>
    public class InteractionTests
    {
        /// <summary>
        /// Everything a test creates hangs off this and dies with it. PlayMode
        /// tests share one scene, so a stray pickup left behind is picked up by
        /// the next test's raycast and a stray floor blocks the next test's
        /// player - both of which look like bugs in the code under test.
        /// </summary>
        GameObject sceneRoot;

        GameObject floor;
        GameObject player;
        PlayerInteractor interactor;
        PlayerInventory inventory;

        [SetUp]
        public void SetUp()
        {
            sceneRoot = new GameObject("InteractionTestRoot");

            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TestFloor";
            floor.layer = GameLayers.LevelGeometry;
            floor.transform.SetParent(sceneRoot.transform);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);

            // A bare stand-in rather than the full rig: these tests are about the
            // raycast and the inventory, not about locomotion.
            player = new GameObject("TestPlayer");
            player.transform.SetParent(sceneRoot.transform);
            player.transform.position = new Vector3(0f, 1.6f, 0f);

            var eye = new GameObject("Eye").transform;
            eye.SetParent(player.transform, false);

            interactor = player.AddComponent<PlayerInteractor>();
            inventory = player.AddComponent<PlayerInventory>();

            Wire(interactor, "eye", eye);
            Wire(inventory, "dropOrigin", eye);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;

            if (sceneRoot != null) Object.DestroyImmediate(sceneRoot);

            // Items spawned by dropping or by searching a drawer are parented to
            // nothing, so they have to be swept up by type.
            foreach (var stray in Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None))
                if (stray != null) Object.DestroyImmediate(stray.gameObject);
        }

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

        /// <summary>Puts a pickup two metres straight ahead of the test player.</summary>
        PickupItem PlacePickup(ItemDefinition definition)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Pickup_{definition.Id}";
            go.transform.SetParent(sceneRoot.transform);
            go.transform.position = player.transform.position + Vector3.forward * 2f;
            go.transform.localScale = Vector3.one * 0.3f;

            // Awake runs the moment the component is added on an active object,
            // and PickupItem logs an error when it wakes up without a definition
            // - which the test runner treats as a failure. Wire it up first.
            go.SetActive(false);
            var pickup = go.AddComponent<PickupItem>();
            Wire(pickup, "definition", definition);
            go.SetActive(true);

            return pickup;
        }

        [UnityTest]
        public IEnumerator LookingAtAPickupOffersATakePrompt()
        {
            var item = MakeItem("hammer", "pry");
            PlacePickup(item);

            yield return null;

            Assert.IsNotNull(interactor.Target, "Nothing was found under the crosshair.");
            StringAssert.Contains("hammer", interactor.Target.Prompt.ToLowerInvariant());
        }

        [UnityTest]
        public IEnumerator LookingAtNothingClearsTheTarget()
        {
            yield return null;
            Assert.IsNull(interactor.Target);
        }

        [UnityTest]
        public IEnumerator InteractingWithAPickupMovesItIntoTheInventory()
        {
            var item = MakeItem("hammer", "pry");
            var pickup = PlacePickup(item);

            yield return null;
            interactor.Target.Interact(player);
            yield return null;

            Assert.AreSame(item, inventory.Held);
            Assert.IsTrue(pickup == null, "The world object should be gone once taken.");
        }

        [UnityTest]
        public IEnumerator TurningAwayDropsTheTarget()
        {
            PlacePickup(MakeItem("hammer"));
            yield return null;
            Assert.IsNotNull(interactor.Target);

            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            yield return null;

            Assert.IsNull(interactor.Target, "Looking away must clear the prompt.");
        }

        [UnityTest]
        public IEnumerator ADoorOpensAndClosesWithoutGivingThePlayerAway()
        {
            var doorGo = new GameObject("Door");
            doorGo.transform.SetParent(sceneRoot.transform);
            doorGo.transform.position = player.transform.position + Vector3.forward * 2f;

            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            leaf.SetParent(doorGo.transform, false);
            leaf.localScale = new Vector3(0.9f, 2f, 0.08f);

            var door = doorGo.AddComponent<HingeDoor>();
            Wire(door, "leaf", leaf);

            var heard = 0;
            var filtered = 0;
            void OnHeard(Noise _) => heard++;
            void OnFiltered(Noise _) => filtered++;

            NoiseBus.Heard += OnHeard;
            NoiseBus.Filtered += OnFiltered;

            try
            {
                yield return null;

                Assert.IsFalse(door.IsOpen);
                door.Interact(player);
                Assert.IsTrue(door.IsOpen);

                door.Interact(player);
                Assert.IsFalse(door.IsOpen);

                door.Slam(player);
            }
            finally
            {
                NoiseBus.Heard -= OnHeard;
                NoiseBus.Filtered -= OnFiltered;
                Object.DestroyImmediate(doorGo);
            }

            Assert.AreEqual(2, filtered, "Easing a door open and shut is silent, as in the original.");
            Assert.AreEqual(1, heard, "Slamming it is not.");
        }

        [UnityTest]
        public IEnumerator ALockedDoorStaysShutWithoutTheKeyAndOpensWithIt()
        {
            var doorGo = new GameObject("LockedDoor");
            doorGo.transform.SetParent(sceneRoot.transform);
            doorGo.transform.position = player.transform.position + Vector3.forward * 2f;

            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            leaf.SetParent(doorGo.transform, false);

            var door = doorGo.AddComponent<HingeDoor>();
            Wire(door, "leaf", leaf);

#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(door);
            so.FindProperty("locked").boolValue = true;
            so.FindProperty("unlockTag").stringValue = "key.test";
            so.ApplyModifiedPropertiesWithoutUndo();
#endif
            yield return null;

            door.Interact(player);
            Assert.IsTrue(door.IsLocked, "Without the key the door must stay locked.");
            Assert.IsFalse(door.IsOpen);

            inventory.TryTake(MakeItem("testkey", "key.test"));
            door.Interact(player);

            Assert.IsFalse(door.IsLocked);
            Assert.IsTrue(door.IsOpen, "Unlocking should also swing it open.");

            Object.DestroyImmediate(doorGo);
        }

        [UnityTest]
        public IEnumerator SearchingADrawerRevealsItsContentsOnce()
        {
            var contents = MakeItem("wirecutters", "cut");

            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.transform.SetParent(sceneRoot.transform);
            prefab.SetActive(false);
            var prefabPickup = prefab.AddComponent<PickupItem>();
            Wire(prefabPickup, "definition", contents);
#if UNITY_EDITOR
            var itemSo = new UnityEditor.SerializedObject(contents);
            itemSo.FindProperty("worldPrefab").objectReferenceValue = prefab;
            itemSo.ApplyModifiedPropertiesWithoutUndo();
#endif

            var dresserGo = new GameObject("Dresser");
            dresserGo.transform.SetParent(sceneRoot.transform);
            dresserGo.transform.position = player.transform.position + Vector3.forward * 2f;

            var sliding = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            sliding.SetParent(dresserGo.transform, false);

            var drawer = dresserGo.AddComponent<Drawer>();
            Wire(drawer, "sliding", sliding);
            yield return null;

            drawer.SetContents(contents);

            Assert.IsFalse(drawer.HasBeenSearched);
            drawer.Interact(player);
            yield return null;

            Assert.IsTrue(drawer.HasBeenSearched);
            Assert.IsNull(drawer.Contents, "Contents should be handed to the world, not kept.");

            Object.DestroyImmediate(dresserGo);
            Object.DestroyImmediate(prefab);
        }

        [UnityTest]
        public IEnumerator HidingParksThePlayerAndLeavingPutsThemBack()
        {
            // The bare test player has no input reader, and PlayerMotor rightly
            // complains about that on the real prefab. RequireComponent brings the
            // CharacterController along, so it must not be added separately.
            LogAssert.ignoreFailingMessages = true;
            var motor = player.AddComponent<PlayerMotor>();
            yield return null;

            var spotGo = new GameObject("Wardrobe");
            spotGo.transform.SetParent(sceneRoot.transform);
            spotGo.transform.position = new Vector3(5f, 0f, 0f);

            var viewpoint = new GameObject("Viewpoint").transform;
            viewpoint.SetParent(spotGo.transform, false);

            var exit = new GameObject("Exit").transform;
            exit.SetParent(spotGo.transform, false);
            exit.localPosition = new Vector3(0f, 0f, 1f);

            var spot = spotGo.AddComponent<HidingSpot>();
            Wire(spot, "viewpoint", viewpoint);
            Wire(spot, "exitPoint", exit);
            yield return null;

            spot.Interact(player);
            yield return null;

            Assert.IsTrue(spot.IsOccupied);
            Assert.IsFalse(motor.enabled, "A hidden player must not be able to walk out of the wardrobe.");
            Assert.AreEqual(viewpoint.position.x, player.transform.position.x, 0.05f);

            spot.Interact(player);
            yield return null;

            Assert.IsFalse(spot.IsOccupied);
            Assert.IsTrue(motor.enabled);
            Assert.AreEqual(exit.position.z, player.transform.position.z, 0.05f);

            Object.DestroyImmediate(spotGo);
        }
    }
}
