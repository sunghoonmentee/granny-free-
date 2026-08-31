using System.Collections;
using Granny.Core;
using Granny.Gameplay.AI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// Granny is only frightening if she plays fair, so these check she cannot see
    /// through walls, does not react to her own noise, and forgets in bounded time.
    /// </summary>
    public class GrannyPerceptionTests
    {
        GameObject sceneRoot;
        GameObject grannyGo;
        GameObject targetGo;
        GrannyPerception perception;
        DifficultyProfile profile;

        static DifficultyProfile MakeProfile(float hearing = 1f, float reaction = 0f,
            float sightRange = 15f, float sightAngle = 100f)
        {
            var p = ScriptableObject.CreateInstance<DifficultyProfile>();
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(p);
            so.FindProperty("hearingScale").floatValue = hearing;
            so.FindProperty("reactionDelay").floatValue = reaction;
            so.FindProperty("sightRange").floatValue = sightRange;
            so.FindProperty("sightAngle").floatValue = sightAngle;
            so.FindProperty("awarenessRange").floatValue = 2f;
            so.ApplyModifiedPropertiesWithoutUndo();
#endif
            return p;
        }

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
            sceneRoot = new GameObject("PerceptionTestRoot");

            targetGo = new GameObject("Target");
            targetGo.transform.SetParent(sceneRoot.transform);
            targetGo.transform.position = new Vector3(0f, 0f, 6f);

            profile = MakeProfile();

            grannyGo = new GameObject("Granny");
            grannyGo.transform.SetParent(sceneRoot.transform);
            grannyGo.transform.position = Vector3.zero;
            grannyGo.SetActive(false);

            var eye = new GameObject("Eye").transform;
            eye.SetParent(grannyGo.transform, false);
            eye.localPosition = new Vector3(0f, 1.6f, 0f);

            perception = grannyGo.AddComponent<GrannyPerception>();
            Wire(perception, "difficulty", profile);
            Wire(perception, "eye", eye);
            Wire(perception, "target", targetGo.transform);

            grannyGo.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (sceneRoot != null) Object.DestroyImmediate(sceneRoot);
            if (profile != null) Object.DestroyImmediate(profile);
        }

        GameObject PlaceWall(Vector3 position)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.layer = GameLayers.LevelGeometry;
            wall.transform.SetParent(sceneRoot.transform);
            wall.transform.position = position;
            wall.transform.localScale = new Vector3(6f, 4f, 0.4f);

            // Physics.autoSyncTransforms is off, so a collider that was just moved
            // is still at its old pose as far as raycasts are concerned until the
            // next fixed step. Without this the wall silently is not there yet.
            Physics.SyncTransforms();
            return wall;
        }

        /// <summary>Runs frames until <paramref name="condition"/> holds, or gives up.</summary>
        static IEnumerator WaitUntil(System.Func<bool> condition, int maxFrames = 20)
        {
            for (var i = 0; i < maxFrames && !condition(); i++)
                yield return null;
        }

        [UnityTest]
        public IEnumerator SeesAnUnobstructedTargetInFront()
        {
            yield return null;

            Assert.IsTrue(perception.CanSeeTarget);
            Assert.IsTrue(perception.LastKnownPosition.HasValue);
            Assert.AreEqual(0f, perception.TimeSinceSeen, 0.05f);
        }

        [UnityTest]
        public IEnumerator CannotSeeThroughAWall()
        {
            PlaceWall(new Vector3(0f, 2f, 3f));
            yield return null;

            Assert.IsFalse(perception.CanSeeTarget, "A solid wall must block line of sight.");
        }

        [UnityTest]
        public IEnumerator CannotSeeBehindHerself()
        {
            targetGo.transform.position = new Vector3(0f, 0f, -6f);
            yield return null;

            Assert.IsFalse(perception.CanSeeTarget, "Six metres behind her is outside the cone.");
        }

        [UnityTest]
        public IEnumerator NoticesSomeoneStandingRightBehindHer()
        {
            // Creeping past at arm's length should not be free.
            targetGo.transform.position = new Vector3(0f, 0f, -1.5f);
            yield return null;

            Assert.IsTrue(perception.CanSeeTarget);
        }

        [UnityTest]
        public IEnumerator CannotSeeBeyondHerSightRange()
        {
            targetGo.transform.position = new Vector3(0f, 0f, 40f);
            yield return null;

            Assert.IsFalse(perception.CanSeeTarget);
        }

        [UnityTest]
        public IEnumerator ANearbyNoiseBecomesSomethingToInvestigate()
        {
            targetGo.transform.position = new Vector3(0f, 0f, -40f);   // out of sight
            yield return null;
            perception.Forget();

            NoiseBus.Emit(new Vector3(4f, 0f, 0f), 10f, NoiseKind.Impact);
            yield return WaitUntil(() => perception.LastKnownPosition.HasValue);

            Assert.IsTrue(
                perception.LastKnownPosition.HasValue,
                "A noise well inside her hearing radius produced nothing to investigate.");
            Assert.AreEqual(4f, perception.LastKnownPosition.Value.x, 0.01f);
        }

        [UnityTest]
        public IEnumerator ADistantNoiseIsIgnored()
        {
            targetGo.transform.position = new Vector3(0f, 0f, -40f);
            yield return null;
            perception.Forget();

            NoiseBus.Emit(new Vector3(60f, 0f, 0f), 5f, NoiseKind.Impact);
            yield return WaitUntil(() => perception.LastKnownPosition.HasValue, maxFrames: 5);

            Assert.IsFalse(perception.LastKnownPosition.HasValue, "A noise past its radius must not carry.");
        }

        [UnityTest]
        public IEnumerator HerOwnNoiseIsIgnored()
        {
            targetGo.transform.position = new Vector3(0f, 0f, -40f);
            yield return null;
            perception.Forget();

            NoiseBus.Emit(grannyGo.transform.position, 10f, NoiseKind.Footstep, grannyGo);
            yield return WaitUntil(() => perception.LastKnownPosition.HasValue, maxFrames: 5);

            Assert.IsFalse(
                perception.LastKnownPosition.HasValue,
                "She would otherwise chase her own footsteps around the house.");
        }

        [UnityTest]
        public IEnumerator ForgetClearsEverything()
        {
            yield return null;
            Assert.IsTrue(perception.LastKnownPosition.HasValue);

            perception.Forget();

            Assert.IsFalse(perception.CanSeeTarget);
            Assert.IsFalse(perception.LastKnownPosition.HasValue);
        }
    }
}
