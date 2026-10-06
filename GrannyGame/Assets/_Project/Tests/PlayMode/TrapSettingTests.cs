using System.Collections;
using System.Linq;
using Granny.Core;
using Granny.Gameplay.AI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// What she leaves behind after searching a room and finding nobody.
    ///
    /// The rule that matters most here is the limit. A trap is frightening
    /// because it is somewhere specific; a house with a trap in every doorway is
    /// just a slower house.
    /// </summary>
    public class TrapSettingTests
    {
        GameObject root;
        GameObject trapPrefab;
        TrapSetter setter;

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
            root = new GameObject("TrapTestRoot");

            // A trap with nothing but its collider and script — the real prefab's
            // meshes would only slow the run down.
            trapPrefab = new GameObject("BearTrapPrefab");
            trapPrefab.AddComponent<BoxCollider>().isTrigger = true;
            trapPrefab.AddComponent<BearTrap>();
            trapPrefab.SetActive(false);

            var her = new GameObject("Granny");
            her.transform.SetParent(root.transform, false);
            setter = her.AddComponent<TrapSetter>();
            Wire(setter, "trapPrefab", trapPrefab);
        }

        [TearDown]
        public void TearDown()
        {
            if (setter != null) setter.ClearAll();
            if (root != null) Object.DestroyImmediate(root);
            if (trapPrefab != null) Object.DestroyImmediate(trapPrefab);

            foreach (var stray in Object.FindObjectsByType<BearTrap>(FindObjectsSortMode.None))
                Object.DestroyImmediate(stray.gameObject);
        }

        TrapSpot MakeSpot(Vector3 position, bool choke = true)
        {
            var go = new GameObject("TrapSpot");
            go.transform.SetParent(root.transform, false);
            go.transform.position = position;

            var spot = go.AddComponent<TrapSpot>();
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(spot);
            so.FindProperty("chokePoint").boolValue = choke;
            so.ApplyModifiedPropertiesWithoutUndo();
#endif
            return spot;
        }

        [UnityTest]
        public IEnumerator SheUsesAChokePointRatherThanWhereSheStands()
        {
            yield return null;

            var spot = MakeSpot(new Vector3(4f, 0f, 0f));
            var trap = setter.Lay(Vector3.zero);

            Assert.IsNotNull(trap, "She had somewhere to put it and did not.");
            Assert.AreEqual(spot.transform.position.x, trap.transform.position.x, 0.5f,
                "A trap belongs where the player has to walk, not where she gave up.");
            Assert.IsTrue(spot.IsTaken);
        }

        [UnityTest]
        public IEnumerator AChokePointTooFarAwayIsNotWorthIt()
        {
            yield return null;

            MakeSpot(new Vector3(60f, 0f, 0f));
            var trap = setter.Lay(Vector3.zero);

            Assert.IsNotNull(trap);
            Assert.Less(trap.transform.position.x, 1f,
                "She should drop it at her feet rather than cross the house for a spot.");
        }

        [UnityTest]
        public IEnumerator TheOldestTrapGoesWhenANewOneLands()
        {
            yield return null;

            setter.Limit = 3;

            for (var i = 0; i < 4; i++)
                MakeSpot(new Vector3(i * 5f, 0f, 0f));

            var first = setter.Lay(new Vector3(0f, 0f, 0f));
            setter.Lay(new Vector3(5f, 0f, 0f));
            setter.Lay(new Vector3(10f, 0f, 0f));

            Assert.AreEqual(3, setter.Laid.Count);

            setter.Lay(new Vector3(15f, 0f, 0f));

            // Destroy lands at the end of the frame, so the reference to the
            // discarded trap only reads as null on the next one.
            yield return null;

            Assert.AreEqual(3, setter.Laid.Count, "Three is the whole house's worth.");
            Assert.IsTrue(first == null, "The fourth trap has to cost her the first.");
        }

        [UnityTest]
        public IEnumerator TrapsAreNotStackedOnTopOfEachOther()
        {
            yield return null;

            setter.Limit = 5;

            setter.Lay(Vector3.zero);
            var second = setter.Lay(new Vector3(0.3f, 0f, 0.3f));

            Assert.IsNull(second, "Two traps in the same square inch is one trap.");
            Assert.AreEqual(1, setter.Laid.Count);
        }

        [UnityTest]
        public IEnumerator SettingOneIsAudibleButNotToHer()
        {
            yield return null;

            Noise? heard = null;
            void Listener(Noise n) => heard = n;

            NoiseBus.Heard += Listener;
            try
            {
                setter.Lay(Vector3.zero);
            }
            finally
            {
                NoiseBus.Heard -= Listener;
            }

            Assert.IsTrue(heard.HasValue, "A player listening carefully should hear it go down.");
            Assert.AreEqual(setter.gameObject, heard.Value.Source,
                "Tagged as hers, or she walks back to investigate her own trap.");
        }

        [UnityTest]
        public IEnumerator TheMorningTakesHerTrapsBackUp()
        {
            yield return null;

            for (var i = 0; i < 3; i++)
                MakeSpot(new Vector3(i * 5f, 0f, 0f));

            setter.Lay(new Vector3(0f, 0f, 0f));
            setter.Lay(new Vector3(5f, 0f, 0f));
            Assert.AreEqual(2, setter.Laid.Count);

            setter.ClearAll();
            yield return null;
            yield return null;

            Assert.AreEqual(0, setter.Laid.Count);
            Assert.IsFalse(Object.FindObjectsByType<TrapSpot>(FindObjectsSortMode.None).Any(s => s.IsTaken),
                "A cleared trap frees its spot for tomorrow.");
        }

        // ------------------------------------------------------------------
        // What a trap does
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AThrownThingSpringsATrapHarmlesslyAndLoudly()
        {
            yield return null;

            var trap = setter.Lay(Vector3.zero);
            Assert.IsTrue(trap.IsArmed);

            Noise? heard = null;
            void Listener(Noise n) => heard = n;

            NoiseBus.Heard += Listener;
            try
            {
                Assert.IsTrue(trap.SpringEmpty(root));
            }
            finally
            {
                NoiseBus.Heard -= Listener;
            }

            Assert.IsFalse(trap.IsArmed, "A sprung trap is spent.");
            Assert.IsFalse(trap.HasCaptive, "Nobody was in it.");
            Assert.AreEqual(NoiseKind.Trap, heard.Value.Kind);

            Assert.IsFalse(trap.SpringEmpty(root), "It only goes off once.");
        }
    }
}
