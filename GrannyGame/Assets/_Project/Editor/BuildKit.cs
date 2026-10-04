using UnityEditor;
using UnityEngine;

namespace Granny.EditorTools
{
    /// <summary>
    /// Shared primitives for the level builders. Everything the house is made of
    /// comes out of here, so wall thickness, door sizes and material handling stay
    /// consistent across floors instead of being retyped per builder.
    /// </summary>
    public static class BuildKit
    {
        public const float MaterialDirTrailing = 0f;
        const string MaterialDir = "Assets/_Project/Art/Materials";

        /// <summary>Storey-to-storey height. Every floor uses the same one.</summary>
        public const float FloorHeight = Granny.Core.HouseLayout.FloorHeight;

        public const float WallThickness = 0.25f;
        public const float SlabThickness = 0.3f;

        /// <summary>
        /// Doorways have to survive the NavMesh bake: the walkable area is eroded
        /// by the agent's radius on both sides, so a 1.5 m opening with a 0.3 m
        /// agent leaves 0.9 m of path. At the old 1.3 m with the default 0.5 m
        /// agent it came to 0.3 m, which the voxeliser closed off entirely - and
        /// that is one of the reasons she never left the cellar.
        /// </summary>
        public const float DoorWidth = 1.5f;
        public const float DoorHeight = 2.15f;

        // ------------------------------------------------------------------
        // Materials
        // ------------------------------------------------------------------

