using System;
using System.IO;
using UnityEngine;
using VastSpace.State;

namespace VastSpace.Core
{
    /// <summary>Saves the <see cref="GameState"/> as JSON in Application.persistentDataPath.</summary>
    public static class SaveSystem
    {
        private const string FileName = "savegame.json";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool HasSave => File.Exists(SavePath);

        public static void Save(GameState state)
        {
            try
            {
                state.version = GameState.CurrentVersion;
                state.lastSavedUtcTicks = DateTime.UtcNow.Ticks;
                string json = JsonUtility.ToJson(state, prettyPrint: true);

                // Write to a temp file first so a crash mid-write cannot corrupt the save.
                string tmp = SavePath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(tmp, SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Could not save: {e}");
            }
        }

        /// <summary>Returns null if there is no save or it could not be read.</summary>
        public static GameState Load()
        {
            if (!HasSave) return null;
            try
            {
                var state = JsonUtility.FromJson<GameState>(File.ReadAllText(SavePath));
                // Future: migrate older versions here based on state.version.
                return state;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Could not load save, starting a new game: {e}");
                return null;
            }
        }

        public static void Delete()
        {
            if (HasSave) File.Delete(SavePath);
        }
    }
}
