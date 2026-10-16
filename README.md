# Vast Space

Ett 2D-rymdspel (management/idle) i Unity 6. Spelet är på engelska.

Du börjar på en rymdstation. Därifrån låser du upp galaxer och utforskar planeter, som sedan utvinner material. Materialet skeppas till stationen, där det säljs eller smälts till nya material.

## Spelloop

1. **Lås upp en galax** från rymdstationen. Det kostar SC och kan ta tid.
2. **Utforska en planet** i galaxen. Det kostar SC + 1 Mining Equipment och tar tid (1:50 som standard).
3. Planeten **utvinner material** automatiskt tills lagret är fullt.
4. **Uppgradera planeten:**
   - *Extraction Rig*: material per minut
   - *Storage*: hur mycket som får plats per material
   - *Hangar*: skeppets kapacitet och hastighet
5. **Skicka skeppet** till stationen. Det tar tid att flyga dit och tillbaka.
6. På stationen: **sälj** material för SC eller **smält/tillverka** nya saker (t.ex. Mining Equipment för att utforska nästa planet).

Spelet räknar även tiden när det är stängt (offline-progress, max 24 h som standard).

## Kom igång

1. Öppna projektet i Unity 6. Första gången skapas exempeldatan automatiskt: material, recept, galaxer och planeter under `Assets/Data`, och `GameDatabase` under `Assets/Resources`.
2. Tryck Play i valfri scen. En GameManager skapas automatiskt, och dev-panelen syns uppe till vänster i **Game**-fönstret.

Vill du återskapa datan: **Vast Space → Create Sample Data**. Vill du ha GameManager synligt i scenen: **Vast Space → Add Game Manager To Scene**.

Siffrorna är platshållare. Balansera spelet genom att ändra asseten i Inspector.

## Kodstruktur (`Assets/Scripts`)

| Mapp | Innehåll |
|---|---|
| `Data/` | ScriptableObjects: `MaterialDefinition`, `RecipeDefinition`, `PlanetDefinition` (med uppgraderingsnivåer), `GalaxyDefinition`, `GameDatabase` |
| `State/` | Det som sparas: `GameState`, `PlanetState`, `Inventory`, skepp och smältkö. Bara id:n, inga Unity-referenser |
| `Systems/` | Spellogik: `PlanetSystem` (galaxer, utforskning, uppgraderingar, utvinning), `ShippingSystem`, `StationSystem` (sälj + smältning) |
| `Core/` | `GameManager` (startpunkt, tick, autosave, offline-tid), `SaveSystem` (JSON), `Economy` (kostnader), `GameEvents` |
| `Util/` | `NumberFormat`: formatering av tal och tider för HUD:en |
| `DevTools/` | `DevPanel`: tillfällig testpanel |
| `Editor/` | Menyn *Vast Space* |

### För UI-skript

Allt nås via `GameManager.Instance`:

```csharp
var gm = GameManager.Instance;
gm.Planets.StartExploration(planet);   // returnerar ActionResult (Success + Message)
gm.Shipping.SendShip(planet);
gm.Station.Sell(material, 100);
double sc = gm.State.GetCurrency(CurrencyType.SC);
```

Prenumerera på `GameEvents` (t.ex. `CurrenciesChanged`, `PlanetChanged`, `Message`) för att uppdatera UI:t, och avprenumerera i `OnDisable`.

## Nästa steg

- Riktigt UI: stationskarta, galaxkarta, planetpanel, inventory, balansvy
- Market (köp/sälj med varierande priser), Research, Missions, Achievements
- Energi och personal per planet (knapparna ENERGY/STAFF i mockupen)
