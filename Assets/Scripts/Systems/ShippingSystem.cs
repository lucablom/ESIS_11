using System;
using System.Collections.Generic;
using VastSpace.Core;
using VastSpace.Data;
using VastSpace.State;
using VastSpace.Util;

namespace VastSpace.Systems
{
    /// <summary>
    /// Each operating planet has one ship in its hangar. Sending it moves materials from
    /// planet storage to the space station. The ship flies there (delivery) and back (available again).
    /// </summary>
    public class ShippingSystem
    {
        private readonly GameState _state;
        private readonly GameDatabase _db;
        private readonly PlanetSystem _planets;

        public ShippingSystem(GameState state, GameDatabase db, PlanetSystem planets)
        {
            _state = state;
            _db = db;
            _planets = planets;
        }

        private double Now => _state.gameTime;

        /// <summary>One-way travel time in seconds.</summary>
        public double GetTravelTime(PlanetDefinition planet)
        {
            double speed = _planets.GetValue(planet, PlanetUpgrade.ShipSpeed);
            return speed <= 0 ? double.PositiveInfinity : planet.travelDistance / speed;
        }

        public double GetShipCapacity(PlanetDefinition planet) =>
            _planets.GetValue(planet, PlanetUpgrade.ShipCapacity);

        public bool IsShipHome(PlanetDefinition planet) => _planets.GetState(planet).shipReturnsAt <= Now;

        public double GetShipReturnTimeRemaining(PlanetDefinition planet) =>
            Math.Max(0, _planets.GetState(planet).shipReturnsAt - Now);

        public IEnumerable<Shipment> GetShipmentsInTransit() => _state.shipments;

        public double GetArrivalTimeRemaining(Shipment shipment) => Math.Max(0, shipment.arrivesAt - Now);

        public ActionResult SendShip(PlanetDefinition planet)
        {
            var ps = _planets.GetState(planet);
            if (ps.status != UnlockStatus.Unlocked)
                return ActionResult.Fail($"{planet.displayName} is not operating yet.");
            if (!IsShipHome(planet))
                return ActionResult.Fail($"Ship is away ({NumberFormat.Duration(GetShipReturnTimeRemaining(planet))} until it returns).");
            if (ps.storage.Total < 1)
                return ActionResult.Fail("Nothing to ship yet.");

            double travel = GetTravelTime(planet);
            if (double.IsInfinity(travel))
                return ActionResult.Fail("The ship has no speed. Check the ShipSpeed upgrade track.");

            var cargo = LoadCargo(ps.storage, GetShipCapacity(planet));
            var shipment = new Shipment
            {
                id = _state.nextShipmentId++,
                planetId = planet.id,
                cargo = cargo,
                departedAt = Now,
                arrivesAt = Now + travel
            };
            _state.shipments.Add(shipment);
            ps.shipReturnsAt = Now + travel * 2;

            GameEvents.RaiseMessage($"Ship left {planet.displayName} with {NumberFormat.Amount(cargo.Total)} units.");
            GameEvents.RaisePlanetChanged(planet.id);
            return ActionResult.Ok();
        }

        public void SetAutoShip(PlanetDefinition planet, bool enabled)
        {
            _planets.GetState(planet).autoShip = enabled;
            GameEvents.RaisePlanetChanged(planet.id);
        }

        /// <summary>
        /// Moves up to <paramref name="capacity"/> units out of storage. If everything does not fit,
        /// each material is loaded in proportion to how much is stored.
        /// </summary>
        private static Inventory LoadCargo(Inventory storage, double capacity)
        {
            var cargo = new Inventory();
            double total = storage.Total;
            if (total <= 0 || capacity <= 0) return cargo;

            double fraction = Math.Min(1.0, capacity / total);
            var ids = new List<string>(storage.All.Keys);
            foreach (var id in ids)
            {
                double take = storage.Get(id) * fraction;
                // Ship whole units only; keep fractions on the planet.
                take = Math.Floor(take);
                if (take <= 0) continue;
                cargo.Add(id, storage.RemoveUpTo(id, take));
            }
            return cargo;
        }

        public void Tick(double dt)
        {
            bool delivered = false;
            for (int i = _state.shipments.Count - 1; i >= 0; i--)
            {
                var s = _state.shipments[i];
                if (Now < s.arrivesAt) continue;

                _state.station.AddAll(s.cargo);
                _state.shipments.RemoveAt(i);
                delivered = true;

                var def = _db.GetPlanet(s.planetId);
                GameEvents.RaiseMessage($"Shipment from {(def != null ? def.displayName : s.planetId)} arrived: {NumberFormat.Amount(s.cargo.Total)} units.");
                GameEvents.RaiseShipmentDelivered(s.planetId);
            }
            if (delivered) GameEvents.RaiseStationInventoryChanged();

            // Auto shipping: send as soon as the ship is home and there is something to carry.
            foreach (var ps in _state.planets)
            {
                if (!ps.autoShip || ps.status != UnlockStatus.Unlocked || ps.shipReturnsAt > Now) continue;
                var def = _db.GetPlanet(ps.id);
                if (def == null) continue;

                // Wait until the ship can be reasonably full, or storage is full, to avoid tiny trips.
                double capacity = GetShipCapacity(def);
                if (ps.storage.Total >= capacity * 0.5 || _planets.IsStorageFull(def))
                    SendShip(def);
            }
        }
    }
}
