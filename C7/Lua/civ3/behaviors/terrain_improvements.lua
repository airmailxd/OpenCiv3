local YieldType = ENUMS.Tile_YieldType
local Layer = ENUMS.TerrainImprovement_Layer

-- NATIVE_DEFAULT marks a function that the game runs as C# instead, since
-- tile yields are worked out very often. If you change what such a function
-- does, take away its NATIVE_DEFAULT so the game runs your Lua (or change the
-- C# in C7Engine/Lua/NativeBehaviors.cs to match). A function put in its place
-- by an addon always runs as Lua.
local terrain_improvements = {
  railroad = {
    tile_modifier = NATIVE_DEFAULT("terrain_improvements.railroad.tile_modifier", function(yield)
      local yield_improvement = yield.tile.overlays:ImprovementAtLayer(Layer.ResourceDevelopment)

      if not yield_improvement then
        return
      end

      -- bonus production for a mined tile
      if yield_improvement.key == "mine" and yield.type == YieldType.Production then
        yield.bonus = yield.bonus + 1
      end

      -- bonus food for an irrigated tile
      if yield_improvement.key == "irrigation" and yield.type == YieldType.Food then
        yield.bonus = yield.bonus + 1
      end
    end),
  },
}

return terrain_improvements
