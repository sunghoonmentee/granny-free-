using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using Granny.Gameplay.Player;
using UnityEngine;

namespace Granny.EditorTools
{
    /// <summary>
    /// Photographs the house from a fixed set of places, at eye height, under the
    /// house's own lights and nothing else.
    ///
    /// The portrait tool brings its own lamp, which is right for judging a face
    /// and wrong for judging anything else: furniture tuned under it came out
    /// looking like a catalogue in one room and like a hole in the floor in the
    /// next. Art that is only ever seen at a tenth of a lux has to be judged at a
    /// tenth of a lux.
    ///
    /// The viewpoints never move, so two runs can be put side by side and the
    /// difference between them is the change rather than the camera.
    ///
    ///     ./tools/unity.ps1 run -Method Granny.EditorTools.HouseTour.Run -WithGraphics
    /// </summary>
    public static class HouseTour
    {
        const int Width = 1280;
        const int Height = 720;
        const float Eye = 1.62f;

        const float Basement = -3.2f;
        const float Ground = 0f;
        const float Upper = 3.2f;
        const float Attic = 6.4f;

        /// <summary>
        /// Where to stand and what to look at. Chosen to cover one of each kind
        /// of surface and one of each piece of furniture, not to flatter.
        /// </summary>
        static readonly (string name, Vector3 from, Vector3 to)[] Views =
        {
            ("hall-to-door", new Vector3(0f, Ground + Eye, 0.5f), new Vector3(0f, Ground + 1.5f, 9.4f)),
            ("hall-inward", new Vector3(0f, Ground + Eye, 8f), new Vector3(0f, Ground + 1.1f, -6f)),
            ("landing", new Vector3(0f, Upper + Eye, 1.8f), new Vector3(0f, Ground + 0.9f, 7f)),
            ("bedroom", new Vector3(9.5f, Upper + Eye, -1.6f), new Vector3(8f, Upper + 0.5f, -6f)),
            ("kitchen", new Vector3(-6f, Ground + Eye, 2f), new Vector3(-11f, Ground + 0.9f, 6f)),
            ("parlour", new Vector3(-6f, Ground + Eye, -2f), new Vector3(-11f, Ground + 1.1f, -5f)),
            ("cellar", new Vector3(-7f, Basement + Eye, 2f), new Vector3(-11.4f, Basement + 0.8f, 5.5f)),
            ("garage", new Vector3(18f, Basement + Eye, 0f), new Vector3(26f, Basement + 1f, -2f)),
            ("attic", new Vector3(0f, Attic + Eye, 0f), new Vector3(4.5f, Attic + 0.8f, 6f)),

            // Close enough to see what a piece of furniture is actually made of.
            // Judging a material from across a room is how a wardrobe went three
            // rebuilds with two of its doors the wrong colour.
            ("wardrobe-close", new Vector3(4.5f, Attic + Eye, 4.2f), new Vector3(4.5f, Attic + 1.2f, 6.4f)),
            ("rug-close", new Vector3(0f, Ground + Eye, 3.2f), new Vector3(0f, Ground + 0.02f, 5.6f)),
            ("safe", new Vector3(9.4f, Upper + Eye, 4.4f), new Vector3(11.6f, Upper + 1.4f, 4.4f)),
            ("truck", new Vector3(26.5f, Basement + Eye, -5.4f), new Vector3(22f, Basement + 1.3f, -1.5f)),
            ("locker", new Vector3(9.2f, Ground + Eye, -8.2f), new Vector3(11.5f, Ground + 1.1f, -8.5f)),
        };

        [MenuItem("Granny/Tour the House", priority = 4)]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/House.unity", OpenSceneMode.Single);

            var folder = Path.Combine(
                Directory.GetParent(Application.dataPath)!.Parent!.FullName,
                "unity-logs", "tour");
            Directory.CreateDirectory(folder);

            foreach (var view in Views)
                Shoot(view.name, view.from, view.to, folder);

            ShootHand("hammer", folder);
            ShootHand("bottle", folder);

            Debug.Log($"[Tour] done — {Views.Length} views in {folder}");
        }

        /// <summary>
        /// The same hall, with something in your hand.
        ///
        /// What the player carries is on screen more than anything else in the
        /// game, and until it was looked at it was a word in the corner of the
        /// HUD. Shot from the same place every time, with the pose the game
        /// actually uses, so the two cannot drift apart.
        /// </summary>
        static void ShootHand(string item, string folder)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/_Project/Prefabs/Items/{item}.prefab");

            if (prefab == null)
            {
                Debug.LogWarning($"[Tour] No {item} prefab to photograph.");
                return;
            }

            var held = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            try
            {
                Shoot($"hand-{item}", new Vector3(0f, Ground + Eye, 1f),
                    new Vector3(0f, Ground + 1.5f, 9.4f), folder, held);
            }
            finally
            {
                Object.DestroyImmediate(held);
            }
        }

        static void Shoot(string name, Vector3 from, Vector3 to, string folder,
            GameObject inHand = null)
        {
            var rig = new GameObject($"TourCam_{name}");

            try
            {
                var camera = rig.AddComponent<Camera>();
                camera.transform.position = from;
                camera.transform.LookAt(to);

                // The same lens the player looks through. A wider one shows more
                // of the room and tells you less about what playing it is like.
                camera.fieldOfView = 68f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 80f;
                // The same black the player's camera clears to, so a hole in the
                // house shows up here as a hole rather than as weather.
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;

                if (inHand != null)
                {
                    inHand.transform.SetParent(camera.transform, false);
                    inHand.transform.localPosition = HandView.Rest;
                    inHand.transform.localRotation = Quaternion.Euler(HandView.RestAngles);
                }

                var target = new RenderTexture(Width, Height, 24);
                camera.targetTexture = target;
                camera.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = target;

                var shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                shot.Apply();

                File.WriteAllBytes(Path.Combine(folder, $"{name}.jpg"), shot.EncodeToJPG(88));

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
