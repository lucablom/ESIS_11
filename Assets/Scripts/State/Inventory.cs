using System;
using System.Collections.Generic;
using UnityEngine;

namespace VastSpace.State
{
    /// <summary>
    /// Material id -> amount. Serializable with JsonUtility (stored as a list).
    /// Amounts are doubles so extraction can accumulate fractions per frame.
    /// </summary>
    [Serializable]
    public class Inventory : ISerializationCallbackReceiver
    {
        private const double Epsilon = 1e-6;

        [Serializable]
        private struct Entry
        {
            public string id;
            public double amount;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        private Dictionary<string, double> _map;

        private Dictionary<string, double> Map => _map ??= new Dictionary<string, double>();

        public IReadOnlyDictionary<string, double> All => Map;

        public double Get(string id) => id != null && Map.TryGetValue(id, out var v) ? v : 0;

        public bool Has(string id, double amount) => Get(id) >= amount - Epsilon;

        public double Total
        {
            get
            {
                double sum = 0;
                foreach (var v in Map.Values) sum += v;
                return sum;
            }
        }

        public bool IsEmpty => Total <= Epsilon;

        public void Add(string id, double amount)
        {
            if (string.IsNullOrEmpty(id) || amount == 0) return;
            double next = Get(id) + amount;
            if (next <= Epsilon) Map.Remove(id);
            else Map[id] = next;
        }

        public bool TryRemove(string id, double amount)
        {
            if (!Has(id, amount)) return false;
            Add(id, -amount);
            return true;
        }

        /// <summary>Removes up to <paramref name="amount"/> and returns how much was removed.</summary>
        public double RemoveUpTo(string id, double amount)
        {
            double removed = Math.Min(Get(id), amount);
            Add(id, -removed);
            return removed;
        }

        public void AddAll(Inventory other)
        {
            foreach (var kv in other.All) Add(kv.Key, kv.Value);
        }

        public void Clear() => Map.Clear();

        public void OnBeforeSerialize()
        {
            entries = new List<Entry>();
            foreach (var kv in Map) entries.Add(new Entry { id = kv.Key, amount = kv.Value });
        }

        public void OnAfterDeserialize()
        {
            _map = new Dictionary<string, double>();
            if (entries == null) return;
            foreach (var e in entries)
                if (!string.IsNullOrEmpty(e.id) && e.amount > Epsilon) _map[e.id] = e.amount;
        }
    }
}
