using System.Collections;
using Granny.Core;
using Granny.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// The five-day loop as it actually plays: get caught, wake up in bed, lose a
    /// day, and run out on the last one.
    /// </summary>
    public class GameDirectorTests
    {
        GameObject sceneRoot;
        GameDirector director;
        DifficultyProfile profile;

        static DifficultyProfile MakeProfile(int days)
        {
            var p = ScriptableObject.CreateInstance<DifficultyProfile>();
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(p);
            so.FindProperty("daysAllowed").intValue = days;
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

        static void SetFloat(Object target, string field, float value)
        {
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
#endif
        }

        [SetUp]
        public void SetUp()
        {
            sceneRoot = new GameObject("DirectorTestRoot");
            profile = MakeProfile(3);

            var go = new GameObject("Director");
            go.transform.SetParent(sceneRoot.transform);
            go.SetActive(false);

            director = go.AddComponent<GameDirector>();
            Wire(director, "difficulty", profile);
            SetFloat(director, "blackoutSeconds", 0.1f);

            go.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (sceneRoot != null) Object.DestroyImmediate(sceneRoot);
            if (profile != null) Object.DestroyImmediate(profile);
        }

        [UnityTest]
        public IEnumerator TheClockStartsOnDayOneWithTheProfilesAllowance()
        {
            yield return null;

            Assert.AreEqual(1, director.Days.Day);
            Assert.AreEqual(3, director.Days.DaysAllowed);
        }

        [UnityTest]
        public IEnumerator TheRunEndsAfterTheLastDayIsUsed()
        {
            yield return null;

            var over = 0;
            director.Days.GameOver += () => over++;

            director.Days.Caught();
            director.Days.Caught();
            Assert.AreEqual(3, director.Days.Day);
            Assert.IsFalse(director.Days.IsOver);

            director.Days.Caught();

            Assert.IsTrue(director.Days.IsOver);
            Assert.AreEqual(1, over);
        }

        [UnityTest]
        public IEnumerator ReportingAnEscapeWinsTheRun()
        {
            yield return null;

            var escaped = false;
            director.Escaped += () => escaped = true;

            director.ReportEscape();
            yield return null;

            Assert.IsTrue(escaped);
            Assert.IsTrue(director.Days.HasEscaped);
        }
    }
}
