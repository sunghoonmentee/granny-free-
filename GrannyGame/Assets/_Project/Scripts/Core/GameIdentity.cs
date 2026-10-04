namespace Granny.Core
{
    /// <summary>
    /// Every player-facing name for the game and its antagonist, in one place.
    ///
    /// These are working titles. Renaming the game or the antagonist is an edit to
    /// this file and a re-run of the builders that bake text into scenes
    /// (MenuBuilder, ProjectBootstrap) — nothing else hard-codes them.
    ///
    /// Code identifiers (GrannyBrain, the "Granny" layer and tag) are deliberately
    /// NOT driven from here: they are internal names, and renaming them would churn
    /// every scene and prefab for no visible change.
    /// </summary>
    public static class GameIdentity
    {
        /// <summary>Title shown on the title screen and in the window.</summary>
        public const string Title = "아가타의 집";

        /// <summary>Latin-script title, for places that cannot take Hangul.</summary>
        public const string TitleLatin = "Agata's House";

        /// <summary>
        /// ASCII product name for PlayerSettings. It becomes part of the save path
        /// (persistentDataPath), so it stays plain and filesystem-safe.
        /// </summary>
        public const string ProductName = "AgatasHouse";

        /// <summary>The antagonist's name.</summary>
        public const string AntagonistName = "아가타";

        /// <summary>What the player calls her most of the time.</summary>
        public const string AntagonistRole = "할머니";

        /// <summary>One-line hook under the title.</summary>
        public const string Tagline = "다섯 번의 아침. 그녀는 모든 소리를 듣는다.";
    }
}
