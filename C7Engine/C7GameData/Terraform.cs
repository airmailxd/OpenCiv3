using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData.Save;
using C7Engine;
using C7Engine.Lua;
using C7Engine.Pathing;

namespace C7GameData;

public class Terraform {
	public struct ScriptContext(Player player, Tile tile, Terraform terraform) {
		public Player player = player;
		public Tile tile = tile;
		public Terraform terraform = terraform;
	}

	public ID Id;
	public string Name;
	public string CivilopediaEntry;
	public int TurnsToComplete;
	public ID RequiredTech;

	public List<Resource> RequiredResources = [];

	public TerrainImprovement Improvement;

	private Action<ScriptContext> Effect;
	private List<Func<ScriptContext, bool>> ActionValidators = [];
	private Func<ScriptContext, int> AIScore;

	public readonly MapUnit.AnimatedAction? Animation;

	// Key of the UI action associated with the Terraform
	public readonly string UIAction;

	// Path to the texture definition in the texture config
	public readonly string ButtonTexture;

	private SaveTerraform dataSource;

	public Terraform(SaveTerraform saveTerraform, GameData gameData) {
		Id = saveTerraform.Id;
		Name = saveTerraform.Name;
		CivilopediaEntry = saveTerraform.CivilopediaEntry;
		TurnsToComplete = saveTerraform.TurnsToComplete;
		RequiredTech = saveTerraform.RequiredTech;
		Animation = saveTerraform.Animation;
		UIAction = saveTerraform.UIAction;
		ButtonTexture = saveTerraform.ButtonTexture;
		dataSource = saveTerraform;
		RequiredResources = saveTerraform.RequiredResources.ConvertAll(resKey => gameData.Resources.Find(res => res.Key == resKey));

		if (saveTerraform.Improvement != null)
			Improvement = gameData.terrainImprovements.Find(ti => ti.key == saveTerraform.Improvement);

		SetRules(gameData.luaBehaviorEngine);
	}

	public override string ToString() {
		return Name;
	}

	private void SetRules(BehaviorEngine engine) {
		foreach (string functionPath in dataSource.Effects) {
			Effect += engine.ImportFunc<Action<ScriptContext>>(functionPath);
		}

		foreach (string functionPath in dataSource.Validators) {
			ActionValidators.Add(engine.ImportFunc<Func<ScriptContext, bool>>(functionPath));
		}

		AIScore = engine.ImportFunc<Func<ScriptContext, int>>(dataSource.AIScore);
	}

	public bool MeetsRequirements(Player player, Tile tile) {
		// Cheapest checks first, stopping at the first one that fails. None of
		// them have side effects, so the order doesn't change the answer.
		if (!player.HasTech(RequiredTech)) {
			return false;
		}

		if (Improvement != null && !tile.overlays.CanAdd(tile, Improvement)) {
			return false;
		}

		// Checking resource access needs the trade network.
		if (RequiredResources.Count > 0) {
			TradeNetwork tradeNetwork = EngineStorage.gameData.GetTradeNetwork();
			foreach (Resource res in RequiredResources) {
				if (!tradeNetwork.HasTradeAccess(tile, player, res)) {
					return false;
				}
			}
		}

		// The Lua validators are the most expensive, so they go last.
		ScriptContext context = new(player, tile, this);
		foreach (Func<ScriptContext, bool> validator in ActionValidators) {
			if (!validator(context)) {
				return false;
			}
		}
		return true;
	}

	public void OnComplete(Player player, Tile tile) {
		Effect?.Invoke(new(player, tile, this));

		if (Improvement != null) {
			if (!tile.overlays.CanAdd(tile, Improvement))
				throw new InvalidOperationException($"Cannot add {Improvement.key} to the tile {tile}");
			// Building any improvement clears the rubble of a destroyed city
			Tile.TryRemoveRuins(tile);
			tile.overlays.Add(Improvement);

			var cityTile = tile.neighbors.FirstOrDefault(n => n.Value.HasCity()).Value;
			if (Improvement.layer == TerrainImprovement.Layer.Roads && cityTile != null) {
				if (this.MeetsRequirements(cityTile.cityAtTile.owner, cityTile))
					cityTile.overlays.Add(Improvement);
			}
		}
	}

	public int CalculateAIScore(Player player, Tile tile) {
		return AIScore(new(player, tile, this));
	}

	public SaveTerraform ToSaveTerraform() {
		return dataSource;
	}

	public bool ProvidesRoad() {
		if (Improvement == null) return false;

		return Improvement.layer == TerrainImprovement.Layer.Roads && Improvement.upgradesFrom == null;
	}
}
