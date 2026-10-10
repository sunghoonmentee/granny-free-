using System.IO;
using UnityEditor;
using UnityEngine;

namespace Granny.EditorTools
{
    /// <summary>
    /// Makes furniture look like nobody has touched it in years.
    ///
    /// Bought models arrive in showroom condition — square, intact, clean — and
    /// a houseful of them reads as a furniture catalogue with the lights off.
    /// What sells a derelict house is not grime on the textures, which nobody
    /// sees at this light level. It is geometry that has given up: a piece
    /// standing out of true, a door off its hinge, a drawer missing, and webbing
    /// in every corner nobody has reason to reach into.
    ///
    /// All of it is applied from a seed derived from where the piece stands, so
    /// the same room decays the same way every rebuild. A house that rearranges
    /// its own damage each time it loads cannot be learned.
    /// </summary>
    public static class DecayBuilder
    {
        const string TextureDir = "Assets/_Project/Art/Textures";
        const string MaterialDir = "Assets/_Project/Art/Materials";

        /// <summary>
        /// Ages a piece of furniture in place.
        /// </summary>
        /// <param name="piece">The prop. Its children are the parts that can fail.</param>
        /// <param name="seed">Anything stable — a position, a name.</param>
        /// <param name="webs">Roughly how many cobwebs to hang on it.</param>
        public static void Weather(GameObject piece, int seed, int webs = 3)
        {
            var random = new System.Random(seed);

            LeanOutOfTrue(piece, random);
            BreakSomethingOff(piece, random);
            HangCobwebs(piece, random, webs);
        }

        /// <summary>
        /// Only the model is allowed to fall apart.
        ///
        /// A prop is a model plus the boxes the game needs — the mattress that
        /// covers the gap under a bed, the drawer face the raycast has to find.
        /// Breaking one of those off is not decay, it is removing a mechanic: the
        /// first version of this shoved the mattress halfway across the room and
        /// left the hiding place a bare frame.
        /// </summary>
        static bool IsScenery(Transform part)
        {
            for (var t = part; t != null; t = t.parent)
                if (t.name == "Model")
                    return true;

            return false;
        }

        /// <summary>
        /// A degree or two off vertical, and settled a little into the floor.
        ///
        /// Small on purpose. Furniture at a jaunty angle reads as comedy; a
        /// wardrobe one and a half degrees out of true reads as a floor that has
        /// been sagging for thirty years, and the player never consciously sees
        /// it at all.
        /// </summary>
        static void LeanOutOfTrue(GameObject piece, System.Random random)
        {
            float Spread(float amount) => (float)(random.NextDouble() * 2.0 - 1.0) * amount;

            piece.transform.localRotation *= Quaternion.Euler(Spread(1.6f), Spread(2.5f), Spread(1.6f));
            piece.transform.localPosition += new Vector3(0f, Spread(0.012f) - 0.008f, 0f);
        }

        /// <summary>
        /// Takes a part off, or hangs it at an angle.
        ///
        /// Only ever one, and never the first child: on these models the first
        /// submesh is the carcass, and a wardrobe with its carcass missing is a
        /// hole rather than a ruin. Doors swing, drawers fall out, and about a
        /// third of the time nothing happens at all — furniture that is uniformly
        /// broken is as monotonous as furniture that is uniformly intact.
        /// </summary>
        static void BreakSomethingOff(GameObject piece, System.Random random)
        {
            var parts = System.Array.FindAll(
                piece.GetComponentsInChildren<MeshRenderer>(),
                r => IsScenery(r.transform));

            if (parts.Length < 3) return;
            if (random.NextDouble() < 0.34) return;

            var victim = parts[1 + random.Next(parts.Length - 1)].transform;

            if (random.NextDouble() < 0.5)
            {
                // Hanging: one hinge has gone and the other has not.
                victim.localRotation *= Quaternion.Euler(
                    0f, (float)(random.NextDouble() * 26.0 + 12.0), (float)(random.NextDouble() * 9.0 - 2.0));
            }
            else
            {
                // Pulled out and left that way, or on the floor beside it.
                victim.localPosition += new Vector3(
                    0f, (float)(-random.NextDouble() * 0.05), (float)(random.NextDouble() * 0.22 + 0.06));
                victim.localRotation *= Quaternion.Euler((float)(random.NextDouble() * 7.0 - 3.5), 0f, 0f);
            }
        }

