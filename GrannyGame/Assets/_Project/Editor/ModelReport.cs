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
    }
}
