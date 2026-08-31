using System.Collections;
using Granny.Core;
using Granny.Gameplay.Interaction;
using Granny.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// The endgame: three fastenings, three different tools, one hand to carry
    /// them in. These check the door refuses until the last one is off.
    /// </summary>
    public class EscapeDoorTests
    {
        GameObject sceneRoot;
        GameObject player;
        PlayerInventory inventory;
        EscapeDoor door;
        LockStage plank;
        LockStage cord;

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

        static ItemDefinition MakeTool(string id, params string[] tags)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(item);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("carry").enumValueIndex = (int)ItemCarry.Held;

            var tagProp = so.FindProperty("tags");
            tagProp.arraySize = tags.Length;
            for (var i = 0; i < tags.Length; i++)
                tagProp.GetArrayElementAtIndex(i).stringValue = tags[i];

            so.ApplyModifiedPropertiesWithoutUndo();
#endif
            return item;
        }

        LockStage AddStage(Transform parent, string tag, string verb)
        {
            var go = new GameObject($"Stage_{tag}");
            go.transform.SetParent(parent, false);
            go.SetActive(false);

            var stage = go.AddComponent<LockStage>();
            SetString(stage, "requiredTag", tag);
            SetString(stage, "verb", verb);
            SetFloat(stage, "workSeconds", 0.5f);   // one press clears it

            go.SetActive(true);
            return stage;
        }

        [SetUp]
        public void SetUp()
        {
            sceneRoot = new GameObject("EscapeTestRoot");

            player = new GameObject("TestPlayer");
            player.transform.SetParent(sceneRoot.transform);
            inventory = player.AddComponent<PlayerInventory>();

            var doorGo = new GameObject("FrontDoor");
            doorGo.transform.SetParent(sceneRoot.transform);
            doorGo.SetActive(false);

            plank = AddStage(doorGo.transform, "pry", "Pry off");
            cord = AddStage(doorGo.transform, "cut", "Cut");

            door = doorGo.AddComponent<EscapeDoor>();
            doorGo.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (sceneRoot != null) Object.DestroyImmediate(sceneRoot);
        }

        [UnityTest]
        public IEnumerator TheDoorStartsFastenedAndCountsWhatIsLeft()
        {
            yield return null;

            Assert.IsFalse(door.IsUnlocked);
            Assert.AreEqual(2, door.StagesRemaining);
            StringAssert.Contains("2", door.Prompt);
        }

        [UnityTest]
        public IEnumerator AStageRefusesWithoutTheRightTool()
        {
            yield return null;

            inventory.TryTake(MakeTool("cutters", "cut"));
            plank.Interact(player);

            Assert.IsFalse(plank.IsCleared, "Wirecutters must not remove a nailed plank.");
            Assert.IsNotEmpty(plank.LastHint);
        }

        [UnityTest]
        public IEnumerator AStageClearsWithTheRightTool()
        {
            yield return null;

            inventory.TryTake(MakeTool("hammer", "pry"));
            plank.Interact(player);

            Assert.IsTrue(plank.IsCleared);
            Assert.AreEqual(1, door.StagesRemaining);
        }

        [UnityTest]
        public IEnumerator TheDoorOnlyOpensOnceEveryStageIsCleared()
        {
            yield return null;

            var escapes = 0;
            door.Escaped += () => escapes++;

            inventory.TryTake(MakeTool("hammer", "pry"));
            plank.Interact(player);

            door.Interact(player);
            Assert.AreEqual(0, escapes, "One fastening left means the door stays shut.");

            inventory.TryTake(MakeTool("cutters", "cut"));
            cord.Interact(player);

            Assert.IsTrue(door.IsUnlocked);
            door.Interact(player);

            Assert.AreEqual(1, escapes);
        }

        [UnityTest]
        public IEnumerator EscapingTwiceIsImpossible()
        {
            yield return null;

            var escapes = 0;
            door.Escaped += () => escapes++;

            inventory.TryTake(MakeTool("hammer", "pry"));
            plank.Interact(player);
            inventory.TryTake(MakeTool("cutters", "cut"));
            cord.Interact(player);

            door.Interact(player);
            door.Interact(player);

            Assert.AreEqual(1, escapes);
        }

        [UnityTest]
        public IEnumerator WorkingOnAStageIsHeardEveryTime()
        {
            yield return null;

            var heard = 0;
            void Listener(Noise n)
            {
                if (n.Kind == NoiseKind.Breakage) heard++;
            }

            NoiseBus.Heard += Listener;
            try
            {
                inventory.TryTake(MakeTool("hammer", "pry"));
                plank.Interact(player);
            }
            finally
            {
                NoiseBus.Heard -= Listener;
            }

            Assert.AreEqual(1, heard, "Every swing has to announce itself.");
        }

        [UnityTest]
        public IEnumerator RestorePutsEveryFasteningBack()
        {
            yield return null;

            inventory.TryTake(MakeTool("hammer", "pry"));
            plank.Interact(player);
            Assert.IsTrue(plank.IsCleared);

            door.Restore();

            Assert.IsFalse(plank.IsCleared);
            Assert.AreEqual(2, door.StagesRemaining);
        }
    }
}
