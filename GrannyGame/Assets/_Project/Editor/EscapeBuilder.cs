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

        /// <summary>
        /// The house's north wall runs along z = +10 and HouseBuilder leaves an
        /// opening at x = 0 for exactly this. The frame sits in the opening rather
        /// than in front of it, so there is no gap to see through.
        /// </summary>
        static readonly Vector3 DoorPosition = new(0f, 0f, 10f);

        /// <summary>
        /// The fastenings, in the order they are added. Each wants a different
        /// tool, and only one tool fits in the player's hands, so clearing the
        /// door is one trip across the house per fastening.
        ///
        /// The first three are always on the door. The rest are switched on by
        /// difficulty, which is what makes Extreme a longer night rather than
        /// only a faster one.
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

            // Normal and up.
            ("Combination", "combination", "Work the combination", "Four digits, and you do not know them.",
                new Color(0.55f, 0.55f, 0.58f), new Vector3(-0.42f, 1.35f, 0.12f), new Vector3(0.2f, 0.14f, 0.1f)),

            // Hard and up.
            ("Battery", "battery", "Fit the cell", "The keypad is dead.",
                new Color(0.25f, 0.45f, 0.30f), new Vector3(0.5f, 1.45f, 0.1f), new Vector3(0.22f, 0.12f, 0.08f)),

            // Extreme, and the extra-locks mode.
            ("Fusebox", "electrics", "Replace the fuse", "The lock has no power at all.",
                new Color(0.62f, 0.40f, 0.18f), new Vector3(-0.6f, 1.75f, 0.1f), new Vector3(0.26f, 0.26f, 0.09f)),
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

            var builtStages = new System.Collections.Generic.List<LockStage>();

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
                builtStages.Add(stage);
            }

            var escape = root2.AddComponent<EscapeDoor>();
            Wire(escape, "leaf", hinge);

            // Wired explicitly rather than left to the runtime fallback, so the
            // saved scene records which fastenings hold this door and the
            // relationship is visible in the inspector.
            var stagesSo = new SerializedObject(escape);
            var stagesProp = stagesSo.FindProperty("stages");
            stagesProp.arraySize = builtStages.Count;
            for (var i = 0; i < builtStages.Count; i++)
                stagesProp.GetArrayElementAtIndex(i).objectReferenceValue = builtStages[i];
            stagesSo.ApplyModifiedPropertiesWithoutUndo();

            // This builder runs after GrannyBuilder baked the NavMesh, and it has
            // just replaced a solid slab with a door frame. Rebake, or she walks
            // into geometry that no longer matches what she can path through.
            var surface = Object.FindAnyObjectByType<Unity.AI.Navigation.NavMeshSurface>();
            if (surface != null)
            {
                BuildKit.Bake(surface);
                Debug.Log("[Escape] NavMesh rebaked around the new door");
            }

            BuildKit.SaveScene(scene, "Escape");
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
