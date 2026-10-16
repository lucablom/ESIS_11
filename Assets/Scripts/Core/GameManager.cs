using System;
using UnityEngine;
using VastSpace.Data;
using VastSpace.State;
using VastSpace.Systems;
using VastSpace.Util;

namespace VastSpace.Core
{
    /// <summary>
    /// Entry point. Put one in the first scene and assign the GameDatabase.
    /// Owns the game state, runs the simulation every frame, saves and handles offline progress.
    /// UI scripts access everything through <see cref="Instance"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private GameDatabase database;

        [Header("Saving")]
        [SerializeField] private bool loadSaveOnStart = true;
        [SerializeField, Min(5f)] private float autosaveIntervalSeconds = 30f;

        [Header("Offline progress")]
        [SerializeField] private bool simulateOfflineTime = true;
        [SerializeField, Min(0f)] private float maxOfflineHours = 24f;

        [Header("Debug")]
        [Tooltip("Speeds up the whole simulation. Handy for testing timers.")]
        [SerializeField, Range(0f, 100f)] private float simulationSpeed = 1f;

        public GameDatabase Database => database;
        public GameState State { get; private set; }
        public PlanetSystem Planets { get; private set; }
        public ShippingSystem Shipping { get; private set; }
        public StationSystem Station { get; private set; }

        public float SimulationSpeed
        {
            get => simulationSpeed;
            set => simulationSpeed = Mathf.Max(0f, value);
        }

        /// <summary>In-game calendar date derived from game time (e.g. 2083-05-16).</summary>
        public DateTime GameDate =>
            new DateTime(database.startYear, 1, 1).AddDays(State.gameTime / database.secondsPerGameDay);

        private float _autosaveTimer;

        // Supports "Enter Play Mode" without domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            GameEvents.Suppressed = false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (database == null)
            {
                Debug.LogError("[GameManager] No GameDatabase assigned. Use 'Vast Space > Create Sample Data' to make one.", this);
                enabled = false;
                return;
            }

            database.Initialize();

            GameState loaded = loadSaveOnStart ? SaveSystem.Load() : null;
            SetState(loaded ?? GameState.CreateNew(database));

            if (loaded != null && simulateOfflineTime) ApplyOfflineProgress();

            GameEvents.RaiseGameLoaded();
        }

        private void SetState(GameState state)
        {
            State = state;
            Planets = new PlanetSystem(State, database);
            Shipping = new ShippingSystem(State, database, Planets);
            Station = new StationSystem(State, database);
        }

        private void Update()
        {
            float realDt = Time.unscaledDeltaTime;
            State.playTimeSeconds += realDt;
            Advance(realDt * simulationSpeed);

            _autosaveTimer += realDt;
            if (_autosaveTimer >= autosaveIntervalSeconds)
            {
                _autosaveTimer = 0f;
                Save();
            }
        }

        /// <summary>Runs the simulation forward by <paramref name="seconds"/> of game time.</summary>
        public void Advance(double seconds)
        {
            if (seconds <= 0) return;
            State.gameTime += seconds;
            Planets.Tick(seconds);
            Shipping.Tick(seconds);
            Station.Tick(seconds);
            GameEvents.RaiseTicked();
        }

        /// <summary>
        /// Simulates the time since the last save in small steps, so ships, timers
        /// and smelting resolve in the right order.
        /// </summary>
        private void ApplyOfflineProgress()
        {
            if (State.lastSavedUtcTicks <= 0) return;

            double offline = (DateTime.UtcNow - new DateTime(State.lastSavedUtcTicks, DateTimeKind.Utc)).TotalSeconds;
            offline = Math.Min(offline, maxOfflineHours * 3600.0);
            if (offline < 1) return;

            const double preferredStep = 5.0;
            const int maxSteps = 20000;
            int steps = (int)Math.Min(maxSteps, Math.Ceiling(offline / preferredStep));
            double step = offline / steps;

            double stationBefore = State.station.Total;

            GameEvents.Suppressed = true;
            try
            {
                for (int i = 0; i < steps; i++) Advance(step);
            }
            finally
            {
                GameEvents.Suppressed = false;
            }

            double delivered = State.station.Total - stationBefore;
            string msg = $"Welcome back! You were away {NumberFormat.Duration(offline)}.";
            if (delivered > 0) msg += $" {NumberFormat.Amount(delivered)} units arrived at the station.";
            GameEvents.RaiseMessage(msg);
        }

        public void Save()
        {
            if (State != null) SaveSystem.Save(State);
        }

        /// <summary>Deletes the save and starts over.</summary>
        public void ResetGame()
        {
            SaveSystem.Delete();
            SetState(GameState.CreateNew(database));
            GameEvents.RaiseGameLoaded();
            GameEvents.RaiseMessage("New game started.");
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
