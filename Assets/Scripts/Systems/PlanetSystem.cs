using System;
using VastSpace.Core;
using VastSpace.Data;
using VastSpace.State;
using VastSpace.Util;

namespace VastSpace.Systems
{
    /// <summary>
    /// Galaxy unlocking, planet exploration, planet upgrades and resource extraction.
    /// </summary>
    public class PlanetSystem
    {
        private readonly GameState _state;
        private readonly GameDatabase _db;

        public PlanetSystem(GameState state, GameDatabase db)
        {
            _state = state;
            _db = db;
        }

        private double Now => _state.gameTime;

        // ---------------------------------------------------------------- Galaxies

        public GalaxyState GetState(GalaxyDefinition galaxy) => _state.GetGalaxy(galaxy.id);

        public bool IsUnlocked(GalaxyDefinition galaxy) => GetState(galaxy).status == UnlockStatus.Unlocked;

        public double GetUnlockTimeRemaining(GalaxyDefinition galaxy)
        {
            var gs = GetState(galaxy);
            return gs.status == UnlockStatus.Unlocking ? Math.Max(0, gs.unlockCompletesAt - Now) : 0;
        }

        public ActionResult UnlockGalaxy(GalaxyDefinition galaxy)
        {
            var gs = GetState(galaxy);
            if (gs.status != UnlockStatus.Locked)
                return ActionResult.Fail($"{galaxy.displayName} is already unlocked.");

            if (!Economy.TryPay(_state, galaxy.unlockCost, out var reason))
                return ActionResult.Fail(reason);

            if (galaxy.unlockDurationSeconds <= 0)
            {
                gs.status = UnlockStatus.Unlocked;
                GameEvents.RaiseMessage($"{galaxy.displayName} unlocked.");
            }
            else
            {
                gs.status = UnlockStatus.Unlocking;
                gs.unlockCompletesAt = Now + galaxy.unlockDurationSeconds;
                GameEvents.RaiseMessage($"Unlocking {galaxy.displayName} ({NumberFormat.Duration(galaxy.unlockDurationSeconds)}).");
            }

            GameEvents.RaiseGalaxyChanged(galaxy.id);
            return ActionResult.Ok();
        }

        // ---------------------------------------------------------------- Planets

        public PlanetState GetState(PlanetDefinition planet) => _state.GetPlanet(planet.id);

        public bool IsOperating(PlanetDefinition planet) => GetState(planet).status == UnlockStatus.Unlocked;

        public double GetExploreTimeRemaining(PlanetDefinition planet)
        {
            var ps = GetState(planet);
            return ps.status == UnlockStatus.Unlocking ? Math.Max(0, ps.exploreCompletesAt - Now) : 0;
        }

        /// <summary>0..1 progress of the exploration timer.</summary>
        public float GetExploreProgress(PlanetDefinition planet)
        {
            var ps = GetState(planet);
            if (ps.status == UnlockStatus.Unlocked) return 1f;
            if (ps.status == UnlockStatus.Locked || planet.exploreDurationSeconds <= 0) return 0f;
            double remaining = GetExploreTimeRemaining(planet);
            return (float)(1.0 - remaining / planet.exploreDurationSeconds);
        }

        public ActionResult StartExploration(PlanetDefinition planet)
        {
            var galaxy = _db.GetGalaxyOfPlanet(planet.id);
            if (galaxy != null && !IsUnlocked(galaxy))
                return ActionResult.Fail($"Unlock {galaxy.displayName} first.");

            var ps = GetState(planet);
            if (ps.status != UnlockStatus.Locked)
                return ActionResult.Fail($"{planet.displayName} is already explored.");

            if (!Economy.TryPay(_state, planet.exploreCost, out var reason))
                return ActionResult.Fail(reason);

            if (planet.exploreDurationSeconds <= 0)
            {
                ps.status = UnlockStatus.Unlocked;
                GameEvents.RaiseMessage($"{planet.displayName} is now operating.");
            }
            else
            {
                ps.status = UnlockStatus.Unlocking;
                ps.exploreCompletesAt = Now + planet.exploreDurationSeconds;
                GameEvents.RaiseMessage($"Exploration team sent to {planet.displayName}.");
            }

            GameEvents.RaisePlanetChanged(planet.id);
            return ActionResult.Ok();
        }

        // ---------------------------------------------------------------- Upgrades

        public int GetLevel(PlanetDefinition planet, PlanetUpgrade type) => GetState(planet).GetLevel(type);

        /// <summary>Highest level index, e.g. 4 if the track has 5 entries.</summary>
        public int GetMaxLevel(PlanetDefinition planet, PlanetUpgrade type)
        {
            var track = planet.GetTrack(type);
            return track == null ? 0 : Math.Max(0, track.levels.Count - 1);
        }

        public bool IsMaxLevel(PlanetDefinition planet, PlanetUpgrade type) =>
            GetLevel(planet, type) >= GetMaxLevel(planet, type);

        /// <summary>The current stat value (rate, capacity, speed...).</summary>
        public double GetValue(PlanetDefinition planet, PlanetUpgrade type) =>
            GetValueAtLevel(planet, type, GetLevel(planet, type));

