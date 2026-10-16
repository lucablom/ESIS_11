using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VastSpace.Core;
using VastSpace.Data;
using MaterialDefinition = VastSpace.Data.MaterialDefinition;
using VastSpace.State;
using VastSpace.Systems;
using VastSpace.Util;

namespace VastSpace.UI
{
    /// <summary>
    /// One planet: the explore panel while locked, a progress view while exploring,
    /// and production / storage / hangar / upgrades once operating.
    /// </summary>
    public class PlanetScreen : UIScreen
    {
        private PlanetDefinition _planet;
        private UnlockStatus _builtFor;
        private Action _refresh;

        public override string Title => _planet != null ? _planet.displayName : "";

        public PlanetScreen(GameUI ui) : base(ui) { }

        public void Show(PlanetDefinition planet)
        {
            _planet = planet;
            Build();
        }

        public override void Refresh()
        {
            if (_planet == null) return;
            if (Game.Planets.GetState(_planet).status != _builtFor) Build();
            _refresh?.Invoke();
        }

        private void Build()
        {
            Root.Clear();
            _refresh = null;
            _builtFor = Game.Planets.GetState(_planet).status;

            var panel = El.Div("panel", "screen").AddTo(Root);
            BuildHeader(panel);

            switch (_builtFor)
            {
                case UnlockStatus.Locked: BuildLocked(panel); break;
                case UnlockStatus.Unlocking: BuildExploring(panel); break;
                default: BuildOperating(panel); break;
            }
        }

        private void BuildHeader(VisualElement parent)
        {
            var header = El.Div("row").AddTo(parent);

            var icon = El.Div("node-body").AddTo(header);
            icon.style.width = icon.style.height = 84;
            icon.style.marginRight = 20;
            if (_planet.icon != null) icon.style.backgroundImage = new StyleBackground(_planet.icon);
            else
            {
                var color = UIColors.ForId(_planet.id);
                icon.style.backgroundColor = color;
                El.Div("node-core").AddTo(icon).style.backgroundColor = Color.Lerp(color, Color.white, 0.45f);
            }

            var titles = El.Div("col", "grow").AddTo(header);
            El.Text(_planet.displayName, "h1").AddTo(titles);
            var galaxy = Game.Database.GetGalaxyOfPlanet(_planet.id);
            if (galaxy != null) El.Text(galaxy.displayName, "muted").AddTo(titles);

            if (galaxy != null)
                El.Btn("«  " + galaxy.displayName, () => UI.ShowGalaxy(galaxy)).AddTo(header);
            El.Btn("X", () =>
            {
                if (galaxy != null) UI.ShowGalaxy(galaxy);
                else UI.ShowStationMap();
            }, "close-btn").AddTo(header);
        }

        // ------------------------------------------------------------ Locked

        private void BuildLocked(VisualElement parent)
        {
            var wrapper = El.Div("grow", "center").AddTo(parent);
            var box = El.Div("panel-inner").AddTo(wrapper);
            box.style.width = 760;

            var title = El.Text("Unlock / Explore", "h2").AddTo(box);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;

            var text = El.Text("This planet has not yet been explored. Allocate an exploring team and equipment " +
                               "to start the extraction of materials.", "paragraph", "mt").AddTo(box);
            text.style.unityTextAlign = TextAnchor.MiddleCenter;

            if (!string.IsNullOrEmpty(_planet.description))
                El.Text(_planet.description, "paragraph", "muted", "mt").AddTo(box);

            var stats = El.Div("mt-lg").AddTo(box);
            El.StatRow(stats, "Known deposits", DescribeResources());
            El.StatRow(stats, "Time", NumberFormat.Duration(_planet.exploreDurationSeconds) + " min");
            var cost = El.StatRow(stats, "Cost", Economy.Describe(_planet.exploreCost));

            var footer = El.Div("row", "mt").AddTo(box);
            var missingLabel = El.Text("", "bad", "grow", "small").AddTo(footer);
            var galaxy = Game.Database.GetGalaxyOfPlanet(_planet.id);
            var allocate = El.Btn("Allocate", () => UI.Report(Game.Planets.StartExploration(_planet)), "btn--big")
                .AddTo(footer);

            _refresh = () =>
            {
                var missing = Economy.GetMissing(Game.State, _planet.exploreCost);
                bool galaxyLocked = galaxy != null && !Game.Planets.IsUnlocked(galaxy);
                cost.EnableInClassList("bad", missing.Count > 0);
                missingLabel.text = galaxyLocked ? $"Unlock {galaxy.displayName} first."
                    : missing.Count > 0 ? "Missing " + string.Join(", ", missing) : "";
                allocate.SetEnabled(!galaxyLocked && missing.Count == 0);
            };
        }

