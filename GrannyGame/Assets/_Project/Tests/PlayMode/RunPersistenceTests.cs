using System.Collections;
using System.Linq;
using Granny.Core;
using Granny.Gameplay;
using Granny.Gameplay.Interaction;
using Granny.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// A five-day run has to survive quitting the game, and it has to survive it
    /// honestly: progress the player earned comes back, and the positions that
    /// would let them undo a chase do not.
    /// </summary>
    public class RunPersistenceTests
    {
        GameObject sceneRoot;
        GameDirector director;
        DifficultyProfile profile;
        PlayerInventory inventory;

        static DifficultyProfile MakeProfile(int days, string name)
        {
            var p = ScriptableObject.CreateInstance<DifficultyProfile>();
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(p);
            so.FindProperty("daysAllowed").intValue = days;
            so.FindProperty("displayName").stringValue = name;
            so.ApplyModifiedPropertiesWithoutUndo();
#endif
            return p;
        }

        static ItemDefinition MakeItem(string id, ItemCarry carry)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(item);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("carry").enumValueIndex = (int)carry;
            so.ApplyModifiedPropertiesWithoutUndo();
#endif
            return item;
        }

        static void Wire(Object target, string field, Object value)
        {
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
#endif
        }

        static void SetString(Object target, string field, string value)
        {
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(target);
            so.FindProperty(field).stringValue = value;
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
            SaveSystem.Delete();

            sceneRoot = new GameObject("PersistenceTestRoot");
            profile = MakeProfile(5, "Hard");

            var playerGo = new GameObject("TestPlayer");
            playerGo.transform.SetParent(sceneRoot.transform);
            LogAssert.ignoreFailingMessages = true;     // no input reader on this stand-in
            var motor = playerGo.AddComponent<PlayerMotor>();
            inventory = playerGo.AddComponent<PlayerInventory>();

            var directorGo = new GameObject("Director");
            directorGo.transform.SetParent(sceneRoot.transform);
            directorGo.SetActive(false);

            director = directorGo.AddComponent<GameDirector>();
            Wire(director, "difficulty", profile);
            Wire(director, "player", motor);
            SetFloat(director, "blackoutSeconds", 0.05f);

            directorGo.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            if (sceneRoot != null) Object.DestroyImmediate(sceneRoot);
            if (profile != null) Object.DestroyImmediate(profile);
            SaveSystem.Delete();
        }

        LockStage AddStage(string name, string tag)
        {
            var go = new GameObject(name);
            go.transform.SetParent(sceneRoot.transform);
            go.SetActive(false);

            var stage = go.AddComponent<LockStage>();
            SetString(stage, "requiredTag", tag);
            SetFloat(stage, "workSeconds", 0.5f);

            go.SetActive(true);
            return stage;
        }

        [UnityTest]
        public IEnumerator ASnapshotRecordsTheDayAndWhatIsCarried()
        {
            yield return null;

            inventory.TryTake(MakeItem("hammer", ItemCarry.Held));
            inventory.TryTake(MakeItem("key.front", ItemCarry.Pocketed));

            var snapshot = director.Snapshot();

            Assert.AreEqual(1, snapshot.day);
            Assert.AreEqual("Hard", snapshot.difficultyName);
            Assert.AreEqual("hammer", snapshot.heldItemId);
            CollectionAssert.Contains(snapshot.pocketItemIds, "key.front");
        }

        [UnityTest]
        public IEnumerator ClearedFasteningsAreRecorded()
        {
            var stage = AddStage("Lock_Plank", "pry");
            yield return null;

            Assert.IsEmpty(director.Snapshot().clearedLockIds);

            stage.ForceClear();

            CollectionAssert.Contains(director.Snapshot().clearedLockIds, "Lock_Plank");
        }

        [UnityTest]
        public IEnumerator ForceClearSkipsTheWorkAndTheNoise()
        {
            var stage = AddStage("Lock_Cord", "cut");
            yield return null;

            var heard = 0;
            void Listener(Noise _) => heard++;

            NoiseBus.Heard += Listener;
            try
            {
                stage.ForceClear();
            }
            finally
            {
                NoiseBus.Heard -= Listener;
            }

            Assert.IsTrue(stage.IsCleared);
            Assert.AreEqual(0, heard, "Resuming a run must not announce the player's position.");
        }

        [UnityTest]
        public IEnumerator ASearchedDrawerStaysEmptyAcrossAResume()
        {
            var go = new GameObject("Dresser_0");
            go.transform.SetParent(sceneRoot.transform);
            go.SetActive(false);

            var sliding = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            sliding.SetParent(go.transform, false);

            var drawer = go.AddComponent<Drawer>();
            Wire(drawer, "sliding", sliding);
            go.SetActive(true);
            yield return null;

            drawer.SetContents(MakeItem("hammer", ItemCarry.Held));
            Assert.IsFalse(drawer.HasBeenSearched);

            drawer.MarkSearched();

            Assert.IsTrue(drawer.HasBeenSearched);
            Assert.IsNull(drawer.Contents, "Finding the same hammer twice would break the run.");
            CollectionAssert.Contains(director.Snapshot().searchedContainerIds, "Dresser_0");
        }

        [UnityTest]
        public IEnumerator EscapingClearsTheSavedRun()
        {
            yield return null;

            SaveSystem.Save(director.Snapshot());
            Assert.IsTrue(SaveSystem.HasSave);

            director.ReportEscape();

            Assert.IsFalse(SaveSystem.HasSave, "A finished run must not offer to be continued.");
        }

        [UnityTest]
        public IEnumerator ASavedRunResumesOnTheRightDay()
        {
            yield return null;

            var snapshot = director.Snapshot();
            snapshot.day = 4;
            SaveSystem.Save(snapshot);

            // A second director stands in for reloading the scene.
            var revived = new GameObject("Director2");
            revived.transform.SetParent(sceneRoot.transform);
            revived.SetActive(false);

            var resumed = revived.AddComponent<GameDirector>();
            Wire(resumed, "difficulty", profile);
            revived.SetActive(true);
            yield return null;

            Assert.AreEqual(4, resumed.Days.Day);
        }

        [UnityTest]
        public IEnumerator SnapshotPocketsLineUpWithTheBeltSlots()
        {
            yield return null;

            inventory.TryTake(MakeItem("key.front", ItemCarry.Pocketed));

            var snapshot = director.Snapshot();

            Assert.AreEqual(inventory.PocketCapacity, snapshot.pocketItemIds.Count,
                "Empty slots must be recorded too, or items shuffle position on reload.");
            Assert.AreEqual("key.front", snapshot.pocketItemIds.First());
        }
    }
}
