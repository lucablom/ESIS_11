using System;
using System.Collections.Generic;

namespace VastSpace.Data
{
    /// <summary>The three in-game currencies shown in the top bar.</summary>
    public enum CurrencyType
    {
        SC = 0,   // Space Credits - the main currency
        SSC = 1,  // Super Space Credits - premium/rare currency
        ST = 2    // Space Tokens
    }

    [Serializable]
    public struct CurrencyAmount
    {
        public CurrencyType currency;
        public double amount;

        public CurrencyAmount(CurrencyType currency, double amount)
        {
            this.currency = currency;
            this.amount = amount;
        }
    }

    [Serializable]
    public struct MaterialAmount
    {
        public MaterialDefinition material;
        public double amount;

        public MaterialAmount(MaterialDefinition material, double amount)
        {
            this.material = material;
            this.amount = amount;
        }
    }

    /// <summary>
    /// A price that can contain both currencies and materials.
    /// Materials are always paid from the space station inventory.
    /// </summary>
    [Serializable]
    public class Cost
    {
        public List<CurrencyAmount> currencies = new List<CurrencyAmount>();
        public List<MaterialAmount> materials = new List<MaterialAmount>();

        public bool IsFree =>
            (currencies == null || currencies.Count == 0) &&
            (materials == null || materials.Count == 0);

        public static Cost Credits(double sc)
        {
            var cost = new Cost();
            cost.currencies.Add(new CurrencyAmount(CurrencyType.SC, sc));
            return cost;
        }
    }
}
