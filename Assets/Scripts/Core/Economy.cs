using System.Collections.Generic;
using VastSpace.Data;
using VastSpace.State;
using VastSpace.Util;

namespace VastSpace.Core
{
    /// <summary>Checks and pays <see cref="Cost"/>s. Materials are paid from the space station.</summary>
    public static class Economy
    {
        public static bool CanAfford(GameState state, Cost cost)
        {
            return GetMissing(state, cost).Count == 0;
        }

        /// <summary>Human readable list of what is missing, e.g. "120,000 SC", "1 Mining Equipment".</summary>
        public static List<string> GetMissing(GameState state, Cost cost)
        {
            var missing = new List<string>();
            if (cost == null) return missing;

            if (cost.currencies != null)
            {
                // Sum per currency in case the same currency is listed twice.
                var totals = new Dictionary<CurrencyType, double>();
                foreach (var c in cost.currencies)
                    totals[c.currency] = (totals.TryGetValue(c.currency, out var t) ? t : 0) + c.amount;

                foreach (var kv in totals)
                {
                    double lacking = kv.Value - state.GetCurrency(kv.Key);
                    if (lacking > 1e-6) missing.Add($"{NumberFormat.Amount(lacking)} {kv.Key}");
                }
            }

            if (cost.materials != null)
            {
                foreach (var m in cost.materials)
                {
                    if (m.material == null) continue;
                    double lacking = m.amount - state.station.Get(m.material.id);
                    if (lacking > 1e-6) missing.Add($"{NumberFormat.Amount(lacking)} {m.material.displayName}");
                }
            }

            return missing;
        }

        public static bool TryPay(GameState state, Cost cost, out string reason)
        {
            var missing = GetMissing(state, cost);
            if (missing.Count > 0)
            {
                reason = "Missing " + string.Join(", ", missing);
                return false;
            }

            reason = null;
            if (cost == null) return true;

            bool currencyChanged = false, materialsChanged = false;
            if (cost.currencies != null)
                foreach (var c in cost.currencies)
                {
                    state.AddCurrency(c.currency, -c.amount);
                    currencyChanged = true;
                }

            if (cost.materials != null)
                foreach (var m in cost.materials)
                {
                    if (m.material == null) continue;
                    state.station.TryRemove(m.material.id, m.amount);
                    materialsChanged = true;
                }

            if (currencyChanged) GameEvents.RaiseCurrenciesChanged();
            if (materialsChanged) GameEvents.RaiseStationInventoryChanged();
            return true;
        }

        public static string Describe(Cost cost)
        {
            if (cost == null || cost.IsFree) return "Free";
            var parts = new List<string>();
            if (cost.currencies != null)
                foreach (var c in cost.currencies) parts.Add($"{NumberFormat.Amount(c.amount)} {c.currency}");
            if (cost.materials != null)
                foreach (var m in cost.materials)
                    if (m.material != null) parts.Add($"{NumberFormat.Amount(m.amount)} {m.material.displayName}");
            return string.Join(", ", parts);
        }
    }
}
