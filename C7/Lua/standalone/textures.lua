--[[
  To add new replacement texture to the config modify the "c7_texture_list" table.
--]]
local utils = require "utils"

local c7_texture_list = {
  "Art/buttonsFINAL.png",
  "Art/TileInfo.png",
  "Art/X-o_ALLstates-sprite.png",
  "Art/SmallHeads/popHeads.png",
  "Art/Terrain/Mountains-snow.png",
  "Art/Terrain/Mountains.png",
  "Art/Terrain/TerrainBuildings.png",
  "Art/Terrain/goodyhuts.png",
  "Art/Terrain/Volcanos forests.png",
  "Art/Terrain/Volcanos jungles.png",
  "Art/Terrain/Volcanos.png",
  "Art/Terrain/grassland forests.png",
  "Art/Terrain/hill forests.png",
  "Art/Terrain/hill jungle.png",
  "Art/Terrain/irrigation DESETT.png",
  "Art/Terrain/irrigation PLAINS.png",
  "Art/Terrain/irrigation TUNDRA.png",
  "Art/Terrain/irrigation.png",
  "Art/Terrain/marsh.png",
  "Art/Terrain/mountain forests.png",
  "Art/Terrain/mountain jungles.png",
  "Art/Terrain/mtnRivers.png",
  "Art/Terrain/deltaRivers.png",
  "Art/Terrain/plains forests.png",
  "Art/Terrain/tnt.png",
  "Art/Terrain/roads.png",
  "Art/Terrain/railroads.png",
  "Art/Terrain/tundra forests.png",
  "Art/Terrain/wCSO.png",
  "Art/Terrain/wOOO.png",
  "Art/Terrain/wSSS.png",
  "Art/Terrain/xdgc.png",
  "Art/Terrain/xdgp.png",
  "Art/Terrain/xdpc.png",
  "Art/Terrain/xggc.png",
  "Art/Terrain/xhills.png",
  "Art/Terrain/xpgc.png",
  "Art/Terrain/xtgc.png",
  "Art/Terrain/craters.png",
  "Art/Terrain/pollution.png",
  "Art/Cities/rMIDEAST.png",
  "Art/Cities/MIDEASTWALL.png",
  "Art/Cities/DESTROY.png",
  "Art/city screen/CityIcons.png",
  "Art/city screen/ProdButton.png",
  "Art/city screen/background.png",
  "Art/city screen/cityMgmtButtons.png",
  "Art/resources.png",
  "Art/WorldSetup/CLIMTEMPAGEDepress.png",
  "Art/WorldSetup/CLIMTEMPAGERollovers.png",
  "Art/WorldSetup/age.png",
  "Art/WorldSetup/background.png",
  "Art/WorldSetup/climate.png",
  "Art/WorldSetup/landmassWaterSMALL.png",
  "Art/WorldSetup/landmassWaterSMALLdepress.png",
  "Art/WorldSetup/landmassWaterSMALLrollovers.png",
  "Art/WorldSetup/landmassWaterlarge.png",
  "Art/WorldSetup/temperature.png",
  "Art/interface/NormButtons.png",
  "Art/interface/rolloverbuttons.png",
  "Art/interface/highlightedbuttons.png",
  "Art/interface/box left color.png",
  "Art/interface/box right color.png",
  "Art/interface/nextturn states color.png",
  "Art/interface/consoleButtons.png",
  "Art/SmallHeads/popupDOMESTIC.png",
  "Art/SmallHeads/popupTRADE.png",
  "Art/SmallHeads/popupMILITARY.png",
  "Art/SmallHeads/popupFOREIGN.png",
  "Art/SmallHeads/popupCULTURE.png",
  "Art/SmallHeads/popupSCIENCE.png",
  "Art/SmallHeads/advisor_tab.png",
  "Art/Advisors/domestic_icons_aux.png",
  "Art/Advisors/domesticBUTTON.png",
  "Art/Cities/city icons.png",
  "Art/popupborders.png",
  "Art/interface/menuButtons.png",
  "Art/Advisors/dialogbox.png",
  "Art/Advisors/domestic.png",
  "Art/Advisors/domestic_plusminus.png",
  "Art/Tech Chooser/scienceNAV.png",
  "Art/Credits/credits_background.png",
  "Art/city screen/ProductionQueueBox.png",
  "Art/Advisors/non_required.png",
  "Art/Advisors/techboxes.png",
  "Art/Advisors/military.png",
  "Art/Advisors/militaryNAV.png",
  "Art/interface/MovementLED.png",
  "Art/Advisors/science_ancient.png",
  "Art/Advisors/science_middle.png",
  "Art/Advisors/science_industrial_new.png",
  "Art/Advisors/science_modern.png",
  "Art/exitBox-backgroundStates.png",
  "Art/PlayerSetup/playerSetup.png",
  "Art/Diplomacy/talk_offer.png",
  "Art/Diplomacy/counter.png",
  "Art/PalaceView/bkgr.png",
  "Art/city screen/luxuryicons_small.png",
  "Art/Terrain/FogOfWar.png",
  "Art/Terrain/Territory.png",
  "Art/Units/units_32.png",
  "Art/city screen/buildings-small.png",
  "Art/city screen/buildings-large.png",
  "Art/Cursor.png",
  "Art/interface/box trans color.png",
  "Art/SpaceShip/SHIPbackground.png",
  "Art/Advisors/trade.png",
  "Art/Advisors/foreign.png",
  "Art/Advisors/foreignTAB.png",
  "Art/Advisors/culture.png",
  "Art/Advisors/demographics.png",
  "Art/Advisors/wonders_background.png",
  "Art/Advisors/domestic_plusminus.png",
  "Art/histograph-win5_final.png",
  "Art/histograph-top5_final.png",
  "Art/3checkboxes-USE.png"
}

