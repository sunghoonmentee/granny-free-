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
    /// The one test that would have caught the bug the player found.
    ///
    /// Everything else about the hunter was unit tested and passing while, in the
    /// actual house, she stood in the cellar for the whole run. This loads the
    /// real scene, breaks something two floors above her, and waits to see whether
    /// she turns up — which is the only claim that actually matters.
    /// </summary>
    public class HouseHearingTests
    {
        Scene house;
        GrannyBrain granny;

        /// <summary>On the first-floor landing, at the top of the house's middle.</summary>
        static readonly Vector3 NoiseSpot = new(-2f, HouseLayout.FloorHeight, 4f);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SaveSystem.Delete();

            yield return SceneManager.LoadSceneAsync(SceneNames.House, LoadSceneMode.Additive);

            house = SceneManager.GetSceneByName(SceneNames.House);
            Assert.IsTrue(house.IsValid() && house.isLoaded, "The House scene did not load.");
            SceneManager.SetActiveScene(house);

            yield return null;

            granny = Object.FindAnyObjectByType<GrannyBrain>();
            Assert.IsNotNull(granny, "No Granny in the House scene.");

            var spawn = GameObject.Find("GrannySpawn");
            Assert.IsNotNull(spawn, "No GrannySpawn marker.");
            granny.ResetForNewDay(spawn.transform.position);

            // Remove the player entirely. These tests are about hearing, and a
            // player she can see would make them tests about sight instead.
            var player = Object.FindAnyObjectByType<PlayerMotor>();
            if (player != null) Object.DestroyImmediate(player.gameObject);

            yield return null;
            Time.timeScale = 4f;
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
        public IEnumerator SheStartsInTheCellar()
        {
            yield return null;

            Assert.AreEqual(HouseLayout.Basement, HouseLayout.FloorOf(granny.transform.position),
                "The scenario only means anything if she begins two floors down.");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator ABreakingJarUpstairsBringsHerUpFromTheCellar()
        {
            yield return null;

            NoiseBus.Emit(NoiseSpot, NoiseKind.Breakage);

            var reachedGround = false;
            var elapsed = 0f;

            while (elapsed < 60f)
            {
                elapsed += Time.deltaTime;

                var floor = HouseLayout.FloorOf(granny.transform.position);
                if (floor >= HouseLayout.Ground) reachedGround = true;

                if (Vector3.Distance(granny.transform.position, NoiseSpot) < 3f)
                {
                    Assert.IsTrue(reachedGround, "She teleported rather than walked.");
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                $"She never arrived. After 60s she is on {HouseLayout.FloorName(HouseLayout.FloorOf(granny.transform.position))} " +
                $"at {granny.transform.position}, state {granny.State}, " +
                $"{Vector3.Distance(granny.transform.position, NoiseSpot):F1} m from the noise.");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator WalkingAroundUpstairsDoesNotBringHerAtAll()
        {
            yield return null;

            var startFloor = HouseLayout.FloorOf(granny.transform.position);

            // Footsteps are filtered by NoiseRules, so these must do nothing.
            for (var i = 0; i < 10; i++)
            {
                NoiseBus.Emit(NoiseSpot, NoiseKind.Footstep);
                NoiseBus.Emit(NoiseSpot, NoiseKind.Container);
                yield return null;
            }

            var elapsed = 0f;
            while (elapsed < 6f)
            {
                elapsed += Time.deltaTime;

                Assert.AreNotEqual(GrannyState.Investigate, granny.State,
                    "Walking and opening drawers must not draw her.");

                yield return null;
            }

            Assert.AreEqual(startFloor, HouseLayout.FloorOf(granny.transform.position),
                "She left the cellar over sounds she should not be able to hear.");
        }
    }
}
