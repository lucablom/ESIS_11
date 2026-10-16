using System;
using System.Globalization;

namespace VastSpace.Util
{
    /// <summary>Formatting helpers for the HUD. Always uses invariant culture (English).</summary>
    public static class NumberFormat
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] Suffixes = { "", "K", "M", "B", "T", "Qa", "Qi" };

        /// <summary>1234 -> "1,234", 0.5 -> "0.5", 12.34 -> "12.3".</summary>
        public static string Amount(double value)
        {
            double abs = Math.Abs(value);
            if (abs >= 1000 || abs == Math.Floor(abs)) return Math.Round(value).ToString("#,0", Inv);
            if (abs >= 10) return value.ToString("0.#", Inv);
            return value.ToString("0.##", Inv);
        }

        /// <summary>1_250_000 -> "1.25M". Below 10,000 the full number is shown.</summary>
        public static string Short(double value)
        {
            double abs = Math.Abs(value);
            if (abs < 10000) return Amount(value);
            int tier = Math.Min((int)(Math.Log10(abs) / 3), Suffixes.Length - 1);
            double scaled = value / Math.Pow(1000, tier);
            return scaled.ToString(scaled >= 100 ? "0" : "0.##", Inv) + Suffixes[tier];
        }

        /// <summary>Two decimals, like the currency display "0.00 SC".</summary>
        public static string Money(double value) => value.ToString("#,0.00", Inv);

        /// <summary>65 -> "1:05", 3725 -> "1:02:05".</summary>
        public static string Duration(double seconds)
        {
            if (seconds < 0) seconds = 0;
            var t = TimeSpan.FromSeconds(Math.Ceiling(seconds));
            if (t.TotalHours >= 1)
                return $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}";
            return $"{t.Minutes}:{t.Seconds:00}";
        }

        /// <summary>Clock style "00:00:00" for the top-right play timer.</summary>
        public static string Clock(double seconds)
        {
            if (seconds < 0) seconds = 0;
            var t = TimeSpan.FromSeconds(Math.Floor(seconds));
            return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }
    }
}
