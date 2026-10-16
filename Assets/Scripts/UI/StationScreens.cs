using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VastSpace.Data;
using MaterialDefinition = VastSpace.Data.MaterialDefinition;
using VastSpace.Util;

namespace VastSpace.UI
{
    // ==================================================================== Inventory

    public class InventoryScreen : UIScreen
    {
        public override string Title => "Inventory";

        private MaterialCategory? _filter;
        private readonly List<Action> _rows = new List<Action>();
        private VisualElement _grid, _incoming, _tabs;
        private int _incomingCount = -1;

        public InventoryScreen(GameUI ui) : base(ui)
        {
            var panel = El.Div("panel", "screen").AddTo(Root);

            var header = El.Div("row").AddTo(panel);
            El.Text("Inventory", "h1", "grow").AddTo(header);
            _tabs = El.Div("row").AddTo(header);
            AddTab("All", null);
            AddTab("Ores", MaterialCategory.Ore);
            AddTab("Refined", MaterialCategory.Refined);
            AddTab("Components", MaterialCategory.Component);
            AddTab("Equipment", MaterialCategory.Equipment);

            El.Text("Materials stored at the space station.", "muted").AddTo(panel);
            _incoming = El.Div("mt").AddTo(panel);

            var scroll = new ScrollView(ScrollViewMode.Vertical).AddTo(panel);
            scroll.style.flexGrow = 1;
            scroll.AddToClassList("mt");
            _grid = El.Div("row", "wrap").AddTo(scroll);
            _grid.style.alignItems = Align.FlexStart;
        }

        private void AddTab(string text, MaterialCategory? category)
        {
            var b = El.Btn(text, () => { _filter = category; Show(); }, "tab-btn").AddTo(_tabs);
            b.userData = category;
        }

        public void Show()
        {
            foreach (var child in _tabs.Children())
                child.EnableInClassList("tab-btn--active", Equals(child.userData, _filter));

            _grid.Clear();
            _rows.Clear();
            foreach (var material in Game.Database.materials)
            {
                if (material == null || (_filter.HasValue && material.category != _filter.Value)) continue;
                var m = material;
                var cell = El.Div("inv-cell").AddTo(_grid);
                El.MaterialIcon(m).AddTo(cell);
                El.Text(m.displayName, "inv-name").AddTo(cell);
                var amount = El.Text("", "inv-amount").AddTo(cell);
                _rows.Add(() =>
                {
                    double have = Game.State.station.Get(m.id);
                    amount.text = NumberFormat.Amount(Math.Floor(have));
                    cell.EnableInClassList("zero", have < 1);
                });
            }
            _incomingCount = -1;
            Refresh();
        }

        public override void Refresh()
        {
            foreach (var r in _rows) r();
            ShipmentList.Refresh(Game, _incoming, ref _incomingCount);
        }
    }

    /// <summary>"Incoming shipments" lines shared by the inventory and market screens.</summary>
    internal static class ShipmentList
    {
        public static void Refresh(Core.GameManager game, VisualElement container, ref int builtCount)
        {
            var shipments = game.State.shipments;
            if (shipments.Count != builtCount)
            {
                builtCount = shipments.Count;
                container.Clear();
                if (shipments.Count > 0) El.Text("INCOMING SHIPMENTS", "h3").AddTo(container);
                foreach (var _ in shipments) El.Text("", "small").AddTo(container);
            }

            for (int i = 0; i < shipments.Count; i++)
            {
                var s = shipments[i];
                var planet = game.Database.GetPlanet(s.planetId);
                var label = (Label)container[i + 1];
                label.text = $"From {(planet != null ? planet.displayName : s.planetId)}:  " +
                             $"{NumberFormat.Amount(s.cargo.Total)} units  ·  arrives in " +
                             NumberFormat.Duration(game.Shipping.GetArrivalTimeRemaining(s));
            }
        }
    }

    // ==================================================================== Market

    public class MarketScreen : UIScreen
    {
        public override string Title => "Market";

        private readonly List<Action> _rows = new List<Action>();
        private readonly VisualElement _list, _incoming;
        private readonly Label _totalValue;
        private int _incomingCount = -1;

