using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VastSpace.Core;
using VastSpace.Data;
using VastSpace.State;
using VastSpace.Util;

namespace VastSpace.UI
{
    /// <summary>
    /// Shared map behaviour: clickable nodes placed by mapPosition and lines drawn between them.
    /// mapPosition is in a 1000 x 700 space with (0,0) in the middle and +y up.
    /// </summary>
    public abstract class MapScreenBase : UIScreen
    {
        private const float MapWidth = 1000f, MapHeight = 700f;

        protected readonly VisualElement Lines;
        private readonly Dictionary<VisualElement, Vector2> _nodePercent = new Dictionary<VisualElement, Vector2>();
        private readonly List<(VisualElement from, VisualElement to)> _links = new List<(VisualElement, VisualElement)>();

        protected MapScreenBase(GameUI ui) : base(ui)
        {
            Root.AddToClassList("map");
            Lines = El.Div("map-lines").AddTo(Root);
            Lines.pickingMode = PickingMode.Ignore;
            Lines.generateVisualContent += DrawLines;
            Root.RegisterCallback<GeometryChangedEvent>(_ => Lines.MarkDirtyRepaint());
        }

        protected void ClearMap()
        {
            Root.Clear();
            Root.Add(Lines);
            _nodePercent.Clear();
            _links.Clear();
        }

        protected static Vector2 ToPercent(Vector2 mapPos) => new Vector2(
            Mathf.Clamp(50f + mapPos.x / MapWidth * 100f, 6f, 94f),
            Mathf.Clamp(50f - mapPos.y / MapHeight * 100f, 9f, 88f));

        /// <summary>Creates a node. Returns the node; its status label is the last child.</summary>
        protected VisualElement Node(Vector2 mapPos, string label, Color color, Sprite icon,
            Action onClick, params string[] classes)
        {
            var node = El.Div("node").AddTo(Root);
            foreach (var c in classes) node.AddToClassList(c);

            var pct = ToPercent(mapPos);
            node.style.left = Length.Percent(pct.x);
            node.style.top = Length.Percent(pct.y);
            _nodePercent[node] = pct;

            var body = El.Div("node-body").AddTo(node);
            if (icon != null)
            {
                body.style.backgroundImage = new StyleBackground(icon);
                body.style.borderTopWidth = body.style.borderBottomWidth =
                    body.style.borderLeftWidth = body.style.borderRightWidth = 0;
            }
            else
            {
                var core = El.Div("node-core").AddTo(body);
                // A transparent colour means "use the stylesheet colours" (station / hub).
                if (color.a > 0f)
                {
                    body.style.backgroundColor = color;
                    core.style.backgroundColor = Color.Lerp(color, Color.white, 0.45f);
                }
            }

            El.Text(label, "node-label").AddTo(node);
            El.Text("", "node-status").AddTo(node);

            if (onClick != null) node.RegisterCallback<ClickEvent>(_ => onClick());
            else node.pickingMode = PickingMode.Ignore;
            return node;
        }

        protected static void SetStatus(VisualElement node, string text) =>
            ((Label)node[node.childCount - 1]).text = text;

        protected void SetLinks(IEnumerable<(VisualElement, VisualElement)> links)
        {
            _links.Clear();
            _links.AddRange(links);
            Lines.MarkDirtyRepaint();
        }

        private void DrawLines(MeshGenerationContext ctx)
        {
            var rect = Lines.contentRect;
            if (rect.width <= 0 || _links.Count == 0) return;

            var p = ctx.painter2D;
            p.strokeColor = new Color(0.25f, 0.85f, 0.93f, 0.55f);
            p.lineWidth = 2f;

            foreach (var (from, to) in _links)
            {
                if (!_nodePercent.TryGetValue(from, out var a) || !_nodePercent.TryGetValue(to, out var b)) continue;
                var start = new Vector2(a.x / 100f * rect.width, a.y / 100f * rect.height);
                var end = new Vector2(b.x / 100f * rect.width, b.y / 100f * rect.height);
                DrawDashed(p, start, end, 10f, 7f, 62f);
            }
        }

        /// <summary>Dashed line that leaves a gap of <paramref name="inset"/> around both nodes.</summary>
        private static void DrawDashed(Painter2D p, Vector2 a, Vector2 b, float dash, float gap, float inset)
        {
            float length = Vector2.Distance(a, b);
            if (length <= inset * 2f) return;
            var dir = (b - a) / length;

            p.BeginPath();
            for (float d = inset; d < length - inset; d += dash + gap)
            {
                p.MoveTo(a + dir * d);
                p.LineTo(a + dir * Mathf.Min(d + dash, length - inset));
            }
            p.Stroke();
        }
    }

