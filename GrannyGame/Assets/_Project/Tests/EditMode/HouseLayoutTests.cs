using System.Linq;
using Granny.Core;
using Granny.Gameplay;
using Granny.Gameplay.Interaction;
using Granny.Gameplay.Player;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Granny.Tests
{
    /// <summary>
    /// The house is generated, so it can silently regress into something
    /// unplayable — a floor with no way up, a tool hidden behind geometry, a
    /// NavMesh that never baked. These load the real scene and check it holds
    /// together.
    /// </summary>
    public class HouseLayoutTests
    {
        const string ScenePath = "Assets/_Project/Scenes/House.unity";

        [OneTimeSetUp]
        public void OpenScene() => EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        static T Find<T>() where T : Object => Object.FindAnyObjectByType<T>();

        [Test]
        public void TheSceneIsStoredAsText()
        {
            // A NavMeshData living inside the scene cannot be serialised as text,
            // so Unity silently rewrites the whole scene as binary. .gitattributes
            // marks *.unity as text, git then "normalises" line endings inside the
            // binary payload, and the level is destroyed on the next checkout.
            // It has happened once; this makes it impossible to happen quietly.
            var full = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath)!.FullName, ScenePath);

            var header = new byte[5];
            using (var stream = System.IO.File.OpenRead(full))
                _ = stream.Read(header, 0, header.Length);

            Assert.AreEqual(
                "%YAML", System.Text.Encoding.ASCII.GetString(header),
                "House.unity is not text YAML. Check that the NavMesh bake is stored " +
                "as its own asset rather than inside the scene.");
        }

        [Test]
        public void TheHouseExists()
        {
            var house = GameObject.Find("House");
            Assert.IsNotNull(house, "No 'House' root. Run Granny > Build House.");
            Assert.Greater(house.GetComponentsInChildren<MeshRenderer>().Length, 100,
                "The house looks far too sparse to be four storeys.");
        }

        [Test]
        public void EveryStoreyIsBuilt()
        {
            var house = GameObject.Find("House").transform;

            foreach (var storey in new[] { "Basement", "Ground", "Upper", "Attic" })
                Assert.IsNotNull(house.Find(storey), $"Storey '{storey}' is missing.");
        }

        [Test]
        public void ThreeFlightsOfStairsConnectTheFourStoreys()
        {
            var stairs = GameObject.Find("House").transform.Find("Stairs");
            Assert.IsNotNull(stairs, "No stairs at all.");

            // Four storeys need three flights, or a floor is unreachable.
            var flights = Enumerable.Range(0, stairs.childCount)
                .Select(i => stairs.GetChild(i))
                .Count(t => t.name.StartsWith("Stair_") && !t.name.Contains("tread"));

            Assert.AreEqual(3, flights, "Four storeys need exactly three flights.");
        }

        [Test]
        public void StairsAreShallowEnoughToWalkUp()
        {
            var stairs = GameObject.Find("House").transform.Find("Stairs");

            foreach (Transform child in stairs)
            {
                if (!child.name.StartsWith("Stair_") || child.name.Contains("tread")) continue;

                // The ramp's own up-vector against world up gives the pitch. Above
                // the agent's slope limit and Granny simply cannot follow you up.
                var pitch = Vector3.Angle(child.up, Vector3.up);
                Assert.Less(pitch, 45f, $"{child.name} climbs at {pitch:F1} degrees, too steep to bake.");
            }
        }

        [Test]
        public void ANavMeshIsBakedAndCoversEveryStorey()
        {
            Assert.IsNotNull(Find<NavMeshSurface>(), "No NavMeshSurface in the scene.");

            var triangulation = NavMesh.CalculateTriangulation();
            Assert.Greater(triangulation.vertices.Length, 0, "The NavMesh is empty; it never baked.");

            // Every storey needs walkable ground, or Granny can never go there.
            foreach (var (name, y) in new[]
                     {
                         ("basement", -3.2f), ("ground", 0f), ("upper", 3.2f), ("attic", 6.4f),
                     })
            {
                var reached = triangulation.vertices.Any(v => Mathf.Abs(v.y - y) < 0.6f);
                Assert.IsTrue(reached, $"No NavMesh on the {name} floor.");
            }
        }

        [Test]
        public void PatrolMarkersAreSpreadOverEveryStorey()
        {
            var markers = GameObject.FindGameObjectsWithTag("SpawnPoint");
            Assert.Greater(markers.Length, 4, "Too few patrol markers for a four-storey house.");

            var storeys = markers.Select(m => Mathf.RoundToInt(m.transform.position.y / 3.2f)).Distinct();
            Assert.GreaterOrEqual(storeys.Count(), 4,
                "Granny only patrols some floors; the rest would be free real estate.");
        }

        [Test]
        public void EnoughContainersExistToHideEveryRequiredTool()
        {
            var spawner = Find<ItemSpawner>();
            Assert.IsNotNull(spawner, "No ItemSpawner; tool positions would never be randomised.");

            var drawers = Object.FindObjectsByType<Drawer>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(drawers.Length, 3,
                "Three tools need at least three containers or the run is unwinnable.");
        }

        [Test]
        public void ThereIsSomewhereToHideOnMoreThanOneFloor()
        {
            var spots = Object.FindObjectsByType<HidingSpot>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(spots.Length, 2, "One wardrobe in the whole house is not a mechanic.");

            var storeys = spots.Select(s => Mathf.RoundToInt(s.transform.position.y / 3.2f)).Distinct();
            Assert.GreaterOrEqual(storeys.Count(), 2, "Hiding places should not all be on one floor.");
        }

        [Test]
        public void ThePlayerWakesUpInsideTheHouse()
        {
            var bed = GameObject.Find("Bed");
            Assert.IsNotNull(bed, "No bed marker; there would be nowhere to wake up.");

            var footprint = new Rect(-12f, -10f, 24f, 20f);
            Assert.IsTrue(
                footprint.Contains(new Vector2(bed.transform.position.x, bed.transform.position.z)),
                "The bed is outside the house footprint.");
        }

        [Test]
        public void TheDirectorKnowsAboutTheBedAndTheHunter()
        {
            var director = Find<GameDirector>();
            Assert.IsNotNull(director, "No GameDirector; nothing would advance the day.");

            var so = new SerializedObject(director);
            foreach (var field in new[] { "granny", "bed", "grannySpawn", "difficulty" })
                Assert.IsNotNull(
                    so.FindProperty(field).objectReferenceValue,
                    $"GameDirector.{field} is unassigned.");
        }

        [Test]
        public void TheFrontDoorIsStillFastenedShut()
        {
            var door = Find<EscapeDoor>();
            Assert.IsNotNull(door, "No escape door. Run Granny > Build Escape Door.");
            Assert.AreEqual(3, door.StagesRemaining, "The front door should start with three fastenings.");
        }

        // ------------------------------------------------------------------
        // Things that were actually wrong, and must not come back
        // ------------------------------------------------------------------

        static Vector3 OnNavMesh(Vector3 position, string what)
        {
            Assert.IsTrue(
                NavMesh.SamplePosition(position, out var hit, 3f, NavMesh.AllAreas),
                $"{what} at {position} has no NavMesh anywhere near it.");

            return hit.position;
        }

        [Test]
        public void SheCanWalkFromWhereSheStartsToEveryPlaceThatMatters()
        {
            // The regression this pins: with the default 0.5 m agent, doorways
            // eroded shut and whole floors became unreachable. She would stand in
            // the cellar all run, which read as "the AI is broken".
            var spawn = GameObject.Find("GrannySpawn");
            Assert.IsNotNull(spawn, "No GrannySpawn marker.");

            var from = OnNavMesh(spawn.transform.position, "Her starting point");

            var destinations = new System.Collections.Generic.List<(string name, Vector3 point)>
            {
                ("the bed", GameObject.Find("Bed").transform.position),
                ("the front door", Find<EscapeDoor>().transform.position + Vector3.back * 1.5f),
            };

            foreach (var marker in GameObject.FindGameObjectsWithTag("SpawnPoint"))
                destinations.Add((marker.name, marker.transform.position));

            foreach (var (name, point) in destinations)
            {
                var path = new NavMeshPath();
                NavMesh.CalculatePath(from, OnNavMesh(point, name), NavMesh.AllAreas, path);

                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status,
                    $"She cannot reach {name}. A door is too narrow, or a floor is cut off.");
            }
        }

        /// <summary>
        /// Rebuilding the house has to replace what it built last time, not add
        /// to it. A root left off the builder's cleanup list is not noticed as a
        /// missing delete — it is noticed weeks later as a second copy of
        /// something standing in a doorway, and the only symptom is a floor that
        /// has quietly become unreachable.
        /// </summary>
        [Test]
        public void TheHouseIsBuiltOnceNotTwice()
        {
            var seen = new System.Collections.Generic.Dictionary<string, int>();

            foreach (var root in UnityEngine.SceneManagement.SceneManager
                         .GetActiveScene().GetRootGameObjects())
            {
                seen.TryGetValue(root.name, out var count);
                seen[root.name] = count + 1;
            }

            foreach (var (name, count) in seen)
                Assert.AreEqual(1, count,
                    $"There are {count} roots called '{name}'. The builder is " +
                    "leaving the old one behind instead of replacing it.");
        }

        /// <summary>
        /// The crawl between the cellar and the garage is the one place in the
        /// house she cannot follow you into, and it is not a flag or a special
        /// case — the ceiling is 1.2 m and she is 1.9 m to the bake, so no
        /// walkable surface is generated in there at all.
        ///
        /// This checks the mechanic the way the game does: by measuring, not by
        /// trusting that a value somewhere is still what it was.
        /// </summary>
        [Test]
        public void SheCannotGetIntoTheCrawl()
        {
            var crawl = GameObject.Find("House").transform.Find("Garage/CrawlFloor");
            Assert.IsNotNull(crawl, "No crawl was built.");

            var inside = crawl.position + Vector3.up * 0.4f;

            Assert.IsFalse(NavMesh.SamplePosition(inside, out _, 0.55f, NavMesh.AllAreas),
                "There is walkable surface inside the crawl. She can follow the " +
                "player through it, and the only safe route in the house is gone.");
        }

        /// <summary>
        /// The other half of the same mechanic: a crouched player has to fit.
        ///
        /// This reads both numbers rather than trusting either — the crawl is
        /// only a shortcut if it is taller than a crouch and shorter than a
        /// stand, and both of those are tuning values somebody will change.
        /// </summary>
        [Test]
        public void ACrouchedPlayerFitsThroughItAndAStandingOneDoesNot()
        {
            var roof = GameObject.Find("House").transform.Find("Garage/CrawlRoof");
            var floor = GameObject.Find("House").transform.Find("Garage/CrawlFloor");
            Assert.IsNotNull(roof, "No crawl roof.");
            Assert.IsNotNull(floor, "No crawl floor.");

            var headroom = roof.GetComponent<Renderer>().bounds.min.y
                           - floor.GetComponent<Renderer>().bounds.max.y;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Player.prefab");
            Assert.IsNotNull(prefab, "No player prefab.");

            var motor = new SerializedObject(prefab.GetComponent<PlayerMotor>());
            var crouched = motor.FindProperty("crouchHeight").floatValue;
            var standing = motor.FindProperty("standingHeight").floatValue;

            Assert.Greater(headroom, crouched,
                $"The crawl is {headroom:F2} m and a crouched player is {crouched:F2} m. " +
                "Nobody can get through it.");

            Assert.Less(headroom, standing,
                $"The crawl is {headroom:F2} m and a standing player is {standing:F2} m. " +
                "You can walk through it, so crouching costs nothing and she can follow.");
        }

        /// <summary>
        /// ...but the garage itself is not sealed off from her. The crawl is a
        /// shortcut the player owns, not a room she is banned from, or hiding in
        /// the garage would simply end the game.
        /// </summary>
        [Test]
        public void SheCanStillWalkRoundToTheGarage()
        {
            var spawn = GameObject.Find("GrannySpawn");
            var bench = Find<WeaponBench>();

            var from = OnNavMesh(spawn.transform.position, "Her starting point");
            var to = OnNavMesh(bench.transform.position + Vector3.left * 1.5f, "the workbench");

            var path = new NavMeshPath();
            NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path);

            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status,
                "She cannot reach the garage at all. The passage beside the crawl " +
                "is there so the crawl is a shortcut rather than a sanctuary.");
        }

        [Test]
        public void NoFurnitureStandsOnTheStairs()
        {
            // A wardrobe used to sit squarely on the foot of the stairs to the
            // first floor, which blocked the only way up from the living room.
            var stairs = GameObject.Find("House").transform.Find("Stairs");
            var ramps = new System.Collections.Generic.List<Bounds>();

            foreach (Transform child in stairs)
            {
                if (!child.name.StartsWith("Stair_") || child.name.Contains("tread")) continue;
                if (!child.TryGetComponent<Renderer>(out var renderer)) continue;

                var bounds = renderer.bounds;
                bounds.Expand(new Vector3(0.6f, 0f, 0.6f));   // keep the foot clear too
                ramps.Add(bounds);
            }

            Assert.IsNotEmpty(ramps, "No stair ramps found to test against.");

            var furnishings = GameObject.Find("Furnishings");
            Assert.IsNotNull(furnishings, "No furniture in the scene.");

            foreach (var collider in furnishings.GetComponentsInChildren<Collider>())
            {
                foreach (var ramp in ramps)
                    Assert.IsFalse(ramp.Intersects(collider.bounds),
                        $"{collider.transform.parent?.name}/{collider.name} is standing on the stairs.");
            }
        }

        [Test]
        public void EveryInteriorDoorHangsInAnOpening()
        {
            // Doors used to be placed from a hand-written list that drifted out of
            // step with the walls: some sat across their own doorway, and one stood
            // in the middle of a room with no wall at all.
            Physics.SyncTransforms();

            var doors = GameObject.Find("Furnishings").GetComponentsInChildren<HingeDoor>();
            Assert.Greater(doors.Length, 3, "The house should have interior doors.");

            foreach (var door in doors)
            {
                var centre = door.transform.position + Vector3.up * 1.2f;
                var across = door.transform.right;

                foreach (var side in new[] { across, -across })
                    Assert.IsTrue(
                        Physics.Raycast(centre, side, 2.2f, GameLayers.SightBlockers,
                            QueryTriggerInteraction.Ignore),
                        $"{door.name} has no wall beside it — it is standing in the open.");

                Assert.IsFalse(
                    Physics.CheckBox(centre, new Vector3(0.45f, 0.8f, 0.12f),
                        door.transform.rotation, 1 << GameLayers.LevelGeometry,
                        QueryTriggerInteraction.Ignore),
                    $"{door.name} is turned across its own doorway, not hung in it.");
            }
        }

        [Test]
        public void WallsStopBelowTheFloorAbove()
        {
            // A wall that reaches the top surface of the slab above leaves two
            // coplanar faces, and they flicker against each other along every wall
            // line on the storey above.
            var ground = GameObject.Find("House").transform.Find("Ground");

            foreach (Transform child in ground)
            {
                if (!child.name.StartsWith("Wall_")) continue;
                if (!child.TryGetComponent<Renderer>(out var renderer)) continue;

                Assert.Less(renderer.bounds.max.y, HouseLayout.FloorY(HouseLayout.Upper) - 0.05f,
                    $"{child.name} reaches into the floor above.");
            }
        }
    }
}
