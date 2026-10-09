using System.Collections;
using Granny.Gameplay.Player;
using Granny.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// What the five days do to the person living them.
    ///
    /// The rule being protected here is that an injury never takes speed away.
    /// The original does that, and it is the one thing worth refusing: a player
    /// who has been slowed cannot recover from the mistake that slowed them, so
    /// the last two days stop being playable. These cost aim and composure.
    /// </summary>
    public class InjuryTests
    {
        GameObject player;
        PlayerInjury injury;
        Transform pivot;

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
            player = new GameObject("TestPlayer");
            player.SetActive(false);

            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(player.transform, false);

            injury = player.AddComponent<PlayerInjury>();
            Wire(injury, "cameraPivot", pivot);

            player.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (player != null) Object.DestroyImmediate(player);
        }

        [UnityTest]
        public IEnumerator TheFirstMorningIsSteady()
        {
            injury.SetDay(1);

            yield return null;
            yield return null;

            Assert.AreEqual(0f, injury.Severity, "Day one has nothing wrong with it.");
            Assert.AreEqual(0f, injury.Roll, 0.0001f, "And the view must not move on its own.");
        }

        [UnityTest]
        public IEnumerator EachMorningIsWorseThanTheLast()
        {
            yield return null;

            injury.SetDay(2);
            var second = injury.Severity;

            injury.SetDay(4);
            var fourth = injury.Severity;

            injury.SetDay(5);

            Assert.Greater(second, 0f, "Being caught once has to show.");
            Assert.Greater(fourth, second);
            Assert.AreEqual(1f, injury.Severity, 0.001f, "The last day is as bad as it gets.");
        }

        [UnityTest]
        public IEnumerator ABadLegMakesTheViewLean()
        {
            injury.SetDay(5);

            // Perlin noise is smooth, so a single frame can legitimately sit near
            // zero. Watch for a while and take the worst of it.
            var worst = 0f;
            var elapsed = 0f;

            while (elapsed < 2.5f)
            {
                elapsed += Time.deltaTime;
                worst = Mathf.Max(worst, Mathf.Abs(injury.Roll));
                yield return null;
            }

            Assert.Greater(worst, 0.1f, "By day five the camera should not hold still.");
        }

        [UnityTest]
        public IEnumerator TheMorningAfterIsToldByWhoeverRunsTheDays()
        {
            injury.SetDay(3);
            yield return null;

            Assert.AreEqual(3, injury.Day);
        }

        [UnityTest]
        public IEnumerator BeingCaughtPlaysAndEndsOnItsOwn()
        {
            var hudGo = new GameObject("HUD");
            var scare = hudGo.AddComponent<Jumpscare>();

            try
            {
                Assert.IsFalse(scare.IsPlaying);

                hudGo.GetComponent<MonoBehaviour>().StartCoroutine(scare.Play());
                yield return null;

                Assert.IsTrue(scare.IsPlaying, "The scare has to actually run.");

                var elapsed = 0f;
                while (elapsed < 3f && scare.IsPlaying)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                Assert.IsFalse(scare.IsPlaying,
                    "A scare that outstays its welcome is a cutscene.");
                Assert.Less(elapsed, 2f, "And it has to be brief.");
            }
            finally
            {
                Object.DestroyImmediate(hudGo);
            }
        }
    }
}