--- For ease of editing, we define the civ colors as hex codes, not 1x1 px images
local civ_colors = {
  "F0F8FF", -- Alice Blue (whiteish)
  "E6194B", -- Red
  "F58231", -- Orange
  "FFE119", -- Yellow
  "3CB44B", -- Green
  "4363D8", -- Blue
  "000075", -- Navy
  "FABED4", -- Pink
  "911EB4", -- Purple
  "9A6324", -- Brown
  "AAFFC3", -- Mint
  "42D4F4", -- Cyan
  "F032E6", -- Magenta
  "808000", -- Olive
  "DCBEFF", -- Lavender
  "A9A9A9", -- Grey
  "008080", -- Teal
  "FFD700", -- Gold
  "800000", -- Maroon
  "00FF00", -- Lime
  "FFC0CB", -- Hot Pink
  "4682B4", -- Steel Blue
  "D2B48C", -- Tan
  "FF7F50", -- Coral
  "6A5ACD", -- Slate Blue
  "2E8B57", -- Sea Green
  "DAA520", -- Goldenrod
  "C71585", -- Medium Violet Red
  "556B2F", -- Dark Olive Green
  "8B4513", -- Saddle Brown
  "B0C4DE", -- Light Steel Blue
  "696969"  -- Dim Gray
}

-- The icon files of the advances and buildings of the bundled rules
-- (C7/ModernArt, made by ArtGen/civilopedia_icons/ui_icons.py from their
-- Civilopedia icons), by Civilopedia entry.
local icon_files = require "icon_files"

-- The civilizations that have a leader head of our own, by name (see
-- leader_heads below).
local leader_head_civs = {}
for _, name in ipairs({
  "America", "Arabia", "Aztecs", "Babylon", "Byzantines", "Carthage", "Celts", "China",
  "Egypt", "England", "France", "Germany", "Greece", "Hittites", "Inca", "India",
  "Iroquois", "Japan", "Korea", "Maya", "Mongols", "Netherlands", "Ottomans", "Persia",
  "Portugal", "Rome", "Russia", "Scandinavia", "Spain", "Sumeria", "Zululand",
}) do
  leader_head_civs[name] = true
end

-- Build lookup table from c7_texture_list without extensions
local lookup = {}
for _, path in ipairs(c7_texture_list) do
  lookup[utils.strip_extension(path)] = path
end

local function path_transformer(path)
  local stripped = utils.strip_extension(path)
  return lookup[stripped] or path
end


