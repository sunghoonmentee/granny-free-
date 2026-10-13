using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Granny.EditorTools
{
    /// <summary>
    /// Re-lights a downloaded model for this house.
    ///
    /// Models arrive photographed in daylight, and dropping one into a derelict
    /// house unchanged does the same damage a photographed wall texture did: the
    /// first chest of drawers that went in came out as polished mahogany glowing
    /// in a room where the walls sit at four tenths of their own brightness. It
    /// did not look like good furniture in a dark house. It looked like the dark
    /// house was a mistake.
    ///
    /// So every model gets a material built here instead of the one the exporter
    /// shipped: same maps, pulled down to the light level everything else lives
    /// at, and with the varnish taken off.
    /// </summary>
    public static class ModelSkin
    {
        const string ModelDir = "Assets/_Project/Art/Models";
        const string MaterialDir = "Assets/_Project/Art/Materials";

        /// <summary>
        /// How far down each model comes, and towards what.
        ///
        /// Not one number for everything. The cabinet is already near black and
        /// taking half of it away would delete it; the drawers are bright orange
        /// wood and need most of it gone. The tints lean grey-brown because a
        /// neutral grey over a warm photograph still reads warm, and nothing in
        /// this house should look recently cared for.
        /// </summary>
        static readonly (string asset, Color tint, float smoothness)[] Skins =
        {
            ("vintage_wooden_drawer_01", new Color(0.50f, 0.43f, 0.36f), 0.18f),
            ("old_bed_frame", new Color(0.42f, 0.40f, 0.36f), 0.22f),
            ("GothicCabinet_01", new Color(0.62f, 0.58f, 0.54f), 0.16f),

            // Pale woods under a warm bulb. At the common tint these two came out
            // the brightest things in the hall — a clock and a chair glowing like
            // they were lit from inside, in a room where the walls sit at four
            // tenths.
            ("vintage_grandfather_clock_01", new Color(0.25f, 0.21f, 0.18f), 0.2f),
            ("WoodenChair_01", new Color(0.27f, 0.23f, 0.20f), 0.18f),
            ("Rockingchair_01", new Color(0.30f, 0.26f, 0.22f), 0.18f),
            ("dining_table", new Color(0.24f, 0.21f, 0.17f), 0.14f),
            ("Shelf_01", new Color(0.31f, 0.27f, 0.22f), 0.16f),
        };

        static readonly Color Default = new(0.34f, 0.30f, 0.26f);

        /// <summary>
        /// Re-lights every material a model arrived with, keeping them separate.
        ///
        /// Separate matters. The first version built one material from the files
        /// in the model's folder and put it in every slot, which is right for a
        /// model made of one material and destroys one made of several: the
        /// wardrobe's doors have their own texture and their own UVs, and handed
        /// the carcass's atlas instead they sampled a patch of bare plank. Two of
        /// its four doors came out looking like unfinished chipboard.
        ///
        /// So each original material gets its own re-lit copy, built from the
        /// texture that material was already using.
        /// </summary>
        public static void Retint(string asset, IEnumerable<Renderer> renderers)
        {
            var tint = Default;
            var smoothness = 0.2f;

            foreach (var skin in Skins)
                if (skin.asset == asset)
                {
                    tint = skin.tint;
                    smoothness = skin.smoothness;
                }

            // How many distinct materials the model actually has decides whether a
            // texture found lying beside it can be assumed to belong to a slot
            // that has none. With one material it certainly does; with four it is
            // a coin toss, and losing it puts another material's picture on this
            // one's UVs.
            var distinct = new HashSet<Material>();

            foreach (var renderer in renderers)
                foreach (var slot in renderer.sharedMaterials)
                    if (slot != null)
                        distinct.Add(slot);

            var lone = distinct.Count == 1;
            var built = new Dictionary<Material, Material>();

            foreach (var renderer in renderers)
            {
                var slots = renderer.sharedMaterials;
                var changed = false;

                for (var i = 0; i < slots.Length; i++)
                {
                    var original = slots[i];
                    if (original == null) continue;

                    if (!built.TryGetValue(original, out var relit))
                    {
                        relit = Build(asset, original, tint, smoothness, lone);
                        built[original] = relit;
                    }

                    if (relit == null || relit == original) continue;

                    slots[i] = relit;
                    changed = true;
                }

                if (changed) renderer.sharedMaterials = slots;
            }
        }

        /// <summary>
        /// One re-lit copy of one material. Returns null — meaning leave it
        /// alone — when there is no colour map to be found anywhere, because a
        /// tint over nothing is a flat brown rectangle.
        /// </summary>
        static Material Build(string asset, Material original, Color tint, float smoothness, bool lone)
        {
            var folder = $"{ModelDir}/{asset}";
            if (!AssetDatabase.IsValidFolder(folder)) return null;

            // The texture the material was already using, where it has one.
            var colour = original.HasProperty("_BaseMap") ? original.GetTexture("_BaseMap") as Texture2D : null;
            colour ??= original.mainTexture as Texture2D;

            var ownTexture = colour != null;

            // Where it has none, the colour map lying beside the model is used.
            //
            // This was briefly restricted to models with a single material, on the
            // theory that the wardrobe's blank-looking doors were being handed the
            // carcass's atlas. They were not: the wardrobe was simply being turned
            // round, and what everyone was looking at was its back. The
            // restriction cost the grandfather clock and the Victorian chair their
            // textures — Unity binds nothing to a slot whose material it could not
            // resolve on import, which is most of them — and both went flat orange.
            if (!ownTexture) colour = FindMap(folder, "diff", "albedo", "col");

            var path = $"{MaterialDir}/Model_{asset}_{Safe(original.name)}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = $"Model_{asset}_{Safe(original.name)}" };
                AssetDatabase.CreateAsset(material, path);
            }

            if (colour != null) material.SetTexture("_BaseMap", colour);

            // A slot with no picture keeps the shade the artist gave it, pulled
            // down by the same tint as everything else, rather than becoming a
            // flat brown rectangle.
            material.SetColor("_BaseColor", colour != null
                ? tint
                : new Color(original.color.r * tint.r * 1.6f,
                            original.color.g * tint.g * 1.6f,
                            original.color.b * tint.b * 1.6f));

            material.SetFloat("_Smoothness", smoothness);


            // Shape and sheen come from the material's own maps where it has
            // them. Where it does not, the folder's are used only if this
            // material is the one using the folder's colour map — pairing one
            // material's normals onto another's mesh is worse than none.
            var normal = Texture(original, "_BumpMap") ?? (ownTexture ? null : FindMap(folder, "nor_gl", "nor", "normal"));
            if (normal != null)
            {
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
            }

            // No metallic-gloss map. URP reads that slot's RGB as metalness, and
            // a roughness map put there tells the shader the furniture is made of
            // half-polished metal — which throws the albedo away and leaves every
            // piece the same cold grey. One smoothness number is both correct and
            // enough for a house where nothing has been cared for.
            material.SetFloat("_Metallic", 0f);

            EditorUtility.SetDirty(material);
            return material;
        }

        static Texture2D Texture(Material material, string property) =>
            material.HasProperty(property) ? material.GetTexture(property) as Texture2D : null;

        static string Safe(string name)
        {
            var clean = name.Replace(" ", "_");
            foreach (var bad in System.IO.Path.GetInvalidFileNameChars())
                clean = clean.Replace(bad, '_');
            return clean;
        }

        /// <summary>
        /// Finds a map by what the file is called. Nobody agrees on the names —
        /// one of these three downloads calls its roughness "_rough_1k" and
        /// another "_roughness_1k" — so the match is on a fragment, in order of
        /// preference, and a missing map is an absence rather than an error.
        /// </summary>
        static Texture2D FindMap(string folder, params string[] fragments)
        {
            foreach (var fragment in fragments)
            {
                foreach (var file in Directory.GetFiles(folder))
                {
                    var path = file.Replace(Separator, '/');
                    if (path.EndsWith(".meta")) continue;

                    var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    if (!name.Contains("_" + fragment)) continue;

                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture != null) return texture;
                }
            }

            return null;
        }

        static readonly char Separator = Path.DirectorySeparatorChar;

        /// <summary>
        /// Sets the import flags on every downloaded model's textures, once,
        /// before anything is built out of them.
        ///
        /// This used to happen lazily, the first time a map was looked up — which
        /// meant calling SaveAndReimport in the middle of building a prefab. A
        /// reimport invalidates the model instance that was standing in the scene
        /// waiting to be measured, and the build died on a null three lines later
        /// with nothing in the message to say a texture had anything to do with
        /// it. Import settings are a setup step; they belong before the work.
        /// </summary>
        public static void ConfigureImports()
        {
            if (!AssetDatabase.IsValidFolder(ModelDir)) return;

            foreach (var file in Directory.GetFiles(ModelDir, "*.*", SearchOption.AllDirectories))
            {
                var path = file.Replace(Separator, '/');
                if (path.EndsWith(".meta") || path.EndsWith(".fbx")) continue;

                var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                var kind = name.Contains("_nor") ? "nor"
                    : name.Contains("_rough") ? "rough"
                    : "diff";

                Configure(path, kind);
            }
        }

        /// <summary>
        /// A normal map imported as a colour texture is subtly wrong everywhere
        /// and nameable nowhere; a roughness map read as sRGB is the same. Both
        /// are one checkbox, and both are the checkbox somebody forgets.
        /// </summary>
        static void Configure(string path, string kind)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;

            var isNormal = kind.StartsWith("nor");
            var isData = kind.StartsWith("rough");

            var type = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            var srgb = !isNormal && !isData;

            // Anisotropic filtering, for the same reason the floors needed it: a
            // chair across a hall is a surface at a grazing angle, and without it
            // the mip chain hands the screen the texture's average colour. A
            // clock that is dark brown from two metres was pale tan from eight,
            // and no amount of tinting fixes that — the tint was being applied to
            // an average that no longer had any grain in it.
            const int aniso = 9;

            if (importer.textureType == type
                && importer.sRGBTexture == srgb
                && importer.anisoLevel == aniso)
                return;

            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.anisoLevel = aniso;
            importer.SaveAndReimport();
        }
    }
}
