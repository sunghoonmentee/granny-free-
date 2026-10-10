using System.Collections;
using Granny.Gameplay.Interaction;
using Granny.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// The one piece of information a hidden player gets.
    ///
    /// Hiding takes the controls away on purpose, which leaves the player with
    /// nothing to do and nothing to know. The heartbeat is the nothing-to-know
    /// half: how close she is, but never where. These check it only speaks when
    /// it should, because a heartbeat that plays while you are walking around is
    /// mood music and tells you nothing at all.
    /// </summary>
    public class HeartbeatTests
    {
        GameObject root;
        GameObject player;
        HidingHeartbeat heart;
        HidingSpot wardrobe;

        static void Wire(Object target, string field, Object value)
        {
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
#endif
        }

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("HeartbeatTestRoot");

            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Player.prefab");
#endif
            Assert.IsNotNull(prefab, "No player prefab. Run Granny > Build Player Rig.");

            // The spot has to exist before the player, because the heartbeat
            // subscribes to whatever hiding places are in the scene when it wakes.
            var spotGo = new GameObject("Wardrobe");
            spotGo.transform.SetParent(root.transform, false);
            spotGo.transform.position = new Vector3(3f, 0f, 0f);
            spotGo.SetActive(false);

            var viewpoint = new GameObject("Viewpoint").transform;
            viewpoint.SetParent(spotGo.transform, false);

            var exit = new GameObject("ExitPoint").transform;
            exit.SetParent(spotGo.transform, false);
            exit.localPosition = new Vector3(1.1f, 0f, 0f);

            wardrobe = spotGo.AddComponent<HidingSpot>();
            Wire(wardrobe, "viewpoint", viewpoint);
            Wire(wardrobe, "exitPoint", exit);
            spotGo.SetActive(true);

            player = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            heart = player.GetComponentInChildren<HidingHeartbeat>();
            Assert.IsNotNull(heart, "No heartbeat on the player rig.");
        }

        [TearDown]
        public void TearDown()
        {
            if (player != null) Object.DestroyImmediate(player);
            if (root != null) Object.DestroyImmediate(root);
        }

        [UnityTest]
        public IEnumerator ItIsSilentWhileYouAreWalkingAround()
        {
            for (var i = 0; i < 5; i++) yield return null;

            Assert.IsNull(heart.Spot, "Nobody is hiding.");
            Assert.AreEqual(0f, heart.Level, 0.001f,
                "A heartbeat that plays in the open is mood music, not information.");
        }

        [UnityTest]
        public IEnumerator ItKnowsWhenYouAreInside()
        {
            yield return null;

            wardrobe.Interact(player);
            yield return null;

            Assert.AreEqual(wardrobe, heart.Spot);
        }

        [UnityTest]
        public IEnumerator ClimbingBackOutStopsIt()
        {
            yield return null;

            wardrobe.Interact(player);
            yield return null;
            Assert.IsNotNull(heart.Spot);

            wardrobe.Interact(player);
            yield return null;

            Assert.IsNull(heart.Spot);

            // And it fades rather than cutting, so give it a moment to settle.
            var elapsed = 0f;
            while (elapsed < 2f) { elapsed += Time.deltaTime; yield return null; }

            Assert.AreEqual(0f, heart.Level, 0.01f);
        }

        [UnityTest]
        public IEnumerator WithNobodyHuntingThereIsNothingToBeAfraidOf()
        {
            yield return null;

            wardrobe.Interact(player);

            var elapsed = 0f;
            while (elapsed < 1f) { elapsed += Time.deltaTime; yield return null; }

            // There is no Granny in this scene at all.
            Assert.AreEqual(0f, heart.Level, 0.01f,
                "It should answer to her, not to the wardrobe.");
        }
    }
}
