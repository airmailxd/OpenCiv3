-- Base paths
local SOUNDS = "Sounds/"
local MENU = SOUNDS .. "Menu/"

-- Audio definitions
local audio = {}

audio.menu = {
  main_menu_1 = MENU .. "Menu1.mp3"
}

audio.buttons = {
  button_1 = SOUNDS .. "Button1.wav"
}

-- Played as the City View opens (Civ3 FUN_0041c870 plays sound 7 of
-- FUN_00540cf0's list, this one).
audio.city_view = {
  open = SOUNDS .. "City View.wav"
}

audio.popups = {
  advisor = SOUNDS .. "PopupAdvisor.wav",
  console = SOUNDS .. "PopupConsole.wav",
  info = SOUNDS .. "PopupInfo.wav",
  wonder = SOUNDS .. "Wonder.wav",
  era_change = SOUNDS .. "Era Change.wav"
}

return audio
