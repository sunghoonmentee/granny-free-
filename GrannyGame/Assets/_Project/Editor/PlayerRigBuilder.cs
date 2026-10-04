using Granny.Core;
using Granny.Gameplay.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Granny.EditorTools
{
    /// <summary>
    /// Builds the player prefab from code. The rig has a specific hierarchy that
    /// is easy to get subtly wrong by hand — the bob target must sit *under* the
    /// pivot the motor drives — so it is assembled here and asserted by tests.
    ///
    ///     Unity.exe -batchmode -quit -executeMethod \
    ///       Granny.EditorTools.PlayerRigBuilder.Run
    /// </summary>
    public static class PlayerRigBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string ActionsPath = "Assets/_Project/Input/GrannyControls.inputactions";
        const string HouseScenePath = "Assets/_Project/Scenes/House.unity";

        /// <summary>Where the player wakes up in the greybox room.</summary>
        static readonly Vector3 SpawnPosition = new(0f, 0.1f, -7f);

        [MenuItem("Granny/Build Player Rig", priority = 10)]
        public static void Run()
        {
            var prefab = BuildPrefab();
            if (prefab == null) return;

            PlaceInHouseScene(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PlayerRig] done");
        }

        static GameObject BuildPrefab()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (actions == null)
            {
                Debug.LogError($"[PlayerRig] Input actions not found at {ActionsPath}");
                return null;
            }

            // Hierarchy, and why it is shaped this way:
            //   Player        capsule, motor, look  - yaw turns this
            //   +- CameraPivot                      - motor sets eye height, look sets pitch
            //      +- BobTarget                     - head bob offsets this only
            //         +- Main Camera
            //            +- Flashlight              - beam follows the exact view direction
            var root = new GameObject("Player");
            root.layer = GameLayers.Player;
            root.tag = "Player";

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.32f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.35f;
            controller.skinWidth = 0.02f;
            controller.minMoveDistance = 0f;

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, 1.66f, 0f);

            var bobTarget = new GameObject("BobTarget").transform;
            bobTarget.SetParent(pivot, false);

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(bobTarget, false);

            var cam = cameraGo.GetComponent<Camera>();
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 90f;
            cam.fieldOfView = 68f;

            // URP adds this itself the moment a Camera exists; ask for it rather
            // than adding a second one.
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.renderShadows = true;

            var torchGo = new GameObject("Flashlight");
            torchGo.transform.SetParent(cameraGo.transform, false);
            torchGo.transform.localPosition = new Vector3(0.12f, -0.1f, 0.1f);

            var beam = torchGo.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.color = new Color(1f, 0.94f, 0.82f);
            beam.range = 22f;
            beam.spotAngle = 46f;
            beam.innerSpotAngle = 18f;
            beam.intensity = 0f;
            beam.shadows = LightShadows.Soft;
            beam.enabled = false;

            var input = root.AddComponent<PlayerInputReader>();
            var motor = root.AddComponent<PlayerMotor>();
            var look = root.AddComponent<PlayerLook>();
            var bob = root.AddComponent<HeadBob>();
            var interactor = root.AddComponent<PlayerInteractor>();
            var inventory = root.AddComponent<PlayerInventory>();
            var torch = torchGo.AddComponent<PlayerFlashlight>();

            Wire(input, "actions", actions);

            Wire(motor, "input", input);
            Wire(motor, "cameraPivot", pivot);

            Wire(look, "input", input);
            Wire(look, "body", root.transform);
            Wire(look, "cameraPivot", pivot);

            Wire(bob, "bobTarget", bobTarget);

            // Both aim from the camera, so items are dropped and interacted with
            // exactly where the player is looking rather than where the body faces.
            Wire(interactor, "input", input);
            Wire(interactor, "eye", cameraGo.transform);

            Wire(inventory, "input", input);
            Wire(inventory, "dropOrigin", cameraGo.transform);

            Wire(torch, "input", input);
            Wire(torch, "beam", beam);

            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out var success);
            Object.DestroyImmediate(root);

            if (!success)
            {
                Debug.LogError($"[PlayerRig] Failed to save prefab at {PrefabPath}");
                return null;
            }

            Debug.Log($"[PlayerRig] prefab written to {PrefabPath}");
            return saved;
        }

        /// <summary>
        /// Assigns a private [SerializeField] without making it public. The rig is
        /// built once by tooling; widening the API for it would be worse.
        /// </summary>
        static void Wire(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[PlayerRig] {target.GetType().Name} has no field '{fieldName}'");
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void PlaceInHouseScene(GameObject prefab)
        {
            var scene = EditorSceneManager.OpenScene(HouseScenePath, OpenSceneMode.Single);

            // The greybox scene ships with a bare camera; the player brings its own.
            // Re-running the builder must not stack up duplicate players either.
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name is "Main Camera" or "Player")
                    Object.DestroyImmediate(root);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.position = SpawnPosition;
            instance.transform.rotation = Quaternion.identity;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PlayerRig] player placed in House scene");
        }
    }
}
