using System.Collections.Generic;
using Granny.Core;
using Granny.Gameplay;
using Granny.Gameplay.AI;
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
        //
        // Each well is exactly as wide as its flight, too. They used to be 4.5 m
        // wide around a 2.6 m staircase, which left a metre of open pit down each
        // side of every set of stairs.
        const float MainStairWidth = 2.6f;
        const float AtticStairWidth = 2.2f;

        static readonly Rect BasementWell = new(7.45f, 1.5f, MainStairWidth, 6f);
        static readonly Rect UpperWell = new(-10.05f, 1.5f, MainStairWidth, 6f);

        // Kept inside the attic's smaller footprint; a well hanging off the edge
        // would put the top of the flight outside the floor it arrives at.
        static readonly Rect AtticWell = new(-6.35f, -6f, AtticStairWidth, 6f);

        [MenuItem("Granny/Build House", priority = 15)]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(HouseScenePath, OpenSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
                if (root.name is "Greybox" or "House" or "Furnishings" or "Navigation"
                    or "PatrolPoints" or "Bed" or "Lighting" or "Spawner" or "NoiseMakers")
                    Object.DestroyImmediate(root);

            var palette = new Palette();
            var house = new GameObject("House").transform;

            // Doors are hung in the openings the walls actually left, never at
            // hand-written coordinates. That is what makes a door standing in the
            // middle of a room - or turned across its own doorway - impossible.
            var doorways = new List<BuildKit.Doorway>();

            doorways.AddRange(BuildBasement(house, palette));
            doorways.AddRange(BuildGround(house, palette));
            doorways.AddRange(BuildUpper(house, palette));
            BuildAttic(house, palette);
            BuildStairs(house, palette);

            var furniture = Furnish(house, doorways);
            BuildNoiseMakers();
            PlaceMarkers();
            BuildSpawner(furniture);
            BakeNavigation();
            PlacePlayer();

            BuildKit.SaveScene(scene, "House");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[House] done");
        }

        /// <summary>
        /// Surfaces are much lighter than they were. Painting a dark house in dark
        /// paint and then lighting it with two bulbs is how the first pass ended up
        /// unreadable: the mood has to come from where the light falls, not from
        /// albedo that swallows every photon that lands on it.
        /// </summary>
        sealed class Palette
        {
            public readonly Material Floor = BuildKit.Material("HouseFloor", new Color(0.34f, 0.29f, 0.24f));
            public readonly Material Wall = BuildKit.Material("HouseWall", new Color(0.55f, 0.50f, 0.44f));
            public readonly Material Interior = BuildKit.Material("HouseInterior", new Color(0.60f, 0.55f, 0.48f));
            public readonly Material Concrete = BuildKit.Material("HouseConcrete", new Color(0.36f, 0.36f, 0.38f));
            public readonly Material Ceiling = BuildKit.Material("HouseCeiling", new Color(0.30f, 0.28f, 0.26f));
            public readonly Material Stair = BuildKit.Material("HouseStair", new Color(0.38f, 0.30f, 0.22f));
            public readonly Material Tread = BuildKit.Material("HouseTread", new Color(0.52f, 0.43f, 0.31f));
        }

        // ------------------------------------------------------------------
        // Storeys
        // ------------------------------------------------------------------

        /// <summary>
        /// Basement: one long cellar split by a wall, and no natural light at all.
        /// It is the furthest point from the front door, which is exactly why a
        /// tool ends up down here.
        /// </summary>
        static BuildKit.Doorway[] BuildBasement(Transform parent, Palette palette)
        {
            var floor = new GameObject("Basement").transform;
            floor.SetParent(parent, false);

            BuildKit.Slab(floor, "Slab", Footprint, BasementY, palette.Concrete, GameLayers.LevelGeometry);
            Perimeter(floor, Footprint, BasementY, palette.Concrete);

            // Cellar divider, with one doorway near the south end.
            var doorways = BuildKit.Wall(floor, "Divider",
                new Vector2(0f, Footprint.yMin), new Vector2(0f, Footprint.yMax),
                BasementY, palette.Concrete, GameLayers.LevelGeometry, 4f);

            BuildKit.Bulb(floor, "CellarBulb", new Vector3(-6f, BasementY + 2.7f, -4f),
                new Color(1f, 0.80f, 0.55f), 3.2f, 13f);
            BuildKit.Bulb(floor, "CellarBulbN", new Vector3(-6f, BasementY + 2.7f, 5f),
                new Color(1f, 0.80f, 0.55f), 2.6f, 12f);
            BuildKit.Bulb(floor, "BoilerBulb", new Vector3(6f, BasementY + 2.7f, -5f),
                new Color(0.95f, 0.65f, 0.5f), 2.8f, 12f);
            BuildKit.Bulb(floor, "StairBulb", new Vector3(BasementWell.center.x, BasementY + 2.7f, 4f),
                new Color(1f, 0.85f, 0.6f), 2.4f, 10f);

            return doorways;
        }

        /// <summary>
        /// Ground floor: entrance hall on the north, living room west, kitchen
        /// south-east. The front door is visible from the hall, so the player can
        /// always see how far they are from the thing they are working towards.
        /// </summary>
        static BuildKit.Doorway[] BuildGround(Transform parent, Palette palette)
        {
            var floor = new GameObject("Ground").transform;
            floor.SetParent(parent, false);

            BuildKit.Slab(floor, "Slab", Footprint, GroundY, palette.Floor,
                GameLayers.LevelGeometry, BasementWell);

            // The flight from the cellar arrives at the north end of this hole, so
            // the other three sides get a rail rather than a drop.
            BuildKit.Railing(floor, "StairRail", BasementWell, GroundY,
                palette.Stair, GameLayers.LevelGeometry);

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
            var hallDoors = BuildKit.Wall(floor, "Wall_Hall",
                new Vector2(-4f, Footprint.yMin), new Vector2(-4f, 4f),
                GroundY, palette.Interior, GameLayers.LevelGeometry, 3f, 11f);

            // Kitchen divider.
            var kitchenDoors = BuildKit.Wall(floor, "Wall_Kitchen",
                new Vector2(-4f, -2f), new Vector2(Footprint.xMax, -2f),
                GroundY, palette.Interior, GameLayers.LevelGeometry, 5f, 13f);

            BuildKit.Bulb(floor, "HallBulb", new Vector3(2f, GroundY + 2.8f, 6f),
                new Color(1f, 0.86f, 0.62f), 3.6f, 14f);
            BuildKit.Bulb(floor, "LivingBulbN", new Vector3(-8f, GroundY + 2.8f, 4f),
                new Color(1f, 0.82f, 0.58f), 3.0f, 13f);
            BuildKit.Bulb(floor, "LivingBulbS", new Vector3(-8f, GroundY + 2.8f, -6f),
                new Color(1f, 0.82f, 0.58f), 3.0f, 13f);
            BuildKit.Bulb(floor, "KitchenBulb", new Vector3(6f, GroundY + 2.8f, -6f),
                new Color(0.96f, 0.93f, 0.82f), 3.0f, 13f);

            var doorways = new List<BuildKit.Doorway>();
            doorways.AddRange(hallDoors);
            doorways.AddRange(kitchenDoors);
            return doorways.ToArray();
        }

        /// <summary>
        /// First floor: three bedrooms and a landing. The player wakes up in the
        /// south-east bedroom, the furthest room from the front door.
        /// </summary>
        static BuildKit.Doorway[] BuildUpper(Transform parent, Palette palette)
        {
            var floor = new GameObject("Upper").transform;
            floor.SetParent(parent, false);

            BuildKit.Slab(floor, "Slab", Footprint, UpperY, palette.Floor,
                GameLayers.LevelGeometry, UpperWell);

            BuildKit.Railing(floor, "StairRail", UpperWell, UpperY,
                palette.Stair, GameLayers.LevelGeometry);

            Perimeter(floor, Footprint, UpperY, palette.Wall);

            // Landing runs east-west, with one door into each bedroom. There used
            // to be a third opening in the middle, at x = 1 - which is exactly
            // where the wall dividing the two bedrooms meets this one, so the
            // doorway was filled by the end of another wall.
            var landingDoors = BuildKit.Wall(floor, "Wall_Landing",
                new Vector2(Footprint.xMin, 1f), new Vector2(Footprint.xMax, 1f),
                UpperY, palette.Interior, GameLayers.LevelGeometry, 5f, 20f);

            var bedroomDoors = BuildKit.Wall(floor, "Wall_Bedrooms",
                new Vector2(1f, Footprint.yMin), new Vector2(1f, 1f),
                UpperY, palette.Interior, GameLayers.LevelGeometry, 7f);

            BuildKit.Bulb(floor, "LandingBulb", new Vector3(-2f, UpperY + 2.8f, 5f),
                new Color(1f, 0.83f, 0.6f), 3.4f, 14f);
            BuildKit.Bulb(floor, "LandingBulbE", new Vector3(7f, UpperY + 2.8f, 5f),
                new Color(1f, 0.83f, 0.6f), 2.8f, 12f);
            BuildKit.Bulb(floor, "BedroomBulb", new Vector3(6f, UpperY + 2.8f, -6f),
                new Color(0.96f, 0.80f, 0.62f), 2.8f, 12f);
            BuildKit.Bulb(floor, "BedroomBulbW", new Vector3(-6f, UpperY + 2.8f, -6f),
                new Color(0.96f, 0.80f, 0.62f), 2.8f, 12f);

            var doorways = new List<BuildKit.Doorway>();
            doorways.AddRange(landingDoors);
            doorways.AddRange(bedroomDoors);
            return doorways.ToArray();
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

            BuildKit.Railing(floor, "StairRail", AtticWell, AtticY,
                palette.Stair, GameLayers.LevelGeometry);

            Perimeter(floor, AtticFootprint, AtticY, palette.Wall);

            BuildKit.Slab(floor, "Roof", AtticFootprint, AtticY + BuildKit.FloorHeight,
                palette.Ceiling, GameLayers.LevelGeometry);

            BuildKit.Bulb(floor, "AtticBulb", new Vector3(0f, AtticY + 2.6f, 0f),
                new Color(0.88f, 0.72f, 0.52f), 3.0f, 14f);
            BuildKit.Bulb(floor, "AtticBulbS", new Vector3(0f, AtticY + 2.6f, -4f),
                new Color(0.88f, 0.72f, 0.52f), 2.2f, 10f);
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
                Vector3.forward, BuildKit.FloorHeight, BasementWell.height, MainStairWidth,
                palette.Stair, palette.Tread, GameLayers.LevelGeometry);

            // Ground up to the first floor, in the west stairwell.
            BuildKit.Stair(stairs, "Stair_Upper",
                new Vector3(UpperWell.center.x, GroundY, UpperWell.yMin),
                Vector3.forward, BuildKit.FloorHeight, UpperWell.height, MainStairWidth,
                palette.Stair, palette.Tread, GameLayers.LevelGeometry);

            // First floor up to the attic, through the south-west bedroom.
            BuildKit.Stair(stairs, "Stair_Attic",
                new Vector3(AtticWell.center.x, UpperY, AtticWell.yMin),
                Vector3.forward, BuildKit.FloorHeight, AtticWell.height, AtticStairWidth,
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
        // Furniture stands against walls now, with its back to the plaster. The
        // first pass left it floating a metre or two into rooms, and one wardrobe
        // sat squarely on the foot of the stairs to the first floor - which is
        // both the "map looks wrong" and part of the "I keep bumping into things".
        static readonly Placement[] Dressers =
        {
            new(-11.55f, BasementY, -6f, 90f),
            new(11.55f, BasementY, -7f, -90f),
            new(-11.55f, GroundY, -7f, 90f),
            new(11.55f, GroundY, -7f, -90f),
            new(-11.55f, UpperY, -7f, 90f),
            new(0f, AtticY, 6.6f, 180f),
        };

        static readonly Placement[] Wardrobes =
        {
            new(-11.42f, GroundY, -4f, 90f),
            new(11.42f, UpperY, -3f, -90f),
            new(5.5f, AtticY, 6.42f, 180f),
        };

        /// <summary>
        /// Beds, each with a gap under it to go flat in.
        ///
        /// The first is the one the player wakes in every morning, so the worst
        /// hiding place in the house is also the most familiar — which is the
        /// point. Panicking into the nearest bed is the mistake the game wants
        /// to be available.
        /// </summary>
        static readonly Placement[] Beds =
        {
            new(7f, UpperY, -7f, 315f),     // the guest room, where the day starts
            new(-9f, UpperY, -6f, 90f),     // the far bedroom
            new(9.5f, GroundY, -7.5f, 0f),  // the back room downstairs
        };

        sealed class Furniture
        {
            public readonly List<Drawer> Drawers = new();
        }

        static Furniture Furnish(Transform parent, List<BuildKit.Doorway> doorways)
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

            var bedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropPrefabDir}/Bed.prefab");
            if (bedPrefab != null) Place(bedPrefab, Beds, root);
            else Debug.LogWarning("[House] No Bed prefab — run Granny > Build Content first.");

            // Every opening the walls left gets a door, hung in the opening and
            // turned to match the wall it belongs to.
            for (var i = 0; i < doorways.Count; i++)
            {
                var door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab, root);
                door.name = $"Door_{i}";
                door.transform.SetPositionAndRotation(
                    doorways[i].Centre, Quaternion.Euler(0f, doorways[i].Yaw, 0f));
            }

            Debug.Log($"[House] {doorways.Count} interior doors hung in their own doorways");
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
            new(-5f, UpperY, 5f),
            new(6f, UpperY, -6f),
            new(-6f, BasementY, -5f),
            new(6f, BasementY, -5f),
            new(0f, AtticY, 0f),
        };

        // ------------------------------------------------------------------
        // Things that give the player away
        // ------------------------------------------------------------------

        /// <summary>
        /// Floorboards that creak, at the places everybody has to walk through.
        ///
        /// These are not scattered at random: every one of them sits on a point
        /// taken from a recorded run of the house, so each is known to be on
        /// walkable floor and on the natural route between two storeys. A board
        /// nobody ever steps on is not a trap, it is decoration.
        /// </summary>
        static readonly Vector3[] CreakyBoards =
        {
            new(1.2f, BasementY, -4.8f),    // cellar, mid-floor
            new(5.0f, BasementY, -0.8f),    // cellar, approaching the stairs
            new(7.2f, GroundY, 7.9f),       // top of the cellar stairs
            new(2.45f, GroundY, 5.1f),      // hall, crossing east to west
            new(-2.2f, GroundY, 2.3f),      // middle of the ground floor
            new(-6.8f, GroundY, 1.8f),      // foot of the stairs up
            new(-5.1f, UpperY, 7.0f),       // head of the stairs up
            new(0f, UpperY, 4.8f),          // landing
            new(5.1f, UpperY, 2.5f),        // outside the bedrooms
        };

        /// <summary>In the hall, on the last stretch before the front door.</summary>
        static readonly Vector3 TripwirePosition = new(0f, GroundY, 8.2f);

        /// <summary>
        /// Where she puts a trap when a search comes up empty.
        ///
        /// Every one is a place the player has to pass through rather than a
        /// place they might wander into — the heads and feet of the two flights,
        /// the hall, the landing. Taken from the same recorded run as the
        /// floorboards, so each is known to be reachable floor.
        /// </summary>
        static readonly Vector3[] TrapSpots =
        {
            new(5.0f, BasementY, -0.8f),    // cellar, at the foot of the stairs
            new(-6.0f, BasementY, -5.0f),   // cellar, the far end
            new(7.2f, GroundY, 7.9f),       // head of the cellar stairs
            new(-6.8f, GroundY, 1.8f),      // foot of the stairs up
            new(0f, GroundY, 6.5f),         // hall, in front of the door
            new(-2.2f, GroundY, 2.3f),      // the middle of the ground floor
            new(-5.1f, UpperY, 7.0f),       // head of the stairs up
            new(0f, UpperY, 4.8f),          // the landing
            new(5.1f, UpperY, 2.5f),        // outside the bedrooms
        };

        static void BuildNoiseMakers()
        {
            var root = new GameObject("NoiseMakers").transform;

            for (var i = 0; i < CreakyBoards.Length; i++)
            {
                var board = new GameObject($"CreakyFloor_{i}");
                board.transform.SetParent(root, false);
                board.transform.position = CreakyBoards[i] + new Vector3(0f, 0.2f, 0f);

                var box = board.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(2.2f, 0.5f, 2.2f);

                board.AddComponent<CreakyFloor>();
            }

            var wire = new GameObject("TripwireBell");
            wire.transform.SetParent(root, false);
            wire.transform.position = TripwirePosition + new Vector3(0f, 0.3f, 0f);

            // Shin height and the full width of the opening: you step over it or
            // you ring it, and crouching does not help.
            var wireBox = wire.AddComponent<BoxCollider>();
            wireBox.isTrigger = true;
            wireBox.size = new Vector3(3.2f, 0.6f, 0.3f);

            wire.AddComponent<TripwireBell>();

            BuildWorkbench();

            var spots = new GameObject("TrapSpots").transform;
            spots.SetParent(root, false);

            for (var i = 0; i < TrapSpots.Length; i++)
            {
                var spot = new GameObject($"TrapSpot_{i}");
                spot.transform.SetParent(spots, false);
                spot.transform.position = TrapSpots[i];
                spot.AddComponent<TrapSpot>();
            }

            Debug.Log(
                $"[House] {CreakyBoards.Length} creaky boards, 1 tripwire and " +
                $"{TrapSpots.Length} trap spots placed");
        }

        /// <summary>
        /// The workbench, in the cellar — the furthest point from everything, so
        /// each of the three parts is a full crossing of the house.
        /// </summary>
        static void BuildWorkbench()
        {
            var bench = new GameObject("Workbench");
            bench.transform.position = new Vector3(-9.5f, BasementY, -7f);

            BuildKit.Box(bench.transform, "Top",
                new Vector3(0f, 0.85f, 0f), new Vector3(2.0f, 0.1f, 0.8f),
                BuildKit.Material("PropBench", new Color(0.30f, 0.24f, 0.18f)),
                GameLayers.Prop);

            var reach = bench.AddComponent<BoxCollider>();
            reach.isTrigger = true;
            reach.center = new Vector3(0f, 0.9f, 0f);
            reach.size = new Vector3(2.2f, 1.6f, 1.4f);

            var workbench = bench.AddComponent<WeaponBench>();

            var so = new SerializedObject(workbench);
            so.FindProperty("weapon").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemDataDir}/Item_crossbow.asset");
            so.FindProperty("dart").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemDataDir}/Item_dart.asset");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

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
            var ids = new[]
            {
                "Item_hammer", "Item_wirecutters", "Item_key_front",
                // The three trips that buy the crossbow.
                "Item_bow_stock", "Item_bow_limb", "Item_bow_cord",
            };
            required.arraySize = ids.Length;

            for (var i = 0; i < ids.Length; i++)
                required.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemDataDir}/{ids[i]}.asset");

            var optional = so.FindProperty("optionalItems");
            var extras = new[] { "Item_bottle", "Item_jar" };
            optional.arraySize = extras.Length;

            for (var i = 0; i < extras.Length; i++)
                optional.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemDataDir}/{extras[i]}.asset");

            var containers = so.FindProperty("containers");
            containers.arraySize = furniture.Drawers.Count;
            for (var i = 0; i < furniture.Drawers.Count; i++)
                containers.GetArrayElementAtIndex(i).objectReferenceValue = furniture.Drawers[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The bake uses the project's agent type, not the NavMeshAgent component,
        /// so this is the only place the agent's size can be set.
        ///
        /// The default humanoid is 0.5 m across, and the walkable surface is eroded
        /// by that on both sides of every wall. A 1.5 m doorway came out as 0.5 m
        /// of path, and a 1.3 m one as 0.3 m — narrow enough for the voxeliser to
        /// close it altogether, which left whole rooms unreachable and is a large
        /// part of why she never came upstairs.
        /// </summary>
        static void EnsureAgentSettings()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
            if (assets.Length == 0)
            {
                Debug.LogWarning("[House] NavMeshAreas.asset not found; agent size left at defaults.");
                return;
            }

            var settings = new SerializedObject(assets[0]);
            var list = settings.FindProperty("m_Settings");

            if (list == null || list.arraySize == 0)
            {
                Debug.LogWarning("[House] No NavMesh agent types found; agent size left at defaults.");
                return;
            }

            var humanoid = list.GetArrayElementAtIndex(0);
            humanoid.FindPropertyRelative("agentRadius").floatValue = 0.3f;
            humanoid.FindPropertyRelative("agentHeight").floatValue = 1.9f;
            humanoid.FindPropertyRelative("agentClimb").floatValue = 0.4f;
            humanoid.FindPropertyRelative("agentSlope").floatValue = 45f;
            settings.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[House] NavMesh agent: radius 0.3, height 1.9");
        }

        static void BakeNavigation()
        {
            EnsureAgentSettings();

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
