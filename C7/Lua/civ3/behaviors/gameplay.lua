-- A generic lua script for orchestrating behaviours during gameplay.
-- The idea for this is to host script paths that are not serialized in the json,
-- but rather "hardcoded" in the code

local time_unit = ENUMS.TimeUnit

local function game_data()
  return GAME_DATA()
end

local function rules()
  return GAME_DATA().rules
end

local gameplay = {}

local function disband_reward(context)
  local unit = context
  local unit_shield_cost = unit.unitType.shieldCost;

  -- Only a unit disbanded inside one of its owner's cities gives shields.
  local city = unit.location.cityAtTile
  if city ~= nil and city.owner == unit.owner then
    local item_produced = city.itemBeingProduced
    if item_produced == nil then
      return
    end

    -- GetType() isn't reachable from Lua, so tell the kinds apart by their
    -- members: only Buildings have IsGreatWonder, only Inflows localYield.
    -- Reading a member a type lacks raises an error, hence pcall.
    local ok, is_wonder = pcall(function() return item_produced.IsGreatWonder() end)
    if ok and is_wonder == true then
      return
    end

    local is_inflow = pcall(function() return item_produced.localYield end)
    if is_inflow then
      return
    end

    local reward = math.floor(unit_shield_cost * rules().ShieldRateForDisbanding)
    game_data().SendMessageToUiFromLua("Our " .. unit.name .. " has been converted to " .. reward .. " shields.",
      unit.location)
    city.SetStoredShields(reward, true)
  end
end

-- Splits a count of time units since the start of a calendar into the
-- whole periods (years, say) that passed and the 1-based unit within the
-- current period: unit 1 is the first unit of the first period. Integer
-- arithmetic only, so the year never comes out fractional.
local function split_time(units_since_start, units_per_period)
  local periods = math.floor(units_since_start / units_per_period)
  local unit = math.floor(units_since_start - periods * units_per_period) + 1
  return periods, unit
end

local function era_label(year)
  local options = game_data().timeOptions
  return year < 0 and options.negativeLabel or options.positiveLabel
end

-- The date to show for a raw time (see TimeOptions.GetRawNumber, which
-- counts from the start month, week, day or hour). It only reads the game
-- data, so showing a date never changes anything.
--
-- Civ3 counts a scenario in years, months or weeks, and its start date is
-- the date of turn 0
-- (https://forums.civfanatics.com/threads/time-options-time-scale.456771/),
-- which is what this shows for raw time 1 of a month or week calendar.
-- UNVERIFIED (no Civ3 source found): how Civ3 writes month and week dates
-- ("Jan, 1942 AD", "Week 1, 1942 AD"), and that its year has 52 weeks.
-- Days and hours are C7's own; Civ3 has no such time units.
local function get_display_time_text(raw_time)
  local options = game_data().timeOptions
  local base_unit = options.baseUnit

  -- Years
  if base_unit == time_unit.Years then
    return math.abs(raw_time) .. " " .. era_label(raw_time)
  end

  -- Months, weeks and days count from the first of the year: raw time 1 is
  -- January (or the first week or day) of the start year.
  if base_unit == time_unit.Months then
    local years, month = split_time(raw_time - 1, 12)
    local year = options.startYear + years
    return options.GetAbbrMonthNameByIndex(month) .. ", " .. math.abs(year) .. " " .. era_label(year)
  end

  if base_unit == time_unit.Weeks then
    local years, week = split_time(raw_time - 1, 52)
    local year = options.startYear + years
    return "Week " .. week .. ", " .. math.abs(year) .. " " .. era_label(year)
  end

  if base_unit == time_unit.Days then
    local years, day = split_time(raw_time - 1, 365)
    local year = options.startYear + years
    return "Day " .. day .. ", " .. math.abs(year) .. " " .. era_label(year)
  end

  -- Hours count from midnight of the start day: raw time 0 is hour 0.
  if base_unit == time_unit.Hours then
    local days, hour = split_time(raw_time, 24)
    return "Hour " .. (hour - 1) .. ", Day " .. (options.startDay + days)
  end

  return tostring(raw_time)
end

gameplay = {
  units = {
    -- Context = MapUnit mapUnit
    disband = function(context)
      disband_reward(context)
    end,
  },
  time = {
    -- Context = int time
    display_text = function(context)
      return get_display_time_text(context)
    end,
  }
}

return gameplay
