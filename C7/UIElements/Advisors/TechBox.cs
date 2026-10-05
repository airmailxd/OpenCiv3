using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Godot;
using static C7GameData.EraUtils;

public partial class TechBox : TextureButton {
	private Tech tech;
	private TechState techState;

	private List<Building> Buildings;
	private List<Building> ObsoleteBuildings;
	private List<Terraform> Terraforms;
	private List<UnitPrototype> Units;

	// Worked out by whoever draws the tree, once for all its boxes, or by the
	// box itself if not.
	private int? estimatedTurns;
	private TechEffectLookup effects;

	// The theme for the tooltip, the same for every box.
	private static Theme tooltipTheme;

	// Whether the tech is from a later era than the player's.
	private bool isTechEraBeyondPlayerEra;

	private static readonly Dictionary<string, string> TruncatedToFullTechTextMap = new();
	private static readonly Dictionary<string, ImageTexture> CachedObsoleteBuildingTextures = new();

	private FontFile smallFont = new();
	private Theme smallFontTheme = new();
	private int smallFontSize = 11;

	private Label techNameLabel;

	private int queueNumber;

	public enum TechState {
		// This tech is known to the player.
		kKnown,
		// The player is actively researching this tech.
		kInProgress,
		// The player could research this tech.
		kPossible,
		// The player needs to research the prerequisites before this tech can
		// be researched.
		kBlocked,
		// The player has queued this tech for research.
		kQueued,
	}

	public TechBox(Tech tech, TechState techState, int queueNumber = 0) {
		this.tech = tech;
		this.techState = techState;
		this.queueNumber = queueNumber;
	}

	// estimatedTurns only matters for techs in progress or possible to
	// research, and effects is the lookup for the current game.
	public TechBox(Tech tech, TechState techState, int queueNumber, int? estimatedTurns, TechEffectLookup effects)
		: this(tech, techState, queueNumber) {
		this.estimatedTurns = estimatedTurns;
		this.effects = effects;
	}

	// The box textures for each state, at this box's size and era.
	private ImageTexture knownTechBox;
	private ImageTexture inProgressTechBox;
	private ImageTexture possibleTechBox;
	private ImageTexture blockedTechBox;

	// How many characters of the name fit in the box.
	private int charLimitOfCurrentBox;

	public Tech Tech => tech;

	public override void _Ready() {
		GameData gameData = EngineStorage.gameData;
		Player player = gameData.GetUIControllerPlayer();
		effects ??= TechEffectLookup.For(gameData);

		int techBoxSizeCost = CalculateTechBoxSizeCost(player);
		string techBoxSize = CostToStringKey(techBoxSizeCost);
		string era = CalculateTechEraTexture(tech.EraCivilopediaName);

		knownTechBox = TextureLoader.Load($"tech_boxes.known.{era}.{techBoxSize}");
		inProgressTechBox = TextureLoader.Load($"tech_boxes.in_progress.{era}.{techBoxSize}");
		possibleTechBox = TextureLoader.Load($"tech_boxes.possible.{era}.{techBoxSize}");
		blockedTechBox = TextureLoader.Load($"tech_boxes.blocked.{era}.{techBoxSize}");

		// The boxes for every state are the same size.
		TextureNormal = knownTechBox;

		ImageTexture techIconTexture = TextureLoader.Load("tech_icons.small", tech, useCache: true);
		TextureRect icon = new() { Texture = techIconTexture };
		icon.SetPosition(new Vector2(12, 32));
		AddChild(icon);

		// Figure out how many characters can fit in the tech box's width.
		// We use this for truncation when creating the label below.
		//
		// We could be calculating this every time based on the font size,
		// but I don't think it's worth the computation cost
		// a larger char length, means a smaller Tech name (more chars will be truncated)
		float averageCharLength = 6.0f;
		int boxBorderWidth = 16;
		charLimitOfCurrentBox = (int)((TextureNormal.GetWidth() - boxBorderWidth) / averageCharLength);

		smallFontTheme.SetFontSize("font_size", "Label", smallFontSize);

		techNameLabel = new() {
			OffsetLeft = 12,
			OffsetTop = 13,
			Theme = smallFontTheme,
		};

		this.MouseEntered += ShowTooltip;
		this.MouseExited += UpdateLabelTheme;

		int offsetX = techIconTexture.GetWidth() + 7;
		int offsetY = 0;
		int counter = 0;

		List<ImageTexture> techEffects = TechEffectTextures();

		for (int i = 0; i < techEffects.Count; i++) {
			TextureRect tr = new() { Texture = techEffects.ElementAt(i), };
			tr.SetPosition(new Vector2(12 + offsetX, 32 + offsetY));
			AddChild(tr);

			if (techBoxSizeCost > 4) {
				if (i == 2) {
					offsetX = 7;
					offsetY += techIconTexture.GetHeight() + 1;
				}
			}

			offsetX += (int)tr.GetSize().X + 1;
		}

		AddChild(techNameLabel);

		if (!tech.RequiredForEraAdvancement) {
			TextureRect notRequired = new() { Texture = TextureLoader.Load("tech_boxes.non_required"), };
			notRequired.SetPosition(new Vector2(TextureNormal.GetWidth() - 20, 0));
			AddChild(notRequired);
		}

		ApplyState(gameData, player);
	}

