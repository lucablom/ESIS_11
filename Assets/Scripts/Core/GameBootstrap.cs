using UnityEngine;
using VastSpace.Data;
using VastSpace.UI;

namespace VastSpace.Core
{
    /// <summary>
    /// Makes sure a GameManager and the GameUI exist in every scene you press Play in, so nothing
    /// has to be set up by hand. If the scene already has them, those are used instead.
    /// The database is loaded from Assets/Resources/GameDatabase.asset.
    /// </summary>
    public static class GameBootstrap
    {
        public const string DatabaseResourceName = "GameDatabase";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGame()
        {
            if (!EnsureGameManager()) return;

            if (Object.FindFirstObjectByType<GameUI>() == null)
                new GameObject("GameUI (auto)").AddComponent<GameUI>();
        }

        private static bool EnsureGameManager()
        {
            if (Object.FindFirstObjectByType<GameManager>() != null) return true;

            var database = Resources.Load<GameDatabase>(DatabaseResourceName);
            if (database == null)
            {
                Debug.LogError("[Vast Space] No GameDatabase found in Assets/Resources. " +
                               "Run the menu 'Vast Space > Create Sample Data'.");
                return false;
            }

            // Created inactive so the database is assigned before GameManager.Awake runs.
            var go = new GameObject("GameManager (auto)");
            go.SetActive(false);
            go.AddComponent<GameManager>().SetDatabase(database);
            go.SetActive(true);
            return true;
        }
    }
}
