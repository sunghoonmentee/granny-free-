using System.Collections.Generic;
using System.IO;
using Granny.Core;
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

            var items = BuildItems();
            var furniture = BuildFurniture();
            var hud = BuildHud();

            PopulateHouse(items, furniture, hud);

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
        static readonly (string id, string display, ItemCarry carry, string[] tags,
            Color colour, Vector3 size, float loudness)[] ItemTable =
        {
            ("hammer", "Hammer", ItemCarry.Held, new[] { "pry" },
                new Color(0.55f, 0.42f, 0.28f), new Vector3(0.08f, 0.30f, 0.08f), 1.2f),

            ("wirecutters", "Wirecutters", ItemCarry.Held, new[] { "cut" },
                new Color(0.75f, 0.15f, 0.15f), new Vector3(0.07f, 0.22f, 0.05f), 0.9f),

            ("key.front", "Rusty Key", ItemCarry.Pocketed, new[] { "key.front", "unlock" },
                new Color(0.85f, 0.72f, 0.30f), new Vector3(0.04f, 0.10f, 0.02f), 0.4f),

            ("bottle", "Glass Bottle", ItemCarry.Held, new[] { "throwable" },
                new Color(0.35f, 0.62f, 0.45f), new Vector3(0.09f, 0.24f, 0.09f), 1.8f),
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

                var prefab = BuildItemPrefab(entry.id, entry.colour, entry.size, definition);

                var so = new SerializedObject(definition);
                so.FindProperty("id").stringValue = entry.id;
                so.FindProperty("displayName").stringValue = entry.display;
                so.FindProperty("carry").enumValueIndex = (int)entry.carry;
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

        static GameObject BuildItemPrefab(string id, Color colour, Vector3 size, ItemDefinition definition)
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
        }

        static Furniture BuildFurniture() => new()
        {
            Drawer = BuildDrawerPrefab(),
            Door = BuildDoorPrefab(),
            Wardrobe = BuildWardrobePrefab(),
        };

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

            // The leaf is offset inside a pivot at the hinge edge, so rotating the
            // pivot swings the door about its hinge rather than about its centre.
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root.transform, false);

            var leaf = Box("Leaf", pivot, new Vector3(0.45f, 1.0f, 0f), new Vector3(0.9f, 2.0f, 0.08f), wood);
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

        static void PopulateHouse(List<ItemDefinition> items, Furniture furniture, GameObject hud)
        {
            var scene = EditorSceneManager.OpenScene(HouseScenePath, OpenSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
                if (root.name is "Furnishings" or "HUD")
                    Object.DestroyImmediate(root);

            var parent = new GameObject("Furnishings").transform;

            // Three dressers in three corners. Which one holds which tool is
            // decided at level setup in a later phase; for now the order is fixed
            // so the greybox is predictable to test against.
            var placements = new[]
            {
                (position: new Vector3(-8f, 0f, 8f), yaw: 135f),
                (position: new Vector3(8f, 0f, 8f), yaw: -135f),
                (position: new Vector3(-8f, 0f, -8f), yaw: 45f),
            };

            for (var i = 0; i < placements.Length; i++)
            {
                var dresser = (GameObject)PrefabUtility.InstantiatePrefab(furniture.Drawer, parent);
                dresser.transform.SetPositionAndRotation(
                    placements[i].position, Quaternion.Euler(0f, placements[i].yaw, 0f));

                if (i >= items.Count) continue;

                var drawer = dresser.GetComponent<Drawer>();
                Wire(drawer, "contents", items[i]);
            }

            var wardrobe = (GameObject)PrefabUtility.InstantiatePrefab(furniture.Wardrobe, parent);
            wardrobe.transform.SetPositionAndRotation(new Vector3(8f, 0f, -8f), Quaternion.Euler(0f, 180f, 0f));

            var door = (GameObject)PrefabUtility.InstantiatePrefab(furniture.Door, parent);
            door.transform.SetPositionAndRotation(new Vector3(-3.5f, 0f, 0f), Quaternion.identity);

            // The bottle starts on the floor so throwing can be tried immediately.
            var bottle = items.Find(i => i.Id == "bottle");
            if (bottle != null && bottle.WorldPrefab != null)
            {
                var loose = (GameObject)PrefabUtility.InstantiatePrefab(bottle.WorldPrefab, parent);
                loose.transform.position = new Vector3(1.5f, 0.3f, -5f);
            }

            PrefabUtility.InstantiatePrefab(hud, scene);

            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var events = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(events, scene);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Content] House scene furnished");
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
