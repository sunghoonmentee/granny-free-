using System.Collections;
using System.Linq;
using Granny.Core;
using Granny.Gameplay;
using Granny.Gameplay.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// Five fixed layouts, and the promise that every one of them can be won.
    ///
    /// This is the test the design document asks for by name, and it is the only
    /// thing standing between the player and a run where the wirecutters were
    /// never placed at all. A hidden tool is a puzzle; a missing one is a bug
    /// that looks exactly like a puzzle, which is the worst kind.
    /// </summary>
    public class ItemLayoutTests
    {
        Scene house;
        ItemSpawner spawner;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SaveSystem.Delete();

            yield return SceneManager.LoadSceneAsync(SceneNames.House, LoadSceneMode.Additive);
            house = SceneManager.GetSceneByName(SceneNames.House);
            SceneManager.SetActiveScene(house);
            yield return null;

            spawner = Object.FindAnyObjectByType<ItemSpawner>();
            Assert.IsNotNull(spawner, "No ItemSpawner in the House scene.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SaveSystem.Delete();

            var blank = SceneManager.CreateScene($"Blank_{System.Guid.NewGuid():N}");
            SceneManager.SetActiveScene(blank);

            if (house.IsValid() && house.isLoaded)
                yield return SceneManager.UnloadSceneAsync(house);
        }

        static Drawer[] Containers() =>
            Object.FindObjectsByType<Drawer>(FindObjectsSortMode.None);

        /// <summary>Which tags the front door needs off it before it will open.</summary>
        static string[] RequiredTags()
        {
            var door = Object.FindAnyObjectByType<EscapeDoor>();
            Assert.IsNotNull(door, "No front door.");

            return door.GetComponentsInChildren<LockStage>(true)
                .Select(stage => stage.RequiredTag)
                .Where(tag => !string.IsNullOrEmpty(tag))
                .Distinct()
                .ToArray();
        }

        [UnityTest]
        public IEnumerator ThereAreFiveOfThemAndTheyAreNotAllTheSame()
        {
            yield return null;

            Assert.AreEqual(5, spawner.LayoutCount, "The design calls for five.");

            var fingerprints = new System.Collections.Generic.HashSet<string>();

            for (var i = 0; i < spawner.LayoutCount; i++)
            {
                spawner.Apply(i);
                fingerprints.Add(string.Join(",", spawner.Placements
                    .OrderBy(p => p.Key)
                    .Select(p => $"{p.Key}={p.Value}")));
            }

            Assert.AreEqual(5, fingerprints.Count,
                "Two layouts hide everything in the same places, so there are really fewer.");
        }

        [UnityTest]
        public IEnumerator TheSameLayoutIsTheSameEveryTime()
        {
            yield return null;

            spawner.Apply(2);
            var first = spawner.Placements.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}").ToArray();

            spawner.Apply(0);
            spawner.Apply(2);
            var second = spawner.Placements.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}").ToArray();

            CollectionAssert.AreEqual(first, second,
                "A layout a player has learned has to still be there next time.");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator EveryLayoutCanActuallyBeWon()
        {
            yield return null;

            var needed = RequiredTags();
            Assert.IsNotEmpty(needed, "The door asks for nothing, so this proves nothing.");

            for (var layout = 0; layout < spawner.LayoutCount; layout++)
            {
                spawner.Apply(layout);
                yield return null;

                foreach (var tag in needed)
                {
                    var holder = Containers().FirstOrDefault(
                        c => c.Contents != null && c.Contents.HasTag(tag));

                    Assert.IsNotNull(holder,
                        $"Layout {layout}: nothing anywhere in the house has the '{tag}' " +
                        "the front door wants. The run cannot be finished.");

                    // ...and it has to be somewhere she could walk to, which is a
                    // good proxy for somewhere the player can walk to.
                    Assert.IsTrue(
                        NavMesh.SamplePosition(holder.transform.position, out _, 2.5f, NavMesh.AllAreas),
                        $"Layout {layout}: the '{tag}' is in {holder.name}, which is not " +
                        "anywhere near reachable floor.");
                }
            }
        }

        [UnityTest]
        public IEnumerator EveryToolEndsUpSomewhereDifferent()
        {
            yield return null;

            for (var layout = 0; layout < spawner.LayoutCount; layout++)
            {
                spawner.Apply(layout);

                var used = spawner.Placements.Values.ToArray();
                CollectionAssert.AllItemsAreUnique(used,
                    $"Layout {layout} puts two tools in one drawer, so one of them is lost.");
            }
        }
    }
}
