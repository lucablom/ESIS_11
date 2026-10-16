using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VastSpace.Core;
using VastSpace.Data;
using VastSpace.DevTools;

namespace VastSpace.EditorTools
{
    /// <summary>
    /// Menu "Vast Space" in the Unity editor:
    ///  - Create Sample Data: generates materials, recipes, galaxies and planets under Assets/Data.
    ///  - Add Game Manager To Scene: adds GameManager + DevPanel to the open scene.
    /// Numbers are placeholders for balancing; edit the assets in the Inspector afterwards.
    /// </summary>
    public static class SampleDataCreator
    {
        private const string Root = "Assets/Data";
        // In a Resources folder so GameBootstrap can load it at runtime.
        public const string DatabasePath = "Assets/Resources/" + GameBootstrap.DatabaseResourceName + ".asset";

        public static bool DatabaseExists => AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath) != null;

        [MenuItem("Vast Space/Create Sample Data")]
        public static void CreateSampleDataMenu() => CreateSampleData(askBeforeOverwrite: true);

        public static void CreateSampleData(bool askBeforeOverwrite)
        {
            if (askBeforeOverwrite && DatabaseExists &&
                !EditorUtility.DisplayDialog("Vast Space",
                    "Sample data already exists. Overwrite the values in Assets/Data with the defaults?\n" +
                    "(Sprites you assigned are kept.)", "Overwrite", "Cancel"))
                return;

            EnsureFolder(Root);
            EnsureFolder("Assets/Resources");
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Recipes");
            EnsureFolder(Root + "/Galaxies");
            EnsureFolder(Root + "/Planets");

            // ------------------------------------------------------------ Materials
            var m = new Dictionary<string, MaterialDefinition>();
            void Mat(string name, MaterialCategory cat, double price, string desc = "")
            {
                var asset = GetOrCreate<MaterialDefinition>($"{Root}/Materials/{Id(name)}.asset");
                asset.id = Id(name);
                asset.displayName = name;
                asset.category = cat;
                asset.sellPrice = price;
                asset.description = desc;
                EditorUtility.SetDirty(asset);
                m[name] = asset;
            }

            Mat("Iron Ore", MaterialCategory.Ore, 10);
            Mat("Copper Ore", MaterialCategory.Ore, 14);
            Mat("Coal Ore", MaterialCategory.Ore, 8);
            Mat("Titanium Ore", MaterialCategory.Ore, 30);
            Mat("Nickel Ore", MaterialCategory.Ore, 18);
            Mat("Zinc Ore", MaterialCategory.Ore, 16);
            Mat("Sulfur Ore", MaterialCategory.Ore, 12);
            Mat("Magnesium Ore", MaterialCategory.Ore, 20);
            Mat("Liquid Nitrogen", MaterialCategory.Ore, 22);
            Mat("Sand Particles", MaterialCategory.Ore, 4);
            Mat("Iron Ingot", MaterialCategory.Refined, 28);
            Mat("Copper Ingot", MaterialCategory.Refined, 38);
            Mat("Steel Ingot", MaterialCategory.Refined, 60);
            Mat("Steel Screw", MaterialCategory.Component, 20);
            Mat("Iron Plate", MaterialCategory.Component, 70);
            Mat("Iron Rod", MaterialCategory.Component, 35);
            Mat("Copper Wire", MaterialCategory.Component, 24);
            Mat("Metal Pipe", MaterialCategory.Component, 180);
            Mat("Mining Equipment", MaterialCategory.Equipment, 2500,
                "Needed to send an exploring team to a new planet.");

            // ------------------------------------------------------------ Recipes
            var recipes = new List<RecipeDefinition>();
            void Recipe(string name, float seconds, (string, double)[] inputs, (string, double)[] outputs)
            {
                var asset = GetOrCreate<RecipeDefinition>($"{Root}/Recipes/{Id(name)}.asset");
                asset.id = Id(name);
                asset.displayName = name;
                asset.durationSeconds = seconds;
                asset.inputs = inputs.Select(i => new MaterialAmount(m[i.Item1], i.Item2)).ToList();
                asset.outputs = outputs.Select(o => new MaterialAmount(m[o.Item1], o.Item2)).ToList();
                EditorUtility.SetDirty(asset);
                recipes.Add(asset);
            }

            Recipe("Smelt Iron Ingot", 4, new[] { ("Iron Ore", 2.0) }, new[] { ("Iron Ingot", 1.0) });
            Recipe("Smelt Copper Ingot", 4, new[] { ("Copper Ore", 2.0) }, new[] { ("Copper Ingot", 1.0) });
            Recipe("Smelt Steel Ingot", 6, new[] { ("Iron Ingot", 1.0), ("Coal Ore", 2.0) }, new[] { ("Steel Ingot", 1.0) });
            Recipe("Make Steel Screws", 3, new[] { ("Steel Ingot", 1.0) }, new[] { ("Steel Screw", 4.0) });
            Recipe("Press Iron Plate", 5, new[] { ("Iron Ingot", 2.0) }, new[] { ("Iron Plate", 1.0) });
            Recipe("Forge Iron Rod", 3, new[] { ("Iron Ingot", 1.0) }, new[] { ("Iron Rod", 1.0) });
            Recipe("Draw Copper Wire", 3, new[] { ("Copper Ingot", 1.0) }, new[] { ("Copper Wire", 2.0) });
            Recipe("Roll Metal Pipe", 8, new[] { ("Iron Plate", 2.0) }, new[] { ("Metal Pipe", 1.0) });
            Recipe("Assemble Mining Equipment", 30,
                new[] { ("Iron Plate", 5.0), ("Steel Screw", 10.0), ("Copper Wire", 5.0), ("Metal Pipe", 2.0) },
                new[] { ("Mining Equipment", 1.0) });

            // ------------------------------------------------------------ Planets (Veltrix Expanse)
            PlanetDefinition Planet(string name, Vector2 pos, float distance, float costMul, string desc,
                params (string, float)[] resources)
            {
                var asset = GetOrCreate<PlanetDefinition>($"{Root}/Planets/{Id(name)}.asset");
                asset.id = Id(name);
                asset.displayName = name;
                asset.description = desc;
                asset.mapPosition = pos;
                asset.travelDistance = distance;
                asset.exploreDurationSeconds = 110;
                asset.exploreCost = new Cost();
                asset.exploreCost.currencies.Add(new CurrencyAmount(CurrencyType.SC, 25000 * costMul));
                asset.exploreCost.materials.Add(new MaterialAmount(m["Mining Equipment"], 1));
                asset.resources = resources
                    .Select(r => new ResourceYield { material = m[r.Item1], weight = r.Item2 }).ToList();
                asset.upgrades = PlanetDefinition.DefaultUpgrades(costMul);
                EditorUtility.SetDirty(asset);
                return asset;
            }

            var veltrixPlanets = new List<PlanetDefinition>
            {
                Planet("Epsilon Wastes", new Vector2(-300, 250), 500, 1f,
                    "A shattered, ringed rock rich in the basics: iron, copper and coal.",
                    ("Iron Ore", 3), ("Copper Ore", 2), ("Coal Ore", 2)),
                Planet("Zephyron Drift", new Vector2(-50, 300), 650, 1.2f,
                    "A frozen, spiked world. Nickel veins under nitrogen ice.",
                    ("Nickel Ore", 2), ("Liquid Nitrogen", 2), ("Iron Ore", 1)),
                Planet("Myrr Sector", new Vector2(250, 220), 800, 1.5f,
                    "Volcanic and unstable, but full of sulfur and magnesium.",
                    ("Sulfur Ore", 3), ("Magnesium Ore", 2)),
                Planet("Halon Verge", new Vector2(-200, 60), 550, 1.3f,
                    "A calm gas giant with nitrogen clouds and titanium moonlets.",
                    ("Liquid Nitrogen", 3), ("Titanium Ore", 1)),
                Planet("Crimson Coil", new Vector2(380, 30), 950, 1.8f,
                    "An asteroid cluster packed with heavy metals.",
                    ("Iron Ore", 2), ("Nickel Ore", 2), ("Titanium Ore", 1)),
                Planet("Orions Maw", new Vector2(-280, -200), 700, 1.6f,
                    "A crystal-crusted planet with magnesium and zinc deposits.",
                    ("Magnesium Ore", 2), ("Zinc Ore", 2)),
                Planet("Talmaris Halo", new Vector2(30, -220), 750, 1.7f,
                    "A ringed desert world with sulfur, zinc and copper.",
                    ("Sulfur Ore", 1), ("Zinc Ore", 2), ("Copper Ore", 2)),
                Planet("Nexan Reach", new Vector2(320, -210), 1100, 2.2f,
                    "A cracked red planet far out, rich in titanium.",
                    ("Titanium Ore", 2), ("Iron Ore", 1), ("Sand Particles", 3)),
            };

            // ------------------------------------------------------------ Galaxies
            var galaxies = new List<GalaxyDefinition>();
            GalaxyDefinition Galaxy(string name, Vector2 pos, double cost, float seconds, string desc,
                List<PlanetDefinition> planets = null)
            {
                var asset = GetOrCreate<GalaxyDefinition>($"{Root}/Galaxies/{Id(name)}.asset");
                asset.id = Id(name);
                asset.displayName = name;
                asset.description = desc;
                asset.mapPosition = pos;
                asset.unlockCost = Cost.Credits(cost);
                asset.unlockDurationSeconds = seconds;
                asset.planets = planets ?? new List<PlanetDefinition>();
                EditorUtility.SetDirty(asset);
                galaxies.Add(asset);
                return asset;
            }

            Galaxy("Veltrix Expanse", new Vector2(-330, 30), 600000, 0,
                "A cold, mystic galaxy where icy worlds hide veins of iron, copper, and nickel, and drifting " +
                "pockets of coal, magnesium, sulfur, and nitrogen wait to be harvested. Ancient secrets linger " +
                "in its frozen landscapes, inviting daring explorers.", veltrixPlanets);
            Galaxy("Elarian Reach", new Vector2(-180, 190), 2500000, 300, "Coming soon.");
            Galaxy("Vesper Arc", new Vector2(-70, 290), 6000000, 600, "Coming soon.");
            Galaxy("Miridian Shards", new Vector2(0, 170), 12000000, 900, "Coming soon.");
            Galaxy("Drift Sector 9", new Vector2(160, 270), 25000000, 1200, "Coming soon.");
            Galaxy("Cyngari Chain", new Vector2(240, 120), 50000000, 1800, "Coming soon.");
            Galaxy("Kaldros Divide", new Vector2(360, 50), 100000000, 2700, "Coming soon.");
            Galaxy("Echo Sector", new Vector2(220, -70), 200000000, 3600, "Coming soon.");
            Galaxy("Zarith Belt", new Vector2(190, -180), 400000000, 5400, "Coming soon.");
            Galaxy("The Black Crown", new Vector2(-20, -200), 1000000000, 7200, "Coming soon.");

            // ------------------------------------------------------------ Database
            var db = GetOrCreate<GameDatabase>(DatabasePath);
            db.materials = m.Values.ToList();
            db.recipes = recipes;
            db.galaxies = galaxies;
            db.startingCurrencies = new List<CurrencyAmount> { new CurrencyAmount(CurrencyType.SC, 650000) };
            db.startingMaterials = new List<MaterialAmount> { new MaterialAmount(m["Mining Equipment"], 1) };
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
            Debug.Log($"[Vast Space] Sample data created in {Root}.");
        }

