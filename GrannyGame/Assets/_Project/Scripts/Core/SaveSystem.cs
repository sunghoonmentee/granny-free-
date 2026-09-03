using System;
using System.IO;
using UnityEngine;

namespace Granny.Core
{
    /// <summary>
    /// Reads and writes the run to disk.
    ///
    /// Saving is deliberately not something the player triggers: it happens when
    /// a new day starts. Being able to save mid-chase would let a player undo
    /// every mistake, and the mistakes are the game.
    /// </summary>
    public static class SaveSystem
    {
        const string FileName = "run.json";

        static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        public static bool HasSave => File.Exists(Path);

        public static void Save(SaveData data)
        {
            if (data == null) return;

            data.version = SaveData.CurrentVersion;

            try
            {
                var json = JsonUtility.ToJson(data, prettyPrint: true);

                // Write beside the target and move into place, so a crash midway
                // leaves the previous save intact rather than a truncated file.
                var temp = Path + ".tmp";
                File.WriteAllText(temp, json);

                if (File.Exists(Path)) File.Delete(Path);
                File.Move(temp, Path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Could not write {Path}: {e.Message}");
            }
        }

        /// <summary>Returns the saved run, or null if there is none or it is unreadable.</summary>
        public static SaveData Load()
        {
            if (!HasSave) return null;

            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(Path));

                if (data == null || !data.IsCompatible)
                {
                    Debug.LogWarning("[SaveSystem] Save file is from an older build; starting fresh.");
                    return null;
                }

                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Could not read {Path}: {e.Message}");
                return null;
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(Path)) File.Delete(Path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Could not delete {Path}: {e.Message}");
            }
        }
    }
}
