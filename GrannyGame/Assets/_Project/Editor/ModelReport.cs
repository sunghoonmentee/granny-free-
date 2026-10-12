using System.IO;
using UnityEditor;
using UnityEngine;

namespace Granny.EditorTools
{
    /// <summary>
    /// Says how big each downloaded model actually is, and which way up.
    ///
    /// Scale and orientation are the first things wrong with a model somebody
    /// else made, and they are wrong quietly: a wardrobe imported at a tenth
    /// scale is not an error, it is a doll's house wardrobe sitting in the
    /// corner that nobody notices until they try to hide in it. Measuring before
    /// building anything is cheaper than finding out from a playtest.
    ///
    ///     ./tools/unity.ps1 run -Method Granny.EditorTools.ModelReport.Run
    /// </summary>
    public static class ModelReport
    {
        const string ModelDir = "Assets/_Project/Art/Models";

        [MenuItem("Granny/Measure Models", priority = 19)]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(ModelDir))
            {
                Debug.LogWarning($"[Models] Nothing at {ModelDir}.");
                return;
            }

            foreach (var path in Directory.GetFiles(ModelDir, "*.fbx", SearchOption.AllDirectories))
            {
                var asset = path.Replace('\\', '/');
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(asset);

                if (prefab == null)
                {
                    Debug.LogError($"[Models] {asset} did not import.");
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

                try
                {
                    var renderers = instance.GetComponentsInChildren<Renderer>();
                    if (renderers.Length == 0)
                    {
                        Debug.LogWarning($"[Models] {Path.GetFileName(asset)} has no renderers.");
                        continue;
                    }

                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);

                    var size = bounds.size;
                    var materials = 0;
                    foreach (var renderer in renderers) materials += renderer.sharedMaterials.Length;

                    Debug.Log(
                        $"[Models] {Path.GetFileName(asset),-34} " +
                        $"{size.x:F2} x {size.y:F2} x {size.z:F2} m   " +
                        $"floor at y {bounds.min.y:F2}   " +
                        $"{renderers.Length} renderer(s), {materials} material slot(s)");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            Debug.Log("[Models] done");
        }

        /// <summary>
        /// Lists every part of every model, with where that part sits inside the
        /// model's own bounds.
        ///
        /// Needed the moment a model has to do something rather than just stand
        /// there. A chest of drawers is seven meshes, and which of them is a
        /// drawer front — the one that has to slide out when the player pulls it
        /// — cannot be guessed from the names an exporter writes. What gives it
        /// away is position: a drawer face is the part flush with the front of
        /// the carcass, and a handle is the only thing in front of that.
        ///
        /// Printed as a fraction of the model, not in metres, so the numbers mean
        /// the same thing whatever size the piece is: z 1.00 is the front face,
        /// y 0.00 the floor.
        ///
        ///     ./tools/unity.ps1 run -Method Granny.EditorTools.ModelReport.Parts
        /// </summary>
        [MenuItem("Granny/Measure Models (parts)", priority = 20)]
        public static void Parts()
        {
            if (!AssetDatabase.IsValidFolder(ModelDir))
            {
                Debug.LogWarning($"[Parts] Nothing at {ModelDir}.");
                return;
            }

            foreach (var path in Directory.GetFiles(ModelDir, "*.fbx", SearchOption.AllDirectories))
            {
                var asset = path.Replace('\\', '/');
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(asset);
                if (prefab == null) continue;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

                try
                {
                    var renderers = instance.GetComponentsInChildren<Renderer>();
                    if (renderers.Length == 0) continue;

                    var whole = renderers[0].bounds;
                    foreach (var renderer in renderers) whole.Encapsulate(renderer.bounds);

                    Debug.Log($"[Parts] {Path.GetFileName(asset)} — {renderers.Length} part(s), " +
                              $"{whole.size.x:F2} x {whole.size.y:F2} x {whole.size.z:F2} m");

                    float Fraction(float value, float min, float size) =>
                        size <= 0.0001f ? 0.5f : Mathf.Clamp01((value - min) / size);

                    foreach (var renderer in renderers)
                    {
                        var box = renderer.bounds;

                        Debug.Log(
                            $"[Parts]   {renderer.name,-28} " +
                            $"size {box.size.x:F2} x {box.size.y:F2} x {box.size.z:F2}   " +
                            $"centre x {Fraction(box.center.x, whole.min.x, whole.size.x):F2} " +
                            $"y {Fraction(box.center.y, whole.min.y, whole.size.y):F2} " +
                            $"z {Fraction(box.center.z, whole.min.z, whole.size.z):F2}   " +
                            $"front face z {Fraction(box.max.z, whole.min.z, whole.size.z):F2}");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            Debug.Log("[Parts] done");
        }
    }
}
