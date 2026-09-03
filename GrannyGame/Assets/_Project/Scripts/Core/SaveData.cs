using System;
using System.Collections.Generic;

namespace Granny.Core
{
    /// <summary>
    /// A run in progress, as plain serialisable data.
    ///
    /// What is worth saving is only what the player earned: which day they are
    /// on, which fastenings they got off the door, and what they are carrying.
    /// Where Granny happens to be standing is not progress, and restoring it
    /// would let a player quit out of a chase.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>Bumped when the shape below changes so old files can be rejected.</summary>
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        public string difficultyName = "Normal";
        public int day = 1;

        /// <summary>Item id in the player's hands, or empty.</summary>
        public string heldItemId = string.Empty;

        /// <summary>Item ids on the belt, in slot order. Empty strings are empty slots.</summary>
        public List<string> pocketItemIds = new();

        /// <summary>Ids of the door fastenings already removed.</summary>
        public List<string> clearedLockIds = new();

        /// <summary>Ids of drawers already searched, so a reload does not restock them.</summary>
        public List<string> searchedContainerIds = new();

        public bool IsCompatible => version == CurrentVersion;
    }
}
