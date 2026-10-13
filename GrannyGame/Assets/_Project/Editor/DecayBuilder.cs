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
        /// Webbing in the corners, strung across the angle rather than laid flat
        /// on a face.
        ///
        /// Small, and smaller on small furniture. The first version used a fixed
        /// hand-span, which was right for a wardrobe and covered a third of the
        /// front of a chest of drawers — at that size it stops reading as a web
        /// and starts reading as a sticker somebody put there. And faint: a web
        /// is mostly the air between the threads, so anything solid enough to
        /// see clearly is already wrong.
        /// </summary>
        static void HangCobwebs(GameObject piece, System.Random random, int count)
        {
            if (count <= 0) return;

            var bounds = Bounds(piece);
            if (bounds.size == Vector3.zero) return;

            var material = WebMaterial();

            // A hand's span at most, and a quarter of the piece's narrower way
            // when the piece is small.
            var size = Mathf.Clamp(0.3f * Mathf.Min(bounds.size.x, bounds.size.z), 0.09f, 0.22f);

            for (var i = 0; i < count; i++)
            {
                var web = GameObject.CreatePrimitive(PrimitiveType.Quad);
                web.name = $"Cobweb_{i}";
                web.transform.SetParent(piece.transform, false);

                Object.DestroyImmediate(web.GetComponent<Collider>());
                web.GetComponent<MeshRenderer>().sharedMaterial = material;
                web.GetComponent<MeshRenderer>().shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;

                // The top corners: where two surfaces meet, nothing passes, and
                // the air is still.
                var x = random.Next(2) == 0 ? bounds.min.x : bounds.max.x;
                var z = random.Next(2) == 0 ? bounds.min.z : bounds.max.z;
                var y = Mathf.Lerp(bounds.max.y, bounds.center.y, (float)random.NextDouble() * 0.3f);

                var corner = piece.transform.InverseTransformPoint(new Vector3(x, y, z));
                var inward = piece.transform.InverseTransformPoint(bounds.center) - corner;

                // Hung vertically, always. Taking the full direction to the
                // centre let a web on a low, wide piece tilt until it was lying
                // face up on the floor like a dropped doily. A web is strung
                // between two walls of a corner, so its sheet stands up.
                inward.y = 0f;
                if (inward.sqrMagnitude < 0.0001f)
                {
                    Object.DestroyImmediate(web);
                    continue;
                }

                inward = inward.normalized;

                // Half a span in from the corner, so one edge of the web passes
                // through the corner itself and the rest spans the angle.
                web.transform.localPosition = corner + inward * (size * 0.5f);
                web.transform.localRotation = Quaternion.LookRotation(-inward, Vector3.up)
                                              * Quaternion.Euler(0f, 0f, (float)(random.NextDouble() * 360.0));
                web.transform.localScale = Vector3.one * size;
            }
        }

        static Bounds Bounds(GameObject piece)
        {
            var bounds = new Bounds();
            var found = false;

            foreach (var renderer in piece.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled) continue;

                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }

            return found ? bounds : new Bounds();
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
            // Dust grey rather than white, and thin. A web lit by a bulb at
            // three-quarters white reads as a sheet of paper somebody dropped:
            // it was the brightest thing in several rooms.
            material.SetColor("_BaseColor", new Color(0.58f, 0.57f, 0.53f, 0.85f));

            // Transparent, unlit-ish and never casting a shadow: a cobweb that
            // throws a hard shadow across a wall looks like a cut-out of a
            // cobweb, which is exactly what it is.
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_Surface", 1f);              // transparent
            material.SetFloat("_Blend", 0f);                // alpha

            // Straight alpha, explicitly, and this is the line that matters.
            //
            // URP defaults a transparent Lit material to "preserve specular",
            // which is premultiplied blending: source factor One, and the shader
            // is expected to have multiplied its colour by alpha already. The
            // shader only does that with _ALPHAPREMULTIPLY_ON, which is off here —
            // so every fully transparent pixel of the web was *adding* its colour
            // to whatever was behind it. What that looks like is not a web with
            // gaps in it. It is a sheet of tracing paper, which is exactly what
            // these have been in every photograph: the texture was right the whole
            // time and the blend was throwing the gaps away.
            material.SetFloat("_BlendModePreserveSpecular", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
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
        /// Draws a cobweb: radial threads from a corner, with strands sagging
        /// between them, fading out before it reaches the edge of its own quad.
        ///
        /// The fade is the whole trick. Without it the threads run to the border
        /// and stop dead, and what the player sees is not a web but the square it
        /// was drawn on — which is exactly what the first one looked like hanging
        /// on the end of a bed.
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
            var alpha = new float[size * size];

            var random = new System.Random(7);
            var anchor = new Vector2(0.04f, 0.04f) * size;   // the corner it hangs from
            const int spokes = 13;

            var ends = new Vector2[spokes];

            for (var s = 0; s < spokes; s++)
            {
                var t = s / (float)(spokes - 1);
                var angle = Mathf.Lerp(0f, Mathf.PI * 0.5f, t);
                var reach = size * (0.72f + (float)random.NextDouble() * 0.26f);

                ends[s] = anchor + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
                Thread(alpha, size, anchor, ends[s], 0.5f);
            }

            // The rings: each one sags between neighbouring spokes, which is what
            // makes it read as thread under its own weight rather than wire.
            for (var ring = 1; ring <= 8; ring++)
            {
                var depth = ring / 9f;

                for (var s = 0; s < spokes - 1; s++)
                {
                    var a = Vector2.Lerp(anchor, ends[s], depth);
                    var b = Vector2.Lerp(anchor, ends[s + 1], depth);
                    var sag = (a + b) * 0.5f - (a + b - anchor * 2f).normalized * (size * 0.018f);

                    Thread(alpha, size, a, sag, 0.34f);
                    Thread(alpha, size, sag, b, 0.34f);
                }
            }

            // Thin it out towards the far edge, and harder still in the last
            // tenth, so nothing reaches the border of the quad.
            var pixels = new Color32[alpha.Length];

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var index = y * size + x;
                var reach = (new Vector2(x, y) - anchor).magnitude / size;

                var fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.98f, reach));
                var edge = Mathf.Min(
                    Mathf.InverseLerp(0f, 0.06f * size, x),
                    Mathf.InverseLerp(0f, 0.06f * size, size - 1 - x));
                edge = Mathf.Min(edge, Mathf.InverseLerp(0f, 0.06f * size, size - 1 - y));

                var value = Mathf.Clamp01(alpha[index]) * fade * edge;
                pixels[index] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(value * 255f));
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

                // Same reason the rug and the floors needed it: a web is seen
                // small and at an angle, which is the case mipmapping handles
                // worst.
                importer.anisoLevel = 9;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// One strand, drawn by walking the line with a two-pixel brush.
        ///
        /// Two pixels rather than one, and that is not a detail. A one-pixel
        /// thread survives exactly one mip level: by the time a web is a hand's
        /// span across a room the chain has averaged every thread into the gaps
        /// between them, and what reaches the screen is a uniform haze — which is
        /// why these read as scraps of paper stuck to the furniture rather than as
        /// webs. A thicker strand keeps its shape two levels further down.
        ///
        /// Takes the brighter of what is there and what it is laying down rather
        /// than adding them: threads cross at the hub by the dozen, and adding
        /// turned every crossing into a white knot.
        /// </summary>
        static void Thread(float[] alpha, int size, Vector2 from, Vector2 to, float strength)
        {
            var steps = Mathf.CeilToInt(Vector2.Distance(from, to)) * 2;

            for (var i = 0; i <= steps; i++)
            {
                var point = Vector2.Lerp(from, to, i / (float)Mathf.Max(1, steps));

                for (var dx = 0; dx <= 1; dx++)
                for (var dy = 0; dy <= 1; dy++)
                {
                    var x = Mathf.RoundToInt(point.x) + dx;
                    var y = Mathf.RoundToInt(point.y) + dy;

                    if (x < 0 || y < 0 || x >= size || y >= size) continue;

                    // The second pixel of the pair is thinner, so a strand has an
                    // edge rather than being a two-pixel bar.
                    var weight = dx + dy == 0 ? strength : strength * 0.6f;

                    var index = y * size + x;
                    alpha[index] = Mathf.Max(alpha[index], weight);
                }
            }
        }
    }
}
