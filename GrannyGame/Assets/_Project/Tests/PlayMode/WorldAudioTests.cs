using System.Collections;
using System.Linq;
using Granny.Core;
using Granny.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// The house's voice.
    ///
    /// The thing worth protecting here is that the player hears more than she
    /// does. Her rules decide what she comes running for; they must never decide
    /// what the person pulling a drawer open gets to hear, or the game would
    /// teach that a drawer is silent — which is true for her and false for them.
    /// </summary>
    public class WorldAudioTests
    {
        GameObject root;
        WorldAudio world;
        SoundBank bank;

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
            root = new GameObject("WorldAudioTestRoot");
            root.SetActive(false);

            world = root.AddComponent<WorldAudio>();

#if UNITY_EDITOR
            bank = UnityEditor.AssetDatabase.LoadAssetAtPath<SoundBank>(
                "Assets/_Project/Audio/SoundBank.asset");
#endif
            Assert.IsNotNull(bank, "No sound bank. Run Granny > Build Audio.");
            Wire(world, "bank", bank);

            root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        AudioSource[] Voices() => root.GetComponentsInChildren<AudioSource>();

        [UnityTest]
        public IEnumerator EverySoundSheComesRunningForHasAVoice()
        {
            yield return null;

            foreach (NoiseKind kind in System.Enum.GetValues(typeof(NoiseKind)))
            {
                if (!NoiseRules.IsAudible(kind)) continue;

                Assert.IsTrue(bank.Has(kind),
                    $"{kind} brings her across the house and the player hears nothing.");
            }
        }

        [UnityTest]
        public IEnumerator SheHasAStickToTapWith()
        {
            yield return null;

            Assert.IsNotNull(bank.Cane,
                "Her cane is the only way to tell where she is without looking at her.");
        }

        [UnityTest]
        public IEnumerator ABreakingJarIsPlayedWhereItBroke()
        {
            yield return null;

            var spot = new Vector3(4f, HouseLayout.FloorHeight, -3f);
            NoiseBus.Emit(spot, NoiseKind.Breakage);
            yield return null;

            var playing = Voices().FirstOrDefault(v => v.isPlaying);

            Assert.IsNotNull(playing, "Nothing was heard at all.");
            Assert.AreEqual(spot, playing.transform.position,
                "Upstairs and in the cellar have to sound different.");
            Assert.AreEqual(1f, playing.spatialBlend,
                "A flat sound tells the player nothing about where to run.");
        }

        [UnityTest]
        public IEnumerator SoundsSheIgnoresAreStillHeardByThePlayer()
        {
            yield return null;

            Assert.IsFalse(NoiseRules.IsAudible(NoiseKind.Container),
                "This test is about a sound she is supposed to ignore.");

            NoiseBus.Emit(Vector3.zero, NoiseKind.Container);
            yield return null;

            Assert.IsTrue(Voices().Any(v => v.isPlaying),
                "A drawer is silent to her and perfectly audible to whoever opened it.");
        }

        [UnityTest]
        public IEnumerator WalkingStaysSilent()
        {
            yield return null;

            NoiseBus.Emit(Vector3.zero, NoiseKind.Footstep);
            yield return null;

            Assert.IsFalse(Voices().Any(v => v.isPlaying),
                "Giving footsteps a sound would teach exactly the wrong rule.");
        }

        [UnityTest]
        public IEnumerator ManyNoisesAtOnceDoNotBreedGameObjects()
        {
            yield return null;

            var before = Voices().Length;

            for (var i = 0; i < 40; i++)
                NoiseBus.Emit(Vector3.one * i, NoiseKind.ItemImpact);

            yield return null;

            Assert.AreEqual(before, Voices().Length,
                "A shelf of jars hitting the floor must not leave forty objects behind.");
        }
    }
}
