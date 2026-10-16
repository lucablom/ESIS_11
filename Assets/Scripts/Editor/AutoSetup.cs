using UnityEditor;
using UnityEngine;

namespace VastSpace.EditorTools
{
    /// <summary>
    /// When the project is opened and no GameDatabase exists yet, the sample data is created
    /// automatically, so pressing Play works right away.
    /// </summary>
    [InitializeOnLoad]
    public static class AutoSetup
    {
        static AutoSetup()
        {
            // Wait until the editor has finished importing before touching assets.
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (SampleDataCreator.DatabaseExists) return;

                Debug.Log("[Vast Space] No game data found - creating sample data.");
                SampleDataCreator.CreateSampleData(askBeforeOverwrite: false);
            };
        }
    }
}