        public MarketScreen(GameUI ui) : base(ui)
        {
            var panel = El.Div("panel", "screen").AddTo(Root);

            var header = El.Div("row").AddTo(panel);
            var titles = El.Div("col", "grow").AddTo(header);
            El.Text("Market", "h1").AddTo(titles);
            El.Text("Sell materials from the space station for SC.", "muted").AddTo(titles);
            _totalValue = El.Text("", "h3", "mr").AddTo(header);
            El.Btn("Sell all ores", SellAllOres).AddTo(header);

            _incoming = El.Div("mt").AddTo(panel);

            var head = El.Div("list-row", "mt").AddTo(panel);
            Column(head, "Material", 380, "list-header");
            Column(head, "In stock", 200, "list-header", "right");
            Column(head, "Price", 180, "list-header", "right");
            Column(head, "Value", 220, "list-header", "right");

            var scroll = new ScrollView(ScrollViewMode.Vertical).AddTo(panel);
            scroll.style.flexGrow = 1;
            _list = scroll.contentContainer;
        }

        private static Label Column(VisualElement row, string text, float width, params string[] classes)
        {
            var l = El.Text(text, classes).AddTo(row);
            l.style.width = width;
            return l;
        }

        public void Show()
        {
            _list.Clear();
            _rows.Clear();
            foreach (var material in Game.Database.materials)
            {
                if (material == null) continue;
                var m = material;
                var row = El.Div("list-row").AddTo(_list);
                var nameCell = El.Div("row").AddTo(row);
                nameCell.style.width = 380;
                El.MaterialIcon(m, small: true).AddTo(nameCell);
                El.Text(m.displayName).AddTo(nameCell);

                var stock = Column(row, "", 200, "right");
                Column(row, NumberFormat.Money(m.sellPrice) + " SC", 180, "right", "muted");
                var value = Column(row, "", 220, "right", "bold");
                El.Div("grow").AddTo(row);
                var sell100 = El.Btn("Sell 100", () => UI.Report(Game.Station.Sell(m, 100)), "btn--small").AddTo(row);
                var sellAll = El.Btn("Sell all", () => UI.Report(Game.Station.SellAll(m)), "btn--small").AddTo(row);

                _rows.Add(() =>
                {
                    double have = Math.Floor(Game.State.station.Get(m.id));
                    stock.text = NumberFormat.Amount(have);
                    value.text = NumberFormat.Money(have * m.sellPrice) + " SC";
                    row.EnableInClassList("zero", have < 1);
                    sell100.SetEnabled(have >= 1);
                    sellAll.SetEnabled(have >= 1);
                });
            }
            _incomingCount = -1;
            Refresh();
        }

        private void SellAllOres()
        {
            bool sold = false;
            foreach (var m in Game.Database.materials)
            {
                if (m == null || m.category != MaterialCategory.Ore || Game.State.station.Get(m.id) < 1) continue;
                Game.Station.SellAll(m);
                sold = true;
            }
            if (!sold) UI.ShowToast("No ores to sell.", true);
        }

        public override void Refresh()
        {
            double total = 0;
            foreach (var m in Game.Database.materials)
                if (m != null) total += Math.Floor(Game.State.station.Get(m.id)) * m.sellPrice;
            _totalValue.text = "Stock value: " + NumberFormat.Money(total) + " SC";

            foreach (var r in _rows) r();
            ShipmentList.Refresh(Game, _incoming, ref _incomingCount);
        }
    }

    // ==================================================================== Crafting

    public class CraftingScreen : UIScreen
    {
        public override string Title => "Crafting";

        private readonly List<Action> _rows = new List<Action>();
        private readonly VisualElement _recipes, _queue;
        private readonly Bar _queueBar;
        private readonly Label _queueStatus;
        private string _queueSignature;

        public CraftingScreen(GameUI ui) : base(ui)
        {
            var panel = El.Div("panel", "screen").AddTo(Root);
            El.Text("Crafting", "h1").AddTo(panel);
            El.Text("Smelt ores into ingots and craft components at the space station.", "muted").AddTo(panel);

            var body = El.Div("row", "grow", "mt").AddTo(panel);
            body.style.alignItems = Align.Stretch;

            var left = new ScrollView(ScrollViewMode.Vertical).AddTo(body);
            left.style.flexGrow = 2;
            left.style.flexBasis = 0;
            left.style.marginRight = 20;
            _recipes = left.contentContainer;

            var right = El.Div("panel-inner", "grow").AddTo(body);
            right.style.flexBasis = 0;
            El.Text("SMELTER QUEUE", "h3").AddTo(right);
            _queueStatus = El.Text("", "muted").AddTo(right);
            _queueBar = new Bar().AddTo(right);
            _queueBar.AddToClassList("mt");
            _queue = El.Div("mt").AddTo(right);
        }

