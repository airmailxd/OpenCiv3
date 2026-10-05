using System;
using System.Text.Json.Serialization;

namespace C7GameData {
	public class Government {
		public ID id;
		public string name;
		public string civilopediaEntry;

		// If non-null, the tech required to use this government.
		public ID prerequisiteTech;

		// If true, this is the starting government.
		public bool defaultType;

		// If true, this is the government used while switching governments.
		public bool transitionType;

		// The "despotism penalty" applies for this goverment; reduces all 
		// commerce, production and food output done by citizen laborers by -1 
		// when they produce more than 2.
		public bool hasTilePenalty {
			get => _hasTilePenalty;
			set {
				_hasTilePenalty = value;
				RebuildTileModifier();
			}
		}
		private bool _hasTilePenalty;

		// +1 commerce any tile with at least 1 commerce.
		public bool hasTradeBonus {
			get => _hasTradeBonus;
			set {
				_hasTradeBonus = value;
				RebuildTileModifier();
			}
		}
		private bool _hasTradeBonus;

		// Built from the flags above by their setters; don't add to it
		// directly, as setting a flag replaces it.
		[JsonIgnore]
		public Action<Tile.Yield> tileModifier;

		// Rebuilt from scratch, so setting a flag again (even to the same
		// value) never applies its modifier twice. The penalty goes first,
		// as it always has: it looks at the yield including bonuses.
		private void RebuildTileModifier() {
			Action<Tile.Yield> modifier = null;
			if (_hasTilePenalty) {
				modifier += TilePenalty;
			}
			if (_hasTradeBonus) {
				modifier += TradeBonus;
			}
			tileModifier = modifier;
		}

		// See https://codehappy.net/apolyton/threads/46801-1.htm and
		// https://forums.civfanatics.com/threads/everything-about-corruption-c3c-edition.76619/.
		public enum CorruptionType {
			Minimal,
			Nuisance,
			Problematic,
			Rampant,
			Catastrophic,
			Communal,
			Off
		};
		public CorruptionType corruptionType;

		public enum HurryProductionType {
			CannotHurry,
			ForcedLabor,
			PaidLabor,
		};
		public HurryProductionType hurryingType;

		// The highest value, in tenths, that any one of the tax, science and
		// luxury sliders can be set to under this government.
		public int rateCap = 10;

		// How much the people tire of war: 0 not at all, 1 low, 2 high.
		public int warWeariness;

		public int draftLimit;
		public int militaryPoliceLimit;
		public int workerRate;

		public bool allUnitsFree;
		public int freeUnitsPerTown;
		public int freeUnitsPerCity;
		public int freeUnitsPerMetropolis;
		public int unitCost;

		private static void TradeBonus(Tile.Yield yield) {
			if (yield.type == Tile.YieldType.Commerce && yield.baseYield > 0) {
				yield.bonus += 1;
			}
		}

		private static void TilePenalty(Tile.Yield yield) {
			yield.penalty += yield.yield > 2 ? 1 : 0;
		}
	}
}
