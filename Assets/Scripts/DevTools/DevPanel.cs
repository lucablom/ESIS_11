using System.Collections.Generic;
using UnityEngine;
using VastSpace.Core;
using VastSpace.Data;
using VastSpace.State;
using VastSpace.Util;

namespace VastSpace.DevTools
{
    /// <summary>
    /// Temporary on-screen panel (IMGUI) for testing the game logic before the real UI exists.
    /// Add it next to the GameManager and press Play. Remove it from the scene for release builds.
    /// </summary>
    public class DevPanel : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [SerializeField, Range(0.5f, 3f)] private float uiScale = 1f;
        [SerializeField] private double cheatAmount = 100000;

        private enum Tab { Galaxies, Station, Smelting }
        private Tab _tab;
        private GalaxyDefinition _selectedGalaxy;
        private Vector2 _scroll;
        private readonly List<string> _log = new List<string>();

        private void OnEnable() => GameEvents.Message += OnMessage;
        private void OnDisable() => GameEvents.Message -= OnMessage;

        private void OnMessage(string text)
        {
            _log.Insert(0, text);
            if (_log.Count > 6) _log.RemoveAt(_log.Count - 1);
        }

        // IMGUI draws each frame in several passes (Layout, Repaint, input). Changing state while
        // drawing makes the passes disagree, so button actions are queued and run after drawing.
        private readonly List<System.Action> _pending = new List<System.Action>();

        private void Later(System.Action action) => _pending.Add(action);

        private void Do(System.Func<ActionResult> action) => Later(() =>
        {
            var result = action();
            if (!result.Success) OnMessage("<color=#ff7070>" + result.Message + "</color>");
        });

        private void RunPending()
        {
            if (_pending.Count == 0) return;
            var actions = new List<System.Action>(_pending);
            _pending.Clear();
            foreach (var a in actions) a();
        }

