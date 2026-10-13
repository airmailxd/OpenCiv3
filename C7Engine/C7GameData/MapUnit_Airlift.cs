namespace C7GameData {
	// Airlifts: "A city with an [Airport] can be used to ... airlift land
	// units" (Conquests Civilopedia, BLDG_Airport); the order is "Airlif(T):
	// Choose new airbase" (labels.txt, #UNIT_ACTIONS). Which units may be
	// airlifted is the BIQ's PRTO Airlift flag: in conquests.biq every land
	// unit with an attack, and Workers, but not Settlers, artillery, Leaders
	// or armies (md4: the flag "overlaps completely with units also having 0
	// attack & defense"; CKS: "you can't airlift armies or boats",
	// https://forums.civfanatics.com/threads/air-transport.662163/).
	//
	// "The limit is one unit per turn per sending airfield or airport. You
	// can land as many units as you like each turn" and "If you send by air,
	// the units will arrive with no movement" (CKS, same thread). Airfields
	// "function in the same manner that a city with an airport would"
	// (GCON_Airfields), so they send and receive airlifts too.
	//
	// UNVERIFIED (no Civ3 source found): an airlift reaches any distance,
	// and the unit only needs some movement left to be sent, as a
	// paratrooper does for an airdrop (see CanAirdropFrom).
	public partial class MapUnit {
		// Whether the tile is one of the player's airlift bases: a city of
		// theirs with an airport, or an airfield of theirs.
		public static bool IsAirliftBase(Tile tile, Player player) {
			if (!Tile.IsTileValid(tile) || player == null) {
				return false;
			}
			if (HasAirfieldFor(tile, player)) {
				return true;
			}
			return tile.HasCity() && tile.cityAtTile.owner == player && tile.cityAtTile.Airport() != null;
		}

		public bool CanBeAirlifted() {
			return IsLandUnit() && unitType.actions.Contains(UnitAction.Airlift) && !IsArmy();
		}

		// Whether the unit can be airlifted from where it is now.
		public bool CanAirliftFrom() {
			if (!CanBeAirlifted() || !movementPoints.canMove || IsLoaded() || !Tile.IsTileValid(location)) {
				return false;
			}
			return IsAirliftBase(location, owner) && !owner.HasAirliftedFromThisTurn(location);
		}

		public bool CanAirliftTo(Tile tile) {
			if (!CanAirliftFrom() || !Tile.IsTileValid(tile) || tile == location || !IsAirliftBase(tile, owner)) {
				return false;
			}
			if (!unitType.CanEnterTerrainOf(tile)) {
				return false;
			}
			foreach (MapUnit u in tile.unitsOnTile) {
				if (u.owner != owner) {
					return false;
				}
			}
			return true;
		}

		public void Airlift(Tile tile) {
			if (!CanAirliftTo(tile)) {
				return;
			}
			owner.RecordAirliftFrom(location);
			MoveStraightTo(tile);
			EndMission();
		}
	}
}