	// Shows the tech in a new state, e.g. after the player picks what to
	// research, without building the box again.
	public void UpdateState(TechState techState, int queueNumber, int? estimatedTurns) {
		this.techState = techState;
		this.queueNumber = queueNumber;
		this.estimatedTurns = estimatedTurns;
		if (IsNodeReady()) {
			GameData gameData = EngineStorage.gameData;
			ApplyState(gameData, gameData.GetUIControllerPlayer());
		}
	}

	private void ApplyState(GameData gameData, Player player) {
		isTechEraBeyondPlayerEra = GetEraIndex(tech.EraCivilopediaName) > GetEraIndex(player.eraCivilopediaName);

		smallFont = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Regular.ttf");

		if (!tech.RequiredForEraAdvancement)
			smallFont = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Italic.ttf");

		if (tech.id == player.currentlyResearchedTech)
			smallFont = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Bold.ttf");

		smallFontTheme.DefaultFont = smallFont;

		TextureNormal = techState switch {
			TechState.kKnown => knownTechBox,
			TechState.kInProgress => inProgressTechBox,
			TechState.kPossible => possibleTechBox,
			TechState.kBlocked => blockedTechBox,
			TechState.kQueued => inProgressTechBox,
			_ => throw new ArgumentOutOfRangeException("Invalid tech state")
		};

		// Only techs in progress or possible to research show the estimate.
		string estimatedTurnsString = "";
		if (techState is TechState.kInProgress or TechState.kPossible) {
			int turns = estimatedTurns ?? player.EstimateTurnsToResearch(gameData, tech);
			estimatedTurnsString = turns > 50 ? $"(-- turns)" : $"({turns} turns)";
		}

		string techName = tech.Name;
		string prepend = techState is TechState.kInProgress or TechState.kQueued ? $"{queueNumber}." : "";

		if (techState is TechState.kInProgress) {
			techName = $"{prepend} {tech.Name} {estimatedTurnsString}";
		} else if (techState is TechState.kQueued) {
			techName = $"{prepend} {tech.Name}";
		} else if (techState is TechState.kPossible) {
			techName = $"{tech.Name} {estimatedTurnsString}";
		}

		UpdateLabelTheme();

		techNameLabel.Text = TruncateAndCacheName(techName, charLimitOfCurrentBox);
		if (!TruncatedToFullTechTextMap.TryGetValue(tech.Name, out string fullText)) {
			TooltipText = "";
		} else if (!string.IsNullOrEmpty(TooltipText)) {
			TooltipText = fullText;
		}
	}

	private void ShowTooltip() {
		smallFontTheme.SetColor("font_color", "Label", new Color(0.71f, 0.35f, 0.13f));

		if (tooltipTheme == null) {
			tooltipTheme = new Theme();
			tooltipTheme.SetStylebox("panel", "TooltipPanel", TemporaryPopup.PopupTechStyleBox());
			tooltipTheme.SetColor("font_color", "TooltipLabel", Colors.Black);
			tooltipTheme.SetFontSize("font_size", "TooltipLabel", 12);
		}
		this.Theme = tooltipTheme;

		if (TruncatedToFullTechTextMap.TryGetValue(tech.Name, out string fullText))
			this.TooltipText = $"{fullText}";
	}

