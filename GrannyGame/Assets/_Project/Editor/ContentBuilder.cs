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
        static readonly (string id, string display, string[] tags,
            Color colour, Vector3 size, float loudness, bool breakable)[] ItemTable =
        {
            ("hammer", "Hammer", new[] { "pry" },
                new Color(0.55f, 0.42f, 0.28f), new Vector3(0.08f, 0.30f, 0.08f), 1.2f, false),

            ("wirecutters", "Wirecutters", new[] { "cut" },
                new Color(0.75f, 0.15f, 0.15f), new Vector3(0.07f, 0.22f, 0.05f), 0.9f, false),

            // A key takes the hand now, exactly like a hammer does.
            ("key.front", "Rusty Key", new[] { "key.front", "unlock" },
                new Color(0.85f, 0.72f, 0.30f), new Vector3(0.04f, 0.10f, 0.02f), 0.4f, false),

            ("bottle", "Glass Bottle", new[] { "throwable" },
                new Color(0.35f, 0.62f, 0.45f), new Vector3(0.09f, 0.24f, 0.09f), 1.8f, true),

            ("jar", "Preserve Jar", new[] { "throwable" },
                new Color(0.55f, 0.48f, 0.22f), new Vector3(0.13f, 0.20f, 0.13f), 1.6f, true),

            // The crossbow, in three trips. Each part takes the hand like
            // anything else, which is what makes building it cost a night.
            ("bow.stock", "Carved Stock", new[] { "bow_stock" },
                new Color(0.42f, 0.30f, 0.18f), new Vector3(0.09f, 0.46f, 0.07f), 1.0f, false),

            ("bow.limb", "Steel Limb", new[] { "bow_limb" },
                new Color(0.45f, 0.46f, 0.50f), new Vector3(0.52f, 0.05f, 0.05f), 1.1f, false),

            ("bow.cord", "Waxed Cord", new[] { "bow_cord" },
                new Color(0.72f, 0.66f, 0.46f), new Vector3(0.10f, 0.10f, 0.10f), 0.3f, false),

            ("crossbow", "Crossbow", new[] { "weapon" },
                new Color(0.38f, 0.30f, 0.22f), new Vector3(0.44f, 0.12f, 0.50f), 1.4f, false),

            // Darts are the exception to the one-hand rule: they go in a pocket,
            // and they can be picked up again from wherever they landed.
            ("dart", "Tranquilliser Dart", new[] { "dart" },
                new Color(0.20f, 0.55f, 0.62f), new Vector3(0.03f, 0.03f, 0.26f), 0.2f, false),

            // The locks the harder settings add. A note rather than a number
            // typed on a keypad: the combination is a thing you have to carry to
            // the door, which costs a trip like everything else.
            ("note", "Torn Note", new[] { "combination" },
                new Color(0.86f, 0.82f, 0.68f), new Vector3(0.14f, 0.01f, 0.18f), 0.1f, false),

            ("battery", "Dry Cell", new[] { "battery" },
                new Color(0.30f, 0.50f, 0.34f), new Vector3(0.07f, 0.14f, 0.07f), 0.5f, false),

            ("fuse", "Ceramic Fuse", new[] { "electrics" },
                new Color(0.72f, 0.55f, 0.28f), new Vector3(0.05f, 0.05f, 0.14f), 0.3f, false),

            // The truck. Five parts, all of which have to end up in the garage.
            ("sparkplug", "Spark Plug", new[] { "sparkplug" },
                new Color(0.78f, 0.76f, 0.70f), new Vector3(0.04f, 0.14f, 0.04f), 0.4f, false),

            ("truckbattery", "Truck Battery", new[] { "truckbattery" },
                new Color(0.20f, 0.40f, 0.26f), new Vector3(0.26f, 0.20f, 0.18f), 1.3f, false),

            ("fuel", "Jerry Can", new[] { "fuel" },
                new Color(0.68f, 0.42f, 0.14f), new Vector3(0.20f, 0.32f, 0.14f), 1.1f, false),

            ("truckkey", "Truck Key", new[] { "truckkey" },
                new Color(0.82f, 0.72f, 0.32f), new Vector3(0.04f, 0.09f, 0.02f), 0.3f, false),
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

                var prefab = BuildItemPrefab(entry.id, entry.colour, entry.size, definition, entry.breakable);

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

        static GameObject BuildItemPrefab(string id, Color colour, Vector3 size,
            ItemDefinition definition, bool breakable)
        {
            var path = $"{ItemPrefabDir}/{id.Replace('.', '_')}.prefab";

            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = id;
            root.transform.localScale = size;
            root.layer = GameLayers.Pickup;
            root.GetComponent<MeshRenderer>().sharedMaterial =
                EnsureMaterial($"Item_{id.Replace('.', '_')}", colour);

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
        }

        static Furniture BuildFurniture() => new()
        {
            Drawer = BuildDrawerPrefab(),
            Door = BuildDoorPrefab(),
            Wardrobe = BuildWardrobePrefab(),
            BearTrap = BuildBearTrapPrefab(),
            Bed = BuildBedPrefab(),
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
            var sheet = EnsureMaterial("PropBedSheet", new Color(0.46f, 0.43f, 0.38f));

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
                bedding.localPosition = new Vector3(0f, 0.37f, 0f);
                bedding.localScale = new Vector3(0.84f, 0.16f, 1.92f);

                DecayBuilder.Weather(root, seed: 8817, webs: 3);
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
            Wire(drawer, "sliding", front.transform);
            Wire(drawer, "contentsAnchor", anchor);

            var so = new SerializedObject(drawer);
            so.FindProperty("slideAxis").vector3Value = Vector3.forward;
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

            var door = root.AddComponent<HingeDoor>();
            Wire(door, "leaf", pivot);

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
            if (AddModel(root, "GothicCabinet_01", GameLayers.HidingSpot, 180f))
            {
                body.GetComponent<MeshRenderer>().enabled = false;
                doorPanel.GetComponent<MeshRenderer>().enabled = false;
                DecayBuilder.Weather(root, seed: 4021, webs: 4);
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
            mat.SetFloat("_Smoothness", 0.12f);
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
        static bool AddModel(GameObject prop, string asset, int layer, float yaw = 0f)
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
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);

                var origin = prop.transform.position;
                visual.transform.localPosition -= new Vector3(
                    bounds.center.x - origin.x,
                    bounds.min.y - origin.y,
                    bounds.center.z - origin.z);
            }

            foreach (var child in visual.GetComponentsInChildren<Transform>())
                child.gameObject.layer = layer;

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
