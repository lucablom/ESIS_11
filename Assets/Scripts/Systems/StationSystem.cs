using System;
using VastSpace.Core;
using VastSpace.Data;
using VastSpace.State;
using VastSpace.Util;

namespace VastSpace.Systems
{
    /// <summary>
    /// The space station: sell materials for SC and smelt/craft them into other materials.
    /// Smelting runs one batch at a time through a queue.
    /// </summary>
    public class StationSystem
    {
        private readonly GameState _state;
        private readonly GameDatabase _db;

        public StationSystem(GameState state, GameDatabase db)
        {
            _state = state;
            _db = db;
        }

        private double Now => _state.gameTime;

        public Inventory Inventory => _state.station;

        // ---------------------------------------------------------------- Market

        public double GetSellPrice(MaterialDefinition material) => material.sellPrice;

        public ActionResult Sell(MaterialDefinition material, double amount)
        {
            amount = Math.Floor(Math.Min(amount, _state.station.Get(material.id)));
            if (amount <= 0)
                return ActionResult.Fail($"No {material.displayName} to sell.");

            _state.station.TryRemove(material.id, amount);
            double earned = amount * GetSellPrice(material);
            _state.AddCurrency(CurrencyType.SC, earned);

            GameEvents.RaiseMessage($"Sold {NumberFormat.Amount(amount)} {material.displayName} for {NumberFormat.Money(earned)} SC.");
            GameEvents.RaiseStationInventoryChanged();
            GameEvents.RaiseCurrenciesChanged();
            return ActionResult.Ok();
        }

        public ActionResult SellAll(MaterialDefinition material) =>
            Sell(material, _state.station.Get(material.id));

        // ---------------------------------------------------------------- Smelting

        /// <summary>How many batches can be afforded with the current station inventory.</summary>
        public int GetMaxBatches(RecipeDefinition recipe)
        {
            int max = int.MaxValue;
            foreach (var input in recipe.inputs)
            {
                if (input.material == null || input.amount <= 0) continue;
                int possible = (int)Math.Floor(_state.station.Get(input.material.id) / input.amount + 1e-9);
                max = Math.Min(max, possible);
            }
            return max == int.MaxValue ? 0 : max;
        }

        public ActionResult QueueSmelting(RecipeDefinition recipe, int batches)
        {
            if (batches <= 0) return ActionResult.Fail("Choose at least one batch.");
            if (GetMaxBatches(recipe) < batches)
                return ActionResult.Fail($"Not enough materials for {batches}x {recipe.displayName}.");

            // Inputs are reserved immediately so they cannot be sold while queued.
            foreach (var input in recipe.inputs)
                if (input.material != null)
                    _state.station.TryRemove(input.material.id, input.amount * batches);

            var job = new SmeltingJob { recipeId = recipe.id, batchesRemaining = batches };
            if (_state.smeltingQueue.Count == 0)
                job.batchCompletesAt = Now + recipe.durationSeconds;
            _state.smeltingQueue.Add(job);

            GameEvents.RaiseMessage($"Queued {batches}x {recipe.displayName}.");
            GameEvents.RaiseStationInventoryChanged();
            GameEvents.RaiseSmeltingChanged();
            return ActionResult.Ok();
        }

        /// <summary>Cancels a queued job and refunds the inputs of the batches that have not finished.</summary>
        public ActionResult CancelSmelting(int queueIndex)
        {
            if (queueIndex < 0 || queueIndex >= _state.smeltingQueue.Count)
                return ActionResult.Fail("No such job.");

            var job = _state.smeltingQueue[queueIndex];
            var recipe = _db.GetRecipe(job.recipeId);
            if (recipe != null)
                foreach (var input in recipe.inputs)
                    if (input.material != null)
                        _state.station.Add(input.material.id, input.amount * job.batchesRemaining);

            _state.smeltingQueue.RemoveAt(queueIndex);
            if (queueIndex == 0) StartHeadJob(Now);

            GameEvents.RaiseStationInventoryChanged();
            GameEvents.RaiseSmeltingChanged();
            return ActionResult.Ok();
        }

        /// <summary>0..1 progress of the batch currently being smelted.</summary>
        public float GetCurrentBatchProgress()
        {
            if (_state.smeltingQueue.Count == 0) return 0f;
            var job = _state.smeltingQueue[0];
            var recipe = _db.GetRecipe(job.recipeId);
            if (recipe == null || recipe.durationSeconds <= 0) return 0f;
            double remaining = Math.Max(0, job.batchCompletesAt - Now);
            return (float)(1.0 - remaining / recipe.durationSeconds);
        }

        private void StartHeadJob(double startTime)
        {
            if (_state.smeltingQueue.Count == 0) return;
            var head = _state.smeltingQueue[0];
            var recipe = _db.GetRecipe(head.recipeId);
            head.batchCompletesAt = startTime + (recipe != null ? recipe.durationSeconds : 0);
        }

        public void Tick(double dt)
        {
            bool changed = false;

            while (_state.smeltingQueue.Count > 0)
            {
                var job = _state.smeltingQueue[0];
                if (Now < job.batchCompletesAt) break;

                var recipe = _db.GetRecipe(job.recipeId);
                double finishedAt = job.batchCompletesAt;

                if (recipe != null)
                    foreach (var output in recipe.outputs)
                        if (output.material != null)
                            _state.station.Add(output.material.id, output.amount);

                job.batchesRemaining--;
                changed = true;

                if (job.batchesRemaining > 0 && recipe != null)
                {
                    job.batchCompletesAt = finishedAt + recipe.durationSeconds;
                }
                else
                {
                    _state.smeltingQueue.RemoveAt(0);
                    if (recipe != null) GameEvents.RaiseMessage($"{recipe.displayName} finished.");
                    StartHeadJob(finishedAt);
                }
            }

            if (changed)
            {
                GameEvents.RaiseStationInventoryChanged();
                GameEvents.RaiseSmeltingChanged();
            }
        }
    }
}
