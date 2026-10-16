using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VastSpace.Core;
using VastSpace.Data;
using MaterialDefinition = VastSpace.Data.MaterialDefinition;
using VastSpace.Util;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace VastSpace.UI
{
    /// <summary>Tabs in the bottom navigation bar.</summary>
    public enum NavTab { Map, Inventory, Market, Crafting, Research, Missions, Achievements }

    /// <summary>
    /// Builds the whole game UI in code with UI Toolkit (no prefabs or UXML needed).
    /// Created automatically by <see cref="GameBootstrap"/>; one per game.
    /// Layout: top bar (currencies, location, play time) / main area (one screen at a time) / bottom nav bar.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public static GameUI Instance { get; private set; }

        private const float RefreshInterval = 0.1f;

        public GameManager Game => GameManager.Instance;

        private UIDocument _document;
        private VisualElement _root, _main, _overlay, _devPanel;
        private Label _sc, _ssc, _st, _title, _clock, _date, _toast;
        private readonly Dictionary<NavTab, Button> _navButtons = new Dictionary<NavTab, Button>();

        private UIScreen _current;
        private NavTab _currentTab;
        private StationMapScreen _stationMap;
        private GalaxyMapScreen _galaxyMap;
        private PlanetScreen _planetScreen;
        private InventoryScreen _inventory;
        private MarketScreen _market;
        private CraftingScreen _crafting;
        private ComingSoonScreen _research, _missions, _achievements;

        /// <summary>The map the "Map" tab returns to (station, a galaxy or a planet).</summary>
        private UIScreen _lastMapScreen;

        private float _refreshTimer;
        private float _toastTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureEventSystem();
        }

        private void Start()
        {
            if (Game == null)
            {
                Debug.LogError("[GameUI] No GameManager found.");
                enabled = false;
                return;
            }

            CreateDocument();
            BuildLayout();
            GameEvents.Message += OnMessage;
            GameEvents.GameLoaded += OnGameLoaded;
            ShowStationMap();
            RefreshNow();
        }

        private void OnDestroy()
        {
            GameEvents.Message -= OnMessage;
            GameEvents.GameLoaded -= OnGameLoaded;
            if (Instance == this) Instance = null;
        }

        // Supports "Enter Play Mode" without domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        /// <summary>
        /// The project uses the new Input System only. UI Toolkit receives clicks through an
        /// EventSystem with an InputSystemUIInputModule, so make sure one exists.
        /// </summary>
        private static void EnsureEventSystem()
        {
#if ENABLE_INPUT_SYSTEM
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            DontDestroyOnLoad(go);
#endif
        }

        private void CreateDocument()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "VastSpace PanelSettings";
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/VastSpaceTheme");
            if (settings.themeStyleSheet == null)
                Debug.LogWarning("[GameUI] Theme not found at Resources/UI/VastSpaceTheme.tss - text may not render.");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;

            // Created inactive so the PanelSettings are set before the UIDocument enables.
            var go = new GameObject("UIDocument");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            _document = go.AddComponent<UIDocument>();
            _document.panelSettings = settings;
            go.SetActive(true);
        }

        private void BuildLayout()
        {
            _root = _document.rootVisualElement;
            _root.Clear();
            var sheet = Resources.Load<StyleSheet>("UI/VastSpace");
            if (sheet != null) _root.styleSheets.Add(sheet);
            else Debug.LogWarning("[GameUI] Stylesheet not found at Resources/UI/VastSpace.uss");

            var app = El.Div("vs-root").AddTo(_root);
            app.style.flexGrow = 1;
            app.style.backgroundImage = new StyleBackground(Starfield.Create(1920, 1080, 1400, 7));

            // ---- Top bar
            var top = El.Div("vs-topbar").AddTo(app);
            _sc = Currency(top, "SC", "currency-icon--sc");
            _ssc = Currency(top, "SSC", "currency-icon--ssc");
            _st = Currency(top, "ST", "currency-icon--st");
            _title = El.Text("Vast Space", "vs-title").AddTo(top);
            _clock = El.Text("00:00:00", "vs-clock").AddTo(top);

            // ---- Main area
            _main = El.Div("vs-main").AddTo(app);

            // ---- Bottom bar
            var bottom = El.Div("vs-bottombar").AddTo(app);
            Nav(bottom, NavTab.Map, "Map", ShowLastMap);
            Nav(bottom, NavTab.Inventory, "Inventory", () => ShowTab(NavTab.Inventory));
            Nav(bottom, NavTab.Market, "Market", () => ShowTab(NavTab.Market));
            Nav(bottom, NavTab.Crafting, "Crafting", () => ShowTab(NavTab.Crafting));
            Nav(bottom, NavTab.Research, "Research", () => ShowTab(NavTab.Research));
            Nav(bottom, NavTab.Missions, "Missions", () => ShowTab(NavTab.Missions));
            Nav(bottom, NavTab.Achievements, "Achievements", () => ShowTab(NavTab.Achievements));
            El.Div("grow").AddTo(bottom);
            El.Btn("DEV", ToggleDevPanel, "btn--small", "btn--ghost").AddTo(bottom);
            El.Text("build " + Application.version, "build-label").AddTo(bottom);
            _date = El.Text("", "vs-date").AddTo(bottom);

            // ---- Layers above everything (later = on top)
            _devPanel = BuildDevPanel().AddTo(app);
            _devPanel.SetVisible(false);

            _overlay = El.Div("overlay").AddTo(app);
            _overlay.SetVisible(false);
            _overlay.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target == _overlay) CloseModal();
            });

            _toast = El.Text("", "toast").AddTo(app);
            _toast.pickingMode = PickingMode.Ignore;
            _toast.SetVisible(false);

            // ---- Screens
            _stationMap = new StationMapScreen(this);
            _galaxyMap = new GalaxyMapScreen(this);
            _planetScreen = new PlanetScreen(this);
            _inventory = new InventoryScreen(this);
            _market = new MarketScreen(this);
            _crafting = new CraftingScreen(this);
            _research = new ComingSoonScreen(this, "Research",
                "Research new technologies to improve extraction, unlock auto-shipping and better smelters.");
            _missions = new ComingSoonScreen(this, "Missions",
                "Complete contracts for the space station to earn SC, SSC and ST.");
            _achievements = new ComingSoonScreen(this, "Achievements",
                "Track your milestones across the galaxies.");
        }

        private Label Currency(VisualElement parent, string code, string iconClass)
        {
            var row = El.Div("currency").AddTo(parent);
            El.Text(code, "currency-icon", iconClass).AddTo(row);
            return El.Text("0.00 " + code, "currency-label").AddTo(row);
        }

        private void Nav(VisualElement parent, NavTab tab, string text, System.Action onClick)
        {
            _navButtons[tab] = El.Btn(text, onClick, "nav-btn").AddTo(parent);
        }

        // ------------------------------------------------------------ Navigation

        public void ShowStationMap() => ShowMapScreen(_stationMap, () => _stationMap.Show());

        public void ShowGalaxy(GalaxyDefinition galaxy) => ShowMapScreen(_galaxyMap, () => _galaxyMap.Show(galaxy));

        public void ShowPlanet(PlanetDefinition planet) => ShowMapScreen(_planetScreen, () => _planetScreen.Show(planet));

        private void ShowMapScreen(UIScreen screen, System.Action show)
        {
            CloseModal();
            show();
            _lastMapScreen = screen;
            SetCurrent(screen, NavTab.Map);
        }

        private void ShowLastMap()
        {
            if (_lastMapScreen == null || _lastMapScreen == _stationMap) ShowStationMap();
            else SetCurrent(_lastMapScreen, NavTab.Map);
        }

        public void ShowTab(NavTab tab)
        {
            CloseModal();
            switch (tab)
            {
                case NavTab.Map: ShowLastMap(); return;
                case NavTab.Inventory: _inventory.Show(); SetCurrent(_inventory, tab); break;
                case NavTab.Market: _market.Show(); SetCurrent(_market, tab); break;
                case NavTab.Crafting: _crafting.Show(); SetCurrent(_crafting, tab); break;
                case NavTab.Research: SetCurrent(_research, tab); break;
                case NavTab.Missions: SetCurrent(_missions, tab); break;
                case NavTab.Achievements: SetCurrent(_achievements, tab); break;
            }
        }

        private void SetCurrent(UIScreen screen, NavTab tab)
        {
            if (_current != null && _current != screen) _current.Root.RemoveFromHierarchy();
            if (screen.Root.parent != _main) _main.Add(screen.Root);
            _current = screen;
            _currentTab = tab;

            foreach (var kv in _navButtons)
                kv.Value.EnableInClassList("nav-btn--active", kv.Key == tab);

            _title.text = string.IsNullOrEmpty(screen.Title) ? "Vast Space" : "Vast Space  ·  " + screen.Title;
            screen.Refresh();
        }

        public void ShowModal(VisualElement content)
        {
            _overlay.Clear();
            _overlay.Add(content);
            _overlay.SetVisible(true);
        }

        public void CloseModal()
        {
            if (_overlay == null) return;
            _overlay.Clear();
            _overlay.SetVisible(false);
            _modalRefresh = null;
        }

        private System.Action _modalRefresh;

        /// <summary>Lets an open modal update its labels on the refresh tick.</summary>
        public void SetModalRefresh(System.Action refresh) => _modalRefresh = refresh;

        private void OnGameLoaded()
        {
            // State object was replaced (e.g. reset): rebuild everything from scratch.
            ShowStationMap();
        }

        // ------------------------------------------------------------ Feedback

        /// <summary>Shows the failure message of an action as a red toast.</summary>
        public void Report(ActionResult result)
        {
            if (!result.Success) ShowToast(result.Message, true);
        }

        private void OnMessage(string text) => ShowToast(text, false);

        public void ShowToast(string text, bool error)
        {
            if (_toast == null) return;
            _toast.text = text;
            _toast.EnableInClassList("toast--error", error);
            _toast.SetVisible(true);
            _toastTimer = error ? 4f : 3f;
        }

        // ------------------------------------------------------------ Refresh

        private void Update()
        {
            if (_root == null || Game == null || Game.State == null) return;

            if (_toastTimer > 0)
            {
                _toastTimer -= Time.unscaledDeltaTime;
                if (_toastTimer <= 0) _toast.SetVisible(false);
            }

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0) return;
            _refreshTimer = RefreshInterval;
            RefreshNow();
        }

        private void RefreshNow()
        {
            var s = Game.State;
            _sc.text = FormatCurrency(s.GetCurrency(CurrencyType.SC)) + " SC";
            _ssc.text = FormatCurrency(s.GetCurrency(CurrencyType.SSC)) + " SSC";
            _st.text = FormatCurrency(s.GetCurrency(CurrencyType.ST)) + " ST";
            _clock.text = NumberFormat.Clock(s.playTimeSeconds);
            _date.text = Game.GameDate.ToString("yyyy-MM-dd");

            _current?.Refresh();
            _modalRefresh?.Invoke();
        }

        private static string FormatCurrency(double value) =>
            value < 1_000_000 ? NumberFormat.Money(value) : NumberFormat.Short(value);

        // ------------------------------------------------------------ Dev tools

        private VisualElement BuildDevPanel()
        {
            var panel = El.Div("panel", "dev-panel");
            El.Text("DEV TOOLS", "h3").AddTo(panel);

            var money = El.Div("row").AddTo(panel);
            El.Btn("+100K SC", () => AddCredits(100_000), "btn--small").AddTo(money);
            El.Btn("+1M SC", () => AddCredits(1_000_000), "btn--small").AddTo(money);

            var speed = El.Div("row", "mt").AddTo(panel);
            El.Text("Speed", "small", "mr").AddTo(speed);
            foreach (var x in new[] { 1f, 10f, 50f })
                El.Btn("x" + x, () =>
                {
                    Game.SimulationSpeed = x;
                    ShowToast($"Simulation speed x{x}", false);
                }, "btn--small").AddTo(speed);

            var time = El.Div("row", "mt").AddTo(panel);
            El.Btn("+10 min", () => Game.Advance(600), "btn--small").AddTo(time);
            El.Btn("Save", () => { Game.Save(); ShowToast("Game saved.", false); }, "btn--small").AddTo(time);

            Button reset = null;
            reset = El.Btn("Reset game", () =>
            {
                if (reset.text != "Click again to reset")
                {
                    reset.text = "Click again to reset";
                    return;
                }
                reset.text = "Reset game";
                Game.ResetGame();
            }, "btn--small").AddTo(El.Div("row", "mt").AddTo(panel));

            return panel;
        }

        private void AddCredits(double amount)
        {
            Game.State.AddCurrency(CurrencyType.SC, amount);
            GameEvents.RaiseCurrenciesChanged();
        }

        private void ToggleDevPanel() =>
            _devPanel.SetVisible(_devPanel.resolvedStyle.display == DisplayStyle.None);
    }

    /// <summary>Base class for a full-size screen in the main area.</summary>
    public abstract class UIScreen
    {
        protected readonly GameUI UI;
        public VisualElement Root { get; protected set; }
        public virtual string Title => "";

        protected GameManager Game => UI.Game;

        protected UIScreen(GameUI ui)
        {
            UI = ui;
            Root = new VisualElement();
            Root.style.flexGrow = 1;
        }

        /// <summary>Called about 10 times per second while visible. Only update text/values here.</summary>
        public virtual void Refresh() { }
    }

    public class ComingSoonScreen : UIScreen
    {
        private readonly string _title;
        public override string Title => _title;

        public ComingSoonScreen(GameUI ui, string title, string description) : base(ui)
        {
            _title = title;
            var panel = El.Div("panel", "screen").AddTo(Root);
            El.Text(title, "h1").AddTo(panel);
            El.Text("Coming soon", "h3", "mt").AddTo(panel);
            El.Text(description, "paragraph", "muted").AddTo(panel);
        }
    }
}
