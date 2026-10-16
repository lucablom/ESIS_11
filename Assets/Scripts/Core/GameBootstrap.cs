using UnityEngine;
using VastSpace.Data;
using VastSpace.DevTools;

namespace VastSpace.Core
{
    /// <summary>
    /// Makes sure a GameManager exists in every scene you press Play in, so nothing has to be
    /// set up by hand. If the scene already has a GameManager, that one is used instead.
    /// The database is loaded from Assets/Resources/GameDatabase.asset.
    /// </summary>
    public static class GameBootstrap
    {
        public const string DatabaseResourceName = "GameDatabase";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGameManager()
        {
            if (Object.FindFirstObjectByType<GameManager>() != null) return;

            var database = Resources.Load<GameDatabase>(DatabaseResourceName);
            if (database == null)
            {
                Debug.LogError("[Vast Space] No GameDatabase found in Assets/Resources. " +
                               "Run the menu 'Vast Space > Create Sample Data'.");
                return;
            }

            // Created inactive so the database is assigned before GameManager.Awake runs.
            var go = new GameObject("GameManager (auto)");
            go.SetActive(false);
            go.AddComponent<GameManager>().SetDatabase(database);
            go.AddComponent<DevPanel>();
            go.SetActive(true);
        }
    }
}
