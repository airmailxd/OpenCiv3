local YieldType = ENUMS.Tile_YieldType

local function rules()
  return GAME_DATA().rules
end

local buildings = {}

buildings.production_rules = {
  must_be_coastal = function(city)
    return city.location:NeighborsOcean()
  end,

  must_be_near_river = function(city)
    return city.location:BordersRiver()
  end,

  can_only_be_built_in_towns = function(city)
    return #city.residents <= rules().MaximumLevel1CitySize
  end,

  allows_city_size_2 = function(city)
    local is_town = #city.residents <= rules().MaximumLevel1CitySize
    local has_fresh_water = city.location:NeighborsFreshWater() or city.location:BordersRiver()
    return is_town and not has_fresh_water
  end,

  -- The Hospital and Shakespeare's Theater let a city grow past size 12, and
  -- the Civilopedia says both need "a population of 7 or higher" to build,
  -- i.e. the city must have grown past a town.
  allows_city_size_3 = function(city)
    return #city.residents > rules().MaximumLevel1CitySize
  end,
}

buildings.unit_production_effects = {
  veteran_ground_units = function(unit)
    if unit.unitType:IsLandUnit() then
      unit:Promote()
    end
  end,

  veteran_sea_units = function(unit)
    if unit.unitType:IsSeaUnit() then
      unit:Promote()
    end
  end,
}

buildings.tile_modifiers = {
  increases_food_in_water = function(yield)
    if yield.type == YieldType.Food and yield.tile:IsWater() and yield.baseYield > 0 then
      yield.bonus = yield.bonus + 1
    end
  end,

  increases_shields_in_water = function(yield)
    if yield.type == YieldType.Production and yield.tile:IsWater() then
      yield.bonus = yield.bonus + 1
    end
  end,

  increases_trade_in_water = function(yield)
    if yield.type == YieldType.Commerce and yield.tile:IsWater() and yield.baseYield > 0 then
      yield.bonus = yield.bonus + 1
    end
  end,

  -- The Colossus: +1 commerce on every tile of the city, land or water,
  -- that already makes commerce, the city center included. Civ3 adds it
  -- before the despotism penalty, so a tile it raises from 2 to 3 is
  -- brought back down to 2.
  increased_trade = function(yield)
    if yield.type == YieldType.Commerce and yield.baseYield > 0 then
      yield.bonus = yield.bonus + 1
      local owner = yield.tile:OwningPlayer()
      if owner ~= nil and owner.government.hasTilePenalty and yield.baseYield == 2 then
        yield.penalty = yield.penalty + 1
      end
    end
  end,
}

return buildings