	private void UpdateLabelTheme() {
		Color color = Colors.Black;

		if (techState is TechState.kKnown)
			color = Colors.MediumBlue;
		else if (techState is TechState.kQueued or TechState.kInProgress)
			color = Colors.RoyalBlue;
		else if (techState is TechState.kPossible)
			color = Colors.DarkOliveGreen;
		else if (isTechEraBeyondPlayerEra)
			color = Colors.DimGray;

		smallFontTheme.SetColor("font_color", "Label", color);
	}

	private string TruncateAndCacheName(string input, int limit) {
		if (input.Length > limit) {
			if (TruncatedToFullTechTextMap.ContainsKey(tech.Name)) {
				TruncatedToFullTechTextMap[tech.Name] = input;
			} else {
				TruncatedToFullTechTextMap.TryAdd(tech.Name, input);
			}
			return $"{input.Trim().Substring(0, limit - 1)}...";
		}
		TruncatedToFullTechTextMap.Remove(tech.Name);
		return input;
	}

	// TODO: When we figure out how to load the data, load the correct textures
	private List<ImageTexture> TechEffectTextures() {
		List<ImageTexture> textures = new ();

		// Units
		foreach (UnitPrototype unit in Units) {
			ImageTexture texture = TextureLoader.LoadByPath(unit.art.pediaArt.small);
			textures.Add(texture);
		}

		// Buildings
		foreach (Building building in Buildings) {
			// TODO : load the correct textures
			// ImageTexture texture = ...
			// textures.Add(texture);
		}

		// Terraforms
		foreach (Terraform terraform in Terraforms) {
			textures.Add(TextureLoader.Load($"{terraform.ButtonTexture}.normal"));
		}

		// Obsolete buildings
		foreach (Building building in ObsoleteBuildings) {
			// if (cachedObsoleteBuildingTextures.TryGetValue(building.name, out ImageTexture texture)) {
			// 	textures.Add(texture);
			// } else {
			// 	// TODO : load the correct textures
			// 	// Image rawImage = ...
			// 	// Image xMarkedImage = DrawXOnImage(rawImage, new Color(1, 0, 0), 1);
			// 	// ImageTexture obsoleteBuilding = ImageTexture.CreateFromImage(xMarkedImage);
			// 	// cachedObsoleteBuildingTextures.TryAdd(building.name, obsoleteBuilding);
			// 	// textures.Add(texture);
			// }
		}

		return textures;
	}

	private int CalculateTechBoxSizeCost(Player player) {
		int cost = 0;

		// List of building that require this tech to be built
		Buildings = effects.BuildingsRequiring(tech);
		cost += Buildings.Count;

		// List of Great Wonders this tech renders obsolete
		ObsoleteBuildings = effects.BuildingsObsoletedBy(tech);
		cost += ObsoleteBuildings.Count;

		List<UnitPrototype> units = new();
		// List of units that require this tech to be built, taking into account that some are unique
		Civilization civilization = player.civilization;
		foreach (UnitPrototype u in effects.UnitsRequiring(tech)) {
			if (u.producibleBy.Contains(civilization)) {
				units.Add(u);
			}
		}

		Units = units;
		cost += units.Count;

		// List of terraform actions that require this tech to be done
		Terraforms = effects.TerraformsRequiring(tech);
		cost += Terraforms.Count;

		return cost;
	}

	private string CostToStringKey(int cost) {
		// at best the large container fits 6 items
		if (cost is > 4 and <= 6) {
			return "large";
		} else if (cost > 3) {
			return "long";
		} else if (cost > 1) {
			return "medium";
		} else {
			return "small";
		}
	}

	private string CalculateTechEraTexture(string techEra) {
		return techEra switch {
			ANCIENT_TIMES_CVLPD => "ancient",
			MIDDLE_AGES_CVLPD => "middle",
			INDUSTRIAL_AGE_CVLPD => "industrial",
			MODERN_ERA_CVLPD => "modern",
			_ => "ancient"
		};
	}

