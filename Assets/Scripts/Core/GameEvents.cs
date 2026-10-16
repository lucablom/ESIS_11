using System;

namespace VastSpace.Core
{
    /// <summary>
    /// Global events the UI can subscribe to so it only refreshes when something changes.
    /// Remember to unsubscribe in OnDisable/OnDestroy.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Raised every simulation step (each frame). Good for timers/progress bars.</summary>
        public static event Action Ticked;
        public static event Action CurrenciesChanged;
        public static event Action StationInventoryChanged;
        /// <summary>Galaxy id.</summary>
        public static event Action<string> GalaxyChanged;
        /// <summary>Planet id. Raised on status/upgrade/ship changes, not on every extraction tick.</summary>
        public static event Action<string> PlanetChanged;
        public static event Action<string> ShipmentDelivered;
        public static event Action SmeltingChanged;
        /// <summary>Short text for toasts / the log line at the bottom of the screen.</summary>
        public static event Action<string> Message;
        public static event Action GameLoaded;

        /// <summary>
        /// While true no events are raised. Used when simulating offline time so the UI
        /// is not flooded; the GameManager raises <see cref="GameLoaded"/> afterwards.
        /// </summary>
        internal static bool Suppressed;

        internal static void RaiseTicked() { if (!Suppressed) Ticked?.Invoke(); }
        internal static void RaiseCurrenciesChanged() { if (!Suppressed) CurrenciesChanged?.Invoke(); }
        internal static void RaiseStationInventoryChanged() { if (!Suppressed) StationInventoryChanged?.Invoke(); }
        internal static void RaiseGalaxyChanged(string id) { if (!Suppressed) GalaxyChanged?.Invoke(id); }
        internal static void RaisePlanetChanged(string id) { if (!Suppressed) PlanetChanged?.Invoke(id); }
        internal static void RaiseShipmentDelivered(string planetId) { if (!Suppressed) ShipmentDelivered?.Invoke(planetId); }
        internal static void RaiseSmeltingChanged() { if (!Suppressed) SmeltingChanged?.Invoke(); }
        internal static void RaiseMessage(string text) { if (!Suppressed) Message?.Invoke(text); }
        internal static void RaiseGameLoaded() { if (!Suppressed) GameLoaded?.Invoke(); }
    }

    /// <summary>Result of a player action. UI can show <see cref="Message"/> when it fails.</summary>
    public readonly struct ActionResult
    {
        public readonly bool Success;
        public readonly string Message;

        private ActionResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static ActionResult Ok(string message = null) => new ActionResult(true, message);
        public static ActionResult Fail(string message) => new ActionResult(false, message);

        public static implicit operator bool(ActionResult r) => r.Success;
        public override string ToString() => Message ?? (Success ? "OK" : "Failed");
    }
}