        public static Material Material(string name, Color colour, float smoothness = 0.08f)
        {
            var path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new UnityEngine.Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", colour);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------
        // Geometry
        // ------------------------------------------------------------------

        /// <summary>An axis-aligned box with a collider, on the given layer.</summary>
        public static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size,
            Material material, int layer, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = size;
            go.layer = layer;
            go.isStatic = true;

            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>A box with no collider — decoration the NavMesh and physics ignore.</summary>
        public static GameObject Decal(Transform parent, string name, Vector3 centre, Vector3 size,
            Material material, Quaternion? rotation = null)
        {
            var go = Box(parent, name, centre, size, material, 0, rotation);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>
        /// A floor slab, optionally with a rectangular hole cut for a stairwell.
        /// The hole is made by emitting up to four boxes around it rather than by
        /// mesh boolean work, which keeps every piece a clean box collider.
        /// </summary>
        public static void Slab(Transform parent, string name, Rect area, float topY,
            Material material, int layer, Rect? hole = null)
        {
            var centreY = topY - SlabThickness * 0.5f;

            if (hole == null)
            {
                Box(parent, name, new Vector3(area.center.x, centreY, area.center.y),
                    new Vector3(area.width, SlabThickness, area.height), material, layer);
                return;
            }

            var h = hole.Value;

            // South strip, north strip, then the two side strips between them.
            if (h.yMin > area.yMin)
                Box(parent, $"{name}_S", new Vector3(area.center.x, centreY, (area.yMin + h.yMin) * 0.5f),
                    new Vector3(area.width, SlabThickness, h.yMin - area.yMin), material, layer);

            if (h.yMax < area.yMax)
                Box(parent, $"{name}_N", new Vector3(area.center.x, centreY, (h.yMax + area.yMax) * 0.5f),
                    new Vector3(area.width, SlabThickness, area.yMax - h.yMax), material, layer);

            if (h.xMin > area.xMin)
                Box(parent, $"{name}_W", new Vector3((area.xMin + h.xMin) * 0.5f, centreY, h.center.y),
                    new Vector3(h.xMin - area.xMin, SlabThickness, h.height), material, layer);

            if (h.xMax < area.xMax)
                Box(parent, $"{name}_E", new Vector3((h.xMax + area.xMax) * 0.5f, centreY, h.center.y),
                    new Vector3(area.xMax - h.xMax, SlabThickness, h.height), material, layer);
        }

        /// <summary>
        /// An opening left in a wall, reported back so a door can be hung in it.
        /// Hanging doors from these rather than from a separate list of positions
        /// is what makes a door in the middle of a room impossible.
        /// </summary>
        public readonly struct Doorway
        {
            /// <summary>Centre of the opening, at floor level.</summary>
            public readonly Vector3 Centre;

            /// <summary>Yaw for a door whose leaf spans its own local X.</summary>
            public readonly float Yaw;

            public readonly float Width;

            public Doorway(Vector3 centre, float yaw, float width)
            {
                Centre = centre;
                Yaw = yaw;
                Width = width;
            }
        }

        /// <summary>
        /// A wall from a to b at floor level <paramref name="floorY"/>, with a
        /// doorway cut at each distance in <paramref name="doorCentres"/> measured
        /// along the run. Each doorway keeps a lintel above it, so the wall still
        /// reads as one surface and Granny cannot path over the top.
        ///
        /// The wall stops at the underside of the slab above rather than reaching
        /// its top surface: a wall that ends exactly level with the floor above
        /// leaves two coplanar faces, which flicker against each other along every
        /// wall line on the storey above.
        /// </summary>
        public static Doorway[] Wall(Transform parent, string name, Vector2 a, Vector2 b,
            float floorY, Material material, int layer, params float[] doorCentres)
        {
            var start = new Vector3(a.x, floorY, a.y);
            var end = new Vector3(b.x, floorY, b.y);
            var run = end - start;
            var length = run.magnitude;

            if (length < 0.01f) return System.Array.Empty<Doorway>();

            var direction = run / length;
            var rotation = Quaternion.LookRotation(direction, Vector3.up);
            var height = FloorHeight - SlabThickness;

            System.Array.Sort(doorCentres);

            var doorways = new System.Collections.Generic.List<Doorway>();
            var cursor = 0f;
            var index = 0;

            foreach (var centre in doorCentres)
            {
                var gapStart = Mathf.Clamp(centre - DoorWidth * 0.5f, 0f, length);
                var gapEnd = Mathf.Clamp(centre + DoorWidth * 0.5f, 0f, length);

                if (gapStart > cursor)
                    Segment(parent, $"{name}_{index++}", start, direction, rotation,
                        cursor, gapStart, floorY, height, material, layer);

                // Lintel over the opening.
                var lintelHeight = height - DoorHeight;
                if (lintelHeight > 0.01f)
                {
                    var mid = start + direction * ((gapStart + gapEnd) * 0.5f);
                    Box(parent, $"{name}_lintel{index}",
                        new Vector3(mid.x, floorY + DoorHeight + lintelHeight * 0.5f, mid.z),
                        new Vector3(WallThickness, lintelHeight, gapEnd - gapStart),
                        material, layer, rotation);
                }

                // A door leaf spans its own local X, so it is turned a quarter turn
                // from the wall's own facing.
                doorways.Add(new Doorway(
                    start + direction * ((gapStart + gapEnd) * 0.5f),
                    rotation.eulerAngles.y - 90f,
                    gapEnd - gapStart));

                cursor = gapEnd;
            }

            if (cursor < length)
                Segment(parent, $"{name}_{index}", start, direction, rotation,
                    cursor, length, floorY, height, material, layer);

            return doorways.ToArray();
        }

        /// <summary>
        /// A low wall around three sides of a stairwell opening, leaving the side
        /// the flight arrives at clear. Without it the hole is simply a pit in the
        /// floor that the player walks into.
        /// </summary>
        public static void Railing(Transform parent, string name, Rect hole, float floorY,
            Material material, int layer, float height = 1.0f)
        {
            const float thickness = 0.1f;
            var centreY = floorY + height * 0.5f;

            Box(parent, $"{name}_W", new Vector3(hole.xMin - thickness * 0.5f, centreY, hole.center.y),
                new Vector3(thickness, height, hole.height), material, layer);

            Box(parent, $"{name}_E", new Vector3(hole.xMax + thickness * 0.5f, centreY, hole.center.y),
                new Vector3(thickness, height, hole.height), material, layer);

            Box(parent, $"{name}_S", new Vector3(hole.center.x, centreY, hole.yMin - thickness * 0.5f),
                new Vector3(hole.width + thickness * 2f, height, thickness), material, layer);
        }

        static void Segment(Transform parent, string name, Vector3 start, Vector3 direction,
            Quaternion rotation, float from, float to, float floorY, float height,
            Material material, int layer)
        {
            var mid = start + direction * ((from + to) * 0.5f);
            Box(parent, name,
                new Vector3(mid.x, floorY + height * 0.5f, mid.z),
                new Vector3(WallThickness, height, to - from),
                material, layer, rotation);
        }

        /// <summary>
        /// A straight flight of stairs, built as one sloped box so the NavMesh
        /// bakes a clean walkable surface, with tread-shaped decoration on top.
        /// A staircase modelled as real steps bakes into a jagged, unreliable mesh.
        /// </summary>
        public static void Stair(Transform parent, string name, Vector3 bottomCentre,
            Vector3 horizontalDirection, float rise, float run, float width,
            Material rampMaterial, Material treadMaterial, int layer)
        {
            var dir = horizontalDirection.normalized;
            var slopeLength = Mathf.Sqrt(rise * rise + run * run);
            var pitch = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;

            var rotation = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(-pitch, 0f, 0f);
            var centre = bottomCentre + dir * (run * 0.5f) + Vector3.up * (rise * 0.5f);

            Box(parent, name, centre - rotation * Vector3.up * (SlabThickness * 0.5f),
                new Vector3(width, SlabThickness, slopeLength), rampMaterial, layer, rotation);

            // Treads are decoration only; the ramp underneath is what is walked on.
            var steps = Mathf.Max(2, Mathf.RoundToInt(rise / 0.19f));
            for (var i = 1; i <= steps; i++)
            {
                var t = i / (float)steps;
                var nose = bottomCentre + dir * (run * t) + Vector3.up * (rise * t);
                Decal(parent, $"{name}_tread{i}",
                    nose - dir * 0.11f + Vector3.up * 0.02f,
                    new Vector3(width * 0.98f, 0.05f, 0.24f),
                    treadMaterial, Quaternion.LookRotation(dir, Vector3.up));
            }
        }

        /// <summary>A dim bulb. The house is meant to be lit in pools, not evenly.</summary>
        public static Light Bulb(Transform parent, string name, Vector3 position,
            Color colour, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.isStatic = true;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = colour;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;

            return light;
        }

        public static Transform Marker(Transform parent, string name, Vector3 position,
            float yaw = 0f, string tag = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            if (!string.IsNullOrEmpty(tag)) go.tag = tag;
            return go.transform;
        }

        // ------------------------------------------------------------------
        // Navigation
        // ------------------------------------------------------------------

        const string NavMeshAssetPath = "Assets/_Project/Settings/HouseNavMesh.asset";

        /// <summary>
        /// Bakes the surface and stores the result as its own asset.
        ///
        /// This matters more than it looks. A NavMeshData left living inside the
        /// scene cannot be serialised as text, so Unity silently rewrites the
        /// whole scene in binary - and .gitattributes declares *.unity as text,
        /// so git then "normalises" line endings inside a binary file and corrupts
        /// it. Keeping the bake in a separate asset keeps the scene diffable and
        /// keeps git from eating it.
        /// </summary>
        public static void Bake(Unity.AI.Navigation.NavMeshSurface surface)
        {
            if (surface == null) return;

            surface.BuildNavMesh();

            var data = surface.navMeshData;
            if (data == null)
            {
                Debug.LogError("[BuildKit] NavMesh bake produced nothing.");
                return;
            }

            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(NavMeshAssetPath);
            if (existing != null) AssetDatabase.DeleteAsset(NavMeshAssetPath);

            AssetDatabase.CreateAsset(data, NavMeshAssetPath);
            AssetDatabase.SaveAssets();

            surface.navMeshData = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(NavMeshAssetPath);
            EditorUtility.SetDirty(surface);
        }

        // ------------------------------------------------------------------
        // Scenes
        // ------------------------------------------------------------------

        /// <summary>
        /// Saves a scene, refusing to write one that is empty or failed to load.
        ///
        /// A builder that opens a scene, gets an error, and saves anyway will
        /// happily overwrite the level with nothing — which is exactly how the
        /// house was lost once already.
        /// </summary>
        public static bool SaveScene(UnityEngine.SceneManagement.Scene scene, string context)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError($"[{context}] Scene is not loaded; refusing to save over it.");
                return false;
            }

            if (scene.rootCount == 0)
            {
                Debug.LogError($"[{context}] Scene has no objects; refusing to save an empty scene.");
                return false;
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            return true;
        }

        /// <summary>Assigns a private [SerializeField] without widening its API.</summary>
        public static void Wire(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);

            if (prop == null)
            {
                Debug.LogError($"[BuildKit] {target.GetType().Name} has no field '{fieldName}'");
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