    // ==================================================================== Station map

    public class StationMapScreen : MapScreenBase
    {
        public override string Title => "Space Station";

        private VisualElement _station;
        private readonly List<(GalaxyDefinition galaxy, VisualElement node)> _galaxies =
            new List<(GalaxyDefinition, VisualElement)>();
        private string _linkSignature;

        public StationMapScreen(GameUI ui) : base(ui) { }

        public void Show()
        {
            ClearMap();
            _galaxies.Clear();
            _linkSignature = null;

            // Decorations from the mockup.
            var earthColor = new Color(0.2f, 0.55f, 0.9f);
            Node(new Vector2(-250, -170), "Earth", earthColor, null, null, "node--deco");
            Node(new Vector2(-300, -95), "Moon", new Color(0.8f, 0.8f, 0.82f), null, null, "node--deco", "node--small");

            _station = Node(Vector2.zero, "Space Station", Color.clear, null,
                () => UI.ShowTab(NavTab.Market), "node--station");

            var galaxies = Game.Database.galaxies;
            for (int i = 0; i < galaxies.Count; i++)
            {
                var galaxy = galaxies[i];
                if (galaxy == null) continue;
                var node = Node(galaxy.mapPosition, $"{i + 1}. {galaxy.displayName}",
                    UIColors.ForId(galaxy.id), galaxy.icon, () => OnGalaxyClicked(galaxy));
                _galaxies.Add((galaxy, node));
            }

            Refresh();
        }

        private void OnGalaxyClicked(GalaxyDefinition galaxy)
        {
            if (Game.Planets.IsUnlocked(galaxy)) UI.ShowGalaxy(galaxy);
            else GalaxyModal.Open(UI, galaxy);
        }

        public override void Refresh()
        {
            if (_station == null) return;
            SetStatus(_station, $"{Game.State.shipments.Count} ships incoming");

            var signature = "";
            var links = new List<(VisualElement, VisualElement)>();
            foreach (var (galaxy, node) in _galaxies)
            {
                var gs = Game.Planets.GetState(galaxy);
                node.EnableInClassList("node--locked", gs.status == UnlockStatus.Locked);

                switch (gs.status)
                {
                    case UnlockStatus.Locked:
                        SetStatus(node, Economy.Describe(galaxy.unlockCost));
                        break;
                    case UnlockStatus.Unlocking:
                        SetStatus(node, "Unlocking " + NumberFormat.Duration(Game.Planets.GetUnlockTimeRemaining(galaxy)));
                        break;
                    default:
                        int operating = 0;
                        foreach (var p in galaxy.planets)
                            if (p != null && Game.Planets.IsOperating(p)) operating++;
                        SetStatus(node, $"{operating}/{galaxy.planets.Count} planets operating");
                        links.Add((_station, node));
                        signature += galaxy.id + ";";
                        break;
                }
            }

            if (signature != _linkSignature)
            {
                _linkSignature = signature;
                SetLinks(links);
            }
        }
    }

