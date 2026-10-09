using System.Collections;
using Granny.Core;
using Granny.Gameplay.AI;
using Granny.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// Being put down, and getting back up somewhere else.
    ///
    /// A dart has to be worth firing and must not be an answer. The shape that
    /// gives both is: she is harmless for a while, and then she is standing
    /// somewhere you are not — so the stun buys working time, never the house.
    /// </summary>
    public class StunTests
    {
        Scene house;
        GrannyBrain granny;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SaveSystem.Delete();

            yield return SceneManager.LoadSceneAsync(SceneNames.House, LoadSceneMode.Additive);
            house = SceneManager.GetSceneByName(SceneNames.House);
            SceneManager.SetActiveScene(house);
            yield return null;

            granny = Object.FindAnyObjectByType<GrannyBrain>();
            Assert.IsNotNull(granny);

            var spawn = GameObject.Find("GrannySpawn");
            granny.ResetForNewDay(spawn.transform.position);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            SaveSystem.Delete();

            var blank = SceneManager.CreateScene($"Blank_{System.Guid.NewGuid():N}");
            SceneManager.SetActiveScene(blank);

            if (house.IsValid() && house.isLoaded)
                yield return SceneManager.UnloadSceneAsync(house);
        }

        [UnityTest]
        public IEnumerator ADartPutsHerOnTheFloor()
        {
            yield return null;

            var downs = 0;
            granny.DownedChanged += down => { if (down) downs++; };

            granny.Stun(30f);
            yield return null;

            Assert.IsTrue(granny.IsDown);
            Assert.AreEqual(GrannyState.Downed, granny.State);
            Assert.AreEqual(1, downs, "Anything watching her needs to know she went down.");
        }

        [UnityTest]
        public IEnumerator SheDoesNotMoveWhileSheIsDown()
        {
            yield return null;

            granny.Stun(30f);
            yield return null;

            var where = granny.transform.position;

            var elapsed = 0f;
            while (elapsed < 1.5f) { elapsed += Time.deltaTime; yield return null; }

            Assert.Less(Vector3.Distance(where, granny.transform.position), 0.3f,
                "A stunned hunter that keeps walking is not stunned.");
            Assert.IsTrue(granny.IsDown);
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator SheGetsBackUpAWayOffAndCarriesOn()
        {
            yield return null;

            var fellAt = granny.transform.position;
            granny.Stun(1.0f);

            var elapsed = 0f;
            while (elapsed < 10f && granny.IsDown)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsFalse(granny.IsDown, "She has to get back up, or a dart ends the game.");
            Assert.AreEqual(GrannyState.Patrol, granny.State);

            Assert.Greater(Vector3.Distance(fellAt, granny.transform.position), 5f,
                "Getting up where she fell hands the player the room she was guarding.");
        }

        [UnityTest]
        public IEnumerator TheClockRunsDownWhileSheIsOut()
        {
            yield return null;

            granny.Stun(20f);
            yield return null;

            var first = granny.StunRemaining;

            var elapsed = 0f;
            while (elapsed < 0.6f) { elapsed += Time.deltaTime; yield return null; }

            Assert.Less(granny.StunRemaining, first, "The HUD needs this to count down.");
            Assert.Greater(granny.StunRemaining, 0f);
        }

        /// <summary>
        /// The whole weapon, in the real house: stand in front of her, pull the
        /// trigger, and she goes down. Her only collider is a trigger, so this is
        /// the test that catches a dart quietly passing straight through her.
        /// </summary>
        [UnityTest]
        [Timeout(60000)]
        public IEnumerator ADartFiredAtHerFromAcrossTheRoomPutsHerDown()
        {
            yield return null;

            var player = Object.FindAnyObjectByType<PlayerMotor>();
            Assert.IsNotNull(player, "No player in the House scene.");

            var inventory = player.GetComponentInChildren<PlayerInventory>();
            var crossbow = player.GetComponentInChildren<Crossbow>();
            Assert.IsNotNull(crossbow, "No crossbow on the player. Run Granny > Build Player Rig.");

            ItemDefinition weapon = null;
            ItemDefinition dart = null;
#if UNITY_EDITOR
            weapon = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                "Assets/_Project/Data/Items/Item_crossbow.asset");
            dart = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                "Assets/_Project/Data/Items/Item_dart.asset");
#endif
            Assert.IsNotNull(weapon);
            Assert.IsNotNull(dart);

            inventory.TryTake(weapon);
            inventory.TryTake(dart);

            // Four metres away, facing her, with nothing in between.
            var her = granny.transform.position;
            var stand = her - granny.transform.forward * 4f;
            var yaw = Quaternion.LookRotation(
                new Vector3(her.x - stand.x, 0f, her.z - stand.z)).eulerAngles.y;

            player.Warp(stand, yaw);
            yield return null;
            yield return null;

            Assert.IsTrue(crossbow.Fire(), "The shot was not even taken.");
            yield return null;

            Assert.IsTrue(granny.IsDown,
                "The dart went through her. Her collider is a trigger, and an " +
                "ordinary raycast ignores those.");
        }

        [UnityTest]
        public IEnumerator DifficultySaysHowLongSheIsOutFor()
        {
            yield return null;

            var perception = granny.GetComponent<GrannyPerception>();
            Assert.IsNotNull(perception.Difficulty, "No difficulty profile on her.");
            Assert.Greater(perception.Difficulty.StunSeconds, 0f,
                "A stun of zero seconds means the weapon does nothing.");
        }
    }
}
