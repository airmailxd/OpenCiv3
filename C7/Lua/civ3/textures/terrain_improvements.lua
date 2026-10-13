local terrain_improvements = {}

local TERRAIN = "Art/Terrain/"
local CITIES = "Art/Cities/"

--[[
TerrainBuildings.pcx contains multiple pieces of art in a grid, with each
item being 128x64 pixesl.

The basic version is:
  Fortress (ancient)     | Colony (an)   | Barb camp
  Fortress (medieval)    | Colony (me)   | Mine
  Fortress (industrial)  | Colony (in)   | Empty
  Fortress (modern)      | Colony (mo)   | Empty
--]]
local function make_entry(x, y)
  return {
    path = TERRAIN .. "TerrainBuildings.pcx",
    crop_region = { 128 * x, 64 * y, 128, 64 },
    shadows = false,
  }
end

terrain_improvements = {
  mine = make_entry(2, 1),
  fortress = make_entry(0, 0),
  barricade = make_entry(3, 0),
  -- The colony of the ancient era.
  colony = make_entry(1, 0),
}

--[[
"x_airfields and detect.pcx" (from Play the World) has the airfields in its
top row of 128x64 cells, and below them the outposts (one per era, left to
right) and then the radar tower, each reaching into the row above. An
outpost's foot is at y = 147, so a 128x128 crop from y = 52 has it at the
middle of the tile in the crop's bottom half (TileOverlayLayer draws a tall
sprite with its bottom on the tile's).
--]]
terrain_improvements.outpost = {
  path = TERRAIN .. "x_airfields and detect.pcx",
  crop_region = { 0, 52, 128, 128 },
  shadows = false,
}

terrain_improvements.irrigation = {
  grass = TERRAIN .. "irrigation.pcx",
  desert = TERRAIN .. "irrigation DESETT.pcx",
  plains = TERRAIN .. "irrigation PLAINS.pcx",
  tundra = TERRAIN .. "irrigation TUNDRA.pcx",
}

terrain_improvements.railroad = TERRAIN .. "railroads.pcx"

terrain_improvements.road = TERRAIN .. "roads.pcx"

terrain_improvements.ruins = CITIES .. "DESTROY.PCX"
terrain_improvements.pollution = TERRAIN .. "pollution.pcx"
terrain_improvements.craters = TERRAIN .. "craters.pcx"

return terrain_improvements
