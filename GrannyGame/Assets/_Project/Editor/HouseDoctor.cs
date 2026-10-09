using System.Linq;
using Granny.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Granny.EditorTools
{
    /// <summary>
    /// Says which parts of the house she can and cannot walk to, and why.
    ///
    /// The layout test reports the first thing it cannot reach and stops, which
    /// is the right thing for a test and the wrong thing for working out what a
    /// rebuilt floor plan has broken. This walks the whole list and prints it.
    ///
    ///     ./tools/unity.ps1 run -Method Granny.EditorTools.HouseDoctor.Run
    /// </summary>
    public static class HouseDoctor
    {
        [MenuItem("Granny/Check the House", priority = 2)]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/House.unity", OpenSceneMode.Single);

            var spawn = GameObject.Find("GrannySpawn");
            if (spawn == null)
            {
                Debug.LogError("[Doctor] No GrannySpawn marker.");
                return;
            }

            if (!Sample(spawn.transform.position, out var from, out var fromOffset))
            {
                Debug.LogError($"[Doctor] Her own starting point is not on the NavMesh: {spawn.transform.position}");
                return;
            }

            Debug.Log($"[Doctor] from GrannySpawn {spawn.transform.position} -> {from} (moved {fromOffset:F2} m)");

            foreach (var (name, point) in Destinations())
            {
                if (!Sample(point, out var to, out var offset))
                {
                    Debug.LogError($"[Doctor] {name}: {point} has no NavMesh anywhere near it");
                    continue;
                }

                var path = new NavMeshPath();
                NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path);

                var floor = HouseLayout.FloorName(HouseLayout.FloorOf(to));
                var verdict = path.status == NavMeshPathStatus.PathComplete ? "ok" : path.status.ToString();

                Debug.Log($"[Doctor] {name} ({floor}) {point} -> {to} moved {offset:F2} m : {verdict}");
            }

            ReportCoverage(from);
            ReportStairs(from);
            ReportObstructions();

            Debug.Log("[Doctor] done");
        }

        /// <summary>
        /// Walks a grid over every storey and says, per room-sized square, whether
        /// there is floor she can stand on and whether she can get there from
        /// where she starts. A floor plan that looks right and bakes wrong shows
        /// up here as a block of dashes.
        /// </summary>
        static void ReportCoverage(Vector3 from)
        {
            var floors = new (string name, float y)[]
            {
                ("지하", -HouseLayout.FloorHeight),
                ("1층", 0f),
                ("2층", HouseLayout.FloorHeight),
                ("다락", HouseLayout.FloorHeight * 2f),
            };

            foreach (var (name, y) in floors)
            {
                var rows = new System.Text.StringBuilder();
                rows.AppendLine($"[Doctor] {name} — '#' reachable, 'o' floor but cut off, '.' no floor");

                for (var z = 9f; z >= -9f; z -= 2f)
                {
                    rows.Append("  ");

                    for (var x = -11f; x <= 11f; x += 2f)
                    {
                        if (!NavMesh.SamplePosition(new Vector3(x, y + 0.2f, z), out var hit, 1.2f,
                                NavMesh.AllAreas))
                        {
                            rows.Append('.');
                            continue;
                        }

                        var path = new NavMeshPath();
                        NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path);
                        rows.Append(path.status == NavMeshPathStatus.PathComplete ? '#' : 'o');
                    }

                    rows.AppendLine();
                }

                Debug.Log(rows.ToString());
            }
        }

        static System.Collections.Generic.IEnumerable<(string, Vector3)> Destinations()
        {
            var bed = GameObject.Find("Bed");
            if (bed != null) yield return ("the bed", bed.transform.position);

            var door = Object.FindObjectsByType<Gameplay.Interaction.EscapeDoor>(FindObjectsSortMode.None)
                .FirstOrDefault();
            if (door != null) yield return ("the front door", door.transform.position + Vector3.back * 1.5f);

            foreach (var marker in GameObject.FindGameObjectsWithTag("SpawnPoint"))
                yield return (marker.name, marker.transform.position);
        }

        /// <summary>
        /// Lists what is actually standing in the air above each flight.
        ///
        /// When a staircase bakes as two halves the cause is nearly always
        /// something overhead that nobody thought of as being over a staircase —
        /// the floor it passes through, a beam, a bit of furniture upstairs.
        /// </summary>
        static void ReportObstructions()
        {
            var stairs = GameObject.Find("House")?.transform.Find("Stairs");
            if (stairs == null) return;

            foreach (Transform flight in stairs)
            {
                if (!flight.name.StartsWith("Stair_") || flight.name.Contains("tread")) continue;
                if (!flight.TryGetComponent<Renderer>(out var renderer)) continue;

                var bounds = renderer.bounds;

                for (var t = 0.1f; t <= 0.9f; t += 0.2f)
                {
                    var surface = new Vector3(
                        bounds.center.x,
                        Mathf.Lerp(bounds.min.y, bounds.max.y, t),
                        Mathf.Lerp(bounds.min.z, bounds.max.z, t));

                    // The box an agent standing on this step would occupy.
                    var centre = surface + Vector3.up * 1.1f;
                    var hits = Physics.OverlapBox(centre, new Vector3(0.3f, 0.9f, 0.3f),
                        Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

                    foreach (var hit in hits)
                        Debug.Log($"[Doctor] {flight.name} at z {surface.z:F1}: " +
                                  $"{Path(hit.transform)} is in the way");
                }
            }
        }

        static string Path(Transform t) =>
            t.parent == null ? t.name : $"{Path(t.parent)}/{t.name}";

        /// <summary>
        /// Walks up each flight a step at a time. A staircase that bakes as two
        /// disconnected halves, or not at all, is the usual reason a whole floor
        /// goes dark — and it is invisible in a coverage map, because both ends
        /// have perfectly good floor.
        /// </summary>
        static void ReportStairs(Vector3 from)
        {
            var stairs = GameObject.Find("House")?.transform.Find("Stairs");
            if (stairs == null)
            {
                Debug.LogError("[Doctor] No Stairs group.");
                return;
            }

            foreach (Transform flight in stairs)
            {
                if (!flight.TryGetComponent<Renderer>(out var renderer)) continue;

                var bounds = renderer.bounds;
                var line = new System.Text.StringBuilder($"[Doctor] {flight.name}: ");

                // Up the middle of the flight, from the bottom tread to the top.
                for (var t = 0f; t <= 1.001f; t += 0.1f)
                {
                    var point = new Vector3(
                        bounds.center.x,
                        Mathf.Lerp(bounds.min.y, bounds.max.y, t) + 0.3f,
                        Mathf.Lerp(bounds.min.z, bounds.max.z, t));

                    if (!NavMesh.SamplePosition(point, out var hit, 0.8f, NavMesh.AllAreas))
                    {
                        line.Append('.');
                        continue;
                    }

                    var path = new NavMeshPath();
                    NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path);
                    line.Append(path.status == NavMeshPathStatus.PathComplete ? '#' : 'o');
                }

                line.Append($"   (y {bounds.min.y:F1} to {bounds.max.y:F1}, z {bounds.min.z:F1} to {bounds.max.z:F1})");
                Debug.Log(line.ToString());
            }
        }

        /// <summary>
        /// How far the point had to move to land on walkable floor. A big number
        /// means the thing is sitting inside furniture or off the edge entirely.
        /// </summary>
        static bool Sample(Vector3 wanted, out Vector3 found, out float distance)
        {
            if (NavMesh.SamplePosition(wanted, out var hit, 6f, NavMesh.AllAreas))
            {
                found = hit.position;
                distance = Vector3.Distance(wanted, hit.position);
                return true;
            }

            found = wanted;
            distance = -1f;
            return false;
        }
    }
}
