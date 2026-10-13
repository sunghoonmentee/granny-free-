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
                if (root.name is "FrontDoor" or "Truck")
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

            // The front door is the thing the whole game is pointed at. The player
            // walks up to it on the first night, finds six fastenings on it, and
            // comes back to it every night after; it is on screen more than any
            // other single object and it was a brown rectangle. Boards and strap
            // hinges, rather than the moulded panels of the interior doors — this
            // is the door somebody reinforced, not the one to the parlour.
            var plank = EnsureMaterial("EscapePlanking", new Color(0.26f, 0.17f, 0.10f));
            var iron = EnsureMaterial("EscapeIron", new Color(0.13f, 0.12f, 0.12f));

            for (var i = 0; i < 5; i++)
            {
                var board = Box($"Board_{i}", pivot,
                    new Vector3(0.6f + (i - 2) * 0.225f, 1.05f, -0.055f),
                    new Vector3(0.2f, 2.04f, 0.025f), plank);
                board.layer = GameLayers.Door;
            }

            foreach (var y in new[] { 0.35f, 1.75f })
            {
                var strap = Box($"Strap_{y:F2}", pivot, new Vector3(0.52f, y, -0.072f),
                    new Vector3(0.96f, 0.1f, 0.02f), iron);
                strap.layer = GameLayers.Door;
            }

            var ring = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ring.name = "Ring";
            ring.transform.SetParent(pivot, false);
            ring.transform.localPosition = new Vector3(1.08f, 1.05f, -0.075f);
            ring.transform.localScale = new Vector3(0.13f, 0.13f, 0.05f);
            ring.GetComponent<MeshRenderer>().sharedMaterial = iron;
            ring.layer = GameLayers.Door;
            Object.DestroyImmediate(ring.GetComponent<Collider>());

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

            BuildTruck();

            BuildKit.SaveScene(scene, "Escape");
            Debug.Log($"[Escape] front door built with {Stages.Length} fastenings, and a truck");
            Debug.Log("[Escape] done");
        }

        // ------------------------------------------------------------------
        // The second way out
        // ------------------------------------------------------------------

        /// <summary>In the garage, nose to the shutter.</summary>
        static readonly Vector3 TruckPosition = new(22f, -BuildKit.FloorHeight, -2f);

        /// <summary>
        /// What the truck wants before it will start, and the chain on the
        /// shutter in front of it.
        ///
        /// Five trips against the front door's three to six, and every one of
        /// them ends in the garage — which is across the house and down, through
        /// either a passage she walks or a crawl she cannot. The truck is the
        /// longer route that is safer at the end of it.
        /// </summary>
        static readonly (string id, string tag, string verb, string hint, Color colour,
            Vector3 localPosition, Vector3 size)[] TruckStages =
        {
            ("Chain", "cut", "Cut the shutter chain", "A chain holds the shutter down.",
                new Color(0.55f, 0.55f, 0.58f), new Vector3(0f, 1.2f, -3.2f), new Vector3(0.1f, 1.4f, 0.1f)),

            ("Plug", "sparkplug", "Fit the plug", "It will not even turn over.",
                new Color(0.80f, 0.78f, 0.70f), new Vector3(0f, 1.25f, 1.6f), new Vector3(0.12f, 0.2f, 0.12f)),

            ("Battery", "truckbattery", "Drop in the battery", "No battery under the bonnet.",
                new Color(0.22f, 0.42f, 0.28f), new Vector3(-0.6f, 1.2f, 1.5f), new Vector3(0.4f, 0.3f, 0.3f)),

            // Tucked against the chassis under the flatbed rather than hanging
            // off the side of it, where it read as a crate floating in mid-air.
            ("Fuel", "fuel", "Fill the tank", "The gauge is on empty.",
                new Color(0.70f, 0.45f, 0.15f), new Vector3(0.98f, 0.72f, -0.6f), new Vector3(0.2f, 0.28f, 0.5f)),

            ("Ignition", "truckkey", "Start the engine", "No key in it.",
                new Color(0.85f, 0.75f, 0.35f), new Vector3(-0.5f, 1.3f, -0.4f), new Vector3(0.1f, 0.12f, 0.1f)),
        };

        static void BuildTruck()
        {
            var root = new GameObject("Truck");
            root.transform.position = TruckPosition;
            root.layer = GameLayers.Interactable;

            var paint = EnsureMaterial("TruckPaint", new Color(0.32f, 0.26f, 0.22f));
            var glass = EnsureMaterial("TruckGlass", new Color(0.16f, 0.20f, 0.22f));

            // Not "Bed": the marker the player wakes at is called that, and
            // GameObject.Find would start handing the truck out instead.
            var flatbed = Box("Flatbed", root.transform, new Vector3(0f, 0.9f, -0.6f),
                new Vector3(2.2f, 1.0f, 4.4f), paint);
            flatbed.layer = GameLayers.Prop;

            var cab = Box("Cab", root.transform, new Vector3(0f, 1.9f, -0.3f), new Vector3(2.0f, 1.0f, 2.0f), paint);
            cab.layer = GameLayers.Prop;

            var bonnet = Box("Bonnet", root.transform, new Vector3(0f, 1.05f, 1.7f), new Vector3(2.0f, 0.7f, 1.6f), paint);
            bonnet.layer = GameLayers.Prop;

            // Everything below is what turns three boxes into a lorry.
            //
            // One of the two ways out of this house is this vehicle, and the
            // player spends five nights carrying parts to it. Until now it was a
            // stack of grey cubes in a dark garage — indistinguishable, in a
            // photograph, from the crates beside it. Wheels are most of the work:
            // nothing reads as a vehicle without them, and nothing reads as
            // anything else with them.
            var rubber = EnsureMaterial("TruckRubber", new Color(0.07f, 0.07f, 0.08f));
            var iron = EnsureMaterial("TruckIron", new Color(0.17f, 0.17f, 0.18f));
            var lamp = EnsureMaterial("TruckLamp", new Color(0.72f, 0.70f, 0.60f));

            foreach (var at in new[]
                     {
                         new Vector3(-1.02f, 0.44f, 1.45f), new Vector3(1.02f, 0.44f, 1.45f),
                         new Vector3(-1.02f, 0.44f, -1.55f), new Vector3(1.02f, 0.44f, -1.55f),
                     })
            {
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = $"Wheel_{at.x:F1}_{at.z:F1}";
                wheel.transform.SetParent(root.transform, false);
                wheel.transform.localPosition = at;

                // A cylinder stands up the Y axis and is two units tall at scale
                // one, so it is laid on its side and halved.
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.transform.localScale = new Vector3(0.88f, 0.15f, 0.88f);
                wheel.GetComponent<MeshRenderer>().sharedMaterial = rubber;
                wheel.layer = GameLayers.Prop;

                Object.DestroyImmediate(wheel.GetComponent<Collider>());
            }

            // A cab you can see into, which is also where the ignition is.
            Box("Windscreen", root.transform, new Vector3(0f, 2.02f, 0.66f),
                new Vector3(1.74f, 0.78f, 0.06f), glass).layer = GameLayers.Prop;
            Box("WindowL", root.transform, new Vector3(-1.0f, 2.0f, -0.35f),
                new Vector3(0.05f, 0.62f, 1.1f), glass).layer = GameLayers.Prop;
            Box("WindowR", root.transform, new Vector3(1.0f, 2.0f, -0.35f),
                new Vector3(0.05f, 0.62f, 1.1f), glass).layer = GameLayers.Prop;
            Box("Roof", root.transform, new Vector3(0f, 2.44f, -0.3f),
                new Vector3(2.06f, 0.1f, 2.06f), paint).layer = GameLayers.Prop;

            // Sideboards, so the flatbed is a flatbed rather than a crate.
            Box("SideL", root.transform, new Vector3(-1.06f, 1.62f, -0.6f),
                new Vector3(0.1f, 0.44f, 4.4f), paint).layer = GameLayers.Prop;
            Box("SideR", root.transform, new Vector3(1.06f, 1.62f, -0.6f),
                new Vector3(0.1f, 0.44f, 4.4f), paint).layer = GameLayers.Prop;
            Box("Tailgate", root.transform, new Vector3(0f, 1.62f, -2.76f),
                new Vector3(2.2f, 0.44f, 0.1f), paint).layer = GameLayers.Prop;

            // Bumper and headlamps. The lamps are not lit — a working headlight on
            // a truck that will not start is a lie about the puzzle.
            Box("Bumper", root.transform, new Vector3(0f, 0.74f, 2.56f),
                new Vector3(2.16f, 0.2f, 0.16f), iron).layer = GameLayers.Prop;

            foreach (var side in new[] { -0.72f, 0.72f })
            {
                var light = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                light.name = $"Headlamp_{side:F1}";
                light.transform.SetParent(root.transform, false);
                light.transform.localPosition = new Vector3(side, 1.16f, 2.48f);
                light.transform.localScale = new Vector3(0.28f, 0.28f, 0.16f);
                light.GetComponent<MeshRenderer>().sharedMaterial = lamp;
                light.layer = GameLayers.Prop;

                Object.DestroyImmediate(light.GetComponent<Collider>());
            }

            // Arches over the wheels, and a board to stand on between them.
            //
            // Without them the wheels read as four discs leaning against a crate:
            // a wheel only becomes part of a vehicle when something covers the top
            // of it. The running board does the same job along the side, which is
            // otherwise two and a half metres of flat panel.
            foreach (var at in new[]
                     {
                         new Vector3(-1.02f, 0.92f, 1.45f), new Vector3(1.02f, 0.92f, 1.45f),
                         new Vector3(-1.02f, 0.92f, -1.55f), new Vector3(1.02f, 0.92f, -1.55f),
                     })
                Box($"Arch_{at.x:F1}_{at.z:F1}", root.transform, at,
                    new Vector3(0.34f, 0.12f, 1.06f), paint).layer = GameLayers.Prop;

            foreach (var side in new[] { -1.06f, 1.06f })
                Box($"Step_{side:F1}", root.transform, new Vector3(side, 0.62f, 0f),
                    new Vector3(0.3f, 0.07f, 1.9f), iron).layer = GameLayers.Prop;

            // A radiator, because the front of the truck is the face of it and it
            // was a blank panel with two lamps either side.
            Box("Radiator", root.transform, new Vector3(0f, 1.18f, 2.52f),
                new Vector3(1.1f, 0.74f, 0.1f), iron).layer = GameLayers.Prop;

            for (var slat = 0; slat < 7; slat++)
                Box($"Slat_{slat}", root.transform,
                    new Vector3(-0.42f + slat * 0.14f, 1.18f, 2.58f),
                    new Vector3(0.05f, 0.62f, 0.04f), lamp).layer = GameLayers.Prop;

            // A hub on each wheel, so a wheel is a wheel rather than a hole in the
            // picture. Pale on near-black is the only thing that reads at the light
            // level of that garage.
            foreach (var at in new[]
                     {
                         new Vector3(-1.17f, 0.44f, 1.45f), new Vector3(1.17f, 0.44f, 1.45f),
                         new Vector3(-1.17f, 0.44f, -1.55f), new Vector3(1.17f, 0.44f, -1.55f),
                     })
            {
                var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hub.name = $"Hub_{at.x:F1}_{at.z:F1}";
                hub.transform.SetParent(root.transform, false);
                hub.transform.localPosition = at;
                hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                hub.transform.localScale = new Vector3(0.36f, 0.02f, 0.36f);
                hub.GetComponent<MeshRenderer>().sharedMaterial = iron;
                hub.layer = GameLayers.Prop;

                Object.DestroyImmediate(hub.GetComponent<Collider>());
            }

            // An exhaust up the back of the cab, the way a working lorry has one.
            var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pipe.name = "Exhaust";
            pipe.transform.SetParent(root.transform, false);
            pipe.transform.localPosition = new Vector3(1.0f, 1.9f, -1.28f);
            pipe.transform.localScale = new Vector3(0.11f, 0.78f, 0.11f);
            pipe.GetComponent<MeshRenderer>().sharedMaterial = iron;
            pipe.layer = GameLayers.Prop;
            Object.DestroyImmediate(pipe.GetComponent<Collider>());

            // The shutter the truck has to go out through. It stays shut; cutting
            // the chain is what the player does about it.
            var shutter = Box("Shutter", root.transform, new Vector3(0f, 1.5f, -3.4f),
                new Vector3(4.6f, 3.0f, 0.12f), EnsureMaterial("TruckShutter", new Color(0.38f, 0.38f, 0.40f)));
            shutter.layer = GameLayers.LevelGeometry;

            var stages = new System.Collections.Generic.List<LockStage>();

            foreach (var spec in TruckStages)
            {
                var holder = new GameObject($"Truck_{spec.id}");
                holder.transform.SetParent(root.transform, false);
                holder.SetActive(false);

                var visual = Box($"{spec.id}_Visual", holder.transform,
                    spec.localPosition, spec.size, EnsureMaterial($"Truck{spec.id}", spec.colour));
                visual.layer = GameLayers.Interactable;

                var stage = holder.AddComponent<LockStage>();

                var so = new SerializedObject(stage);
                so.FindProperty("requiredTag").stringValue = spec.tag;
                so.FindProperty("verb").stringValue = spec.verb;
                so.FindProperty("missingToolHint").stringValue = spec.hint;
                so.FindProperty("visual").objectReferenceValue = visual;
                so.ApplyModifiedPropertiesWithoutUndo();

                holder.SetActive(true);
                stages.Add(stage);
            }

            var escape = root.AddComponent<EscapeDoor>();

            var escapeSo = new SerializedObject(escape);

            // The truck is five parts whatever the setting. Difficulty lengthens
            // the front door instead, so the two routes diverge as it rises.
            escapeSo.FindProperty("scaledByDifficulty").boolValue = false;

            var stagesProp = escapeSo.FindProperty("stages");
            stagesProp.arraySize = stages.Count;
            for (var i = 0; i < stages.Count; i++)
                stagesProp.GetArrayElementAtIndex(i).objectReferenceValue = stages[i];
            escapeSo.ApplyModifiedPropertiesWithoutUndo();
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

            // Flat materials come out of URP at half smoothness, which on a dark
            // surface is a mirror: every unlit black panel in the house caught the
            // nearest bulb and wore a smeared orange blob the size of a hand. The
            // front door looked like somebody had lit a fire on it.
            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_Metallic", 0f);
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
