using System.Collections;
using Granny.Gameplay.AI;
using Granny.Gameplay.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// Hiding is a gamble, not a wall.
    ///
    /// The rule that decides it is whether she watched you climb in. Hiding out
    /// of sight is a real escape; hiding while she is looking at you only picks
    /// the place she finds you. These check that the spot itself behaves, and
    /// that a spot reports who is in it so her perception can tell the difference.
    /// </summary>
    public class HidingTests
    {
        GameObject root;
        GameObject player;
        HidingSpot wardrobe;

        static void SetEnum(Object target, string field, int value)
        {
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(target);
            so.FindProperty(field).enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
#endif
        }

        static void Wire(Object target, string field, Object value)
        {
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
#endif
        }

        HidingSpot MakeSpot(HideStyle style, Vector3 position)
        {
            var go = new GameObject($"Spot_{style}");
            go.transform.SetParent(root.transform, false);
            go.transform.position = position;
            go.SetActive(false);

            var viewpoint = new GameObject("Viewpoint").transform;
            viewpoint.SetParent(go.transform, false);
            viewpoint.localPosition = new Vector3(0f, -0.7f, 0f);

            var exit = new GameObject("ExitPoint").transform;
            exit.SetParent(go.transform, false);
            exit.localPosition = new Vector3(1.1f, 0f, 0f);

            var spot = go.AddComponent<HidingSpot>();
            Wire(spot, "viewpoint", viewpoint);
            Wire(spot, "exitPoint", exit);
            SetEnum(spot, "style", (int)style);

            go.SetActive(true);
            return spot;
        }

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("HidingTestRoot");

            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Player.prefab");
#endif
            Assert.IsNotNull(prefab, "No player prefab. Run Granny > Build Player Rig.");

            player = Object.Instantiate(prefab, new Vector3(0f, 0.2f, 0f), Quaternion.identity);
            wardrobe = MakeSpot(HideStyle.StepIn, new Vector3(3f, 0f, 0f));
        }

        [TearDown]
        public void TearDown()
        {
            if (player != null) Object.DestroyImmediate(player);
            if (root != null) Object.DestroyImmediate(root);
        }

        [UnityTest]
        public IEnumerator GettingInAndOutReportsWhoIsInside()
        {
            yield return null;

            GameObject reported = null;
            var changes = 0;

            wardrobe.OccupancyChanged += who => { reported = who; changes++; };

            wardrobe.Interact(player);

            Assert.IsTrue(wardrobe.IsOccupied);
            Assert.AreEqual(player, wardrobe.Occupant, "She needs to know who is in it.");
            Assert.AreEqual(player, reported);

            wardrobe.Interact(player);

            Assert.IsFalse(wardrobe.IsOccupied);
            Assert.IsNull(reported, "Coming out has to be announced too, or she waits at an empty wardrobe.");
            Assert.AreEqual(2, changes);
        }

        [UnityTest]
        public IEnumerator BeingDraggedOutPutsYouBackInTheRoom()
        {
            yield return null;

            wardrobe.Interact(player);
            var inside = player.transform.position;

            wardrobe.ForceExit();
            yield return null;

            Assert.IsFalse(wardrobe.IsOccupied);
            Assert.AreNotEqual(inside, player.transform.position,
                "Dragged out means out, standing in front of her.");
        }

        [UnityTest]
        public IEnumerator ADraggedOutPlayerCanMoveAgain()
        {
            yield return null;

            var motor = player.GetComponent<Granny.Gameplay.Player.PlayerMotor>();

            wardrobe.Interact(player);
            Assert.IsFalse(motor.enabled, "Hiding parks the player.");

            wardrobe.ForceExit();
            yield return null;

            Assert.IsTrue(motor.enabled, "Being found has to leave you able to run.");
        }

        [UnityTest]
        public IEnumerator UnderABedReadsDifferentlyFromAWardrobe()
        {
            yield return null;

            var bed = MakeSpot(HideStyle.Crawl, new Vector3(-3f, 0f, 0f));

            Assert.AreEqual(HideStyle.Crawl, bed.Style);
            StringAssert.Contains("under", bed.Prompt);
            StringAssert.DoesNotContain("under", wardrobe.Prompt);
        }

        // ------------------------------------------------------------------
        // The rule that decides whether hiding worked
        // ------------------------------------------------------------------

        /// <summary>
        /// Her, with the brain switched off: these are about what she notices,
        /// and a NavMeshAgent with no NavMesh under it would only add noise.
        /// </summary>
        GrannyPerception MakeWatcher(Vector3 position, Vector3 lookAt)
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Granny.prefab");
#endif
            Assert.IsNotNull(prefab, "No Granny prefab. Run Granny > Build Granny.");

            var her = Object.Instantiate(prefab, position, Quaternion.identity);
            her.transform.SetParent(root.transform, true);
            her.transform.LookAt(new Vector3(lookAt.x, position.y, lookAt.z));

            foreach (var brain in her.GetComponentsInChildren<GrannyBrain>()) brain.enabled = false;
            foreach (var agent in her.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>())
                agent.enabled = false;

            return her.GetComponentInChildren<GrannyPerception>();
        }

        [UnityTest]
        public IEnumerator HidingWhileSheIsWatchingTellsHerExactlyWhereYouAre()
        {
            var watcher = MakeWatcher(new Vector3(0f, 0f, -4f), player.transform.position);

            // Let a frame of sight run so she actually has the player in view.
            yield return null;
            yield return null;

            Assert.IsTrue(watcher.CanSeeTarget,
                "This test is meaningless unless she can see the player to begin with.");

            wardrobe.Interact(player);
            yield return null;

            Assert.IsTrue(watcher.SawThemHide, "She watched them climb in and learned nothing.");
            Assert.AreEqual(wardrobe, watcher.TargetHidingSpot);
        }

        [UnityTest]
        public IEnumerator HidingBehindHerBackIsFree()
        {
            // Facing away, with the player well outside her cone of vision.
            var watcher = MakeWatcher(new Vector3(0f, 0f, 14f), new Vector3(0f, 0f, 40f));

            yield return null;
            yield return null;

            Assert.IsFalse(watcher.CanSeeTarget);

            wardrobe.Interact(player);
            yield return null;

            Assert.IsFalse(watcher.SawThemHide,
                "Hiding out of sight is the one move that actually works.");
            Assert.IsNull(watcher.TargetHidingSpot);
        }

        [UnityTest]
        public IEnumerator ComingBackOutClearsWhatSheKnew()
        {
            var watcher = MakeWatcher(new Vector3(0f, 0f, -4f), player.transform.position);

            yield return null;
            yield return null;

            wardrobe.Interact(player);
            yield return null;
            Assert.IsTrue(watcher.SawThemHide);

            wardrobe.Interact(player);
            yield return null;

            Assert.IsFalse(watcher.SawThemHide,
                "Otherwise she keeps coming back to a wardrobe nobody is in.");
        }

        [UnityTest]
        public IEnumerator OnlyTheOccupantCanWorkAnOccupiedSpot()
        {
            yield return null;

            wardrobe.Interact(player);

            var someoneElse = new GameObject("Other");
            someoneElse.transform.SetParent(root.transform, false);

            Assert.IsFalse(wardrobe.CanInteract(someoneElse));
            Assert.IsTrue(wardrobe.CanInteract(player));
        }
    }
}