        [MenuItem("Vast Space/Add Game Manager To Scene")]
        public static void AddGameManagerToScene()
        {
            var existing = Object.FindFirstObjectByType<GameManager>();
            if (existing != null)
            {
                Selection.activeObject = existing.gameObject;
                Debug.Log("[Vast Space] The scene already has a GameManager.");
                return;
            }

            var go = new GameObject("GameManager");
            var gm = go.AddComponent<GameManager>();
            go.AddComponent<DevPanel>();

            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (db == null)
                Debug.LogWarning("[Vast Space] No GameDatabase found. Run 'Vast Space > Create Sample Data' and assign it.");
            else
            {
                var so = new SerializedObject(gm);
                so.FindProperty("database").objectReferenceValue = db;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Undo.RegisterCreatedObjectUndo(go, "Add Game Manager");
            Selection.activeObject = go;
        }

        [MenuItem("Vast Space/Delete Save File")]
        public static void DeleteSave()
        {
            SaveSystem.Delete();
            Debug.Log($"[Vast Space] Deleted {SaveSystem.SavePath}");
        }

        [MenuItem("Vast Space/Open Save Folder")]
        public static void OpenSaveFolder()
        {
            EditorUtility.RevealInFinder(SaveSystem.SavePath);
        }

        // ------------------------------------------------------------ Helpers

        private static string Id(string displayName) =>
            displayName.ToLowerInvariant().Replace(" ", "_");

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