--[[
  This function returns a table with texture definitions for "modern" graphics.

  It produces this table by copying the config of the civ3 assets and
  replacing the paths to the original Civilization 3 PCX textures with
  modern PNG versions.
--]]
return function(civ3_textures)
  local c7_textures = utils.transform_paths(civ3_textures, path_transformer)

  -- TODO: Add proper replacement for the asset
  c7_textures.terrain.river_delta = "Art/Terrain/mtnRivers.png"

  -- The replacement map yield icons fill more of their cells than Civ3's.
  c7_textures.icons.map_shield.crop_region = { 411, 9, 15, 18 }
  c7_textures.icons.map_commerce.crop_region = { 438, 5, 26, 24 }
  c7_textures.icons.map_food.crop_region = { 470, 7, 21, 21 }

  c7_textures.civ_colors = {}
  for i, hex_color in ipairs(civ_colors) do
    c7_textures.civ_colors["color_" .. (i-1)] = { path = "", hex_color = hex_color }
  end

  c7_textures.animations.cursor = {
    path = "Art/Animations/Cursor.png",
    animation_rows = 2,
    animation_cols = 9,
    frame_duration = 0.6,
  }
  c7_textures.animations.disorder = {
    path = "Art/Animations/DisorderDefault.png",
    animation_rows = 1,
    animation_cols = 6,
    frame_duration = 0.6,
  }

  -- The standalone asset pack only has placeholders for Civ3's advance and
  -- building icons, which showed the words "Tech" and "Bldg" in the tech
  -- tree and the production menus (owner's report, 2026-10-12). Advances
  -- and buildings of the bundled rules show their own icons instead, the
  -- same pictures as their Civilopedia icons; any others (from an addon)
  -- still show the placeholders.
  function c7_textures.tech_icons.small:map_object_to_sprite(tech)
    local path = icon_files.advances[tech.CivilopediaEntry]
    return { path = path or "Art/Tech Chooser/Icons/placeholder.png" }
  end

  -- A building's entry, or for Wealth (an Inflow) the Civilopedia's.
  local function building_entry(building)
    if building:GetType().Name == "Inflow" then
      return "BLDG_" .. building.name
    end
    return building.civilopediaEntry
  end
  local function use_own_icons(size)
    local icons = c7_textures.building_icons[size]
    local placeholder = icons.map_object_to_sprite
    function icons:map_object_to_sprite(building)
      local files = icon_files.buildings[building_entry(building)]
      if files then
        return { path = files[size] }
      end
      return placeholder(self, building)
    end
  end
  use_own_icons("small")
  use_own_icons("large")

  -- Each civilization of the bundled rules has its own leader head
  -- (C7/ModernArt/Art/Leaderheads/<civ name>.png, made by
  -- ArtGen/leaderheads), whose 2x picture ModernGraphics loads at Civ3's
  -- 115x115. There's one per leader, the same in every era and mood. Any
  -- other civilization shows the placeholder.
  function c7_textures.leader_heads:map_object_to_sprite(player_or_civ)
    local civ = player_or_civ
    if player_or_civ:GetType().Name == "Player" then
      civ = player_or_civ.civilization
    end
    if civ and leader_head_civs[civ.name] then
      return { path = "Art/Leaderheads/" .. civ.name .. ".png" }
    end
    return {
      path = "Art/Advisors/placeholder_leaderhead.png",
    }
  end

  -- There's no replacement for Civ3's barracks, harbor and airport icons, so the building's own icon is shown.
  local function building_icon(_, building)
    return c7_textures.building_icons.small:map_object_to_sprite(building)
  end
  c7_textures.city_building_icons.barracks.map_object_to_sprite = building_icon
  c7_textures.city_building_icons.harbor.map_object_to_sprite = building_icon
  c7_textures.city_building_icons.airport.map_object_to_sprite = building_icon

  function c7_textures.borders:find_column(_)
    return 0
  end

  -- Instead of a place in Civ3's units_32.pcx, each unit art has its own
  -- thumbnail next to its sheets (C7/ModernArt/Art/Units/<art>/<art>_32.png,
  -- made by ArtGen/units/thumbnails.py), which ModernGraphics loads. It's the
  -- thumbnail of the art the player's unit is drawn with in their era, so
  -- it changes with the era like the unit does.
  -- Context - ItemContext (UnitPrototype proto, Player player)
  function c7_textures.unit_icons:map_object_to_sprite(context)
    local main_art = context.proto.art.mainArt
    local art = main_art.defaultName
    local variations = main_art.variations
    local era = context.player.eraCivilopediaName
    if variations and era and variations[era] then
      art = variations[era]
    end
    return { path = "Art/Units/" .. art .. "/" .. art .. "_32.png" }
  end

  return c7_textures
end
