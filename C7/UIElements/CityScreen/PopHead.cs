using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using C7GameData;

// A utility class for rendering pop heads.
public class PopHead {
	public record struct TextureKey {
		public CityResident cityResident;
		public int eraNum;
	}

	public const int HEAD_SIZE = 48;

	// A head's picture only depends on the citizen type, mood and era, so
	// cache it by those. The cache is held per citizen type, so it goes away
	// with the game the type belongs to.
	private static readonly ConditionalWeakTable<CitizenType, Dictionary<(CityResident.Mood, int), ImageTexture>> cache = new();

	public static ImageTexture GetTexture(CityResident cityResident, int eraNum) {
		if (cityResident.citizenType == null) {
			return LoadTexture(cityResident, eraNum);
		}
		Dictionary<(CityResident.Mood, int), ImageTexture> textures = cache.GetOrCreateValue(cityResident.citizenType);
		var key = (cityResident.mood, eraNum);
		if (!textures.TryGetValue(key, out ImageTexture texture) || !GodotObject.IsInstanceValid(texture)) {
			texture = LoadTexture(cityResident, eraNum);
			textures[key] = texture;
		}
		return texture;
	}

	// Civ3's tooltip look: black text in a light box with a black border.
	private static Theme tooltipTheme;
	public static Theme TooltipTheme {
		get {
			if (tooltipTheme == null) {
				tooltipTheme = new Theme();
				tooltipTheme.SetStylebox("panel", "TooltipPanel", TemporaryPopup.PopupTechStyleBox());
				tooltipTheme.SetColor("font_color", "TooltipLabel", Colors.Black);
				tooltipTheme.SetFontSize("font_size", "TooltipLabel", 13);
			}
			return tooltipTheme;
		}
	}

	// Like "Happy Laborer (Babylonian)", or "Scientist (Babylonian)" for a
	// specialist, whose mood doesn't matter.
	public static string GetTooltip(CityResident cityResident) {
		string name = cityResident.citizenType?.SingularName ?? "Citizen";
		if (cityResident.citizenType == null || cityResident.citizenType.IsDefaultCitizen) {
			name = $"{cityResident.mood} {name}";
		}
		Civilization nation = cityResident.nationality;
		string nationName = string.IsNullOrEmpty(nation?.adjective) ? nation?.name : nation.adjective;
		return string.IsNullOrEmpty(nationName) ? name : $"{name} ({nationName})";
	}

	private static ImageTexture LoadTexture(CityResident cityResident, int eraNum) {
		return TextureLoader.Load("popheads", new TextureKey() { cityResident = cityResident, eraNum = eraNum });
	}
}
