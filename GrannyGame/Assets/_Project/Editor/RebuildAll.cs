using UnityEditor;
using UnityEngine;

namespace Granny.EditorTools
{
    /// <summary>
    /// Runs every builder, in the one order that works.
    ///
    /// The builders are not independent: HouseBuilder throws away and recreates
    /// the patrol markers and the bed, and GrannyBuilder is what points the
    /// director at them. Run them the other way round and the scene comes out
    /// looking fine while the director holds references to objects that were
    /// deleted after it took them — the kind of break that only shows up as a
    /// null three tests later.
    ///
    /// So the order lives here, once, instead of in whoever last remembered it:
    ///
    ///     ./tools/unity.ps1 run -Method Granny.EditorTools.RebuildAll.Run
    /// </summary>
    public static class RebuildAll
    {
        [MenuItem("Granny/Rebuild Everything", priority = 1)]
        public static void Run()
        {
            Debug.Log("[Rebuild] start");

            // Prefabs and data first: everything downstream loads them by path.
            ContentBuilder.Run();
            PlayerRigBuilder.Run();

            // The house wires the sound bank into its speaker, so the bank has
            // to exist by the time the house is built.
            AudioBuilder.Run();

            // The house destroys and recreates its own markers...
            HouseBuilder.Run();

            // ...so she, and the director that points at those markers, come after.
            GrannyBuilder.Run();

            // The front door is placed into the finished house.
            EscapeBuilder.Run();

            MenuBuilder.Run();

            Debug.Log("[Rebuild] done");
        }
    }
}
