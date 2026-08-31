using Granny.Core;
using Granny.Gameplay.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Granny.EditorTools
{
    /// <summary>
    /// Replaces the placeholder front door with the real one: a leaf held by three
    /// fastenings, each wanting a different tool.
    ///
    ///     Unity.exe -batchmode -quit -executeMethod \
    ///       Granny.EditorTools.EscapeBuilder.Run
    /// </summary>
    public static class EscapeBuilder
    {
        const string MaterialDir = "Assets/_Project/Art/Materials";
        const string HouseScenePath = "Assets/_Project/Scenes/House.unity";

        /// <summary>In the greybox room the north wall sits at z = +10.</summary>
        static readonly Vector3 DoorPosition = new(0f, 0f, 9.6f);

        /// <summary>
        /// The three fastenings, in the order they appear on the door. Each wants
        /// a different tool, and only one tool fits in the player's hands, so
        /// clearing the door is three separate trips across the house.
        /// </summary>
        static readonly (string id, string tag, string verb, string hint, Color colour,
            Vector3 localPosition, Vector3 size)[] Stages =
        {
            ("Plank", "pry", "Pry off the plank", "It is nailed shut.",
                new Color(0.42f, 0.30f, 0.18f), new Vector3(0f, 1.35f, 0.1f), new Vector3(1.4f, 0.18f, 0.08f)),

            ("Cord", "cut", "Cut the alarm cord", "A cord runs into the frame.",
                new Color(0.20f, 0.55f, 0.65f), new Vector3(-0.5f, 0.95f, 0.1f), new Vector3(0.06f, 1.5f, 0.06f)),

            ("Padlock", "key.front", "Unlock the padlock", "It needs a key.",
                new Color(0.78f, 0.66f, 0.25f), new Vector3(0.42f, 1.0f, 0.12f), new Vector3(0.16f, 0.22f, 0.1f)),
        };

        [MenuItem("Granny/Build Escape Door", priority = 40)]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(HouseScenePath, OpenSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "FrontDoor")
                    Object.DestroyImmediate(root);

            // The greybox shipped a decorative slab where the door goes; the real
            // door replaces it rather than sitting inside it.
            foreach (var placeholder in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (placeholder != null && placeholder.name == "FrontDoor_Placeholder")
                    Object.DestroyImmediate(placeholder.gameObject);

            var root2 = new GameObject("FrontDoor");
            root2.transform.position = DoorPosition;
            root2.layer = GameLayers.Interactable;

            var frame = Box("Frame", root2.transform, new Vector3(0f, 1.1f, 0f), new Vector3(1.6f, 2.3f, 0.16f),
                EnsureMaterial("EscapeFrame", new Color(0.14f, 0.10f, 0.08f)));
            frame.layer = GameLayers.Interactable;

            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root2.transform, false);
            pivot.localPosition = new Vector3(-0.6f, 0f, 0f);

            var leafBox = Box("Leaf", pivot, new Vector3(0.6f, 1.05f, 0f), new Vector3(1.2f, 2.1f, 0.09f),
                EnsureMaterial("EscapeLeaf", new Color(0.30f, 0.19f, 0.11f)));
            leafBox.layer = GameLayers.Door;

            var hinge = root2.AddComponent<HingeDoor>();
            Wire(hinge, "leaf", pivot);

            foreach (var spec in Stages)
            {
                var holder = new GameObject($"Lock_{spec.id}");
                holder.transform.SetParent(root2.transform, false);
                holder.transform.localPosition = Vector3.zero;
                holder.SetActive(false);

                var visual = Box($"{spec.id}_Visual", holder.transform,
                    spec.localPosition, spec.size, EnsureMaterial($"Escape{spec.id}", spec.colour));
                visual.layer = GameLayers.Interactable;

                var stage = holder.AddComponent<LockStage>();

                var so = new SerializedObject(stage);
                so.FindProperty("requiredTag").stringValue = spec.tag;
                so.FindProperty("verb").stringValue = spec.verb;
                so.FindProperty("missingToolHint").stringValue = spec.hint;
                so.FindProperty("visual").objectReferenceValue = visual;
                so.ApplyModifiedPropertiesWithoutUndo();

                holder.SetActive(true);
            }

            var escape = root2.AddComponent<EscapeDoor>();
            Wire(escape, "leaf", hinge);

            // This builder runs after GrannyBuilder baked the NavMesh, and it has
            // just replaced a solid slab with a door frame. Rebake, or she walks
            // into geometry that no longer matches what she can path through.
            var surface = Object.FindAnyObjectByType<Unity.AI.Navigation.NavMeshSurface>();
            if (surface != null)
            {
                surface.BuildNavMesh();
                Debug.Log("[Escape] NavMesh rebaked around the new door");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Escape] front door built with three fastenings");
            Debug.Log("[Escape] done");
        }

        static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static Material EnsureMaterial(string name, Color colour)
        {
            var path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", colour);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void Wire(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);

            if (prop == null)
            {
                Debug.LogError($"[Escape] {target.GetType().Name} has no field '{fieldName}'");
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
