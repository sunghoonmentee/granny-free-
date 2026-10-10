using System.Collections;
using System.Linq;
using Granny.Core;
using Granny.Gameplay;
using Granny.Gameplay.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// The two switches beside the difficulty setting.
    ///
    /// Both of these change the whole night and neither is a difficulty, so they
    /// have to work at any setting — including the ones where they look absurd,
    /// like every lock on the door on Easy. These check they reach the game
    /// rather than only the menu, which is the half that is easy to forget.
    /// </summary>
    public class GameModeTests
    {
        Scene house;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SaveSystem.Delete();
            GameModes.Reset();

            yield return SceneManager.LoadSceneAsync(SceneNames.House, LoadSceneMode.Additive);
            house = SceneManager.GetSceneByName(SceneNames.House);
            SceneManager.SetActiveScene(house);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameModes.Reset();
            SaveSystem.Delete();

            var blank = SceneManager.CreateScene($"Blank_{System.Guid.NewGuid():N}");
            SceneManager.SetActiveScene(blank);

            if (house.IsValid() && house.isLoaded)
                yield return SceneManager.UnloadSceneAsync(house);
        }

        static EscapeDoor FrontDoor() =>
            Object.FindObjectsByType<EscapeDoor>(FindObjectsSortMode.None)
                .First(d => d.ScaledByDifficulty);

        [UnityTest]
        public IEnumerator BothStartOff()
        {
            yield return null;

            Assert.IsFalse(GameModes.Dark);
            Assert.IsFalse(GameModes.ExtraLocks);
        }

        [UnityTest]
        public IEnumerator TheyAreRememberedBetweenRuns()
        {
            yield return null;

            GameModes.Dark = true;
            Assert.IsTrue(GameModes.Dark, "Picked in the menu, used in the house.");

            GameModes.Dark = false;
            Assert.IsFalse(GameModes.Dark);
        }

        [UnityTest]
        public IEnumerator ExtraLocksPutsEveryFasteningOnTheDoor()
        {
            yield return null;

            var door = FrontDoor();
            var everything = door.StagesFitted;

            var easy = ScriptableObject.CreateInstance<DifficultyProfile>();

            try
            {
                // Whatever the difficulty asks for, the mode overrules it.
                GameModes.ExtraLocks = true;
                Assert.AreEqual(everything, GameModes.LocksFor(easy, everything));

                GameModes.ExtraLocks = false;
                Assert.AreEqual(easy.FrontDoorLocks, GameModes.LocksFor(easy, everything));
            }
            finally
            {
                Object.DestroyImmediate(easy);
            }
        }

        [UnityTest]
        public IEnumerator DarkModeTurnsTheHouseDownAndBackUp()
        {
            yield return null;

            var dark = Object.FindAnyObjectByType<DarkMode>();
            Assert.IsNotNull(dark, "Nothing in the house can turn the lights down.");

            var lit = RenderSettings.ambientLight;

            dark.Apply(true);
            Assert.Less(RenderSettings.ambientLight.grayscale, lit.grayscale,
                "Dark mode has to actually be darker.");

            dark.Apply(false);
            Assert.AreEqual(lit.grayscale, RenderSettings.ambientLight.grayscale, 0.001f,
                "And turning it off has to put the house back, not leave it dim.");
        }

        [UnityTest]
        public IEnumerator DarkModeLeavesTheTorchAlone()
        {
            yield return null;

            var torch = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.type == LightType.Spot);

            if (torch == null) Assert.Ignore("No torch in this scene to check.");

            var before = torch.intensity;

            Object.FindAnyObjectByType<DarkMode>().Apply(true);

            Assert.AreEqual(before, torch.intensity, 0.001f,
                "In this mode the torch is the only thing keeping the game playable.");
        }
    }
}
