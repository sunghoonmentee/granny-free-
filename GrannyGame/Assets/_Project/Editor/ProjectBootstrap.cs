using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Granny.EditorTools
{
    /// <summary>
    /// One-shot, idempotent project setup. Everything the project needs that would
    /// otherwise be clicked together in the Editor GUI lives here instead, so the
    /// setup is reproducible, reviewable in diffs, and runnable headlessly:
    ///
    ///     Unity.exe -batchmode -quit -projectPath GrannyGame \
    ///               -executeMethod Granny.EditorTools.ProjectBootstrap.Run
    /// </summary>
    public static class ProjectBootstrap
    {
        const string SettingsDir = "Assets/_Project/Settings";
        const string ScenesDir = "Assets/_Project/Scenes";
        const string MaterialsDir = "Assets/_Project/Art/Materials";

        const string RendererPath = SettingsDir + "/GrannyRenderer.asset";
        const string PipelinePath = SettingsDir + "/GrannyPipeline.asset";

        [MenuItem("Granny/Bootstrap Project", priority = 0)]
        public static void Run()
        {
            Debug.Log("[Bootstrap] start");

            EnsureFolders();
            ConfigureLayersAndTags();
            var pipeline = ConfigureRenderPipeline();
            ConfigurePlayerSettings();
            var greybox = EnsureGreyboxMaterials();
            BuildScenes(greybox);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Bootstrap] done (pipeline={(pipeline != null ? pipeline.name : "none")})");
        }

        // ------------------------------------------------------------------
        // Folders
        // ------------------------------------------------------------------

        static readonly string[] ProjectFolders =
        {
            "Assets/_Project",
            "Assets/_Project/Art",
            "Assets/_Project/Art/Materials",
            "Assets/_Project/Audio",
            "Assets/_Project/Data",
            "Assets/_Project/Input",
            "Assets/_Project/Prefabs",
            "Assets/_Project/Scenes",
            "Assets/_Project/Settings",
        };

        static void EnsureFolders()
        {
            foreach (var folder in ProjectFolders)
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                var parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
        }

        // ------------------------------------------------------------------
        // Layers & tags
        // ------------------------------------------------------------------

        /// <summary>Layer index -> name. Indices 0-7 are reserved by Unity except 3, 6, 7.</summary>
        static readonly (int index, string name)[] Layers =
        {
            (6, "Player"),
            (7, "Granny"),
            (8, "Interactable"),
            (9, "HidingSpot"),
            (10, "Door"),
            (11, "Pickup"),
            (12, "Prop"),
            (13, "LevelGeometry"),
        };

        static readonly string[] Tags = { "Granny", "HidingSpot", "SpawnPoint", "EscapeDoor" };

        static void ConfigureLayersAndTags()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0)
            {
                Debug.LogWarning("[Bootstrap] TagManager.asset not found; skipping layers/tags");
                return;
            }

            var tagManager = new SerializedObject(assets[0]);

            var layerProp = tagManager.FindProperty("layers");
            foreach (var (index, name) in Layers)
            {
                if (index >= layerProp.arraySize) continue;
                layerProp.GetArrayElementAtIndex(index).stringValue = name;
            }

            var tagsProp = tagManager.FindProperty("tags");
            foreach (var tag in Tags)
            {
                if (HasTag(tagsProp, tag)) continue;
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();

            // Granny should never physically push the player around; the catch is
            // handled by a trigger, not by collision response.
            Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Granny"), true);
        }

        static bool HasTag(SerializedProperty tagsProp, string tag)
        {
            for (var i = 0; i < tagsProp.arraySize; i++)
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                    return true;
            return false;
        }

        // ------------------------------------------------------------------
        // Render pipeline
        // ------------------------------------------------------------------

        static UniversalRenderPipelineAsset ConfigureRenderPipeline()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            // Forward+ instead of plain Forward. In Forward, URP lights each object
            // with at most a handful of the nearest additional lights, and the
            // house is built from very large pieces - a whole floor slab is one
            // object, so most of its bulbs were simply dropped and the floor went
            // black between pools of light. Forward+ has no such per-object limit.
            var rendererSettings = new SerializedObject(renderer);
            var modeProperty = rendererSettings.FindProperty("m_RenderingMode");
            if (modeProperty != null)
            {
                modeProperty.intValue = 2;   // RenderingMode.ForwardPlus
                rendererSettings.ApplyModifiedPropertiesWithoutUndo();
            }

            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 4;
            pipeline.shadowDistance = 35f;
            pipeline.shadowCascadeCount = 2;
            pipeline.supportsCameraDepthTexture = true;   // required by soft particles / fog effects
            pipeline.supportsCameraOpaqueTexture = false;

            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;

            var current = QualitySettings.GetQualityLevel();
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);

            return pipeline;
        }

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.companyName = "sunghoon";
            PlayerSettings.productName = Granny.Core.GameIdentity.ProductName;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.runInBackground = true;
        }

        // ------------------------------------------------------------------
        // Placeholder art
        // ------------------------------------------------------------------

        public struct GreyboxMaterials
        {
            public Material Floor;
            public Material Wall;
            public Material Ceiling;
            public Material Accent;
        }

        static GreyboxMaterials EnsureGreyboxMaterials()
        {
            return new GreyboxMaterials
            {
                Floor = EnsureLitMaterial("GreyboxFloor", new Color(0.16f, 0.14f, 0.12f), 0.85f),
                Wall = EnsureLitMaterial("GreyboxWall", new Color(0.22f, 0.19f, 0.16f), 0.9f),
                Ceiling = EnsureLitMaterial("GreyboxCeiling", new Color(0.10f, 0.09f, 0.08f), 1f),
                Accent = EnsureLitMaterial("GreyboxAccent", new Color(0.35f, 0.16f, 0.10f), 0.7f),
            };
        }

        static Material EnsureLitMaterial(string name, Color albedo, float smoothnessInverse)
        {
            var path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogWarning("[Bootstrap] URP/Lit shader not found; falling back to default material");
                    shader = Shader.Find("Standard");
                }
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", albedo);
            mat.SetFloat("_Smoothness", 1f - smoothnessInverse);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------
        // Scenes
        // ------------------------------------------------------------------

        static void BuildScenes(GreyboxMaterials greybox)
        {
            var boot = CreateScene("Boot", s => BuildBootScene());
            var menu = CreateScene("MainMenu", s => BuildMenuScene());
            var house = CreateScene("House", s => BuildHouseScene(greybox));

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(boot, true),
                new EditorBuildSettingsScene(menu, true),
                new EditorBuildSettingsScene(house, true),
            };
        }

        static string CreateScene(string name, System.Action<Scene> populate)
        {
            var path = $"{ScenesDir}/{name}.unity";
            if (File.Exists(path))
            {
                Debug.Log($"[Bootstrap] scene '{name}' already exists; leaving it alone");
                return path;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            populate(scene);
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        static void BuildBootScene()
        {
            var go = new GameObject("Bootstrapper");
            go.AddComponent<Granny.Core.Bootstrapper>();
        }

        static void BuildMenuScene()
        {
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0, 1.6f, -4f);
            cam.GetComponent<Camera>().backgroundColor = Color.black;
            cam.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;

            ApplyHorrorLighting();
        }

        static void BuildHouseScene(GreyboxMaterials greybox)
        {
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0, 1.7f, -8f);

            var sun = new GameObject("Moonlight");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.62f, 0.85f);
            light.intensity = 0.12f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(38f, 150f, 0f);

            ApplyHorrorLighting();

            // Greybox shell: a single 20x20 room. The real four-storey house is
            // built in a later phase; this exists so the scene is walkable now.
            var root = new GameObject("Greybox").transform;
            const float size = 20f;
            const float height = 3.2f;
            const float thickness = 0.4f;

            MakeBox(root, "Floor", new Vector3(0, -thickness / 2f, 0), new Vector3(size, thickness, size), greybox.Floor);
            MakeBox(root, "Ceiling", new Vector3(0, height + thickness / 2f, 0), new Vector3(size, thickness, size), greybox.Ceiling);

            var half = size / 2f;
            MakeBox(root, "Wall_North", new Vector3(0, height / 2f, half), new Vector3(size, height, thickness), greybox.Wall);
            MakeBox(root, "Wall_South", new Vector3(0, height / 2f, -half), new Vector3(size, height, thickness), greybox.Wall);
            MakeBox(root, "Wall_East", new Vector3(half, height / 2f, 0), new Vector3(thickness, height, size), greybox.Wall);
            MakeBox(root, "Wall_West", new Vector3(-half, height / 2f, 0), new Vector3(thickness, height, size), greybox.Wall);

            // A stand-in for the front door, so the escape target has a location.
            MakeBox(root, "FrontDoor_Placeholder", new Vector3(0, 1.05f, half - thickness), new Vector3(1.1f, 2.1f, 0.12f), greybox.Accent);

            var lamp = new GameObject("Hall Bulb");
            var bulb = lamp.AddComponent<Light>();
            bulb.type = LightType.Point;
            bulb.color = new Color(1f, 0.82f, 0.58f);
            bulb.intensity = 2.4f;
            bulb.range = 9f;
            bulb.shadows = LightShadows.Soft;
            lamp.transform.position = new Vector3(0, height - 0.4f, 0);
        }

        static void MakeBox(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.layer = LayerMask.NameToLayer("LevelGeometry");
            go.isStatic = true;
            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>
        /// Dim, but readable.
        ///
        /// The first pass at this was unplayable: ambient light at 0.035 is black,
        /// the fog was dense enough to swallow anything past about fifteen metres,
        /// and the house was painted in 0.14-0.28 albedo on top of that. You could
        /// not see the room you were standing in. Granny's own house is gloomy but
        /// legible - you can read the layout, and the torch is for corners and
        /// cupboards. These values aim at that; the old values become the optional
        /// "darker" mode in a later phase.
        /// </summary>
        static void ApplyHorrorLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.155f, 0.17f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.05f, 0.048f, 0.055f);
            RenderSettings.fogDensity = 0.018f;
            RenderSettings.skybox = null;
        }
    }
}