    /// <summary>Popup with the galaxy description and the unlock button.</summary>
    public static class GalaxyModal
    {
        public static void Open(GameUI ui, GalaxyDefinition galaxy)
        {
            var game = ui.Game;
            int index = game.Database.galaxies.IndexOf(galaxy) + 1;

            var modal = El.Div("panel", "modal");
            var header = El.Div("row").AddTo(modal);
            El.Text($"{index}. {galaxy.displayName}", "h1", "grow").AddTo(header);
            El.Btn("X", ui.CloseModal, "close-btn").AddTo(header);

            El.Text(galaxy.description, "paragraph", "mt").AddTo(modal);

            var unlockBox = El.Div("mt-lg").AddTo(modal);
            El.Text("Unlock this galaxy", "h3").AddTo(unlockBox);
            var cost = El.StatRow(unlockBox, "Cost");
            El.StatRow(unlockBox, "Time",
                galaxy.unlockDurationSeconds <= 0 ? "Instant" : NumberFormat.Duration(galaxy.unlockDurationSeconds));
            El.StatRow(unlockBox, "Planets", galaxy.planets.Count.ToString());

            var bar = new Bar().AddTo(unlockBox);
            bar.AddToClassList("mt");
            var footer = El.Div("row", "mt").AddTo(modal);
            var status = El.Text("", "grow").AddTo(footer);
            var unlock = El.Btn("Unlock", () => ui.Report(game.Planets.UnlockGalaxy(galaxy)), "btn--big").AddTo(footer);
            var enter = El.Btn("Enter", () => ui.ShowGalaxy(galaxy), "btn--big", "btn--good").AddTo(footer);

            void Refresh()
            {
                var gs = game.Planets.GetState(galaxy);
                var missing = Economy.GetMissing(game.State, galaxy.unlockCost);
                cost.text = Economy.Describe(galaxy.unlockCost);
                cost.EnableInClassList("bad", gs.status == UnlockStatus.Locked && missing.Count > 0);

                unlock.SetVisible(gs.status == UnlockStatus.Locked);
                unlock.SetEnabled(missing.Count == 0);
                enter.SetVisible(gs.status == UnlockStatus.Unlocked);
                bar.SetVisible(gs.status == UnlockStatus.Unlocking);

                switch (gs.status)
                {
                    case UnlockStatus.Locked:
                        status.text = missing.Count > 0 ? "Missing " + string.Join(", ", missing) : "";
                        status.EnableInClassList("bad", missing.Count > 0);
                        break;
                    case UnlockStatus.Unlocking:
                        double left = game.Planets.GetUnlockTimeRemaining(galaxy);
                        bar.Value = galaxy.unlockDurationSeconds > 0
                            ? 1f - (float)(left / galaxy.unlockDurationSeconds) : 1f;
                        status.text = "Unlocking... " + NumberFormat.Duration(left);
                        status.EnableInClassList("bad", false);
                        break;
                    default:
                        status.text = "Unlocked";
                        status.EnableInClassList("bad", false);
                        break;
                }
            }

            Refresh();
            ui.ShowModal(modal);
            ui.SetModalRefresh(Refresh);
        }
    }

    // ==================================================================== Galaxy map

    public class GalaxyMapScreen : MapScreenBase
    {
        private GalaxyDefinition _galaxy;
        private VisualElement _hub;
        private readonly List<(PlanetDefinition planet, VisualElement node)> _planets =
            new List<(PlanetDefinition, VisualElement)>();
        private string _linkSignature;

        public override string Title => _galaxy != null ? _galaxy.displayName : "";

        public GalaxyMapScreen(GameUI ui) : base(ui) { }

        public void Show(GalaxyDefinition galaxy)
        {
            _galaxy = galaxy;
            ClearMap();
            _planets.Clear();
            _linkSignature = null;

            El.Btn("«  Space Station", UI.ShowStationMap, "map-back").AddTo(Root);
            El.Text("Click a planet to explore or manage it", "map-hint").AddTo(Root);

            _hub = Node(Vector2.zero, "HUB", Color.clear, null, null, "node--station");

            foreach (var planet in galaxy.planets)
            {
                if (planet == null) continue;
                var node = Node(planet.mapPosition, planet.displayName, UIColors.ForId(planet.id), planet.icon,
                    () => UI.ShowPlanet(planet));
                _planets.Add((planet, node));
            }

            if (galaxy.planets.Count == 0)
            {
                var empty = El.Text("No planets discovered in this galaxy yet.", "h2", "muted").AddTo(Root);
                empty.style.position = Position.Absolute;
                empty.style.left = 0;
                empty.style.right = 0;
                empty.style.top = Length.Percent(62);
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
            }

            Refresh();
        }

        public override void Refresh()
        {
            if (_galaxy == null) return;
            var planets = Game.Planets;
            var shipping = Game.Shipping;

            var signature = "";
            var links = new List<(VisualElement, VisualElement)>();
            int operating = 0;

            foreach (var (planet, node) in _planets)
            {
                var ps = planets.GetState(planet);
                node.EnableInClassList("node--locked", ps.status == UnlockStatus.Locked);

                switch (ps.status)
                {
                    case UnlockStatus.Locked:
                        SetStatus(node, "Not explored");
                        break;
                    case UnlockStatus.Unlocking:
                        SetStatus(node, "Exploring " + NumberFormat.Duration(planets.GetExploreTimeRemaining(planet)));
                        break;
                    default:
                        operating++;
                        string text = $"{NumberFormat.Amount(planets.GetValue(planet, PlanetUpgrade.ExtractionRig))}/min";
                        if (planets.IsStorageFull(planet)) text += "  ·  STORAGE FULL";
                        if (!shipping.IsShipHome(planet)) text += "  ·  ship away";
                        SetStatus(node, text);
                        links.Add((_hub, node));
                        signature += planet.id + ";";
                        break;
                }
            }

            SetStatus(_hub, $"{operating}/{_planets.Count} operating");

            if (signature != _linkSignature)
            {
                _linkSignature = signature;
                SetLinks(links);
            }
        }
    }
}
