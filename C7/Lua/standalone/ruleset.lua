--[[
The file name the Civilopedia icons made for standalone mode use for an
entry (ArtGen/civilopedia_icons, common.file_stem): the entry's name without
its PRTO_ prefix, with anything but letters and digits made underscores.
]]
local function icon_stem(civilopedia_entry)
  local name = civilopedia_entry:gsub("^%u+_", "", 1)
  local stem = name:gsub("[^%w]", "_"):gsub("_+", "_"):gsub("^_", ""):gsub("_$", "")
  return stem
end

--[[
This function defines the standalone addon to the base ruleset.

Every unit of the base ruleset is kept, with its upgrades. A unit is drawn
with the same art as in Civ3 mode (its art name and its era, slave and
scientific leader variations), which in standalone mode is our own:
C7/ModernArt/Art/Units/<art name> holds its sheets, a metadata file that
stands in for Civ3's INI and flics, and its thumbnail (see ModernGraphics and
AnimationManager on the C# side, and textures.lua for the thumbnails). Its
Civilopedia art is the icon made for it in C7/Civilopedia.

On the C# side, this function is called as part of addon loading logic
in GameModeLoader class
]]
return function(civ3_game_mode)
  for _, unit_prototype in ipairs(civ3_game_mode.unitPrototypes) do
    local stem = icon_stem(unit_prototype.civilopediaEntry)
    unit_prototype.art.pediaArt.large = "Art/Civilopedia/Icons/Units/" .. stem .. "_large.png"
    unit_prototype.art.pediaArt.small = "Art/Civilopedia/Icons/Units/" .. stem .. "_small.png"
  end

  -- A scenario's resources whose pictures Civ3's resources.pcx hasn't got
  -- (a scenario has its own sheet), by Civilopedia entry: our sheet
  -- (C7/ModernArt/Art/resources.png, ArtGen/terrain_features/resources.py)
  -- has them past Civ3's own, in cells it leaves empty. MDJ's (C7/Lua/mdj)
  -- Jade and Sake are in cells 7 and 8 of the Sengoku conquest's sheet,
  -- where Civ3's has Uranium and Wine.
  local own_icons = { GOOD_Jade = 26, GOOD_Sake = 27 }
  for _, resource in ipairs(civ3_game_mode.resources or {}) do
    local icon = own_icons[resource.civilopediaEntry]
    if icon then
      resource.icon = icon
    end
  end

  return civ3_game_mode
end
