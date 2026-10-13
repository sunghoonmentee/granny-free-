using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Granny.EditorTools
{
    /// <summary>
    /// Turns the downloaded texture sets into materials the house can be built
    /// out of, and sets the import flags they need to be read correctly.
    ///
    /// The flags are the part worth automating. A normal map imported as an
    /// ordinary colour texture looks almost right and is subtly wrong
    /// everywhere; a roughness map imported as sRGB is wrong in a way nobody
    /// can name but everybody can see. Both are one checkbox, and both are a
    /// checkbox somebody forgets on the twentieth texture.
    ///
    /// Sources: ambientCG, CC0. No attribution required and none claimed.
    ///
    ///     ./tools/unity.ps1 run -Method Granny.EditorTools.TextureBuilder.Run
    /// </summary>
    public static class TextureBuilder
    {
        const string TextureDir = "Assets/_Project/Art/Textures";
        const string MaterialDir = "Assets/_Project/Art/Materials";

        /// <summary>
        /// Which texture set each surface uses, how big the pattern is in metres,
        /// and a tint.
        ///
        /// The tint is doing most of the work and it has to go much further than
        /// feels reasonable. These are photographs of real surfaces lit by
        /// daylight; dropped into the house at anything near full strength they
        /// read as a clean, well-kept home with the lights off, which is the one
        /// thing this house must never look like. Walls sit around half, floors
        /// lower, the ceiling lower still.
        ///
        /// The fabrics also need warming. The cloth scan has a cool cast, and a
        /// neutral grey tint over it came out denim blue — she looked like she
        /// was wearing workwear rather than something she has slept in for years.
        /// </summary>
        static readonly (string material, string set, float tiling, Color tint)[] Surfaces =
        {
            ("HouseFloor", "WoodFloor051", 1.4f, new Color(0.40f, 0.34f, 0.27f)),
            ("HouseStair", "WoodFloor043", 1.2f, new Color(0.36f, 0.30f, 0.23f)),
            ("HouseTread", "Planks037A", 0.8f, new Color(0.46f, 0.39f, 0.29f)),
            ("HouseWall", "PaintedPlaster017", 2.2f, new Color(0.47f, 0.43f, 0.36f)),
            ("HouseInterior", "Wallpaper001A", 1.8f, new Color(0.42f, 0.37f, 0.31f)),
            ("HouseConcrete", "Concrete034", 2.0f, new Color(0.46f, 0.44f, 0.42f)),
            ("HouseCeiling", "PaintedPlaster017", 2.6f, new Color(0.26f, 0.24f, 0.21f)),
            ("PropWardrobe", "Planks037A", 0.7f, new Color(0.30f, 0.24f, 0.18f)),
            ("PropBench", "Planks037A", 0.9f, new Color(0.34f, 0.27f, 0.20f)),
            ("PropBedFrame", "Planks037A", 0.6f, new Color(0.28f, 0.22f, 0.16f)),

            // Sixteen interior doors and the panels on them. Flat brown was the
            // single largest piece of untextured surface left in the house.
            ("PropDoor", "Planks037A", 0.5f, new Color(0.30f, 0.22f, 0.15f)),
            ("PropDoorPanel", "Planks037A", 0.35f, new Color(0.24f, 0.17f, 0.12f)),

            // The way out, and the plank somebody nailed across it.
            ("EscapeLeaf", "Planks037A", 0.45f, new Color(0.29f, 0.21f, 0.14f)),
            ("EscapePlanking", "Planks037A", 0.3f, new Color(0.25f, 0.18f, 0.12f)),
            ("EscapeFrame", "Planks037A", 0.5f, new Color(0.19f, 0.14f, 0.10f)),
            ("EscapePlank", "Planks037A", 0.4f, new Color(0.38f, 0.28f, 0.17f)),
            ("TruckPaint", "Concrete034", 1.2f, new Color(0.46f, 0.40f, 0.34f)),
            ("PropWindowFrame", "Planks037A", 0.35f, new Color(0.34f, 0.28f, 0.21f)),
            ("PropWindowBoard", "Planks037A", 0.3f, new Color(0.52f, 0.43f, 0.31f)),
            // No cloth scan. The one that was downloaded has a strong blue cast
            // that a warm tint could not pull out — she came out dressed in
            // denim. Her clothes and the bedding stay flat colour, which is not
            // a compromise: she is read by the contrast between pale hands, a
            // mid nightdress and a near-black apron, and a photograph with a
            // colour of its own fights that for no gain at the distance she is
            // usually seen from.
        };

        [MenuItem("Granny/Build Textures", priority = 18)]
        public static void Run()
        {
            Debug.Log("[Textures] start");

            if (!AssetDatabase.IsValidFolder(TextureDir))
            {
                Debug.LogWarning($"[Textures] Nothing at {TextureDir}; skipping.");
                return;
            }

            ConfigureImports();

            var built = 0;
            foreach (var surface in Surfaces)
                if (Build(surface.material, surface.set, surface.tiling, surface.tint))
                    built++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Textures] done — {built} materials textured");
        }

        /// <summary>
        /// Normal maps have to be imported as normal maps, and the data maps —
        /// roughness, occlusion — have to be imported as data rather than colour.
        /// </summary>
        static void ConfigureImports()
        {
            foreach (var path in Directory.GetFiles(TextureDir, "*.png"))
            {
                var asset = path.Replace('\\', '/');
                if (AssetImporter.GetAtPath(asset) is not TextureImporter importer) continue;

                var isNormal = asset.EndsWith("_Normal.png");
                var isData = asset.EndsWith("_Roughness.png") || asset.EndsWith("_AO.png");

                var wantedType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                var wantedSrgb = !isNormal && !isData;

                // Floors and walls are nearly always seen edge-on, which is the
                // case ordinary mipmapping handles worst: past a few metres the
                // grain gives up and the surface goes flat. Nine is generous and
                // costs almost nothing on surfaces this large.
                const int aniso = 9;

                if (importer.textureType == wantedType
                    && importer.sRGBTexture == wantedSrgb
                    && importer.anisoLevel == aniso)
                    continue;

                importer.textureType = wantedType;
                importer.sRGBTexture = wantedSrgb;
                importer.anisoLevel = aniso;
                importer.SaveAndReimport();
            }
        }

        static bool Build(string materialName, string set, float tiling, Color tint,
            float surfaceSmoothness = 0.16f)
        {
            var colour = Load(set, "Color");
            if (colour == null)
            {
                Debug.LogWarning($"[Textures] No '{set}' downloaded; {materialName} stays flat.");
                return false;
            }

            var path = $"{MaterialDir}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = materialName };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", colour);
            material.SetColor("_BaseColor", tint);

            var normal = Load(set, "Normal");
            if (normal != null)
            {
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
            }

            var occlusion = Load(set, "AO");
            if (occlusion != null)
            {
                material.EnableKeyword("_OCCLUSIONMAP");
                material.SetTexture("_OcclusionMap", occlusion);
            }

            // No metallic-gloss map, deliberately.
            //
            // What was downloaded is a roughness map, and URP's _MetallicGlossMap
            // is not that: it reads RGB as metalness and alpha as smoothness. Bound
            // to it, a mid-grey roughness map tells the shader the surface is half
            // metal — and a metal surface throws its albedo away and mirrors
            // whatever is around it. That is why every floor and wall in the house
            // came out the same cold blue-grey however they were tinted, and why
            // nothing looked like wood. The comment that used to be here claimed a
            // channel setting and a negative scale were doing the conversion. They
            // were not; neither was ever set.
            //
            // Converting roughness into a proper mask is possible, but a derelict
            // house wants one number anyway: nothing in it has been polished in
            // thirty years.
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", surfaceSmoothness);

            // One number for every map, so the grain lines up across them.
            var scale = new Vector2(tiling, tiling);
            foreach (var slot in new[] { "_BaseMap", "_BumpMap", "_OcclusionMap" })
                if (material.HasProperty(slot))
                    material.SetTextureScale(slot, scale);

            EditorUtility.SetDirty(material);
            return true;
        }

        static Texture2D Load(string set, string map) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/{set}_{map}.png");

        /// <summary>Every texture set the house expects to find, for the report.</summary>
        public static IEnumerable<string> Sets()
        {
            var seen = new HashSet<string>();
            foreach (var surface in Surfaces)
                if (seen.Add(surface.set))
                    yield return surface.set;
        }
    }
}