        private string DescribeResources()
        {
            var names = new List<string>();
            foreach (var r in _planet.resources)
                if (r.material != null) names.Add(r.material.displayName);
            return names.Count > 0 ? string.Join(", ", names) : "Unknown";
        }

        // ------------------------------------------------------------ Exploring

        private void BuildExploring(VisualElement parent)
        {
            var wrapper = El.Div("grow", "center").AddTo(parent);
            var box = El.Div("panel-inner").AddTo(wrapper);
            box.style.width = 760;

            El.Text("Exploration in progress", "h2").AddTo(box);
            El.Text("The exploring team is surveying the surface and setting up the extraction rig.",
                "paragraph", "muted", "mt").AddTo(box);
            var bar = new Bar().AddTo(box);
            bar.AddToClassList("mt-lg");
            var time = El.Text("", "h3", "mt").AddTo(box);

            _refresh = () =>
            {
                bar.Value = Game.Planets.GetExploreProgress(_planet);
                time.text = NumberFormat.Duration(Game.Planets.GetExploreTimeRemaining(_planet)) + " remaining";
            };
        }

        // ------------------------------------------------------------ Operating

        private void BuildOperating(VisualElement parent)
        {
            var planets = Game.Planets;
            var shipping = Game.Shipping;
            var ps = planets.GetState(_planet);

            var body = El.Div("row", "grow", "mt").AddTo(parent);
            body.style.alignItems = Align.Stretch;

            // ---------------- Left: production + storage
            var left = El.Div("panel-inner", "grow").AddTo(body);
            left.style.flexBasis = 0;
            left.style.marginRight = 20;

            var statusRow = El.Div("row").AddTo(left);
            El.Text("PRODUCTION", "h3", "grow").AddTo(statusRow);
            var statusPill = El.Text("", "pill").AddTo(statusRow);

            var rate = El.StatRow(left, "Extraction rate");
            var total = El.StatRow(left, "Total materials extracted");

            El.Div("divider").AddTo(left);
            El.Text("STORAGE", "h3").AddTo(left);
            var capacityLabel = El.StatRow(left, "Capacity per material");

            var storageRows = new List<Action>();
            foreach (var r in _planet.resources)
            {
                if (r.material == null) continue;
                var material = r.material;
                var row = El.Div("row", "mt").AddTo(left);
                El.MaterialIcon(material, small: true).AddTo(row);
                var name = El.Text(material.displayName).AddTo(row);
                name.style.width = 190;
                var bar = new Bar().AddTo(row);
                var amount = El.Text("", "right").AddTo(row);
                amount.style.width = 170;
                var perMin = El.Text("", "muted", "small", "right").AddTo(row);
                perMin.style.width = 110;

                storageRows.Add(() =>
                {
                    double cap = planets.GetStorageCapacity(_planet);
                    double have = ps.storage.Get(material.id);
                    bar.Value = cap > 0 ? (float)(have / cap) : 0f;
                    bar.EnableInClassList("bar--full", have >= cap - 0.5);
                    amount.text = $"{NumberFormat.Amount(Math.Floor(have))} / {NumberFormat.Amount(cap)}";
                    perMin.text = $"+{NumberFormat.Amount(planets.GetExtractionRatePerMinute(_planet, material))}/min";
                });
            }

            // ---------------- Right: hangar + upgrades
            var right = El.Div("col", "grow").AddTo(body);
            right.style.flexBasis = 0;

            var hangar = El.Div("panel-inner").AddTo(right);
            El.Text("HANGAR", "h3").AddTo(hangar);
            var shipStatus = El.StatRow(hangar, "Ship");
            var shipBar = new Bar("bar--good").AddTo(hangar);
            shipBar.style.marginBottom = 10;
            var capacity = El.StatRow(hangar, "Cargo capacity");
            var travel = El.StatRow(hangar, "Travel time (one way)");
            var ready = El.StatRow(hangar, "Ready to ship");

            var shipButtons = El.Div("row", "mt").AddTo(hangar);
            var send = El.Btn("Send ship to station", () => UI.Report(shipping.SendShip(_planet))).AddTo(shipButtons);
            El.Div("grow").AddTo(shipButtons);
            var auto = new Toggle("Auto ship").AddTo(shipButtons);
            auto.SetValueWithoutNotify(ps.autoShip);
            auto.RegisterValueChangedCallback(e => shipping.SetAutoShip(_planet, e.newValue));

            var upgrades = El.Div("panel-inner", "mt", "grow").AddTo(right);
            El.Text("UPGRADES", "h3").AddTo(upgrades);
            var upgradeRows = new List<Action>();
            foreach (PlanetUpgrade type in Enum.GetValues(typeof(PlanetUpgrade)))
                upgradeRows.Add(BuildUpgradeRow(upgrades, type));

            _refresh = () =>
            {
                bool full = planets.IsStorageFull(_planet);
                statusPill.text = full ? "STORAGE FULL" : "OPERATING";
                statusPill.EnableInClassList("pill--warn", full);
                statusPill.EnableInClassList("pill--good", !full);

                rate.text = $"{NumberFormat.Amount(planets.GetValue(_planet, PlanetUpgrade.ExtractionRig))} / min";
                total.text = NumberFormat.Amount(Math.Floor(ps.totalExtracted));
                capacityLabel.text = NumberFormat.Amount(planets.GetStorageCapacity(_planet));
                foreach (var r in storageRows) r();

                // Ship
                double oneWay = shipping.GetTravelTime(_planet);
                capacity.text = NumberFormat.Amount(shipping.GetShipCapacity(_planet)) + " units";
                travel.text = NumberFormat.Duration(oneWay);
                ready.text = NumberFormat.Amount(Math.Min(Math.Floor(ps.storage.Total), shipping.GetShipCapacity(_planet))) + " units";

                bool home = shipping.IsShipHome(_planet);
                send.SetEnabled(home && ps.storage.Total >= 1);
                shipBar.SetVisible(!home);
                if (home)
                {
                    shipStatus.text = "In hangar";
                }
                else
                {
                    double back = shipping.GetShipReturnTimeRemaining(_planet);
                    bool outbound = back > oneWay;
                    shipStatus.text = outbound
                        ? $"Flying to station  ·  arrives in {NumberFormat.Duration(back - oneWay)}"
                        : $"Returning  ·  back in {NumberFormat.Duration(back)}";
                    shipBar.Value = oneWay > 0 ? 1f - (float)(back / (oneWay * 2)) : 1f;
                }
                if (auto.value != ps.autoShip) auto.SetValueWithoutNotify(ps.autoShip);

                foreach (var r in upgradeRows) r();
            };
        }

