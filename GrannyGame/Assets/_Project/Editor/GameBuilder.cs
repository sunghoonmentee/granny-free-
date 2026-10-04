using System;
using System.IO;
using System.Linq;
using Granny.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Granny.EditorTools
{
    /// <summary>
    /// Turns the project into something you can double-click.
    ///
    /// The build lands outside the Unity project, in <c>Build/</c> at the repo
    /// root, so the player and its data folder never end up in the asset
    /// database — and so the whole folder can be zipped and handed to someone
    /// who has never installed Unity.
    ///
    ///     ./tools/unity.ps1 run -Method Granny.EditorTools.GameBuilder.BuildWindows
    /// </summary>
    public static class GameBuilder
    {
        const string OutputFolder = "Build";

        [MenuItem("Granny/Build Windows Player", priority = 200)]
        public static void BuildWindows()
        {
            Debug.Log("[Build] start");

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new InvalidOperationException(
                    "No scenes are enabled in Build Settings. Run Granny > Bootstrap Project first.");

            // Boot has to come first: it is the scene that loads a save (or does
            // not) and sends the player on to the menu.
            if (!scenes[0].EndsWith($"/{SceneNames.Boot}.unity", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"'{SceneNames.Boot}' must be the first scene in Build Settings, not '{scenes[0]}'.");

            var root = Directory.GetParent(Application.dataPath)!.Parent!.FullName;
            var folder = Path.Combine(root, OutputFolder, GameIdentity.ProductName);
            var exe = Path.Combine(folder, $"{GameIdentity.ProductName}.exe");

            // A stale player from an earlier build can keep old managed DLLs that
            // Unity will happily leave in place, so the folder starts empty.
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            Directory.CreateDirectory(folder);

            ApplyPlayerSettings();

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            foreach (var step in report.steps)
            foreach (var message in step.messages)
                if (message.type is LogType.Error or LogType.Exception)
                    Debug.LogError($"[Build] {message.content}");

            if (summary.result != BuildResult.Succeeded)
                throw new Exception($"[Build] failed: {summary.result}, {summary.totalErrors} error(s)");

            Debug.Log(
                $"[Build] done — {exe} " +
                $"({summary.totalSize / (1024f * 1024f):F0} MB, {summary.totalTime.TotalSeconds:F0}s)");
        }

        /// <summary>
        /// Settings that only matter once the game runs outside the editor.
        /// </summary>
        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Granny Free";
            PlayerSettings.productName = GameIdentity.ProductName;

            // Mono, not IL2CPP: IL2CPP needs the Visual Studio C++ toolchain
            // installed, which is a second download before anyone can build.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            // Borderless fullscreen at the desktop's own resolution. Escape opens
            // the pause menu, which leads back to the title screen and out.
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;

            // There is no splash art yet, and four seconds of a Unity logo before
            // a horror game's title card is worse than none. Some licences will
            // not let this be turned off; that is not a reason to fail a build.
            try { PlayerSettings.SplashScreen.show = false; }
            catch (Exception e) { Debug.LogWarning($"[Build] splash screen stays on: {e.Message}"); }

            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.visibleInBackground = false;
        }
    }
}
