using UnityEngine;

namespace Granny.Core
{
    /// <summary>
    /// The two switches that sit beside the difficulty setting.
    ///
    /// They are deliberately not difficulties. A difficulty says how good she is
    /// at finding you; these say what kind of night it is, and either can be
    /// turned on at any setting. Kept in PlayerPrefs rather than in the save,
    /// because they are how the player wants to play rather than part of the run
    /// — a run resumed with the lights turned back up is still that run.
    /// </summary>
    public static class GameModes
    {
        const string DarkKey = "granny.mode.dark";
        const string ExtraLocksKey = "granny.mode.extraLocks";

        /// <summary>
        /// The house as it was before the lighting was fixed: almost no ambient
        /// light, heavy fog, and the torch as the only thing you can see by.
        ///
        /// It was a bug then and it is a mode now. Some people want the version
        /// where you cannot see the far end of the hall.
        /// </summary>
        public static bool Dark
        {
            get => PlayerPrefs.GetInt(DarkKey, 0) == 1;
            set { PlayerPrefs.SetInt(DarkKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>
        /// Every fastening the front door has, whatever the difficulty says. The
        /// long way out, available to somebody who wants it on Easy.
        /// </summary>
        public static bool ExtraLocks
        {
            get => PlayerPrefs.GetInt(ExtraLocksKey, 0) == 1;
            set { PlayerPrefs.SetInt(ExtraLocksKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>How many locks to fit, once the mode has had its say.</summary>
        public static int LocksFor(DifficultyProfile difficulty, int maximum)
        {
            if (ExtraLocks) return maximum;
            return difficulty != null ? difficulty.FrontDoorLocks : 3;
        }

        /// <summary>Forgets both, for tests and for a clean reinstall.</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(DarkKey);
            PlayerPrefs.DeleteKey(ExtraLocksKey);
            PlayerPrefs.Save();
        }
    }
}
