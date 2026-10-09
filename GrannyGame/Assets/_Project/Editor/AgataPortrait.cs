using System.IO;
using Granny.Gameplay.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Granny.EditorTools
{
    /// <summary>
    /// Takes her picture, from the front and from the side, lit the way the house
    /// lights her.
    ///
    /// Looks are the one part of this project a test cannot hold the line on. A
    /// suite can prove she walks to the right places; it has no opinion about
    /// whether she is frightening. So: render her on demand and look, rather
    /// than guessing from numbers in a builder.
    ///
    ///     ./tools/unity.ps1 run -Method Granny.EditorTools.AgataPortrait.Run -WithGraphics
    /// </summary>
    public static class AgataPortrait
    {
        const int Width = 720;
        const int Height = 900;

        [MenuItem("Granny/Photograph Agata", priority = 3)]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/House.unity", OpenSceneMode.Single);

            var agata = Object.FindAnyObjectByType<GrannyBrain>();
            if (agata == null)
            {
                Debug.LogError("[Portrait] No Agata in the scene.");
                return;
            }

            var folder = Path.Combine(
                Directory.GetParent(Application.dataPath)!.Parent!.FullName,
                "unity-logs", "portrait");
            Directory.CreateDirectory(folder);

            var stand = agata.transform.position;

            // Three-quarter front, flat side, and a low angle — the one a player
            // gets when she is already too close.
            Shoot(stand, folder, "front", new Vector3(0.9f, 1.45f, 2.3f), 1.35f);
            Shoot(stand, folder, "side", new Vector3(2.6f, 1.35f, 0.2f), 1.25f);
            Shoot(stand, folder, "looming", new Vector3(0.3f, 0.75f, 1.35f), 1.5f);

            Debug.Log($"[Portrait] done — {folder}");
        }

        static void Shoot(Vector3 subject, string folder, string name, Vector3 offset, float lookHeight)
        {
            var rig = new GameObject($"PortraitCam_{name}");

            try
            {
                var camera = rig.AddComponent<Camera>();
                camera.transform.position = subject + offset;
                camera.transform.LookAt(subject + Vector3.up * lookHeight);
                camera.fieldOfView = 46f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 60f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.02f, 0.02f, 0.03f);

                // A single warm lamp just off to one side, which is all the house
                // ever gives her, and is what makes the hunch cast a shadow.
                var lamp = new GameObject("PortraitLamp");
                lamp.transform.SetParent(rig.transform, false);
                lamp.transform.position = subject + new Vector3(1.6f, 2.4f, 1.9f);

                var light = lamp.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.86f, 0.64f);
                light.intensity = 2.0f;
                light.range = 8f;

                // A cold scrap of fill, so the dark side of her is dark but not
                // a silhouette. Lit by one lamp alone she washes out to a single
                // value and every judgement made from the photograph is wrong.
                var fillGo = new GameObject("PortraitFill");
                fillGo.transform.SetParent(rig.transform, false);
                fillGo.transform.position = subject + new Vector3(-2.2f, 1.6f, 1.2f);

                var fill = fillGo.AddComponent<Light>();
                fill.type = LightType.Point;
                fill.color = new Color(0.55f, 0.62f, 0.78f);
                fill.intensity = 0.5f;
                fill.range = 7f;

                var target = new RenderTexture(Width, Height, 24);
                camera.targetTexture = target;
                camera.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = target;

                var shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                shot.Apply();

                File.WriteAllBytes(Path.Combine(folder, $"{name}.jpg"), shot.EncodeToJPG(92));

                RenderTexture.active = previous;
                camera.targetTexture = null;

                Object.DestroyImmediate(shot);
                target.Release();
                Object.DestroyImmediate(target);
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }
    }
}
