using System.Collections.Generic;
using Granny.Core;
using Granny.Gameplay;
using Granny.Gameplay.Interaction;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Granny.EditorTools
{
    /// <summary>
    /// Builds the house: four storeys, the stairs between them, the furniture, the
    /// lighting and the NavMesh.
    ///
    /// It is generated rather than hand-placed for one reason — the layout is the
    /// game's difficulty curve, and being able to change a wall by editing a line
    /// here means the layout can actually be tuned instead of being frozen the
    /// moment it is dragged into place.
    ///
    ///     Unity.exe -batchmode -quit -executeMethod \
    ///       Granny.EditorTools.HouseBuilder.Run
    /// </summary>
    public static class HouseBuilder
    {
        const string HouseScenePath = "Assets/_Project/Scenes/House.unity";
        const string PropPrefabDir = "Assets/_Project/Prefabs/Props";
        const string ItemDataDir = "Assets/_Project/Data/Items";

        // The footprint. X runs west to east, Z runs south to north; the front
        // door is on the north wall, which is where the escape door gets built.
        static readonly Rect Footprint = new(-12f, -10f, 24f, 20f);
        static readonly Rect AtticFootprint = new(-8f, -7f, 16f, 14f);

        const float BasementY = -BuildKit.FloorHeight;
        const float GroundY = 0f;
        const float UpperY = BuildKit.FloorHeight;
        const float AtticY = BuildKit.FloorHeight * 2f;

        // Stairwells. Each is a hole in the slab of the floor the flight arrives
        // at, and each flight spans its well exactly end to end - a flight that
        // stops short of the opening leaves a gap the player falls through.
        static readonly Rect BasementWell = new(6.5f, 1.5f, 4.5f, 6f);
        static readonly Rect UpperWell = new(-11f, 1.5f, 4.5f, 6f);

        // Kept inside the attic's smaller footprint; a well hanging off the edge
        // would put the top of the flight outside the floor it arrives at.
        static readonly Rect AtticWell = new(-7.5f, -6f, 4.5f, 6f);

        [MenuItem("Granny/Build House", priority = 15)]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(HouseScenePath, OpenSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
                if (root.name is "Greybox" or "House" or "Furnishings" or "Navigation"
                    or "PatrolPoints" or "Bed" or "Lighting" or "Spawner")
                    Object.DestroyImmediate(root);

            var palette = new Palette();
            var house = new GameObject("House").transform;

            BuildBasement(house, palette);
            BuildGround(house, palette);
            BuildUpper(house, palette);
            BuildAttic(house, palette);
            BuildStairs(house, palette);

            var furniture = Furnish(house);
            PlaceMarkers();
            BuildSpawner(furniture);
            BakeNavigation();
            PlacePlayer();

            BuildKit.SaveScene(scene, "House");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[House] done");
        }

        sealed class Palette
        {
            public readonly Material Floor = BuildKit.Material("HouseFloor", new Color(0.17f, 0.14f, 0.11f));
            public readonly Material Wall = BuildKit.Material("HouseWall", new Color(0.24f, 0.21f, 0.18f));
            public readonly Material Interior = BuildKit.Material("HouseInterior", new Color(0.28f, 0.25f, 0.21f));
            public readonly Material Concrete = BuildKit.Material("HouseConcrete", new Color(0.14f, 0.14f, 0.15f));
            public readonly Material Ceiling = BuildKit.Material("HouseCeiling", new Color(0.09f, 0.08f, 0.07f));
            public readonly Material Stair = BuildKit.Material("HouseStair", new Color(0.21f, 0.16f, 0.11f));
            public readonly Material Tread = BuildKit.Material("HouseTread", new Color(0.33f, 0.27f, 0.19f));
        }

        // ------------------------------------------------------------------
        // Storeys
        // ------------------------------------------------------------------

        /// <summary>
        /// Basement: one long cellar split by a wall, and no natural light at all.
        /// It is the furthest point from the front door, which is exactly why a
        /// tool ends up down here.
        /// </summary>
        static void BuildBasement(Transform parent, Palette palette)
        {
            var floor = new GameObject("Basement").transform;
            floor.SetParent(parent, false);

            BuildKit.Slab(floor, "Slab", Footprint, BasementY, palette.Concrete, GameLayers.LevelGeometry);
            Perimeter(floor, Footprint, BasementY, palette.Concrete);

            // Cellar divider, with one doorway near the south end.
            BuildKit.Wall(floor, "Divider",
                new Vector2(0f, Footprint.yMin), new Vector2(0f, Footprint.yMax),
                BasementY, palette.Concrete, GameLayers.LevelGeometry, 4f);

            BuildKit.Bulb(floor, "CellarBulb", new Vector3(-6f, BasementY + 2.7f, -4f),
                new Color(1f, 0.78f, 0.5f), 1.6f, 7f);
            BuildKit.Bulb(floor, "BoilerBulb", new Vector3(6f, BasementY + 2.7f, -5f),
                new Color(0.9f, 0.6f, 0.45f), 1.1f, 6f);
        }

        /// <summary>
        /// Ground floor: entrance hall on the north, living room west, kitchen
        /// south-east. The front door is visible from the hall, so the player can
        /// always see how far they are from the thing they are working towards.
        /// </summary>
        static void BuildGround(Transform parent, Palette palette)
        {
            var floor = new GameObject("Ground").transform;
            floor.SetParent(parent, false);

            BuildKit.Slab(floor, "Slab", Footprint, GroundY, palette.Floor,
                GameLayers.LevelGeometry, BasementWell);

            // The north wall carries the front door, so its opening is left for
            // EscapeBuilder rather than being filled in here.
            BuildKit.Wall(floor, "Wall_N",
                new Vector2(Footprint.xMin, Footprint.yMax), new Vector2(Footprint.xMax, Footprint.yMax),
                GroundY, palette.Wall, GameLayers.LevelGeometry, 12f);

            BuildKit.Wall(floor, "Wall_S",
                new Vector2(Footprint.xMin, Footprint.yMin), new Vector2(Footprint.xMax, Footprint.yMin),
                GroundY, palette.Wall, GameLayers.LevelGeometry);

            BuildKit.Wall(floor, "Wall_W",
                new Vector2(Footprint.xMin, Footprint.yMin), new Vector2(Footprint.xMin, Footprint.yMax),
                GroundY, palette.Wall, GameLayers.LevelGeometry);

            BuildKit.Wall(floor, "Wall_E",
                new Vector2(Footprint.xMax, Footprint.yMin), new Vector2(Footprint.xMax, Footprint.yMax),
                GroundY, palette.Wall, GameLayers.LevelGeometry);

            // Hall / living room divider.
            BuildKit.Wall(floor, "Wall_Hall",
                new Vector2(-4f, Footprint.yMin), new Vector2(-4f, 4f),
                GroundY, palette.Interior, GameLayers.LevelGeometry, 3f, 11f);

            // Kitchen divider.
            BuildKit.Wall(floor, "Wall_Kitchen",
                new Vector2(-4f, -2f), new Vector2(Footprint.xMax, -2f),
                GroundY, palette.Interior, GameLayers.LevelGeometry, 5f, 13f);

            BuildKit.Bulb(floor, "HallBulb", new Vector3(2f, GroundY + 2.8f, 6f),
                new Color(1f, 0.85f, 0.6f), 2.2f, 9f);
            BuildKit.Bulb(floor, "LivingBulb", new Vector3(-8f, GroundY + 2.8f, -4f),
                new Color(1f, 0.8f, 0.55f), 1.7f, 8f);
            BuildKit.Bulb(floor, "KitchenBulb", new Vector3(6f, GroundY + 2.8f, -6f),
                new Color(0.95f, 0.92f, 0.8f), 1.5f, 7f);
        }

        /// <summary>
        /// First floor: three bedrooms and a landing. The player wakes up in the
        /// south-east bedroom, the furthest room from the front door.
        /// </summary>
        static void BuildUpper(Transform parent, Palette palette)
        {
            var floor = new GameObject("Upper").transform;
            floor.SetParent(parent, false);

            BuildKit.Slab(floor, "Slab", Footprint, UpperY, palette.Floor,
                GameLayers.LevelGeometry, UpperWell);

            Perimeter(floor, Footprint, UpperY, palette.Wall);

            // Landing runs east-west; bedrooms hang off it.
            BuildKit.Wall(floor, "Wall_Landing",
                new Vector2(Footprint.xMin, 1f), new Vector2(Footprint.xMax, 1f),
                UpperY, palette.Interior, GameLayers.LevelGeometry, 5f, 13f, 20f);

            BuildKit.Wall(floor, "Wall_Bedrooms",
                new Vector2(1f, Footprint.yMin), new Vector2(1f, 1f),
                UpperY, palette.Interior, GameLayers.LevelGeometry, 7f);

            BuildKit.Bulb(floor, "LandingBulb", new Vector3(-2f, UpperY + 2.8f, 5f),
                new Color(1f, 0.82f, 0.58f), 1.8f, 9f);
            BuildKit.Bulb(floor, "BedroomBulb", new Vector3(6f, UpperY + 2.8f, -6f),
                new Color(0.95f, 0.78f, 0.6f), 1.4f, 7f);
        }

        /// <summary>
        /// Attic: one low open space, smaller than the floors below, with a roof
        /// instead of another ceiling.
        /// </summary>
        static void BuildAttic(Transform parent, Palette palette)
        {
            var floor = new GameObject("Attic").transform;
            floor.SetParent(parent, false);

            BuildKit.Slab(floor, "Slab", AtticFootprint, AtticY, palette.Floor,
                GameLayers.LevelGeometry, AtticWell);

            Perimeter(floor, AtticFootprint, AtticY, palette.Wall);

            BuildKit.Slab(floor, "Roof", AtticFootprint, AtticY + BuildKit.FloorHeight,
                palette.Ceiling, GameLayers.LevelGeometry);

            BuildKit.Bulb(floor, "AtticBulb", new Vector3(0f, AtticY + 2.6f, 0f),
                new Color(0.85f, 0.7f, 0.5f), 1.2f, 8f);
        }

        static void Perimeter(Transform parent, Rect area, float floorY, Material material)
        {
            BuildKit.Wall(parent, "Wall_N", new Vector2(area.xMin, area.yMax), new Vector2(area.xMax, area.yMax),
                floorY, material, GameLayers.LevelGeometry);
            BuildKit.Wall(parent, "Wall_S", new Vector2(area.xMin, area.yMin), new Vector2(area.xMax, area.yMin),
                floorY, material, GameLayers.LevelGeometry);
            BuildKit.Wall(parent, "Wall_W", new Vector2(area.xMin, area.yMin), new Vector2(area.xMin, area.yMax),
                floorY, material, GameLayers.LevelGeometry);
            BuildKit.Wall(parent, "Wall_E", new Vector2(area.xMax, area.yMin), new Vector2(area.xMax, area.yMax),
                floorY, material, GameLayers.LevelGeometry);
        }

        // ------------------------------------------------------------------
        // Stairs
        // ------------------------------------------------------------------

        /// <summary>
        /// Three flights, each a single ramp. They are deliberately in different
        /// corners: getting from the attic to the basement means crossing the
        /// whole house twice, which is what makes Granny's position matter.
        /// </summary>
        static void BuildStairs(Transform parent, Palette palette)
        {
            var stairs = new GameObject("Stairs").transform;
            stairs.SetParent(parent, false);

            // Each flight starts at the south edge of its well and climbs the
            // well's full depth, so the top of the ramp meets the slab edge
            // exactly rather than stopping short of it.

            // Ground down to basement, inside the east stairwell.
            BuildKit.Stair(stairs, "Stair_Basement",
                new Vector3(BasementWell.center.x, BasementY, BasementWell.yMin),
                Vector3.forward, BuildKit.FloorHeight, BasementWell.height, 2.6f,
                palette.Stair, palette.Tread, GameLayers.LevelGeometry);

            // Ground up to the first floor, in the west stairwell.
            BuildKit.Stair(stairs, "Stair_Upper",
                new Vector3(UpperWell.center.x, GroundY, UpperWell.yMin),
                Vector3.forward, BuildKit.FloorHeight, UpperWell.height, 2.6f,
                palette.Stair, palette.Tread, GameLayers.LevelGeometry);

            // First floor up to the attic, through the south-west bedroom.
            BuildKit.Stair(stairs, "Stair_Attic",
                new Vector3(AtticWell.center.x, UpperY, AtticWell.yMin),
                Vector3.forward, BuildKit.FloorHeight, AtticWell.height, 2.2f,
                palette.Stair, palette.Tread, GameLayers.LevelGeometry);
        }

        // ------------------------------------------------------------------
        // Furniture
        // ------------------------------------------------------------------

        readonly struct Placement
        {
            public readonly Vector3 Position;
            public readonly float Yaw;

            public Placement(float x, float y, float z, float yaw)
            {
                Position = new Vector3(x, y, z);
                Yaw = yaw;
            }
        }

        /// <summary>
        /// Six dressers, spread across all four storeys. Six is deliberate: with
        /// three required tools, half the drawers in the house are empty, so
        /// searching costs something even when it works.
        /// </summary>
        static readonly Placement[] Dressers =
        {
            new(-10f, BasementY, -6f, 90f),
            new(9f, BasementY, -7f, -90f),
            new(-10f, GroundY, -7f, 90f),
            new(9f, GroundY, -7f, -90f),
            new(-9f, UpperY, -7f, 90f),
            new(0f, AtticY, 4f, 180f),
        };

        static readonly Placement[] Wardrobes =
        {
            new(-9f, GroundY, 2f, 90f),
            new(9f, UpperY, -3f, -90f),
            new(6f, AtticY, -4f, 180f),
        };

        /// <summary>Interior doors, at the openings cut into the walls above.</summary>
        static readonly Placement[] Doors =
        {
            new(-4f, GroundY, -7f, 0f),
            new(1f, GroundY, -2f, 90f),
            new(-4f, UpperY, -3f, 0f),
            new(1f, UpperY, -3f, 0f),
        };

        sealed class Furniture
        {
            public readonly List<Drawer> Drawers = new();
        }

        static Furniture Furnish(Transform parent)
        {
            var furniture = new Furniture();

            var dresserPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropPrefabDir}/Dresser.prefab");
            var wardrobePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropPrefabDir}/Wardrobe.prefab");
            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropPrefabDir}/Door.prefab");

            if (dresserPrefab == null || wardrobePrefab == null || doorPrefab == null)
            {
                Debug.LogError("[House] Prop prefabs missing. Run Granny > Build Content first.");
                return furniture;
            }

            var root = new GameObject("Furnishings").transform;

            Place(dresserPrefab, Dressers, root, instance =>
            {
                var drawer = instance.GetComponent<Drawer>();
                if (drawer != null) furniture.Drawers.Add(drawer);
            });

            Place(wardrobePrefab, Wardrobes, root);
            Place(doorPrefab, Doors, root);

            return furniture;
        }

        static void Place(GameObject prefab, Placement[] placements, Transform parent,
            System.Action<GameObject> onPlaced = null)
        {
            for (var i = 0; i < placements.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.name = $"{prefab.name}_{i}";
                instance.transform.SetPositionAndRotation(
                    placements[i].Position, Quaternion.Euler(0f, placements[i].Yaw, 0f));

                onPlaced?.Invoke(instance);
            }
        }

        // ------------------------------------------------------------------
        // Markers, spawner, navigation
        // ------------------------------------------------------------------

        /// <summary>
        /// Patrol markers on every storey. Granny walking the whole house rather
        /// than one floor is what makes the upper storeys feel unsafe.
        /// </summary>
        static readonly Vector3[] PatrolPositions =
        {
            new(-7f, GroundY, 6f),
            new(6f, GroundY, 6f),
            new(-8f, GroundY, -6f),
            new(6f, GroundY, -6f),
            new(-8f, UpperY, 5f),
            new(6f, UpperY, -6f),
            new(-6f, BasementY, -5f),
            new(6f, BasementY, -5f),
            new(0f, AtticY, 0f),
        };

        static void PlaceMarkers()
        {
            var root = new GameObject("PatrolPoints").transform;

            for (var i = 0; i < PatrolPositions.Length; i++)
                BuildKit.Marker(root, $"Patrol_{i}", PatrolPositions[i], 0f, "SpawnPoint");

            // Granny starts in the cellar, so day one begins with her a long way
            // from the player and the first noise is the player's own.
            BuildKit.Marker(root, "GrannySpawn", new Vector3(-6f, BasementY, -3f), 0f);

            // The player wakes in the south-east bedroom, the furthest room from
            // the front door: every morning starts with the walk down.
            var bed = new GameObject("Bed");
            bed.transform.SetPositionAndRotation(new Vector3(7f, UpperY + 0.1f, -7f),
                Quaternion.Euler(0f, 315f, 0f));
        }

        static void BuildSpawner(Furniture furniture)
        {
            var spawner = new GameObject("Spawner").AddComponent<ItemSpawner>();
            var so = new SerializedObject(spawner);

            var required = so.FindProperty("requiredItems");
            var ids = new[] { "Item_hammer", "Item_wirecutters", "Item_key_front" };
            required.arraySize = ids.Length;

            for (var i = 0; i < ids.Length; i++)
                required.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemDataDir}/{ids[i]}.asset");

            var optional = so.FindProperty("optionalItems");
            optional.arraySize = 1;
            optional.GetArrayElementAtIndex(0).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemDataDir}/Item_bottle.asset");

            var containers = so.FindProperty("containers");
            containers.arraySize = furniture.Drawers.Count;
            for (var i = 0; i < furniture.Drawers.Count; i++)
                containers.GetArrayElementAtIndex(i).objectReferenceValue = furniture.Drawers[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BakeNavigation()
        {
            var navigation = new GameObject("Navigation");
            var surface = navigation.AddComponent<NavMeshSurface>();

            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = (1 << GameLayers.LevelGeometry) | (1 << GameLayers.Prop);

            BuildKit.Bake(surface);

            Debug.Log("[House] NavMesh baked across four storeys");
        }

        static void PlacePlayer()
        {
            var player = Object.FindAnyObjectByType<Gameplay.Player.PlayerMotor>();
            if (player == null)
            {
                Debug.LogWarning("[House] No player in the scene; run Granny > Build Player Rig.");
                return;
            }

            player.transform.SetPositionAndRotation(
                new Vector3(7f, UpperY + 0.15f, -7f), Quaternion.Euler(0f, 315f, 0f));
        }
    }
}