        /// <summary>
        /// Webbing in the corners, strung across the angles rather than laid flat
        /// on a surface. A web on the front of a wardrobe is dressing; a web
        /// across the gap between the wardrobe and the wall is a place nobody has
        /// put a hand for a very long time, which is the thing being said.
        /// </summary>
        static void HangCobwebs(GameObject piece, System.Random random, int count)
        {
            if (count <= 0) return;

            var bounds = Bounds(piece);
            if (bounds.size == Vector3.zero) return;

            var material = WebMaterial();

            for (var i = 0; i < count; i++)
            {
                var web = GameObject.CreatePrimitive(PrimitiveType.Quad);
                web.name = $"Cobweb_{i}";
                web.transform.SetParent(piece.transform, false);

                Object.DestroyImmediate(web.GetComponent<Collider>());
                web.GetComponent<MeshRenderer>().sharedMaterial = material;

                // Upper corners first: webs are built where a surface meets a
                // surface and the air is still.
                var x = random.Next(2) == 0 ? bounds.min.x : bounds.max.x;
                var z = random.Next(2) == 0 ? bounds.min.z : bounds.max.z;
                var y = Mathf.Lerp(bounds.center.y, bounds.max.y, (float)random.NextDouble() * 0.5f + 0.5f);

                var corner = piece.transform.InverseTransformPoint(new Vector3(x, y, z));
                var inward = (piece.transform.InverseTransformPoint(bounds.center) - corner).normalized;

                // A fixed hand-span, not a fraction of the furniture: webs do
                // not scale with what they are strung across, and deriving the
                // size from the bounds grew a web the size of a wardrobe.
                var size = 0.18f + (float)random.NextDouble() * 0.16f;

                web.transform.localPosition = corner + inward * (size * 0.45f);
                web.transform.localRotation = Quaternion.LookRotation(-inward, Vector3.up)
                                              * Quaternion.Euler(0f, 0f, (float)(random.NextDouble() * 360.0));
                web.transform.localScale = Vector3.one * size;
            }
        }

        static Bounds Bounds(GameObject piece)
        {
            var renderers = piece.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) return new Bounds();

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        // ------------------------------------------------------------------
        // The web itself
        // ------------------------------------------------------------------

        static Material WebMaterial()
        {
            const string path = MaterialDir + "/PropCobweb.mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = "PropCobweb" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", WebTexture());
            material.SetColor("_BaseColor", new Color(0.86f, 0.86f, 0.82f, 0.26f));

            // Transparent, unlit-ish and never casting a shadow: a cobweb that
            // throws a hard shadow across a wall looks like a cut-out of a
            // cobweb, which is exactly what it is.
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_Surface", 1f);              // transparent
            material.SetFloat("_Blend", 0f);                // alpha
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);                 // visible from both sides
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_AlphaClip", 0f);

            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Draws a cobweb: radial threads from a corner, with catenary strands
        /// strung between them.
        ///
        /// Generated rather than downloaded for the same reason the sounds were:
        /// the project stays buildable from source. It also means the web is
        /// transparent where a web is transparent, which a photograph of a web on
        /// a white background very much is not.
        /// </summary>
        static Texture2D WebTexture()
        {
            const string path = TextureDir + "/Cobweb.png";
            const int size = 256;

            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];

            for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 0);

            var random = new System.Random(7);
            var anchor = new Vector2(0.06f, 0.06f) * size;   // the corner it hangs from
            const int spokes = 11;

            var ends = new Vector2[spokes];

            for (var s = 0; s < spokes; s++)
            {
                var t = s / (float)(spokes - 1);
                var angle = Mathf.Lerp(0f, Mathf.PI * 0.5f, t);
                var reach = size * (0.78f + (float)random.NextDouble() * 0.2f);

                ends[s] = anchor + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
                Thread(pixels, size, anchor, ends[s], 0.55f);
            }

            // The spiral: each ring sags between neighbouring spokes, which is
            // what makes it read as thread under its own weight rather than wire.
            for (var ring = 1; ring <= 7; ring++)
            {
                var depth = ring / 8f;

                for (var s = 0; s < spokes - 1; s++)
                {
                    var a = Vector2.Lerp(anchor, ends[s], depth);
                    var b = Vector2.Lerp(anchor, ends[s + 1], depth);
                    var sag = (a + b) * 0.5f - (a + b - anchor * 2f).normalized * (size * 0.02f);

                    Thread(pixels, size, a, sag, 0.4f);
                    Thread(pixels, size, sag, b, 0.4f);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>One strand, drawn by walking the line and dropping alpha.</summary>
        static void Thread(Color32[] pixels, int size, Vector2 from, Vector2 to, float strength)
        {
            var steps = Mathf.CeilToInt(Vector2.Distance(from, to));

            for (var i = 0; i <= steps; i++)
            {
                var point = Vector2.Lerp(from, to, i / (float)Mathf.Max(1, steps));
                var x = Mathf.RoundToInt(point.x);
                var y = Mathf.RoundToInt(point.y);

                if (x < 0 || y < 0 || x >= size || y >= size) continue;

                var index = y * size + x;
                var alpha = (byte)Mathf.Clamp(pixels[index].a + strength * 255f, 0f, 255f);
                pixels[index] = new Color32(255, 255, 255, alpha);
            }
        }
    }
}
