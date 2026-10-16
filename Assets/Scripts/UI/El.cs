using System;
using UnityEngine;
using UnityEngine.UIElements;
using VastSpace.Data;

namespace VastSpace.UI
{
    /// <summary>Small helpers for building UI Toolkit elements in code.</summary>
    public static class El
    {
        public static VisualElement Div(params string[] classes)
        {
            var e = new VisualElement();
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        public static Label Text(string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        public static Button Btn(string text, Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("btn");
            foreach (var c in classes) b.AddToClassList(c);
            return b;
        }

        public static T AddTo<T>(this T child, VisualElement parent) where T : VisualElement
        {
            parent.Add(child);
            return child;
        }

        public static void SetVisible(this VisualElement e, bool visible) =>
            e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>Label + value on one row, returns the value label so it can be updated.</summary>
        public static Label StatRow(VisualElement parent, string name, string value = "")
        {
            var row = Div("stat-row").AddTo(parent);
            Text(name, "stat-name").AddTo(row);
            return Text(value, "stat-value").AddTo(row);
        }

        public static VisualElement MaterialIcon(MaterialDefinition material, bool small = false)
        {
            var icon = Div("mat-icon");
            if (small) icon.AddToClassList("mat-icon--small");
            if (material != null && material.icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(material.icon);
                icon.style.backgroundColor = Color.clear;
                icon.style.borderTopWidth = icon.style.borderBottomWidth =
                    icon.style.borderLeftWidth = icon.style.borderRightWidth = 0;
            }
            else
            {
                icon.style.backgroundColor = UIColors.ForMaterial(material);
            }
            return icon;
        }
    }

    /// <summary>A simple horizontal progress bar.</summary>
    public class Bar : VisualElement
    {
        private readonly VisualElement _fill;

        public Bar(params string[] classes)
        {
            AddToClassList("bar");
            foreach (var c in classes) AddToClassList(c);
            _fill = new VisualElement();
            _fill.AddToClassList("bar-fill");
            Add(_fill);
        }

        public float Value
        {
            set => _fill.style.width = Length.Percent(Mathf.Clamp01(value) * 100f);
        }
    }

    /// <summary>Placeholder colours until real sprites are assigned in the data assets.</summary>
    public static class UIColors
    {
        public static readonly Color Accent = new Color32(0x3f, 0xd8, 0xee, 0xff);
        public static readonly Color Good = new Color32(0x4b, 0xe3, 0x7f, 0xff);
        public static readonly Color Bad = new Color32(0xff, 0x6b, 0x6b, 0xff);

        private static readonly Color[] Palette =
        {
            new Color32(0x5b, 0x8d, 0xef, 0xff), new Color32(0x9b, 0x59, 0xd6, 0xff),
            new Color32(0xe8, 0x7d, 0x3e, 0xff), new Color32(0x3e, 0xc1, 0xd3, 0xff),
            new Color32(0xd6, 0x4f, 0x7a, 0xff), new Color32(0x8c, 0xc8, 0x4b, 0xff),
            new Color32(0xe8, 0xc5, 0x47, 0xff), new Color32(0x7f, 0x8c, 0x99, 0xff),
            new Color32(0xc0, 0x5a, 0x3c, 0xff), new Color32(0x4f, 0x6d, 0xd6, 0xff),
        };

        public static Color ForId(string id)
        {
            if (string.IsNullOrEmpty(id)) return Palette[0];
            int hash = 17;
            foreach (char c in id) hash = hash * 31 + c;
            return Palette[(hash & 0x7fffffff) % Palette.Length];
        }

        public static Color ForMaterial(MaterialDefinition m)
        {
            if (m == null) return Color.gray;
            switch (m.id)
            {
                case "iron_ore": return new Color32(0x8a, 0x8f, 0x96, 0xff);
                case "copper_ore": return new Color32(0x3f, 0xa0, 0x7a, 0xff);
                case "coal_ore": return new Color32(0x2a, 0x2a, 0x2e, 0xff);
                case "titanium_ore": return new Color32(0x4d, 0x63, 0x7a, 0xff);
                case "nickel_ore": return new Color32(0x6f, 0xd1, 0x3c, 0xff);
                case "zinc_ore": return new Color32(0xc9, 0xcf, 0xd6, 0xff);
                case "sulfur_ore": return new Color32(0xf0, 0xd0, 0x2a, 0xff);
                case "magnesium_ore": return new Color32(0xb8, 0xbc, 0xc4, 0xff);
                case "liquid_nitrogen": return new Color32(0xe6, 0xf2, 0xff, 0xff);
                case "sand_particles": return new Color32(0xe8, 0xa8, 0x4f, 0xff);
                case "iron_ingot": return new Color32(0xa7, 0xab, 0xb0, 0xff);
                case "copper_ingot": return new Color32(0xc4, 0x6a, 0x3a, 0xff);
                case "steel_ingot": return new Color32(0x6f, 0x7f, 0x8f, 0xff);
                case "mining_equipment": return new Color32(0xf0, 0x9a, 0x2a, 0xff);
            }
            return ForId(m.id);
        }
    }
}
