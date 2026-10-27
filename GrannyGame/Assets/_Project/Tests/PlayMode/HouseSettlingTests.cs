using System.Collections;
using System.Collections.Generic;
using Granny.Core;
using Granny.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// The house groaning to itself.
    ///
    /// There is only one rule it must never break, and it is the reason the
    /// sound exists at all: it is atmosphere, not information. The moment one of
    /// these reaches the noise bus it becomes something she comes running for,
    /// and the player learns that a sound they cannot act on can still get them
    /// killed — which is not tension, it is noise.
    /// </summary>
    public class HouseSettlingTests
    {
        GameObject root;
        readonly List<Noise> heard = new();

        void Listen(Noise noise) => heard.Add(noise);

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("SettlingTestRoot");
            heard.Clear();
            NoiseBus.Heard += Listen;
        }

        [TearDown]
        public void TearDown()
        {
            NoiseBus.Heard -= Listen;
            if (root != null) Object.DestroyImmediate(root);
        }

        HouseSettling Make()
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(root.transform, false);
            go.SetActive(false);

            var world = go.AddComponent<WorldAudio>();
            var settling = go.AddComponent<HouseSettling>();

#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(settling);
            so.FindProperty("world").objectReferenceValue = world;
            so.FindProperty("settle").objectReferenceValue =
                AudioClip.Create("settle", 1024, 1, 44100, false);
            so.ApplyModifiedPropertiesWithoutUndo();
#endif

            go.SetActive(true);
            return settling;
        }

        [UnityTest]
        public IEnumerator TheHouseGroaningIsNeverSomethingSheHears()
        {
            var settling = Make();
            yield return null;

            // Ask for them directly rather than waiting out the timer: what is
            // being tested is what a groan does, not when it happens.
            for (var i = 0; i < 12; i++) settling.Groan();

            yield return null;

            Assert.IsEmpty(heard,
                "A groan reached the noise bus. She would come for the building settling.");
        }

        [UnityTest]
        public IEnumerator ItWaitsBeforeTheFirstOne()
        {
            // Otherwise every new day opens with a groan the moment the scene
            // loads, which trains the player to ignore them by the third morning.
            var settling = Make();
            yield return null;

            Assert.Greater(settling.Waiting, 0f, "It should not fire the instant it wakes up.");
        }
    }
}