        public double GetValueAtLevel(PlanetDefinition planet, PlanetUpgrade type, int level)
        {
            var track = planet.GetTrack(type);
            if (track == null || track.levels.Count == 0)
            {
                UnityEngine.Debug.LogWarning($"[PlanetSystem] {planet.name} has no {type} upgrade track.", planet);
                return 0;
            }
            level = Math.Max(0, Math.Min(level, track.levels.Count - 1));
            return track.levels[level].value;
        }

        /// <summary>Cost of the next level, or null if already max level.</summary>
        public Cost GetNextUpgradeCost(PlanetDefinition planet, PlanetUpgrade type)
        {
            if (IsMaxLevel(planet, type)) return null;
            return planet.GetTrack(type).levels[GetLevel(planet, type) + 1].cost;
        }

        public ActionResult Upgrade(PlanetDefinition planet, PlanetUpgrade type)
        {
            if (!IsOperating(planet))
                return ActionResult.Fail($"{planet.displayName} must be explored first.");
            if (IsMaxLevel(planet, type))
                return ActionResult.Fail($"{UpgradeName(type)} is already at max level.");

            var cost = GetNextUpgradeCost(planet, type);
            if (!Economy.TryPay(_state, cost, out var reason))
                return ActionResult.Fail(reason);

            var ps = GetState(planet);
            int newLevel = ps.GetLevel(type) + 1;
            ps.SetLevel(type, newLevel);

            GameEvents.RaiseMessage($"{planet.displayName}: {UpgradeName(type)} upgraded to level {newLevel + 1}.");
            GameEvents.RaisePlanetChanged(planet.id);
            return ActionResult.Ok();
        }

        public static string UpgradeName(PlanetUpgrade type)
        {
            switch (type)
            {
                case PlanetUpgrade.ExtractionRig: return "Extraction Rig";
                case PlanetUpgrade.Storage: return "Storage";
                case PlanetUpgrade.ShipCapacity: return "Hangar - Ship Capacity";
                case PlanetUpgrade.ShipSpeed: return "Hangar - Ship Speed";
                default: return type.ToString();
            }
        }

        // ---------------------------------------------------------------- Extraction

        public double GetStorageCapacity(PlanetDefinition planet) => GetValue(planet, PlanetUpgrade.Storage);

        /// <summary>Units per minute of one material at the current rig level.</summary>
        public double GetExtractionRatePerMinute(PlanetDefinition planet, MaterialDefinition material)
        {
            double totalWeight = GetTotalWeight(planet);
            if (totalWeight <= 0) return 0;

            double rate = GetValue(planet, PlanetUpgrade.ExtractionRig);
            double weight = 0;
            foreach (var r in planet.resources)
                if (r.material == material) weight += r.weight;
            return rate * weight / totalWeight;
        }

        /// <summary>True if every material on the planet is at max storage (extraction has stopped).</summary>
        public bool IsStorageFull(PlanetDefinition planet)
        {
            var ps = GetState(planet);
            double cap = GetStorageCapacity(planet);
            foreach (var r in planet.resources)
                if (r.material != null && r.weight > 0 && ps.storage.Get(r.material.id) < cap - 1e-6)
                    return false;
            return true;
        }

        private static double GetTotalWeight(PlanetDefinition planet)
        {
            double total = 0;
            foreach (var r in planet.resources)
                if (r.material != null) total += Math.Max(0, r.weight);
            return total;
        }

        /// <summary>Advances timers and extraction. <paramref name="dt"/> has already been added to game time.</summary>
        public void Tick(double dt)
        {
            foreach (var gs in _state.galaxies)
            {
                if (gs.status == UnlockStatus.Unlocking && Now >= gs.unlockCompletesAt)
                {
                    gs.status = UnlockStatus.Unlocked;
                    var def = _db.GetGalaxy(gs.id);
                    GameEvents.RaiseMessage($"{(def != null ? def.displayName : gs.id)} unlocked.");
                    GameEvents.RaiseGalaxyChanged(gs.id);
                }
            }

            foreach (var ps in _state.planets)
            {
                var def = _db.GetPlanet(ps.id);
                if (def == null) continue;

                double effectiveDt = dt;
                if (ps.status == UnlockStatus.Unlocking)
                {
                    if (Now < ps.exploreCompletesAt) continue;
                    ps.status = UnlockStatus.Unlocked;
                    // Only extract for the part of this step after exploration finished.
                    effectiveDt = Math.Min(dt, Now - ps.exploreCompletesAt);
                    GameEvents.RaiseMessage($"{def.displayName} explored - extraction started.");
                    GameEvents.RaisePlanetChanged(ps.id);
                }

                if (ps.status == UnlockStatus.Unlocked)
                    Extract(def, ps, effectiveDt);
            }
        }

        private void Extract(PlanetDefinition def, PlanetState ps, double dt)
        {
            if (dt <= 0) return;
            double totalWeight = GetTotalWeight(def);
            if (totalWeight <= 0) return;

            double ratePerSecond = GetValue(def, PlanetUpgrade.ExtractionRig) / 60.0;
            double capacity = GetStorageCapacity(def);

            foreach (var r in def.resources)
            {
                if (r.material == null || r.weight <= 0) continue;
                double current = ps.storage.Get(r.material.id);
                double space = capacity - current;
                if (space <= 0) continue;

                double produced = Math.Min(space, ratePerSecond * dt * r.weight / totalWeight);
                ps.storage.Add(r.material.id, produced);
                ps.totalExtracted += produced;
            }
        }
    }
}
