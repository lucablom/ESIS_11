using System.Collections.Generic;
using UnityEngine;

namespace VastSpace.Data
{
    /// <summary>
    /// Single asset that references all game content. Assign it to the GameManager.
    /// </summary>
    [CreateAssetMenu(menuName = "Vast Space/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        public List<MaterialDefinition> materials = new List<MaterialDefinition>();
        public List<RecipeDefinition> recipes = new List<RecipeDefinition>();
        public List<GalaxyDefinition> galaxies = new List<GalaxyDefinition>();

        [Header("New game")]
        public List<CurrencyAmount> startingCurrencies = new List<CurrencyAmount>();
        public List<MaterialAmount> startingMaterials = new List<MaterialAmount>();

        [Header("Calendar")]
        [Tooltip("In-game date when a new game starts.")]
        public int startYear = 2083;
        [Tooltip("Real seconds per in-game day.")]
        [Min(1f)] public float secondsPerGameDay = 60f;

        private Dictionary<string, MaterialDefinition> _materials;
        private Dictionary<string, RecipeDefinition> _recipes;
        private Dictionary<string, GalaxyDefinition> _galaxies;
        private Dictionary<string, PlanetDefinition> _planets;
        private Dictionary<string, GalaxyDefinition> _galaxyOfPlanet;

        /// <summary>Builds lookup tables. Called by the GameManager on startup.</summary>
        public void Initialize()
        {
            _materials = new Dictionary<string, MaterialDefinition>();
            _recipes = new Dictionary<string, RecipeDefinition>();
            _galaxies = new Dictionary<string, GalaxyDefinition>();
            _planets = new Dictionary<string, PlanetDefinition>();
            _galaxyOfPlanet = new Dictionary<string, GalaxyDefinition>();

            foreach (var m in materials) AddUnique(_materials, m != null ? m.id : null, m);
            foreach (var r in recipes) AddUnique(_recipes, r != null ? r.id : null, r);
            foreach (var g in galaxies)
            {
                if (g == null) continue;
                AddUnique(_galaxies, g.id, g);
                foreach (var p in g.planets)
                {
                    if (p == null) continue;
                    AddUnique(_planets, p.id, p);
                    _galaxyOfPlanet[p.id] = g;
                }
            }
        }

        private void AddUnique<T>(Dictionary<string, T> dict, string id, T item) where T : Object
        {
            if (item == null) return;
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"[GameDatabase] {item.name} has no id.", item);
                return;
            }
            if (dict.ContainsKey(id))
            {
                Debug.LogWarning($"[GameDatabase] Duplicate id '{id}' ({item.name}).", item);
                return;
            }
            dict[id] = item;
        }

        private void EnsureInitialized()
        {
            if (_materials == null) Initialize();
        }

        public MaterialDefinition GetMaterial(string id) { EnsureInitialized(); return Find(_materials, id); }
        public RecipeDefinition GetRecipe(string id) { EnsureInitialized(); return Find(_recipes, id); }
        public GalaxyDefinition GetGalaxy(string id) { EnsureInitialized(); return Find(_galaxies, id); }
        public PlanetDefinition GetPlanet(string id) { EnsureInitialized(); return Find(_planets, id); }
        public GalaxyDefinition GetGalaxyOfPlanet(string planetId) { EnsureInitialized(); return Find(_galaxyOfPlanet, planetId); }

        private static T Find<T>(Dictionary<string, T> dict, string id) where T : class
        {
            if (id == null) return null;
            return dict.TryGetValue(id, out var v) ? v : null;
        }
    }
}
