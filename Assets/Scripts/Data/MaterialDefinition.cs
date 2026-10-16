using UnityEngine;

namespace VastSpace.Data
{
    public enum MaterialCategory
    {
        Ore,        // Raw materials extracted from planets
        Refined,    // Ingots etc. made by smelting
        Component,  // Screws, plates, wires...
        Equipment   // Things used to unlock/explore (e.g. Mining Equipment)
    }

    [CreateAssetMenu(menuName = "Vast Space/Material", fileName = "NewMaterial")]
    public class MaterialDefinition : ScriptableObject
    {
        [Tooltip("Stable id used in save files. Do not change after release.")]
        public string id;
        public string displayName;
        public Sprite icon;
        [TextArea] public string description;
        public MaterialCategory category;

        [Tooltip("Price in SC per unit when sold at the space station.")]
        public double sellPrice = 1;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(id)) id = name;
            if (string.IsNullOrEmpty(displayName)) displayName = name;
        }
    }
}
