using System.Collections;
using Granny.Core;
using Granny.Gameplay.AI;
using Granny.Gameplay.Interaction;
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

        /// <summary>
        /// A live floorboard on the first floor, taken from the real house rather
        /// than built for the test — so this covers where the boards were put and
        /// how many of them difficulty leaves live, not just the component.
        /// </summary>
        CreakyFloor FindLiveBoardUpstairs()
        {
            foreach (var board in Object.FindObjectsByType<CreakyFloor>(FindObjectsSortMode.None))
                if (board.IsArmed && HouseLayout.FloorOf(board.transform.position) == HouseLayout.Upper)
                    return board;

            return null;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator AFloorboardUpstairsBringsHerUpFromTheCellar()
        {
            yield return null;

            var board = FindLiveBoardUpstairs();
            Assert.IsNotNull(board,
                "No live floorboard upstairs. Either none were placed or difficulty silenced them all.");

            var spot = board.transform.position;
            Assert.IsTrue(board.TryCreak(board.gameObject, speed: 2.6f, crouching: false),
                "A board that will not creak for a walking player is not a trap.");

            var reachedGround = false;
            var elapsed = 0f;

            while (elapsed < 60f)
            {
                elapsed += Time.deltaTime;

                if (HouseLayout.FloorOf(granny.transform.position) >= HouseLayout.Ground)
                    reachedGround = true;

                if (Vector3.Distance(granny.transform.position, spot) < 3f)
                {
                    Assert.IsTrue(reachedGround, "She teleported rather than walked.");
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                $"A creaking board at {spot} never brought her. After 60s she is on " +
                $"{HouseLayout.FloorName(HouseLayout.FloorOf(granny.transform.position))}, " +
                $"state {granny.State}.");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CrossingTheSameBoardCrouchedLeavesHerWhereSheIs()
        {
            yield return null;

            var board = FindLiveBoardUpstairs();
            Assert.IsNotNull(board);

            for (var i = 0; i < 20; i++)
            {
                board.TryCreak(board.gameObject, speed: 1.3f, crouching: true);
                yield return null;
            }

            // Her patrol route crosses storeys, so where she ends up says nothing
            // about whether she heard anything. What matters is that she never
            // stops patrolling: Investigate is the state a noise puts her in.
            var elapsed = 0f;
            while (elapsed < 6f)
            {
                elapsed += Time.deltaTime;

                Assert.AreEqual(GrannyState.Patrol, granny.State,
                    "Crouching over a board has to be the answer to it.");

                yield return null;
            }
        }

        /// <summary>
        /// The whole loop, in the real house: a noise draws her two floors up,
        /// she finds nobody, and the route the player used costs them a trap.
        /// </summary>
        [UnityTest]
        [Timeout(240000)]
        public IEnumerator ASearchThatFindsNobodyLeavesATrapOnTheRoute()
        {
            yield return null;

            var setter = Object.FindAnyObjectByType<TrapSetter>();
            Assert.IsNotNull(setter, "She has no way to lay traps. Rebuild her prefab.");
            Assert.IsEmpty(setter.Laid, "She should start the day with the house clear.");

            NoiseBus.Emit(NoiseSpot, NoiseKind.Breakage);

            var searched = false;
            var elapsed = 0f;

            while (elapsed < 90f)
            {
                elapsed += Time.deltaTime;

                if (granny.State == GrannyState.Search) searched = true;

                if (setter.Laid.Count > 0)
                {
                    Assert.IsTrue(searched, "She laid a trap without ever searching.");

                    var trap = setter.Laid[0];
                    var spots = Object.FindObjectsByType<TrapSpot>(FindObjectsSortMode.None);
                    var nearest = float.MaxValue;

                    foreach (var spot in spots)
                        nearest = Mathf.Min(nearest,
                            Vector3.Distance(spot.transform.position, trap.transform.position));

                    Assert.Less(nearest, 2.5f,
                        $"The trap went down {nearest:F1} m from the nearest choke point. " +
                        "A trap off the route is scenery.");

                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                $"No trap after 90s. She is on " +
                $"{HouseLayout.FloorName(HouseLayout.FloorOf(granny.transform.position))}, " +
                $"state {granny.State}, searched={searched}.");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator WalkingAroundUpstairsDoesNotBringHerAtAll()
        {
            yield return null;

            // Footsteps are filtered by NoiseRules, so these must do nothing.
            for (var i = 0; i < 10; i++)
            {
                NoiseBus.Emit(NoiseSpot, NoiseKind.Footstep);
                NoiseBus.Emit(NoiseSpot, NoiseKind.Container);
                yield return null;
            }

            // She patrols the whole house, cellar to attic, so leaving the floor
            // she started on proves nothing either way. Staying in Patrol does:
            // a sound that reached her would put her in Investigate.
            var elapsed = 0f;
            while (elapsed < 6f)
            {
                elapsed += Time.deltaTime;

                Assert.AreEqual(GrannyState.Patrol, granny.State,
                    "Walking and opening drawers must not draw her.");

                yield return null;
            }
        }
    }
}