        private Action BuildUpgradeRow(VisualElement parent, PlanetUpgrade type)
        {
            var planets = Game.Planets;
            var row = El.Div("upgrade-row").AddTo(parent);

            var names = El.Div("col").AddTo(row);
            names.style.width = 290;
            El.Text(PlanetSystem.UpgradeName(type), "bold").AddTo(names);
            var level = El.Text("", "small", "muted").AddTo(names);

            var values = El.Text("", "grow").AddTo(row);
            var cost = El.Text("", "small", "right").AddTo(row);
            cost.style.width = 170;
            var button = El.Btn("Upgrade", () => UI.Report(planets.Upgrade(_planet, type)), "btn--small").AddTo(row);
            button.style.width = 110;

            return () =>
            {
                int lv = planets.GetLevel(_planet, type);
                int max = planets.GetMaxLevel(_planet, type);
                level.text = $"Level {lv + 1} / {max + 1}";

                string current = FormatUpgradeValue(type, lv);
                if (planets.IsMaxLevel(_planet, type))
                {
                    values.text = current + "   (max)";
                    cost.text = "";
                    button.SetEnabled(false);
                    button.text = "Max";
                    return;
                }

                values.text = $"{current}  »  {FormatUpgradeValue(type, lv + 1)}";
                var next = planets.GetNextUpgradeCost(_planet, type);
                bool affordable = Economy.CanAfford(Game.State, next);
                cost.text = Economy.Describe(next);
                cost.EnableInClassList("bad", !affordable);
                button.SetEnabled(affordable);
                button.text = "Upgrade";
            };
        }

        private string FormatUpgradeValue(PlanetUpgrade type, int level)
        {
            double v = Game.Planets.GetValueAtLevel(_planet, type, level);
            switch (type)
            {
                case PlanetUpgrade.ExtractionRig: return NumberFormat.Amount(v) + "/min";
                case PlanetUpgrade.Storage: return NumberFormat.Amount(v);
                case PlanetUpgrade.ShipCapacity: return NumberFormat.Amount(v) + " units";
                case PlanetUpgrade.ShipSpeed:
                    // Speed is easier to understand as travel time.
                    return v > 0 ? NumberFormat.Duration(_planet.travelDistance / v) + " trip" : "-";
                default: return NumberFormat.Amount(v);
            }
        }
    }
}
