using System;
using System.Collections.Generic;
using UnityEngine;

namespace VastSpace.Data
{
    /// <summary>The things a player can upgrade on an operating planet.</summary>
    public enum PlanetUpgrade
    {
        ExtractionRig = 0,  // value = total units extracted per minute
        Storage = 1,        // value = max units stored per material
        ShipCapacity = 2,   // value = units the ship carries per trip (hangar size)
        ShipSpeed = 3       // value = distance units per second (hangar speed)
    }

    [Serializable]
    public class UpgradeLevel
    {
        [Tooltip("The stat value at this level (see PlanetUpgrade for meaning).")]
        public double value;
        [Tooltip("Cost to reach this level. Ignored for level 0.")]
        public Cost cost = new Cost();

        public UpgradeLevel() { }

        public UpgradeLevel(double value, double scCost)
        {
            this.value = value;
            cost = scCost > 0 ? Cost.Credits(scCost) : new Cost();
        }
    }

    [Serializable]
    public class UpgradeTrack
    {
        public PlanetUpgrade type;
        [Tooltip("Element 0 is the starting level.")]
        public List<UpgradeLevel> levels = new List<UpgradeLevel>();
    }

    [Serializable]
    public struct ResourceYield
    {
        public MaterialDefinition material;
        [Tooltip("Relative share of the extraction rate.")]
        [Min(0f)] public float weight;
    }

    [CreateAssetMenu(menuName = "Vast Space/Planet", fileName = "NewPlanet")]
    public class PlanetDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea(3, 6)] public string description;
        public Sprite icon;
        [Tooltip("Position on the galaxy map (UI coordinates).")]
        public Vector2 mapPosition;

        [Header("Explore / settle")]
        public Cost exploreCost = new Cost();
        [Min(0f)] public float exploreDurationSeconds = 110f;

        [Header("Production")]
        public List<ResourceYield> resources = new List<ResourceYield>();

        [Header("Logistics")]
        [Tooltip("Distance to the space station. Travel time = distance / ship speed.")]
        [Min(1f)] public float travelDistance = 600f;

        [Header("Upgrades")]
        public List<UpgradeTrack> upgrades = new List<UpgradeTrack>();

        public UpgradeTrack GetTrack(PlanetUpgrade type)
        {
            foreach (var track in upgrades)
                if (track != null && track.type == type) return track;
            return null;
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(id)) id = name;
            if (string.IsNullOrEmpty(displayName)) displayName = name;
        }

        /// <summary>Fills in default upgrade tracks when the asset is created in the editor.</summary>
        private void Reset()
        {
            upgrades = DefaultUpgrades(1f);
        }

        public static List<UpgradeTrack> DefaultUpgrades(float costMultiplier)
        {
            UpgradeTrack Track(PlanetUpgrade type, double[] values, double[] costs)
            {
                var t = new UpgradeTrack { type = type };
                for (int i = 0; i < values.Length; i++)
                    t.levels.Add(new UpgradeLevel(values[i], costs[i] * costMultiplier));
                return t;
            }

            return new List<UpgradeTrack>
            {
                Track(PlanetUpgrade.ExtractionRig,
                    new double[] { 30, 45, 65, 90, 125, 170 },
                    new double[] { 0, 25000, 60000, 140000, 320000, 700000 }),
                Track(PlanetUpgrade.Storage,
                    new double[] { 2000, 4000, 8000, 16000, 32000 },
                    new double[] { 0, 20000, 50000, 120000, 300000 }),
                Track(PlanetUpgrade.ShipCapacity,
                    new double[] { 500, 1000, 2000, 4000, 8000 },
                    new double[] { 0, 30000, 75000, 180000, 420000 }),
                Track(PlanetUpgrade.ShipSpeed,
                    new double[] { 5, 7, 10, 14, 20 },
                    new double[] { 0, 25000, 60000, 150000, 350000 }),
            };
        }
    }
}
