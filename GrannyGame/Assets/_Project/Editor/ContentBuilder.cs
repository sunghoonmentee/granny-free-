using System.Collections.Generic;
using System.IO;
using Granny.Core;
using Granny.Gameplay.AI;
using Granny.Gameplay.Interaction;
using Granny.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Granny.EditorTools
{
    /// <summary>
    /// Creates the placeholder content the interaction systems need to be playable:
    /// item definitions and their world prefabs, furniture prefabs, the HUD canvas,
    /// and an arrangement of all of it in the House scene.
    ///
    /// Idempotent — re-running updates assets in place rather than duplicating.
    ///
    ///     Unity.exe -batchmode -quit -executeMethod \
    ///       Granny.EditorTools.ContentBuilder.Run
    /// </summary>
    public static class ContentBuilder
    {
        const string DataDir = "Assets/_Project/Data/Items";
        const string PrefabDir = "Assets/_Project/Prefabs";
        const string ItemPrefabDir = PrefabDir + "/Items";
        const string PropPrefabDir = PrefabDir + "/Props";
        const string MaterialDir = "Assets/_Project/Art/Materials";
        const string HouseScenePath = "Assets/_Project/Scenes/House.unity";

        [MenuItem("Granny/Build Content", priority = 20)]
        public static void Run()
        {
            EnsureFolders();

            ModelSkin.ConfigureImports();
            BuildItems();

            // Written to disk before the furniture and the level reference them,
            // for the same reason GrannyBuilder flushes its difficulty profiles.
            AssetDatabase.SaveAssets();

            BuildFurniture();
            var hud = BuildHud();

            InstallHud(hud);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Content] done");
        }

        static void EnsureFolders()
        {
            foreach (var folder in new[] { DataDir, ItemPrefabDir, PropPrefabDir })
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                var parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(parent)!.Replace('\\', '/'), Path.GetFileName(parent));
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
        }

        // ------------------------------------------------------------------
        // Items
        // ------------------------------------------------------------------

        /// <summary>
        /// The starting item table. Tags rather than ids are what locks match on,
        /// so a second prying tool can be added later without touching any door.
        /// </summary>
        /// <summary>
        /// The last two columns are a downloaded model and, where the model holds
        /// more than one object, which part of it to keep. The colour and size
        /// before them are not dead weight: they are what the item looks like when
        /// no model has been downloaded, and the box the world still collides
        /// with either way.
        /// </summary>
        static readonly (string id, string display, string[] tags,
            Color colour, Vector3 size, float loudness, bool breakable,
            string model, string part)[] ItemTable =
        {
            ("hammer", "Hammer", new[] { "pry" },
                new Color(0.55f, 0.42f, 0.28f), new Vector3(0.08f, 0.30f, 0.08f), 1.2f, false, "cross_pein_hammer", null),

            ("wirecutters", "Wirecutters", new[] { "cut" },
                new Color(0.75f, 0.15f, 0.15f), new Vector3(0.07f, 0.22f, 0.05f), 0.9f, false, "pliers", null),

            // A key takes the hand now, exactly like a hammer does.
            ("key.front", "Rusty Key", new[] { "key.front", "unlock" },
                new Color(0.85f, 0.72f, 0.30f), new Vector3(0.04f, 0.10f, 0.02f), 0.4f, false, null, null),

            ("bottle", "Glass Bottle", new[] { "throwable" },
                new Color(0.35f, 0.62f, 0.45f), new Vector3(0.09f, 0.24f, 0.09f), 1.8f, true, "wine_bottles_01", "burgundy"),

            ("jar", "Preserve Jar", new[] { "throwable" },
                new Color(0.55f, 0.48f, 0.22f), new Vector3(0.13f, 0.20f, 0.13f), 1.6f, true, null, null),

            // The crossbow, in three trips. Each part takes the hand like
            // anything else, which is what makes building it cost a night.
            ("bow.stock", "Carved Stock", new[] { "bow_stock" },
                new Color(0.42f, 0.30f, 0.18f), new Vector3(0.09f, 0.46f, 0.07f), 1.0f, false, null, null),

            ("bow.limb", "Steel Limb", new[] { "bow_limb" },
                new Color(0.45f, 0.46f, 0.50f), new Vector3(0.52f, 0.05f, 0.05f), 1.1f, false, null, null),

            ("bow.cord", "Waxed Cord", new[] { "bow_cord" },
                new Color(0.72f, 0.66f, 0.46f), new Vector3(0.10f, 0.10f, 0.10f), 0.3f, false, null, null),

            ("crossbow", "Crossbow", new[] { "weapon" },
                new Color(0.38f, 0.30f, 0.22f), new Vector3(0.44f, 0.12f, 0.50f), 1.4f, false, null, null),

            // Darts are the exception to the one-hand rule: they go in a pocket,
            // and they can be picked up again from wherever they landed.
            ("dart", "Tranquilliser Dart", new[] { "dart" },
                new Color(0.20f, 0.55f, 0.62f), new Vector3(0.03f, 0.03f, 0.26f), 0.2f, false, null, null),

            // The locks the harder settings add. A note rather than a number
            // typed on a keypad: the combination is a thing you have to carry to
            // the door, which costs a trip like everything else.
            ("note", "Torn Note", new[] { "combination" },
                new Color(0.86f, 0.82f, 0.68f), new Vector3(0.14f, 0.01f, 0.18f), 0.1f, false, null, null),

            // The safe's four digits, written down because she cannot hold them
            // in her head any more. A number you carry costs a trip across the
            // house; a number on the screen costs nothing after the first night.
            ("page", "Torn Page", new[] { "safecode" },
                new Color(0.80f, 0.76f, 0.63f), new Vector3(0.12f, 0.01f, 0.16f), 0.1f, false, null, null),

            ("battery", "Dry Cell", new[] { "battery" },
                new Color(0.30f, 0.50f, 0.34f), new Vector3(0.07f, 0.14f, 0.07f), 0.5f, false, null, null),

            ("fuse", "Ceramic Fuse", new[] { "electrics" },
                new Color(0.72f, 0.55f, 0.28f), new Vector3(0.05f, 0.05f, 0.14f), 0.3f, false, null, null),

            // The truck. Five parts, all of which have to end up in the garage.
            ("sparkplug", "Spark Plug", new[] { "sparkplug" },
                new Color(0.78f, 0.76f, 0.70f), new Vector3(0.04f, 0.14f, 0.04f), 0.4f, false, null, null),

            ("truckbattery", "Truck Battery", new[] { "truckbattery" },
                new Color(0.20f, 0.40f, 0.26f), new Vector3(0.26f, 0.20f, 0.18f), 1.3f, false, null, null),

            ("fuel", "Jerry Can", new[] { "fuel" },
                new Color(0.68f, 0.42f, 0.14f), new Vector3(0.20f, 0.32f, 0.14f), 1.1f, false, null, null),

            ("truckkey", "Truck Key", new[] { "truckkey" },
                new Color(0.82f, 0.72f, 0.32f), new Vector3(0.04f, 0.09f, 0.02f), 0.3f, false, null, null),
        };

        static List<ItemDefinition> BuildItems()
        {
            var built = new List<ItemDefinition>();

            foreach (var entry in ItemTable)
            {
                var assetName = $"Item_{entry.id.Replace('.', '_')}";
                var assetPath = $"{DataDir}/{assetName}.asset";

                var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<ItemDefinition>();
                    AssetDatabase.CreateAsset(definition, assetPath);
                }

                var prefab = BuildItemPrefab(entry.id, entry.colour, entry.size, definition,
                    entry.breakable, entry.model, entry.part);

                var so = new SerializedObject(definition);
                so.FindProperty("id").stringValue = entry.id;
                so.FindProperty("displayName").stringValue = entry.display;
                so.FindProperty("worldPrefab").objectReferenceValue = prefab;
                so.FindProperty("impactLoudness").floatValue = entry.loudness;

                var tags = so.FindProperty("tags");
                tags.arraySize = entry.tags.Length;
                for (var i = 0; i < entry.tags.Length; i++)
                    tags.GetArrayElementAtIndex(i).stringValue = entry.tags[i];

                so.ApplyModifiedPropertiesWithoutUndo();
                built.Add(definition);
            }

            return built;
        }

        /// <summary>
        /// One item, as it exists on the floor and in the hand.
        ///
        /// The root is empty rather than being the cube itself. A cube squashed to
        /// 0.08 x 0.30 x 0.08 is a hammer-shaped hole to put a hammer in, and
        /// anything parented to it comes out squashed to match — which is exactly
        /// what happened the first time a model went in.
        /// </summary>
        static GameObject BuildItemPrefab(string id, Color colour, Vector3 size,
            ItemDefinition definition, bool breakable, string model, string part)
        {
            var path = $"{ItemPrefabDir}/{id.Replace('.', '_')}.prefab";
            var root = new GameObject(id) { layer = GameLayers.Pickup };

            var shape = Box("Shape", root.transform, Vector3.zero, size,
                EnsureMaterial($"Item_{id.Replace('.', '_')}", colour));
            shape.layer = GameLayers.Pickup;
            Object.DestroyImmediate(shape.GetComponent<Collider>());

            var box = root.AddComponent<BoxCollider>();
            box.size = size;

            if (model != null && AddModel(root, model, GameLayers.Pickup, standOnFloor: false, part: part))
            {
                shape.GetComponent<MeshRenderer>().enabled = false;

                // The box follows the thing that is actually there now. An item
                // whose collider is bigger than it looks will not go through the
                // gap the player can plainly see.
                var visual = root.transform.Find("Model");
                var drawn = visual != null
                    ? visual.GetComponentsInChildren<Renderer>()
                    : System.Array.Empty<Renderer>();
                var bounds = new Bounds(root.transform.position, Vector3.zero);
                var found = false;

                foreach (var renderer in drawn)
                {
                    if (!renderer.enabled) continue;
                    if (!found) { bounds = renderer.bounds; found = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }

                if (found)
                {
                    box.center = bounds.center - root.transform.position;
                    box.size = bounds.size;
                }
            }

            var body = root.AddComponent<Rigidbody>();
            body.mass = 1.2f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var pickup = root.AddComponent<PickupItem>();
            Wire(pickup, "definition", definition);

            if (breakable) root.AddComponent<BreakableItem>();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out var ok);
            Object.DestroyImmediate(root);

            if (!ok) Debug.LogError($"[Content] Failed to save item prefab {path}");
            return saved;
        }

        // ------------------------------------------------------------------
        // Furniture
        // ------------------------------------------------------------------

        public struct Furniture
        {
            public GameObject Drawer;
            public GameObject Door;
            public GameObject Wardrobe;
            public GameObject BearTrap;
            public GameObject Bed;
            public GameObject Window;
            public GameObject Crow;
            public GameObject Rug;
            public GameObject Safe;
        }

        static Furniture BuildFurniture() => new()
        {
            Drawer = BuildDrawerPrefab(),
            Door = BuildDoorPrefab(),
            Wardrobe = BuildWardrobePrefab(),
            BearTrap = BuildBearTrapPrefab(),
            Bed = BuildBedPrefab(),
            Window = BuildWindowPrefab(),
            Crow = BuildCrowPrefab(),
            Rug = BuildRugPrefab(),
            Safe = BuildSafePrefab(),
        };

        /// <summary>
        /// A bed with a gap under it.
        ///
        /// The frame is a solid prop and the gap is the hiding place, so the
        /// player goes flat rather than standing inside the furniture. It is the
        /// worse of the two places to hide — she only has to kneel — which is
        /// what makes choosing the wardrobe mean something.
        /// </summary>
        static GameObject BuildBedPrefab()
        {
            var path = $"{PropPrefabDir}/Bed.prefab";
            var frame = EnsureMaterial("PropBedFrame", new Color(0.21f, 0.15f, 0.11f));
            var sheet = EnsureMaterial("PropBedSheet", new Color(0.30f, 0.28f, 0.25f));

            var root = new GameObject("Bed");
            root.layer = GameLayers.HidingSpot;

            var legs = Box("Frame", root.transform, new Vector3(0f, 0.22f, 0f), new Vector3(1.3f, 0.12f, 2.1f), frame);
            legs.layer = GameLayers.Prop;

            var mattress = Box("Mattress", root.transform, new Vector3(0f, 0.44f, 0f), new Vector3(1.26f, 0.32f, 2.04f), sheet);
            mattress.layer = GameLayers.HidingSpot;

            var head = Box("Headboard", root.transform, new Vector3(0f, 0.65f, -1.05f), new Vector3(1.3f, 0.9f, 0.1f), frame);
            head.layer = GameLayers.Prop;

            if (AddModel(root, "old_bed_frame", GameLayers.HidingSpot))
            {
                legs.GetComponent<MeshRenderer>().enabled = false;
                head.GetComponent<MeshRenderer>().enabled = false;

                // The mattress stays, because the model is a bare sprung frame
                // and the gap underneath has to have something over it or going
                // under the bed is going under a table. But it has to fit the
                // frame: built to the old box bed it overhung on every side and
                // read as a slab balanced on a trolley.
                var bedding = mattress.transform;
                bedding.localPosition = new Vector3(0f, 0.45f, 0f);
                bedding.localScale = new Vector3(0.84f, 0.16f, 1.92f);
            }

            // Flat on the floor under the frame, looking out along the room.
            var viewpoint = new GameObject("Viewpoint").transform;
            viewpoint.SetParent(root.transform, false);
            viewpoint.localPosition = new Vector3(0f, -0.75f, 0f);

            var exit = new GameObject("ExitPoint").transform;
            exit.SetParent(root.transform, false);
            exit.localPosition = new Vector3(1.1f, 0f, 0f);
            exit.localRotation = Quaternion.Euler(0f, 90f, 0f);

            var spot = root.AddComponent<HidingSpot>();
            Wire(spot, "viewpoint", viewpoint);
            Wire(spot, "exitPoint", exit);
            SetEnum(spot, "style", (int)HideStyle.Crawl);

            return SavePrefab(root, path);
        }

        /// <summary>
        /// The trap she leaves behind. Low and dark on purpose: it is meant to be
        /// missable at a walk and obvious if you are looking at the floor, which
        /// is the trade the player makes every time they hurry.
        /// </summary>
        static GameObject BuildBearTrapPrefab()
        {
            var path = $"{PropPrefabDir}/BearTrap.prefab";
            var iron = EnsureMaterial("PropTrapIron", new Color(0.20f, 0.19f, 0.18f));
            var sprung = EnsureMaterial("PropTrapSprung", new Color(0.30f, 0.26f, 0.22f));

            var root = new GameObject("BearTrap");
            root.layer = GameLayers.Prop;

            var armed = new GameObject("Armed");
            armed.transform.SetParent(root.transform, false);
            Box("Jaws", armed.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.62f, 0.12f, 0.62f), iron);
            Box("JawL", armed.transform, new Vector3(-0.30f, 0.20f, 0f), new Vector3(0.06f, 0.30f, 0.60f), iron);
            Box("JawR", armed.transform, new Vector3(0.30f, 0.20f, 0f), new Vector3(0.06f, 0.30f, 0.60f), iron);

            var closed = new GameObject("Sprung");
            closed.transform.SetParent(root.transform, false);
            Box("Shut", closed.transform, new Vector3(0f, 0.09f, 0f), new Vector3(0.62f, 0.18f, 0.28f), sprung);
            closed.SetActive(false);

            // The bite area, not the metal: a trigger, so walking into one is
            // caught by the trap rather than blocked by it.
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.25f, 0f);
            trigger.size = new Vector3(0.75f, 0.5f, 0.75f);

            var trap = root.AddComponent<BearTrap>();
            Wire(trap, "armedVisual", armed);
            Wire(trap, "sprungVisual", closed);

            return SavePrefab(root, path);
        }

        /// <summary>
        /// A chest of drawers: the house's one container, and the reason searching
        /// takes time.
        ///
        /// The boxes are built first and the downloaded chest goes in on top of
        /// them, because what the player collides with and what blocks her line of
        /// sight should not change shape because an artist shipped a new mesh.
        /// Only one drawer of the six actually pulls out, which is not a shortcut:
        /// the whole chest answers the interaction ray, so pressing the button
        /// anywhere on it opens the drawer, exactly as it reads in play.
        /// </summary>
        static GameObject BuildDrawerPrefab()
        {
            var path = $"{PropPrefabDir}/Dresser.prefab";
            var wood = EnsureMaterial("PropWood", new Color(0.30f, 0.21f, 0.14f));
            var handle = EnsureMaterial("PropHandle", new Color(0.62f, 0.58f, 0.46f));

            var root = new GameObject("Dresser");
            root.layer = GameLayers.Interactable;

            var carcass = Box("Carcass", root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(1.1f, 0.9f, 0.55f), wood);
            carcass.layer = GameLayers.Prop;

            // Both the drawer face and its handle have to sit on Interactable, or
            // the interaction raycast - which only tests that mask - looks straight
            // through them at the wall behind.
            var front = Box("DrawerFront", root.transform, new Vector3(0f, 0.55f, 0.28f), new Vector3(0.95f, 0.28f, 0.06f), wood);
            front.layer = GameLayers.Interactable;

            var grip = Box("Handle", front.transform, new Vector3(0f, 0f, 0.6f), new Vector3(0.35f, 0.2f, 1.2f), handle);
            grip.layer = GameLayers.Interactable;

            var anchor = new GameObject("ContentsAnchor").transform;
            anchor.SetParent(front.transform, false);
            anchor.localPosition = new Vector3(0f, 0f, 2.5f);

            var drawer = root.AddComponent<Drawer>();
            var sliding = front.transform;
            var pull = 0.42f;

            // A commode rather than the small six-drawer chest that went in
            // first. That one measured 0.86 by 0.55 — a real piece of furniture,
            // but against a three-metre wall, with twenty-two more of it around
            // the house, it read as a doll's chest. This one is 1.2 m across and
            // 1.21 m high, the height a hand reaches for, and it matches the
            // wardrobe it will usually be standing near.
            if (AddModel(root, "GothicCommode_01", GameLayers.Prop))
            {
                // The top drawer of the three: the one at the eye line, and the
                // one a hand goes to first.
                var pulled = FindPart(root.transform.Find("Model"), "door01");

                if (pulled != null)
                {
                    // The carcass keeps both its jobs and loses only its face. It
                    // is refitted to the chest that is now standing there rather
                    // than the taller box that used to be, or the player collides
                    // with a foot of air above it.
                    carcass.GetComponent<MeshRenderer>().enabled = false;
                    Refit(carcass, new Vector3(0f, 0.61f, 0f), new Vector3(1.2f, 1.21f, 0.58f));

                    // The front box has nothing left to do. The model's drawer is a
                    // whole drawer rather than a face, so it is the thing that
                    // slides, and leaving the box behind would put five invisible
                    // centimetres of wall in front of a chest that no longer
                    // reaches that far.
                    Object.DestroyImmediate(front);

                    sliding = pulled;
                    pull = 0.3f;

                    anchor = new GameObject("ContentsAnchor").transform;
                    anchor.SetParent(root.transform, false);
                    anchor.position = pulled.GetComponent<Renderer>().bounds.center
                                      + new Vector3(0f, 0.08f, pull + 0.05f);
                }
            }

            Wire(drawer, "sliding", sliding);
            Wire(drawer, "contentsAnchor", anchor);

            var so = new SerializedObject(drawer);

            // The axis is local to whatever the sliding part hangs off. For the
            // boxes that is the prop and forward is forward; for a part of an
            // imported model it is the model's own frame, which carries the axis
            // conversion Unity baked in, and +Z in there points somewhere else.
            var axis = sliding.parent.InverseTransformDirection(root.transform.forward).normalized;
            so.FindProperty("slideAxis").vector3Value = axis;

            // And a local unit in that frame is not necessarily a metre — the
            // importer is free to bake a unit conversion in as well. Measuring how
            // far one unit actually travels keeps the pull a distance in the world.
            var perUnit = sliding.parent.TransformVector(axis).magnitude;
            so.FindProperty("slideDistance").floatValue = perUnit > 0.0001f ? pull / perUnit : pull;

            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, path);
        }

        static GameObject BuildDoorPrefab()
        {
            var path = $"{PropPrefabDir}/Door.prefab";
            var wood = EnsureMaterial("PropDoor", new Color(0.26f, 0.18f, 0.12f));

            var root = new GameObject("Door");
            root.layer = GameLayers.Door;

            // Two offsets, and both matter:
            //
            //   root   sits at the CENTRE of the doorway, because that is what the
            //          wall builder reports and what the placement code has to
            //          line up with. The old prefab had its root on the hinge
            //          edge, so every door hung half a leaf out of its opening.
            //   pivot  sits at the hinge edge, so rotating it swings the door
            //          about its hinge rather than about its middle.
            //
            // The leaf spans the prefab's own local X. A door is therefore turned
            // a quarter turn from the wall it sits in - see BuildKit.Doorway.
            var width = BuildKit.DoorWidth - 0.08f;
            var height = BuildKit.DoorHeight - 0.1f;

            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(-width * 0.5f, 0f, 0f);

            var leaf = Box("Leaf", pivot, new Vector3(width * 0.5f, height * 0.5f, 0f),
                new Vector3(width, height, 0.06f), wood);
            leaf.layer = GameLayers.Door;

            // Four raised panels and a knob.
            //
            // Sixteen of these hang in the house and until now every one was a
            // brown rectangle, which at a distance is indistinguishable from a
            // brown rectangle painted on the wall. Panels cost four boxes and
            // give a door a near edge and a far edge, so it reads as a door
            // standing in a frame rather than as a hole with a colour in it — and
            // a knob tells you which side it opens from before you reach it.
            var panel = EnsureMaterial("PropDoorPanel", new Color(0.21f, 0.145f, 0.10f));

            for (var column = 0; column < 2; column++)
            for (var row = 0; row < 2; row++)
            {
                var x = width * (0.5f + (column == 0 ? -0.21f : 0.21f));
                var y = height * (0.5f + (row == 0 ? -0.22f : 0.22f));

                // Proud of the leaf on both faces: a panel on the hall side only
                // is a door that is a door from one direction.
                for (var face = -1; face <= 1; face += 2)
                    Box($"Panel_{column}{row}{(face < 0 ? "A" : "B")}", pivot,
                        new Vector3(x, y, face * 0.036f),
                        new Vector3(width * 0.33f, height * 0.33f, 0.014f), panel)
                        .layer = GameLayers.Door;
            }

            var brass = EnsureMaterial("PropHandle", new Color(0.62f, 0.58f, 0.46f));

            for (var face = -1; face <= 1; face += 2)
            {
                var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                knob.name = face < 0 ? "KnobA" : "KnobB";
                knob.transform.SetParent(pivot, false);
                knob.transform.localPosition = new Vector3(width - 0.09f, height * 0.47f, face * 0.05f);
                knob.transform.localScale = new Vector3(0.07f, 0.07f, 0.05f);
                knob.GetComponent<MeshRenderer>().sharedMaterial = brass;
                knob.layer = GameLayers.Door;

                Object.DestroyImmediate(knob.GetComponent<Collider>());
            }

            var door = root.AddComponent<HingeDoor>();
            Wire(door, "leaf", pivot);

            return SavePrefab(root, path);
        }

        /// <summary>
        /// A window with planks nailed over it.
        ///
        /// The house had none at all, and a house with no windows is not a
        /// frightening house, it is a bunker. Boarded is the right answer twice
        /// over: it is what somebody does to a house they are keeping people
        /// inside, and it means there is no outside to build. Behind the planks
        /// is a black panel, and black is exactly as convincing as a night sky
        /// would be at a fraction of the cost.
        ///
        /// No hole is cut in the wall for it. A window that is boarded and black
        /// looks identical whether or not there is anything behind it, and
        /// cutting openings in the perimeter would mean splitting every exterior
        /// wall around them — a lot of geometry, and a lot of new seams, for a
        /// difference nobody can see.
        ///
        /// Faces local +Z, like the door leaf spans local X.
        /// </summary>
        /// <summary>
        /// A crow on a perch.
        ///
        /// Built out of boxes like everything else here, and the shape is doing
        /// one job only: at the distance and darkness it is seen from, a black
        /// wedge with a beak on it and two wings that move is unmistakably a bird,
        /// and that is all it has to be. The beak and the tail point opposite
        /// ways, which is what keeps it from reading as a lump.
        /// </summary>
        /// <summary>
        /// A worn rug.
        ///
        /// Floors are the largest surface in the house and the one the player
        /// looks at most, because in a dark house you walk looking down. Bare
        /// boards wall to wall in every room is most of why the place still reads
        /// as rooms rather than as somebody's house.
        ///
        /// The pattern is drawn rather than built out of boxes. The first version
        /// was a field, four border strips and a medallion, all flat colours, and
        /// under a warm bulb every one of them came out the same brown: what it
        /// looked like from ten metres was a sheet of card dropped on the floor.
        /// A rug is a pattern, and a pattern is a texture.
        /// </summary>
        /// <summary>
        /// A safe in the wall.
        ///
        /// The design doc has had this on its list since the beginning and it is
        /// the last of the original's shapes missing from the house: a container
        /// that wants something other than a free hand. What it adds is a chain —
        /// find the page, carry it to the safe, carry what is in the safe to the
        /// door — and a chain is three trips rather than one, which is three
        /// chances for her to be in the way.
        ///
        /// Built on the same component as every chest of drawers, with its lock
        /// filled in. A safe that is its own kind of object is a safe the spawn
        /// table does not know about, and a container the spawn table does not
        /// know about is a container with nothing in it.
        /// </summary>
        static GameObject BuildSafePrefab()
        {
            var path = $"{PropPrefabDir}/Safe.prefab";

            var iron = EnsureMaterial("PropSafeIron", new Color(0.115f, 0.115f, 0.125f));
            var brass = EnsureMaterial("PropHandle", new Color(0.62f, 0.58f, 0.46f));

            var root = new GameObject("Safe");
            root.layer = GameLayers.Interactable;

            // The carcass is the hole in the wall; the door is what swings. Both
            // on Interactable, so the ray finds the safe from anywhere on it.
            var body = Box("Body", root.transform, new Vector3(0f, 0.3f, -0.08f),
                new Vector3(0.62f, 0.6f, 0.44f), iron);
            body.layer = GameLayers.Interactable;

            // Hung on a pivot at its own left edge, so it turns about the hinge
            // rather than about its middle — the same arrangement every door in
            // the house uses, and for the same reason.
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(root.transform, false);
            hinge.localPosition = new Vector3(-0.3f, 0.3f, 0.15f);

            var door = Box("Door", hinge, new Vector3(0.3f, 0f, 0f),
                new Vector3(0.6f, 0.58f, 0.06f), iron);
            door.layer = GameLayers.Interactable;

            // The dial, and a handle beside it.
            var dial = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dial.name = "Dial";
            dial.transform.SetParent(door.transform, false);
            dial.transform.localPosition = new Vector3(-0.18f, 0f, 0.6f);
            dial.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            dial.transform.localScale = new Vector3(0.22f, 0.3f, 0.22f);
            dial.GetComponent<MeshRenderer>().sharedMaterial = brass;
            dial.layer = GameLayers.Interactable;
            Object.DestroyImmediate(dial.GetComponent<Collider>());

            var lever = Box("Lever", door.transform, new Vector3(0.28f, 0f, 0.5f),
                new Vector3(0.1f, 0.5f, 0.6f), brass);
            lever.layer = GameLayers.Interactable;

            var anchor = new GameObject("ContentsAnchor").transform;
            anchor.SetParent(root.transform, false);
            anchor.localPosition = new Vector3(0f, 0.34f, 0.42f);

            var drawer = root.AddComponent<Drawer>();
            Wire(drawer, "sliding", hinge);
            Wire(drawer, "contentsAnchor", anchor);

            var so = new SerializedObject(drawer);
            so.FindProperty("swingDegrees").floatValue = -104f;
            so.FindProperty("slideDistance").floatValue = 0f;
            so.FindProperty("requiredTag").stringValue = "safecode";
            so.FindProperty("missingHint").stringValue =
                "A dial, and four digits you do not know.";
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, path);
        }

        static GameObject BuildRugPrefab()
        {
            var path = $"{PropPrefabDir}/Rug.prefab";

            var cloth = EnsureMaterial("PropRugField", Color.white);
            cloth.SetTexture("_BaseMap", RugTexture());
            cloth.SetColor("_BaseColor", new Color(0.62f, 0.56f, 0.5f));
            EditorUtility.SetDirty(cloth);

            var fringe = EnsureMaterial("PropRugFringe", new Color(0.3f, 0.26f, 0.2f));

            const float width = 1.75f;
            const float length = 2.7f;

            var root = new GameObject("Rug");
            root.layer = GameLayers.Prop;

            Box("Field", root.transform, new Vector3(0f, 0.008f, 0f),
                new Vector3(width, 0.016f, length), cloth);

            Box("FringeS", root.transform, new Vector3(0f, 0.005f, -length * 0.5f - 0.05f),
                new Vector3(width, 0.01f, 0.1f), fringe);
            Box("FringeN", root.transform, new Vector3(0f, 0.005f, length * 0.5f + 0.05f),
                new Vector3(width, 0.01f, 0.1f), fringe);

            // Nothing underfoot should be felt, only seen. A rug with a collider
            // is a one-centimetre step the player trips over in the dark.
            foreach (var collider in root.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(collider);

            foreach (var child in root.GetComponentsInChildren<Transform>())
                child.gameObject.layer = GameLayers.Prop;

            return SavePrefab(root, path);
        }

        /// <summary>
        /// Draws the rug: a border, a medallion, and a path worn down the middle
        /// of it by thirty years of the same feet.
        ///
        /// The wear is the part that matters. A clean symmetrical pattern reads as
        /// new carpet however dark it is painted; a pattern that has been walked
        /// through reads as a house somebody has lived in for a long time, which
        /// is the one thing this floor has to say.
        /// </summary>
        static Texture2D RugTexture()
        {
            const string path = "Assets/_Project/Art/Textures/Rug.png";
            const int size = 512;

            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            var deep = new Color(0.30f, 0.10f, 0.085f);
            var band = new Color(0.46f, 0.36f, 0.19f);
            var line = new Color(0.12f, 0.05f, 0.05f);
            var figure = new Color(0.40f, 0.30f, 0.17f);

            var texture = new Texture2D(size, size, TextureFormat.RGB24, true);
            var pixels = new Color[size * size];
            var random = new System.Random(3307);

            // A little value noise, so the weave is not perfectly even.
            var grain = new float[size * size];
            for (var i = 0; i < grain.Length; i++) grain[i] = (float)random.NextDouble();

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var u = x / (float)(size - 1);
                var v = y / (float)(size - 1);

                var edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                var colour = deep;

                if (edge < 0.035f) colour = line;
                else if (edge < 0.085f) colour = band;
                else if (edge < 0.1f) colour = line;
                else
                {
                    // A diamond in the middle, with a hole in it.
                    var diamond = Mathf.Abs(u - 0.5f) / 0.30f + Mathf.Abs(v - 0.5f) / 0.36f;
                    if (diamond < 1f && diamond > 0.72f) colour = figure;
                    else if (diamond < 0.34f) colour = figure;
                }

                // Worn down the middle, where everyone walks, and at the two ends
                // where they step on and off.
                var tread = Mathf.Exp(-Mathf.Pow((u - 0.5f) / 0.26f, 2f));
                var wear = tread * (0.45f + 0.35f * Mathf.Abs(v - 0.5f) * 2f);

                colour = Color.Lerp(colour, new Color(0.34f, 0.30f, 0.26f), wear * 0.55f);

                var speck = grain[y * size + x] * 0.22f + 0.89f;
                pixels[y * size + x] = new Color(colour.r * speck, colour.g * speck, colour.b * speck);
            }

            texture.SetPixels(pixels);
            texture.Apply();

            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Clamp;   // one rug, not a tiled floor

                // Anisotropic filtering, and it is not a refinement here.
                //
                // A rug is seen almost edge-on from anywhere but standing over it,
                // and at a grazing angle ordinary mipmapping picks a level so high
                // that the whole texture collapses to its average colour. The
                // first one looked like a sheet of brown card from six metres away
                // — not because the pattern was wrong, but because none of it was
                // reaching the screen.
                importer.anisoLevel = 9;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static GameObject BuildCrowPrefab()
        {
            var path = $"{PropPrefabDir}/Crow.prefab";

            var feather = EnsureMaterial("PropCrow", new Color(0.045f, 0.045f, 0.05f));
            var beakColour = EnsureMaterial("PropCrowBeak", new Color(0.16f, 0.15f, 0.13f));
            var perchWood = EnsureMaterial("PropPerch", new Color(0.22f, 0.17f, 0.12f));
            var eyeColour = EnsureMaterial("PropCrowEye", new Color(0.78f, 0.66f, 0.18f), smoothness: 0.6f);

            var root = new GameObject("Crow");
            root.layer = GameLayers.Prop;

            // A bracket on the wall and the rail it stands on.
            Box("Bracket", root.transform, new Vector3(0f, 0.9f, -0.1f),
                new Vector3(0.06f, 0.06f, 0.2f), perchWood);
            Box("Perch", root.transform, new Vector3(0f, 0.9f, 0f),
                new Vector3(0.5f, 0.05f, 0.05f), perchWood);

            var bird = new GameObject("Bird").transform;
            bird.SetParent(root.transform, false);
            bird.localPosition = new Vector3(0f, 1.04f, 0f);

            Box("Body", bird, new Vector3(0f, 0f, -0.02f), new Vector3(0.11f, 0.13f, 0.21f), feather);
            Box("Tail", bird, new Vector3(0f, -0.01f, -0.18f), new Vector3(0.07f, 0.03f, 0.16f), feather);
            Box("Head", bird, new Vector3(0f, 0.09f, 0.08f), new Vector3(0.085f, 0.08f, 0.09f), feather);
            Box("Beak", bird, new Vector3(0f, 0.08f, 0.15f), new Vector3(0.028f, 0.028f, 0.08f), beakColour);

            foreach (var side in new[] { -1f, 1f })
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = side < 0f ? "EyeL" : "EyeR";
                eye.transform.SetParent(bird, false);
                eye.transform.localPosition = new Vector3(0.036f * side, 0.105f, 0.106f);
                eye.transform.localScale = Vector3.one * 0.019f;
                eye.GetComponent<MeshRenderer>().sharedMaterial = eyeColour;
                Object.DestroyImmediate(eye.GetComponent<Collider>());
            }

            // Wings hinge at the shoulder rather than at their middle, or they
            // beat like a pair of oars.
            var wingL = new GameObject("WingL").transform;
            wingL.SetParent(bird, false);
            wingL.localPosition = new Vector3(-0.05f, 0.03f, -0.01f);
            Box("WingLFeathers", wingL, new Vector3(-0.055f, 0f, 0f),
                new Vector3(0.11f, 0.025f, 0.17f), feather);

            var wingR = new GameObject("WingR").transform;
            wingR.SetParent(bird, false);
            wingR.localPosition = new Vector3(0.05f, 0.03f, -0.01f);
            Box("WingRFeathers", wingR, new Vector3(0.055f, 0f, 0f),
                new Vector3(0.11f, 0.025f, 0.17f), feather);

            var crow = root.AddComponent<Crow>();
            Wire(crow, "wingL", wingL);
            Wire(crow, "wingR", wingR);

            // Nothing about it is solid. A bird you can walk into is a bird that
            // blocks a doorway, and the only thing it is meant to cost is noise.
            foreach (var collider in root.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(collider);

            foreach (var child in root.GetComponentsInChildren<Transform>())
                child.gameObject.layer = GameLayers.Prop;

            return SavePrefab(root, path);
        }

        static GameObject BuildWindowPrefab()
        {
            var path = $"{PropPrefabDir}/Window.prefab";

            var frame = EnsureMaterial("PropWindowFrame", new Color(0.24f, 0.20f, 0.16f));
            var board = EnsureMaterial("PropWindowBoard", new Color(0.33f, 0.26f, 0.19f));
            var dark = EnsureMaterial("PropWindowVoid", new Color(0.008f, 0.008f, 0.012f), smoothness: 0.02f);

            const float width = 1.3f;
            const float height = 1.5f;
            const float sill = 1.15f;     // clear of the chests of drawers below

            var root = new GameObject("Window");
            root.layer = GameLayers.Prop;

            var middle = sill + height * 0.5f;

            // The night on the other side.
            Box("Void", root.transform, new Vector3(0f, middle, -0.015f),
                new Vector3(width, height, 0.04f), dark);

            // Casing: two jambs, a head and a sill board.
            Box("JambW", root.transform, new Vector3(-width * 0.5f - 0.045f, middle, 0f),
                new Vector3(0.09f, height + 0.18f, 0.1f), frame);
            Box("JambE", root.transform, new Vector3(width * 0.5f + 0.045f, middle, 0f),
                new Vector3(0.09f, height + 0.18f, 0.1f), frame);
            Box("Head", root.transform, new Vector3(0f, middle + height * 0.5f + 0.045f, 0f),
                new Vector3(width + 0.18f, 0.09f, 0.1f), frame);
            Box("Sill", root.transform, new Vector3(0f, sill - 0.05f, 0.015f),
                new Vector3(width + 0.26f, 0.1f, 0.16f), frame);

            // The planks. Different lengths and angles, because a man with a
            // hammer and whatever was in the yard did not measure anything.
            var planks = new[]
            {
                (y: 0.22f, angle: -9f, length: 1.62f, thickness: 0.17f),
                (y: 0.52f, angle: 6f, length: 1.52f, thickness: 0.14f),
                (y: 0.78f, angle: -4f, length: 1.58f, thickness: 0.19f),
                (y: 0.95f, angle: 11f, length: 1.46f, thickness: 0.13f),
            };

            for (var i = 0; i < planks.Length; i++)
            {
                var plank = planks[i];

                var piece = Box($"Board_{i}", root.transform,
                    new Vector3(0f, sill + height * plank.y, 0.045f),
                    new Vector3(plank.length, plank.thickness, 0.035f), board);

                piece.transform.localRotation = Quaternion.Euler(0f, 0f, plank.angle);
                piece.layer = GameLayers.Prop;
            }

            // Scenery: it is flat against a wall that already stops everything.
            // Its own colliders would only give the player something invisible to
            // catch on while they are running.
            foreach (var collider in root.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(collider);

            foreach (var child in root.GetComponentsInChildren<Transform>())
                child.gameObject.layer = GameLayers.Prop;

            return SavePrefab(root, path);
        }

        static GameObject BuildWardrobePrefab()
        {
            var path = $"{PropPrefabDir}/Wardrobe.prefab";
            var wood = EnsureMaterial("PropWardrobe", new Color(0.18f, 0.13f, 0.10f));

            var root = new GameObject("Wardrobe");
            root.layer = GameLayers.HidingSpot;

            var body = Box("Body", root.transform, new Vector3(0f, 1.05f, -0.15f), new Vector3(1.2f, 2.1f, 0.5f), wood);
            body.layer = GameLayers.Prop;

            var doorPanel = Box("DoorPanel", root.transform, new Vector3(0f, 1.05f, 0.14f), new Vector3(1.15f, 2.0f, 0.06f), wood);
            doorPanel.layer = GameLayers.HidingSpot;

            // A real wardrobe, if one has been downloaded. The boxes above stay
            // as the shape the player bumps into and the interaction ray finds;
            // they just stop being the thing anybody looks at.
            // No yaw. The half-turn that used to be here put the model's doors
            // against the wall and its back panel into the room, so every wardrobe
            // in the house was a tall frame with two blank boards in it — which
            // read as a bug in the materials, and was chased as one twice. The
            // part dump settles it: this model's doors already face +Z, which is
            // the side the player opens.
            if (AddModel(root, "GothicCabinet_01", GameLayers.HidingSpot))
            {
                body.GetComponent<MeshRenderer>().enabled = false;
                doorPanel.GetComponent<MeshRenderer>().enabled = false;
            }

            var viewpoint = new GameObject("Viewpoint").transform;
            viewpoint.SetParent(root.transform, false);
            viewpoint.localPosition = new Vector3(0f, 0f, -0.15f);

            var exit = new GameObject("ExitPoint").transform;
            exit.SetParent(root.transform, false);
            exit.localPosition = new Vector3(0f, 0f, 0.9f);
            exit.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var spot = root.AddComponent<HidingSpot>();
            Wire(spot, "viewpoint", viewpoint);
            Wire(spot, "exitPoint", exit);

            return SavePrefab(root, path);
        }

        // ------------------------------------------------------------------
        // HUD
        // ------------------------------------------------------------------

        static GameObject BuildHud()
        {
            var path = $"{PrefabDir}/HUD.prefab";

            var root = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var crosshair = MakeImage("Crosshair", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
            crosshair.color = new Color(1f, 1f, 1f, 0.35f);

            var prompt = MakeLabel("Prompt", root.transform, font, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -60f), new Vector2(700f, 40f), TextAnchor.MiddleCenter, 26);

            var inventoryLabel = MakeLabel("Inventory", root.transform, font, new Vector2(0.5f, 0f),
                new Vector2(0f, 48f), new Vector2(1400f, 34f), TextAnchor.MiddleCenter, 22);

            var (staminaGroup, staminaFill) = MakeBar("Stamina", root.transform,
                new Vector2(0.5f, 0f), new Vector2(0f, 104f), new Vector2(320f, 8f),
                new Color(0.85f, 0.85f, 0.9f, 0.8f));

            var (batteryGroup, batteryFill) = MakeBar("Battery", root.transform,
                new Vector2(1f, 0f), new Vector2(-220f, 48f), new Vector2(180f, 8f),
                new Color(1f, 0.85f, 0.5f, 0.85f));

            var hud = root.AddComponent<HudController>();
            Wire(hud, "crosshair", crosshair);
            Wire(hud, "promptLabel", prompt);
            Wire(hud, "inventoryLabel", inventoryLabel);
            Wire(hud, "staminaFill", staminaFill);
            Wire(hud, "staminaGroup", staminaGroup);
            Wire(hud, "batteryFill", batteryFill);
            Wire(hud, "batteryGroup", batteryGroup);

            // Below the blackout so a fade to black still covers it, above
            // everything else so the injury reads over the whole view.
            var blood = MakeImage("Blood", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            blood.sprite = BloodVignette();
            blood.type = Image.Type.Simple;
            blood.color = new Color(1f, 1f, 1f, 0f);

            var bloodRect = blood.rectTransform;
            bloodRect.anchorMin = Vector2.zero;
            bloodRect.anchorMax = Vector2.one;
            bloodRect.offsetMin = Vector2.zero;
            bloodRect.offsetMax = Vector2.zero;

            Wire(hud, "blood", blood);

            var flash = MakeImage("ScareFlash", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            flash.color = new Color(0.55f, 0.03f, 0.03f, 0f);

            var flashRect = flash.rectTransform;
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;

            var scare = root.AddComponent<Jumpscare>();
            Wire(scare, "flash", flash);

            BuildDayOverlay(root, font);

            return SavePrefab(root, path);
        }

        /// <summary>
        /// The blackout between days and the two lines that end a run. Lives on the
        /// same canvas, but above everything else so it covers the HUD.
        /// </summary>
        static void BuildDayOverlay(GameObject canvasRoot, Font font)
        {
            var blackout = MakeImage("Blackout", canvasRoot.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            blackout.color = new Color(0f, 0f, 0f, 0f);
            blackout.enabled = false;

            var blackoutRect = blackout.rectTransform;
            blackoutRect.anchorMin = Vector2.zero;
            blackoutRect.anchorMax = Vector2.one;
            blackoutRect.offsetMin = Vector2.zero;
            blackoutRect.offsetMax = Vector2.zero;

            var dayContainer = new GameObject("DayCard", typeof(RectTransform), typeof(CanvasGroup));
            dayContainer.transform.SetParent(canvasRoot.transform, false);

            var dayRect = dayContainer.GetComponent<RectTransform>();
            dayRect.anchorMin = Vector2.zero;
            dayRect.anchorMax = Vector2.one;
            dayRect.offsetMin = Vector2.zero;
            dayRect.offsetMax = Vector2.zero;

            var dayGroup = dayContainer.GetComponent<CanvasGroup>();
            dayGroup.alpha = 0f;
            dayGroup.interactable = false;
            dayGroup.blocksRaycasts = false;

            var dayLabel = MakeLabel("DayLabel", dayContainer.transform, font, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(800f, 90f), TextAnchor.MiddleCenter, 64);

            var verdict = MakeLabel("Verdict", canvasRoot.transform, font, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -90f), new Vector2(900f, 60f), TextAnchor.MiddleCenter, 36);

            var overlay = canvasRoot.AddComponent<DayOverlay>();
            Wire(overlay, "blackout", blackout);
            Wire(overlay, "dayLabel", dayLabel);
            Wire(overlay, "verdictLabel", verdict);
            Wire(overlay, "dayGroup", dayGroup);
        }

        /// <summary>
        /// The red at the edges on the last two mornings.
        ///
        /// Generated rather than painted, for the same reason the sounds are: it
        /// keeps the project buildable from source. A radial falloff that is
        /// clear in the middle and dark at the corners — it has to obscure the
        /// edges of the view without ever hiding what the player is looking at.
        /// </summary>
        static Sprite BloodVignette()
        {
            const string path = "Assets/_Project/Art/Textures/BloodVignette.png";
            const int size = 256;

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Textures"))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Textures");

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var centre = new Vector2(size * 0.5f, size * 0.5f);
            var reach = size * 0.5f;

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), centre) / reach;

                // Nothing at all until well past halfway out, then quickly up.
                var alpha = Mathf.Clamp01((distance - 0.55f) / 0.45f);
                alpha = alpha * alpha;

                texture.SetPixel(x, y, new Color(0.42f, 0.02f, 0.02f, alpha));
            }

            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Image MakeImage(string name, Transform parent, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        static Text MakeLabel(string name, Transform parent, Font font, Vector2 anchor,
            Vector2 offset, Vector2 size, TextAnchor alignment, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.93f, 0.91f, 0.86f, 0.92f);
            text.raycastTarget = false;
            text.text = string.Empty;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            return text;
        }

        static (CanvasGroup group, Image fill) MakeBar(string name, Transform parent,
            Vector2 anchor, Vector2 offset, Vector2 size, Color colour)
        {
            var container = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            container.transform.SetParent(parent, false);

            var rect = container.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            var group = container.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var back = MakeImage("Back", container.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            back.color = new Color(0f, 0f, 0f, 0.5f);

            var fill = MakeImage("Fill", container.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            fill.color = colour;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            return (group, fill);
        }

        // ------------------------------------------------------------------
        // Scene assembly
        // ------------------------------------------------------------------

        /// <summary>
        /// Puts the HUD and an event system in the scene. Where the furniture goes
        /// is the level's business, not this builder's — HouseBuilder owns that, so
        /// re-running this to tweak an item never disturbs the layout.
        /// </summary>
        static void InstallHud(GameObject hud)
        {
            var scene = EditorSceneManager.OpenScene(HouseScenePath, OpenSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
                if (root.name is "HUD" or "EventSystem")
                    Object.DestroyImmediate(root);

            PrefabUtility.InstantiatePrefab(hud, scene);

            var events = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(events, scene);

            BuildKit.SaveScene(scene, "Content");
            Debug.Log("[Content] HUD installed");
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

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

        /// <summary>
        /// Resizes a box that is already in place.
        ///
        /// Used where a downloaded model has taken over the looking and the box
        /// is left doing the colliding: a hidden box the wrong size is worse than
        /// a visible one, because the player feels it and cannot see why.
        /// </summary>
        static void Refit(GameObject box, Vector3 localPosition, Vector3 localScale)
        {
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;
        }

        /// <summary>
        /// One named piece of an imported model, matched on the end of the name.
        ///
        /// Exporters prefix every mesh with the file it came out of, so the part
        /// wanted here is "vintage_wooden_drawer_01_drawer02" and writing that out
        /// in full buys nothing but a rename away from a silent miss.
        /// </summary>
        static Transform FindPart(Transform model, string nameEndsWith)
        {
            if (model == null) return null;

            foreach (var child in model.GetComponentsInChildren<Transform>())
                if (child.name.EndsWith(nameEndsWith, System.StringComparison.OrdinalIgnoreCase))
                    return child;

            Debug.LogWarning($"[Content] {model.name} has no part ending '{nameEndsWith}'.");
            return null;
        }

        static Material EnsureMaterial(string name, Color colour, float smoothness = 0.12f)
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
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject SavePrefab(GameObject root, string path)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out var ok);
            Object.DestroyImmediate(root);

            if (!ok) Debug.LogError($"[Content] Failed to save prefab {path}");
            return saved;
        }

        /// <summary>Assigns a private [SerializeField] without widening its API.</summary>
        /// <summary>
        /// Puts a downloaded model into a prop as its visual, and says whether
        /// there was one to put in.
        ///
        /// The gameplay parts — colliders, anchors, the components themselves —
        /// stay exactly as they were and keep their own boxes. A model is a
        /// picture of a wardrobe; what the player collides with and what the
        /// raycast finds should not change because an artist shipped a new mesh.
        /// </summary>
        static bool AddModel(GameObject prop, string asset, int layer, float yaw = 0f,
            bool standOnFloor = true, string part = null)
        {
            var path = $"Assets/_Project/Art/Models/{asset}/{asset}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (model == null) return false;

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, prop.transform);
            visual.name = "Model";
            visual.transform.localPosition = Vector3.zero;

            // Multiplied, never assigned. Unity bakes an axis conversion into the
            // imported prefab's own rotation — these come out of Blender Z-up —
            // and assigning a yaw over the top of it threw that away and stood
            // the bed frame on its end.
            visual.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * visual.transform.localRotation;

            // Nobody else's model is modelled around our origin. These come in
            // with their pivot wherever the artist left it, which put the bed
            // frame half a metre off the mattress it is supposed to be holding.
            // Centre it horizontally on the prop and stand it on the floor.
            var renderers = visual.GetComponentsInChildren<Renderer>();

            // Some downloads are a set rather than a thing — four wine bottles in
            // a row, sold as one model. An item is one object, so the rest are
            // switched off and everything below measures only what is left.
            if (part != null)
            {
                var kept = new List<Renderer>();

                foreach (var renderer in renderers)
                    if (renderer.name.EndsWith(part, System.StringComparison.OrdinalIgnoreCase))
                        kept.Add(renderer);
                    else
                        renderer.enabled = false;

                if (kept.Count == 0)
                    Debug.LogWarning($"[Content] {asset} has no part ending '{part}'.");
                else
                    renderers = kept.ToArray();
            }

            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);

                // Furniture stands on the floor; an item hangs in a hand and in
                // mid-air on the way there, so it is centred on all three axes
                // instead and the prefab's origin is the middle of the thing.
                var origin = prop.transform.position;
                visual.transform.localPosition -= new Vector3(
                    bounds.center.x - origin.x,
                    (standOnFloor ? bounds.min.y : bounds.center.y) - origin.y,
                    bounds.center.z - origin.z);
            }

            foreach (var child in visual.GetComponentsInChildren<Transform>())
                child.gameObject.layer = layer;

            // Lit for this house rather than for the photograph it came from.
            ModelSkin.Retint(asset, visual.GetComponentsInChildren<Renderer>());

            // The model is scenery. Its own colliders would fight the box the
            // prop already uses, which is sized and placed on purpose.
            foreach (var collider in visual.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(collider);

            return true;
        }

        static void SetEnum(Object target, string fieldName, int value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);

            if (prop == null)
            {
                Debug.LogError($"[Content] {target.GetType().Name} has no field '{fieldName}'");
                return;
            }

            prop.enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Wire(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);

            if (prop == null)
            {
                Debug.LogError($"[Content] {target.GetType().Name} has no field '{fieldName}'");
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
