using System.Collections;
using System.Collections.Generic;
using Granny.Core;
using Granny.Gameplay.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// The bird on the perch.
    ///
    /// Every other rule in this house is about what she can hear. The crow is the
    /// one hazard that works the other way round — it is looking at you — so these
    /// tests are mostly about the two things that must stay true of it: that
    /// coming near it costs you, and that it is a place rather than a wall.
    /// </summary>
    public class CrowTests
    {
        GameObject root;
        readonly List<Noise> heard = new();

        void Listen(Noise noise) => heard.Add(noise);

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("CrowTestRoot");
            heard.Clear();
            NoiseBus.Heard += Listen;
        }

        [TearDown]
        public void TearDown()
        {
            NoiseBus.Heard -= Listen;
            if (root != null) Object.DestroyImmediate(root);
        }

        Crow MakeCrow()
        {
            var go = new GameObject("Crow");
            go.transform.SetParent(root.transform, false);
            return go.AddComponent<Crow>();
        }

        int Cries => heard.FindAll(noise => noise.Kind == NoiseKind.Crow).Count;

        [UnityTest]
        public IEnumerator ItShoutsWhenSomethingSetsItOff()
        {
            var crow = MakeCrow();

            Assert.IsTrue(crow.Cry(root), "A crow that has not been disturbed should still be watching.");
            yield return null;

            Assert.AreEqual(1, Cries, "The house should have heard it.");
        }

        [UnityTest]
        public IEnumerator SheHearsItFromAnywhereInTheHouse()
        {
            // The point of the bird is that it fetches her. A cry that is filtered
            // out at the bus is a bird that costs nothing, which would make the
            // attic free again.
            Assert.IsTrue(NoiseRules.IsAudible(NoiseKind.Crow),
                "A crow nobody comes for is just scenery.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator ItWillNotShoutTwiceInARow()
        {
            // Otherwise standing in the doorway would call her every frame, which
            // is not a hazard, it is a siren.
            var crow = MakeCrow();

            Assert.IsTrue(crow.Cry(root));
            Assert.IsFalse(crow.Cry(root), "It should be sulking.");
            Assert.IsFalse(crow.IsWatching);

            yield return null;
            Assert.AreEqual(1, Cries);
        }

        [UnityTest]
        public IEnumerator ANewDayFindsItWatchingAgain()
        {
            var crow = MakeCrow();

            crow.Cry(root);
            crow.Rearm();

            Assert.IsTrue(crow.IsWatching, "Morning should reset it.");
            Assert.IsTrue(crow.Cry(root));

            yield return null;
            Assert.AreEqual(2, Cries);
        }
    }
}