        private void OnGUI()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State == null) return;

            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            float width = Screen.width / uiScale;
            float height = Screen.height / uiScale;

            if (GUI.Button(new Rect(width - 90, 5, 85, 25), visible ? "Hide dev" : "Dev panel"))
                visible = !visible;
            if (!visible)
            {
                _pending.Clear();
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, Mathf.Min(560, width - 110), height - 20), GUI.skin.box);
            DrawHeader(gm);
            _scroll = GUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case Tab.Galaxies: DrawGalaxies(gm); break;
                case Tab.Station: DrawStation(gm); break;
                case Tab.Smelting: DrawSmelting(gm); break;
            }
            GUILayout.EndScrollView();
            DrawLog();
            GUILayout.EndArea();
            RunPending();
        }

        private void DrawHeader(GameManager gm)
        {
            var s = gm.State;
            GUILayout.Label(
                $"<b>{NumberFormat.Money(s.GetCurrency(CurrencyType.SC))} SC   " +
                $"{NumberFormat.Money(s.GetCurrency(CurrencyType.SSC))} SSC   " +
                $"{NumberFormat.Money(s.GetCurrency(CurrencyType.ST))} ST</b>   " +
                $"{gm.GameDate:yyyy-MM-dd}   Played {NumberFormat.Clock(s.playTimeSeconds)}",
                RichLabel());

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_tab == Tab.Galaxies, "Galaxies", GUI.skin.button) && _tab != Tab.Galaxies) Later(() => _tab = Tab.Galaxies);
            if (GUILayout.Toggle(_tab == Tab.Station, "Station / Market", GUI.skin.button) && _tab != Tab.Station) Later(() => _tab = Tab.Station);
            if (GUILayout.Toggle(_tab == Tab.Smelting, "Smelting", GUI.skin.button) && _tab != Tab.Smelting) Later(() => _tab = Tab.Smelting);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Speed x{gm.SimulationSpeed:0.#}", GUILayout.Width(80));
            gm.SimulationSpeed = GUILayout.HorizontalSlider(gm.SimulationSpeed, 0f, 100f, GUILayout.Width(120));
            if (GUILayout.Button($"+{NumberFormat.Short(cheatAmount)} SC"))
                Later(() =>
                {
                    gm.State.AddCurrency(CurrencyType.SC, cheatAmount);
                    GameEvents.RaiseCurrenciesChanged();
                });
            if (GUILayout.Button("+1 min")) Later(() => gm.Advance(60));
            if (GUILayout.Button("Save")) Later(() => { gm.Save(); OnMessage("Saved."); });
            if (GUILayout.Button("Reset")) Later(() => { _selectedGalaxy = null; gm.ResetGame(); });
            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------ Galaxies & planets

        private void DrawGalaxies(GameManager gm)
        {
            if (_selectedGalaxy != null)
            {
                if (GUILayout.Button("<- Space Station")) Later(() => _selectedGalaxy = null);
                DrawGalaxy(gm, _selectedGalaxy);
                return;
            }

            foreach (var galaxy in gm.Database.galaxies)
            {
                if (galaxy == null) continue;
                GUILayout.BeginHorizontal(GUI.skin.box);
                var gs = gm.Planets.GetState(galaxy);
                GUILayout.Label($"<b>{galaxy.displayName}</b>  ({galaxy.planets.Count} planets)", RichLabel(), GUILayout.Width(260));

                switch (gs.status)
                {
                    case UnlockStatus.Locked:
                        if (GUILayout.Button($"Unlock ({Economy.Describe(galaxy.unlockCost)})"))
                            Do(() => gm.Planets.UnlockGalaxy(galaxy));
                        break;
                    case UnlockStatus.Unlocking:
                        GUILayout.Label($"Unlocking... {NumberFormat.Duration(gm.Planets.GetUnlockTimeRemaining(galaxy))}");
                        break;
                    case UnlockStatus.Unlocked:
                        if (GUILayout.Button("Enter")) Later(() => _selectedGalaxy = galaxy);
                        break;
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawGalaxy(GameManager gm, GalaxyDefinition galaxy)
        {
            GUILayout.Label($"<size=16><b>{galaxy.displayName}</b></size>", RichLabel());
            foreach (var planet in galaxy.planets)
            {
                if (planet == null) continue;
                GUILayout.BeginVertical(GUI.skin.box);
                DrawPlanet(gm, planet);
                GUILayout.EndVertical();
            }
        }

        private void DrawPlanet(GameManager gm, PlanetDefinition planet)
        {
            var planets = gm.Planets;
            var ps = planets.GetState(planet);
            GUILayout.Label($"<b>{planet.displayName}</b>", RichLabel());

            if (ps.status == UnlockStatus.Locked)
            {
                GUILayout.Label($"Not explored. Time {NumberFormat.Duration(planet.exploreDurationSeconds)}, cost {Economy.Describe(planet.exploreCost)}");
                if (GUILayout.Button("Allocate exploring team")) Do(() => planets.StartExploration(planet));
                return;
            }

            if (ps.status == UnlockStatus.Unlocking)
            {
                GUILayout.Label($"Exploring... {NumberFormat.Duration(planets.GetExploreTimeRemaining(planet))} " +
                                $"({planets.GetExploreProgress(planet) * 100f:0}%)");
                return;
            }

            // Operating
            double cap = planets.GetStorageCapacity(planet);
            GUILayout.Label($"Extraction {NumberFormat.Amount(planets.GetValue(planet, PlanetUpgrade.ExtractionRig))}/min" +
                            (planets.IsStorageFull(planet) ? "  <color=#ffb040>STORAGE FULL</color>" : ""), RichLabel());
            foreach (var r in planet.resources)
            {
                if (r.material == null) continue;
                GUILayout.Label($"   {r.material.displayName}: {NumberFormat.Amount(ps.storage.Get(r.material.id))} / {NumberFormat.Amount(cap)}" +
                                $"   (+{NumberFormat.Amount(planets.GetExtractionRatePerMinute(planet, r.material))}/min)");
            }

            // Hangar / ship
            var shipping = gm.Shipping;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Ship: {NumberFormat.Amount(shipping.GetShipCapacity(planet))} units, " +
                            $"{NumberFormat.Duration(shipping.GetTravelTime(planet))} one way", GUILayout.Width(260));
            if (shipping.IsShipHome(planet))
            {
                if (GUILayout.Button("Send ship")) Do(() => shipping.SendShip(planet));
            }
            else
            {
                GUILayout.Label($"Back in {NumberFormat.Duration(shipping.GetShipReturnTimeRemaining(planet))}");
            }
            bool auto = GUILayout.Toggle(ps.autoShip, "Auto");
            if (auto != ps.autoShip) Later(() => shipping.SetAutoShip(planet, auto));
            GUILayout.EndHorizontal();

            // Upgrades
            foreach (PlanetUpgrade type in System.Enum.GetValues(typeof(PlanetUpgrade)))
            {
                GUILayout.BeginHorizontal();
                int level = planets.GetLevel(planet, type);
                GUILayout.Label($"{PlanetSystemName(type)} Lv {level + 1}/{planets.GetMaxLevel(planet, type) + 1}: " +
                                NumberFormat.Amount(planets.GetValue(planet, type)), GUILayout.Width(260));
                if (planets.IsMaxLevel(planet, type))
                {
                    GUILayout.Label("MAX");
                }
                else
                {
                    double next = planets.GetValueAtLevel(planet, type, level + 1);
                    if (GUILayout.Button($"-> {NumberFormat.Amount(next)} ({Economy.Describe(planets.GetNextUpgradeCost(planet, type))})"))
                        Do(() => planets.Upgrade(planet, type));
                }
                GUILayout.EndHorizontal();
            }
        }

        private static string PlanetSystemName(PlanetUpgrade type) => Systems.PlanetSystem.UpgradeName(type);

        // ------------------------------------------------------------ Station

        private void DrawStation(GameManager gm)
        {
            foreach (var t in gm.Shipping.GetShipmentsInTransit())
            {
                var planet = gm.Database.GetPlanet(t.planetId);
                GUILayout.Label($"Incoming from {(planet != null ? planet.displayName : t.planetId)}: " +
                                $"{NumberFormat.Amount(t.cargo.Total)} units, {NumberFormat.Duration(gm.Shipping.GetArrivalTimeRemaining(t))}");
            }

            GUILayout.Label("<b>Station inventory</b>", RichLabel());
            bool any = false;
            foreach (var material in gm.Database.materials)
            {
                if (material == null) continue;
                double amount = gm.State.station.Get(material.id);
                if (amount < 1) continue;
                any = true;

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{material.displayName}: {NumberFormat.Amount(amount)}  ({NumberFormat.Money(material.sellPrice)} SC each)",
                    GUILayout.Width(330));
                if (GUILayout.Button("Sell 100")) Do(() => gm.Station.Sell(material, 100));
                if (GUILayout.Button("Sell all")) Do(() => gm.Station.SellAll(material));
                GUILayout.EndHorizontal();
            }
            if (!any) GUILayout.Label("Empty. Send ships from your planets.");
        }

        // ------------------------------------------------------------ Smelting

        private void DrawSmelting(GameManager gm)
        {
            var queue = gm.State.smeltingQueue;
            if (queue.Count > 0)
            {
                GUILayout.Label($"<b>Queue</b> (current batch {gm.Station.GetCurrentBatchProgress() * 100f:0}%)", RichLabel());
                int cancelIndex = -1;
                for (int i = 0; i < queue.Count; i++)
                {
                    var recipe = gm.Database.GetRecipe(queue[i].recipeId);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{(recipe != null ? recipe.displayName : queue[i].recipeId)} x{queue[i].batchesRemaining}", GUILayout.Width(330));
                    if (GUILayout.Button("Cancel")) cancelIndex = i;
                    GUILayout.EndHorizontal();
                }
                if (cancelIndex >= 0) Do(() => gm.Station.CancelSmelting(cancelIndex));
            }

            GUILayout.Label("<b>Recipes</b>", RichLabel());
            foreach (var recipe in gm.Database.recipes)
            {
                if (recipe == null) continue;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"<b>{recipe.displayName}</b>  {Describe(recipe.inputs)}  ->  {Describe(recipe.outputs)}  ({recipe.durationSeconds:0.#}s)", RichLabel());
                GUILayout.BeginHorizontal();
                int max = gm.Station.GetMaxBatches(recipe);
                GUILayout.Label($"Can make {max}", GUILayout.Width(100));
                if (GUILayout.Button("x1")) Do(() => gm.Station.QueueSmelting(recipe, 1));
                if (GUILayout.Button("x10")) Do(() => gm.Station.QueueSmelting(recipe, 10));
                if (GUILayout.Button("Max") && max > 0) Do(() => gm.Station.QueueSmelting(recipe, max));
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }

        private static string Describe(List<MaterialAmount> list)
        {
            var parts = new List<string>();
            foreach (var m in list)
                if (m.material != null) parts.Add($"{NumberFormat.Amount(m.amount)} {m.material.displayName}");
            return string.Join(" + ", parts);
        }

        // ------------------------------------------------------------ Misc

        private void DrawLog()
        {
            foreach (var line in _log) GUILayout.Label(line, RichLabel());
        }

        private GUIStyle _rich;
        private GUIStyle RichLabel()
        {
            if (_rich == null) _rich = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
            return _rich;
        }
    }
}