        public void Show()
        {
            _recipes.Clear();
            _rows.Clear();
            foreach (var recipe in Game.Database.recipes)
            {
                if (recipe == null) continue;
                var r = recipe;
                var card = El.Div("card").AddTo(_recipes);

                var top = El.Div("row").AddTo(card);
                El.Text(r.displayName, "bold", "grow").AddTo(top);
                El.Text($"{r.durationSeconds:0.#} s / batch", "muted", "small").AddTo(top);

                var io = El.Div("row", "mt", "wrap").AddTo(card);
                var inputLabels = AddAmounts(io, r.inputs, true);
                El.Text("  »  ", "accent", "bold").AddTo(io);
                AddAmounts(io, r.outputs, false);

                var actions = El.Div("row", "mt").AddTo(card);
                var canMake = El.Text("", "small", "grow").AddTo(actions);
                var x1 = El.Btn("x1", () => UI.Report(Game.Station.QueueSmelting(r, 1)), "btn--small").AddTo(actions);
                var x10 = El.Btn("x10", () => UI.Report(Game.Station.QueueSmelting(r, 10)), "btn--small").AddTo(actions);
                var max = El.Btn("Max", () =>
                {
                    int n = Game.Station.GetMaxBatches(r);
                    if (n > 0) UI.Report(Game.Station.QueueSmelting(r, n));
                }, "btn--small").AddTo(actions);

                _rows.Add(() =>
                {
                    int n = Game.Station.GetMaxBatches(r);
                    canMake.text = $"Can make {n}";
                    x1.SetEnabled(n >= 1);
                    x10.SetEnabled(n >= 10);
                    max.SetEnabled(n >= 1);
                    for (int i = 0; i < r.inputs.Count && i < inputLabels.Count; i++)
                    {
                        var input = r.inputs[i];
                        if (input.material == null) continue;
                        inputLabels[i].EnableInClassList("bad", !Game.State.station.Has(input.material.id, input.amount));
                    }
                });
            }
            _queueSignature = null;
            Refresh();
        }

        private static List<Label> AddAmounts(VisualElement parent, List<MaterialAmount> list, bool isInput)
        {
            var labels = new List<Label>();
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m.material == null) continue;
                if (i > 0) El.Text(" + ", "muted").AddTo(parent);
                El.MaterialIcon(m.material, small: true).AddTo(parent);
                labels.Add(El.Text($"{NumberFormat.Amount(m.amount)} {m.material.displayName}").AddTo(parent));
            }
            return labels;
        }

        public override void Refresh()
        {
            foreach (var r in _rows) r();

            var queue = Game.State.smeltingQueue;
            string signature = "";
            foreach (var job in queue) signature += job.recipeId + ":" + job.batchesRemaining + ";";

            if (signature != _queueSignature)
            {
                _queueSignature = signature;
                _queue.Clear();
                for (int i = 0; i < queue.Count; i++)
                {
                    int index = i;
                    var recipe = Game.Database.GetRecipe(queue[i].recipeId);
                    var row = El.Div("list-row").AddTo(_queue);
                    El.Text($"{(recipe != null ? recipe.displayName : queue[i].recipeId)}  x{queue[i].batchesRemaining}",
                        i == 0 ? "bold" : "muted", "grow").AddTo(row);
                    El.Btn("Cancel", () => UI.Report(Game.Station.CancelSmelting(index)), "btn--small").AddTo(row);
                }
            }

            _queueBar.SetVisible(queue.Count > 0);
            if (queue.Count == 0)
            {
                _queueStatus.text = "Idle. Queue a recipe to start smelting.";
            }
            else
            {
                _queueBar.Value = Game.Station.GetCurrentBatchProgress();
                double left = Math.Max(0, queue[0].batchCompletesAt - Game.State.gameTime);
                _queueStatus.text = $"Next batch in {NumberFormat.Duration(left)}";
            }
        }
    }
}
