using UnityEngine;

namespace Granny.Core
{
    /// <summary>
    /// Layer indices as configured by ProjectBootstrap. Raycasts run every frame
    /// against these, so the masks are resolved once at startup rather than by
    /// name lookup at the call site.
    /// </summary>
    public static class GameLayers
    {
        public const int Player = 6;
        public const int Granny = 7;
        public const int Interactable = 8;
        public const int HidingSpot = 9;
        public const int Door = 10;
        public const int Pickup = 11;
        public const int Prop = 12;
        public const int LevelGeometry = 13;

        /// <summary>Anything the interaction raycast should be able to hit.</summary>
        public static readonly LayerMask InteractionMask =
            (1 << Interactable) | (1 << HidingSpot) | (1 << Door) | (1 << Pickup) | (1 << Prop);

        /// <summary>Anything that blocks Granny's line of sight to the player.</summary>
        public static readonly LayerMask SightBlockers =
            (1 << LevelGeometry) | (1 << Door) | (1 << Prop) | (1 << HidingSpot);

        /// <summary>Solid world the player and Granny walk on and bump into.</summary>
        public static readonly LayerMask WorldMask =
            (1 << LevelGeometry) | (1 << Door) | (1 << Prop) | (1 << HidingSpot);
    }
}
