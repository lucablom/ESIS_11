using System.Collections.Generic;
using UnityEngine;

namespace VastSpace.Data
{
    [CreateAssetMenu(menuName = "Vast Space/Galaxy", fileName = "NewGalaxy")]
    public class GalaxyDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea(3, 6)] public string description;
        public Sprite icon;
        [Tooltip("Position on the space station map (UI coordinates).")]
        public Vector2 mapPosition;

        [Header("Unlock")]
        public Cost unlockCost = new Cost();
        [Tooltip("0 = instant.")]
        [Min(0f)] public float unlockDurationSeconds = 0f;

        [Header("Content")]
        public List<PlanetDefinition> planets = new List<PlanetDefinition>();

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(id)) id = name;
            if (string.IsNullOrEmpty(displayName)) displayName = name;
        }
    }
}