	private Image DrawXOnImage(Image image, Color color, int thickness) {
		// draw line from top left, to bottom right  ╲
		DrawLineOnImage(image, new Vector2I(0, 0), new Vector2I(image.GetSize().X, image.GetSize().Y), color, thickness);
		// draw line from top right, to bottom left  ╱
		DrawLineOnImage(image, new Vector2I(image.GetSize().X, 0), new Vector2I(0, image.GetSize().Y), color, thickness);
		return image;
	}

	private void DrawLineOnImage(Image img, Vector2I start, Vector2I end, Color color, int thickness) {
		Vector2 direction = new Vector2(end.X - start.X, end.Y - start.Y).Normalized();
		float distance = start.DistanceTo(end);

		for (int i = 0; i < distance; i++) {
			Vector2 pos = new Vector2(start.X, start.Y) + direction * i;
			Vector2I point = new Vector2I(Mathf.RoundToInt(pos.X), Mathf.RoundToInt(pos.Y));

			for (int dx = -thickness / 2; dx <= thickness / 2; dx++) {
				for (int dy = -thickness / 2; dy <= thickness / 2; dy++) {
					Vector2I pixel = point + new Vector2I(dx, dy);
					if (pixel.X >= 0 && pixel.X < img.GetWidth() && pixel.Y >= 0 && pixel.Y < img.GetHeight()) {
						img.SetPixel(pixel.X, pixel.Y, color);
					}
				}
			}
		}
	}
}

// What each tech enables or makes obsolete, worked out once per game rather
// than by scanning every building, unit and terraform for each tech box. The
// lists are in game data order and must not be changed.
public class TechEffectLookup {
	private static TechEffectLookup current;

	private readonly GameData gameData;
	private readonly Dictionary<Tech, List<Building>> buildingsRequiring = new();
	private readonly Dictionary<Tech, List<Building>> buildingsObsoletedBy = new();
	private readonly Dictionary<Tech, List<UnitPrototype>> unitsRequiring = new();
	private readonly Dictionary<ID, List<Terraform>> terraformsRequiring = new();

	private static readonly List<Building> noBuildings = new();
	private static readonly List<UnitPrototype> noUnits = new();
	private static readonly List<Terraform> noTerraforms = new();

	// Lets go of the game the lookup was made for.
	public static void ClearCache() {
		current = null;
	}

	public static TechEffectLookup For(GameData gameData) {
		if (current == null || current.gameData != gameData) {
			current = new TechEffectLookup(gameData);
		}
		return current;
	}

	private TechEffectLookup(GameData gameData) {
		this.gameData = gameData;
		foreach (Building b in gameData.Buildings) {
			if (b.requiredTech != null) {
				Add(buildingsRequiring, b.requiredTech, b);
			}
			if (b.renderedObsoleteBy != null) {
				Add(buildingsObsoletedBy, b.renderedObsoleteBy, b);
			}
		}
		foreach (UnitPrototype u in gameData.unitPrototypes) {
			if (u.requiredTech != null) {
				Add(unitsRequiring, u.requiredTech, u);
			}
		}
		foreach (Terraform t in gameData.Terraforms) {
			if (t.RequiredTech is not null) {
				Add(terraformsRequiring, t.RequiredTech, t);
			}
		}
	}

	private static void Add<K, V>(Dictionary<K, List<V>> lookup, K key, V value) {
		if (!lookup.TryGetValue(key, out List<V> list)) {
			list = new();
			lookup[key] = list;
		}
		list.Add(value);
	}

	public List<Building> BuildingsRequiring(Tech tech) => buildingsRequiring.GetValueOrDefault(tech, noBuildings);
	public List<Building> BuildingsObsoletedBy(Tech tech) => buildingsObsoletedBy.GetValueOrDefault(tech, noBuildings);
	public List<UnitPrototype> UnitsRequiring(Tech tech) => unitsRequiring.GetValueOrDefault(tech, noUnits);
	public List<Terraform> TerraformsRequiring(Tech tech) => terraformsRequiring.GetValueOrDefault(tech.id, noTerraforms);
}
