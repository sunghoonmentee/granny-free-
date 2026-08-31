using System.Linq;
using Granny.Core;
using Granny.Gameplay;
using Granny.Gameplay.Interaction;
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
    }
}
