using System;
using System.Collections.Generic;
using VastSpace.Data;

namespace VastSpace.State
{
    public enum UnlockStatus
    {
        Locked = 0,
        Unlocking = 1,   // waiting for a timer (galaxy unlock / planet exploration)
        Unlocked = 2     // galaxy available / planet operating
    }

    [Serializable]
    public class GalaxyState
    {
        public string id;
        public UnlockStatus status;
        public double unlockCompletesAt;
    }

    [Serializable]
    public class PlanetState
    {
        public string id;
        public UnlockStatus status;
        public double exploreCompletesAt;

        /// <summary>Index = (int)PlanetUpgrade.</summary>
        public int[] upgradeLevels = new int[4];

        public Inventory storage = new Inventory();
        public double totalExtracted;

        /// <summary>Game time when the ship is back in the hangar.</summary>
        public double shipReturnsAt;
        /// <summary>Automatically send the ship whenever it is home. Can be gated behind research later.</summary>
        public bool autoShip;

        public int GetLevel(PlanetUpgrade type)
        {
            int i = (int)type;
            if (upgradeLevels == null || upgradeLevels.Length <= i) return 0;
            return upgradeLevels[i];
        }

        public void SetLevel(PlanetUpgrade type, int level)
        {
            int i = (int)type;
            if (upgradeLevels == null || upgradeLevels.Length <= i)
                Array.Resize(ref upgradeLevels, Enum.GetValues(typeof(PlanetUpgrade)).Length);
            upgradeLevels[i] = level;
        }
    }

    [Serializable]
    public class Shipment
    {
        public int id;
        public string planetId;
        public Inventory cargo = new Inventory();
        public double departedAt;
        public double arrivesAt;
    }

    [Serializable]
    public class SmeltingJob
    {
        public string recipeId;
        public int batchesRemaining;
        /// <summary>Only valid for the first job in the queue (the one running).</summary>
        public double batchCompletesAt;
    }

    /// <summary>
    /// Everything that is saved. No Unity object references, only ids,
    /// so it can be serialized with JsonUtility.
    /// All times are in "game time" seconds (see <see cref="gameTime"/>).
    /// </summary>
    [Serializable]
    public class GameState
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        /// <summary>Simulated seconds since the game started. Includes offline time.</summary>
        public double gameTime;
        /// <summary>Real seconds the game has actually been open.</summary>
        public double playTimeSeconds;
        public long lastSavedUtcTicks;

        /// <summary>Index = (int)CurrencyType.</summary>
        public double[] currencies = new double[3];

        public Inventory station = new Inventory();
        public List<GalaxyState> galaxies = new List<GalaxyState>();
        public List<PlanetState> planets = new List<PlanetState>();
        public List<Shipment> shipments = new List<Shipment>();
        public List<SmeltingJob> smeltingQueue = new List<SmeltingJob>();
        public int nextShipmentId = 1;

        public double GetCurrency(CurrencyType type)
        {
            EnsureCurrencyArray();
            return currencies[(int)type];
        }

        public void AddCurrency(CurrencyType type, double amount)
        {
            EnsureCurrencyArray();
            currencies[(int)type] += amount;
        }

        private void EnsureCurrencyArray()
        {
            int count = Enum.GetValues(typeof(CurrencyType)).Length;
            if (currencies == null || currencies.Length < count)
                Array.Resize(ref currencies, count);
        }

        public GalaxyState GetGalaxy(string id)
        {
            foreach (var g in galaxies) if (g.id == id) return g;
            var created = new GalaxyState { id = id };
            galaxies.Add(created);
            return created;
        }

        public PlanetState GetPlanet(string id)
        {
            foreach (var p in planets) if (p.id == id) return p;
            var created = new PlanetState { id = id };
            planets.Add(created);
            return created;
        }

        public static GameState CreateNew(GameDatabase db)
        {
            var state = new GameState();
            foreach (var c in db.startingCurrencies) state.AddCurrency(c.currency, c.amount);
            foreach (var m in db.startingMaterials)
                if (m.material != null) state.station.Add(m.material.id, m.amount);
            return state;
        }
    }
}
