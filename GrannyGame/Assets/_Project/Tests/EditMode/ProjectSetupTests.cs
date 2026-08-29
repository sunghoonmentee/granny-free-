using System.Linq;
using Granny.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Granny.Tests
{
    /// <summary>
    /// Guards the one-off project configuration. These are cheap to run and catch
    /// the failure mode where someone opens the project fresh, Unity regenerates a
    /// setting, and rendering or scene loading silently breaks.
    /// </summary>
    public class ProjectSetupTests
    {
        [Test]
        public void UniversalRenderPipelineIsActive()
        {
            Assert.IsNotNull(
                GraphicsSettings.defaultRenderPipeline,
                "No render pipeline assigned. Run Granny > Bootstrap Project.");

            Assert.AreEqual(
                "GrannyPipeline",
                GraphicsSettings.defaultRenderPipeline.name,
                "Expected the project's own URP asset to be active.");
        }

        [Test]
        public void ColorSpaceIsLinear()
        {
            Assert.AreEqual(ColorSpace.Linear, PlayerSettings.colorSpace);
        }

        [Test]
        public void AllScenesAreInBuildSettings()
        {
            var inBuild = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))
                .ToArray();

            foreach (var expected in new[] { SceneNames.Boot, SceneNames.MainMenu, SceneNames.House })
                CollectionAssert.Contains(inBuild, expected);
        }

        [Test]
        public void BootSceneIsFirstInBuildOrder()
        {
            var first = EditorBuildSettings.scenes.FirstOrDefault(s => s.enabled);
            Assert.IsNotNull(first, "Build settings contain no enabled scenes.");
            Assert.AreEqual(SceneNames.Boot, System.IO.Path.GetFileNameWithoutExtension(first.path));
        }

        [Test]
        public void GameplayLayersExist()
        {
            foreach (var layer in new[] { "Player", "Granny", "Interactable", "HidingSpot", "Door", "Pickup" })
                Assert.AreNotEqual(-1, LayerMask.NameToLayer(layer), $"Layer '{layer}' is missing.");
        }

        [Test]
        public void PlayerAndGrannyDoNotCollidePhysically()
        {
            Assert.IsTrue(
                Physics.GetIgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Granny")),
                "Granny must not shove the player around; the catch is a trigger, not a collision.");
        }
    }
}
