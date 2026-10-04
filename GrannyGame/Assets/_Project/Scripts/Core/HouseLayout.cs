using UnityEngine;

namespace Granny.Core
{
    /// <summary>
    /// The house's vertical layout, shared by the level builder (which uses it to
    /// stack storeys) and by anything that needs to say which floor a point is on
    /// (noise events, debug logs, the verification scenario).
    /// </summary>
    public static class HouseLayout
    {
        /// <summary>Storey-to-storey height. Every floor uses the same one.</summary>
        public const float FloorHeight = 3.2f;

        public const int Basement = -1;
        public const int Ground = 0;
        public const int Upper = 1;
        public const int Attic = 2;

        /// <summary>World Y of the walkable surface of a floor.</summary>
        public static float FloorY(int floor) => floor * FloorHeight;

        /// <summary>
        /// Which floor a world position belongs to. The small lift means a point a
        /// few centimetres below a floor surface (a settling item, a foot) still
        /// counts as that floor rather than the one below.
        /// </summary>
        public static int FloorOf(float y) => Mathf.FloorToInt((y + 0.6f) / FloorHeight);

        public static int FloorOf(Vector3 position) => FloorOf(position.y);

        public static string FloorName(int floor) => floor switch
        {
            Basement => "지하",
            Ground => "1층",
            Upper => "2층",
            Attic => "다락",
            _ => $"{floor}층",
        };
    }
}
