using System.Collections.Generic;
using UnityEngine;

namespace VastSpace.Data
{
    /// <summary>
    /// A smelting/crafting recipe processed at the space station.
    /// Inputs are taken when the job is queued, outputs are added when each batch finishes.
    /// </summary>
    [CreateAssetMenu(menuName = "Vast Space/Recipe", fileName = "NewRecipe")]
    public class RecipeDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public List<MaterialAmount> inputs = new List<MaterialAmount>();
        public List<MaterialAmount> outputs = new List<MaterialAmount>();

        [Tooltip("Seconds per batch.")]
        [Min(0.1f)] public float durationSeconds = 10f;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(id)) id = name;
            if (string.IsNullOrEmpty(displayName)) displayName = name;
        }
    }
}
