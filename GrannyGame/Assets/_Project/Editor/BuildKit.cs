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
        public const float FloorHeight = 3.2f;

        public const float WallThickness = 0.25f;
        public const float SlabThickness = 0.3f;

        /// <summary>Doorways are wide enough for Granny's NavMesh agent to path through.</summary>
        public const float DoorWidth = 1.3f;
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
        /// A wall from a to b at floor level <paramref name="floorY"/>, with a
        /// doorway cut at each distance in <paramref name="doorCentres"/> measured
        /// along the run. Each doorway keeps a lintel above it, so the wall still
        /// reads as one surface and Granny cannot path over the top.
        /// </summary>
        public static void Wall(Transform parent, string name, Vector2 a, Vector2 b,
            float floorY, Material material, int layer, params float[] doorCentres)
        {
            var start = new Vector3(a.x, floorY, a.y);
            var end = new Vector3(b.x, floorY, b.y);
            var run = end - start;
            var length = run.magnitude;

            if (length < 0.01f) return;

            var direction = run / length;
            var rotation = Quaternion.LookRotation(direction, Vector3.up);
            var height = FloorHeight;

            System.Array.Sort(doorCentres);

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

                cursor = gapEnd;
            }

            if (cursor < length)
                Segment(parent, $"{name}_{index}", start, direction, rotation,
                    cursor, length, floorY, height, material, layer);
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
