using Godot;
using System;
using Serilog;
using System.Collections.Generic;
using C7GameData;
using C7.Map;
using C7.Textures;
using C7Engine;
using C7Engine.AI;


// Handles the city screen, where citizens can be assigned and other details of
// the city can bee seen.
[Tool]
public partial class CityScreen : Control {
	private ILogger log = LogManager.ForContext<CityScreen>();
	public TileAssignmentLayer tileAssignmentLayer;
	public MapView mapView;
	public List<CitizenType> citizenTypes;
	// The pop heads and their effect icons are kept and reused from one
	// render to the next; those not needed are hidden. Effects go in a layer
	// above the heads.
	private List<TextureButton> popHeads = new();
	private List<TextureRect> popHeadEffects = new();
	private Control popHeadLayer;
	private Control popHeadEffectLayer;
	private int headsUsed = 0;
	private int effectsUsed = 0;
	// For each pop head, the specialist it cycles when pressed, if any.
	private readonly Dictionary<TextureButton, (City city, int residentIndex)> specialistOfHead = new();

	// The rows of the strategic resources, luxuries and buildings lists, also
	// reused and hidden when not needed.
	private readonly List<(VBoxContainer box, TextureRect rect, Label label)> strategicResourceRows = new();
	private readonly List<(HBoxContainer box, Label label, TextureRect rect)> luxuryRows = new();
	private readonly List<Label> buildingLabels = new();
	private bool listsCleared = false;

	private const int POP_HEAD_OFFSET_Y = 433;
	private const string LUXURIES = "luxuries";
	private const string TAXES = "taxes";
	private const string RESEARCH = "research";
	private const string CORRUPTION = "corruption";
	private const string CONSTRUCTION = "construction";

	[Export] private TextureRect background;
	[Export] private VBoxContainer existingBuildings;
	[Export] private Label culturePerTurn;
	[Export] private Label totalCulture;
	[Export] private Label cityName;

	[Export] private TextureButton productionButton;
	[Export] private TextureButton close;
	[Export] private TextureButton previousCity;
	[Export] private TextureButton nextCity;

	[Export] private ProductionMenu productionMenu;

	[Export] private HBoxContainer strategicResources;
	[Export] private VBoxContainer luxuriesContainer;

	[Export] Label productionLabel;
	[Export] Label completeInLabel;
	[Export] Label growthInLabel;
	[Export] Label foodLabel;
	[Export] Label granaryLabel;
	[Export] GridContainer shieldsInBoxContainer;
	[Export] GridContainer foodInBoxContainer;
	[Export] GridContainer foodInGranaryContainer;

	[Export] Control shieldRowContainer;
	[Export] Control foodRowContainer;

	Theme yieldDetailsFontTheme = new();
	FontFile yieldDetailsFont;

	private Label foodDetails;
	private Label productionDetails;
	private Label commerceTaxesDetails;
	private Label commerceScienceDetails;
	private Label commerceHappinessDetails;

	private ImageTexture shieldTexture;
	private ImageTexture emptyShieldTexture;
	private ImageTexture corruptShieldTexture;
	private ImageTexture foodTexture;
	private ImageTexture emptyFoodTexture;
	private ImageTexture noFoodTexture;
	private ImageTexture eatenFoodTexture;

	private ImageTexture smileyFaceTexture;
	private ImageTexture goodShieldTexture;
	private ImageTexture beakerTexture;
	private ImageTexture goodGoldTexture;
	private ImageTexture wastedGoldTexture;

	private Dictionary<string, ImageTexture> effectIcons = new();

	// The shield and food boxes and rows are drawn by one node each, rather
	// than one TextureRect per icon.
	private IconCanvas shieldsInBoxCanvas;
	private IconCanvas foodInBoxCanvas;
	private IconCanvas foodInGranaryCanvas;
	private IconCanvas shieldRowCanvas;
	private IconCanvas foodRowCanvas;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		background.Texture = TextureLoader.Load("city_screen.background");

		// The close button.
		TextureLoader.SetButtonTextures(close, "city_screen.buttons.close");
		close.Pressed += Hide;

		TextureLoader.SetButtonTextures(previousCity, "city_screen.buttons.previous");
		previousCity.Pressed += SwitchToPreviousCity;

		TextureLoader.SetButtonTextures(nextCity, "city_screen.buttons.next");
		nextCity.Pressed += SwitchToNextCity;

		// Load the font we'll use for the details, at a fixed size that doesn't
		// affect other code using the same font.
		yieldDetailsFont = FixedSizeFonts.Get("res://Fonts/NotoSans-Regular.ttf", 20);

		yieldDetailsFontTheme.DefaultFont = yieldDetailsFont;
		yieldDetailsFontTheme.SetColor("font_color", "Label", Colors.Black);
		yieldDetailsFontTheme.SetFontSize("font_size", "Label", 20);

		foodDetails = new Label() {
			OffsetLeft = 290,
			OffsetTop = 567,
			Theme = yieldDetailsFontTheme,
		};
		background.AddChild(foodDetails);

		productionDetails = new Label() {
			OffsetLeft = 290,
			OffsetTop = 520,
			Theme = yieldDetailsFontTheme,
		};
		background.AddChild(productionDetails);

		commerceTaxesDetails = new Label() {
			OffsetLeft = 290,
			OffsetTop = 620,
			Theme = yieldDetailsFontTheme,
		};
		background.AddChild(commerceTaxesDetails);

		commerceScienceDetails = new Label() {
			OffsetLeft = 290,
			OffsetTop = 653,
			Theme = yieldDetailsFontTheme,
		};
		background.AddChild(commerceScienceDetails);

		commerceHappinessDetails = new Label() {
			OffsetLeft = 290,
			OffsetTop = 685,
			Theme = yieldDetailsFontTheme,
		};
		background.AddChild(commerceHappinessDetails);

		TextureLoader.SetButtonTextures(productionButton, "city_screen.buttons.production");
		productionButton.Pressed += () => { this.productionMenu.Visible = !this.productionMenu.Visible; };

		shieldTexture = TextureLoader.Load("icons.good_shield");
		emptyShieldTexture = TextureLoader.Load("icons.empty_shield");
		corruptShieldTexture = TextureLoader.Load("icons.wasted_shield");
		foodTexture = TextureLoader.Load("icons.full_food");
		emptyFoodTexture = TextureLoader.Load("icons.empty_food");
		noFoodTexture = TextureLoader.Load("icons.no_food");
		eatenFoodTexture = TextureLoader.Load("icons.eaten_food");
		// effects
		smileyFaceTexture = TextureLoader.Load("icons.effect_smiley_face");
		goodGoldTexture = TextureLoader.Load("icons.effect_good_gold");
		beakerTexture = TextureLoader.Load("icons.effect_beaker");
		wastedGoldTexture = TextureLoader.Load("icons.effect_wasted_gold");
		goodShieldTexture = TextureLoader.Load("icons.effect_shield");

		effectIcons = new() {
			{LUXURIES,  smileyFaceTexture},
			{TAXES,  goodGoldTexture},
			{RESEARCH,  beakerTexture},
			{CORRUPTION,  wastedGoldTexture},
			{CONSTRUCTION,  goodShieldTexture}
		};

		shieldsInBoxCanvas = AddIconCanvas(shieldsInBoxContainer);
		foodInBoxCanvas = AddIconCanvas(foodInBoxContainer);
		foodInGranaryCanvas = AddIconCanvas(foodInGranaryContainer);
		shieldRowCanvas = AddIconCanvas(shieldRowContainer);
		shieldRowCanvas.SetAnchorsPreset(LayoutPreset.FullRect);
		foodRowCanvas = AddIconCanvas(foodRowContainer);
		foodRowCanvas.SetAnchorsPreset(LayoutPreset.FullRect);

		// Above everything else on the background, as the heads used to be
		// added last.
		popHeadLayer = new Control() { MouseFilter = MouseFilterEnum.Ignore };
		background.AddChild(popHeadLayer);
		popHeadEffectLayer = new Control() { MouseFilter = MouseFilterEnum.Ignore };
		background.AddChild(popHeadEffectLayer);

		RenderShieldBox(shieldCost: 30, shieldsInBox: 15);
		RenderShieldRow(goodShields: 10, corruptShields: 3);
		RenderFoodBox(foodNeededToGrow: 20, foodStored: 10, foodLostPerTurn: 2, hasGranary: true);
		RenderFoodRow(foodEatenPerTurn: 3, foodSurplus: 1);

		Hidden += OnExit;
	}

	public override void _UnhandledInput(InputEvent @event) {
		// Only capture mouse events if we're visible.
		if (!this.Visible) {
			return;
		}

		// If we left clicked on a tile, handle reassigning a citizen accordingly.
		if (@event is InputEventMouseButton eventMouseButton) {
			if (eventMouseButton.ButtonIndex == MouseButton.Left) {
				GetViewport().SetInputAsHandled();
				if (eventMouseButton.IsPressed()) {
					EngineStorage.ReadGameData((GameData gameData) => {
						if (productionMenu != null) {
							productionMenu.Visible = false;
						}

						// The engine moves the citizen, and the city screen redraws
						// when it hears the city changed.
						Tile tile = mapView.tileOnScreenAt(gameData.map, eventMouseButton.Position);
						if (tile != null) {
							new MsgReassignCitizen(tileAssignmentLayer.city, tile).send();
						}
					});
				}
			}
		}
	}

	private void OnShowCityScreen(ParameterWrapper<City> city) {
		EngineStorage.ReadGameData((GameData gameData) => {
			OnShowCityScreenLocked(gameData, city);
		});
	}

	private void OnShowCityScreenLocked(GameData gameData, ParameterWrapper<City> city) {
		this.Show();
		mapView.centerCameraOnTile(city.Value.location.neighbors[TileDirection.SOUTH]);
		tileAssignmentLayer.city = city.Value;
		cityName.Text = city.Value.name;
		RenderPopHeads(city.Value);
		RenderCulture(city.Value);
		RenderFoodDetails(city.Value);
		RenderCommerceDetails(city.Value);
		RenderProductionDetails(gameData, city.Value);
		RenderExistingBuildings(city.Value.GetBuildings());
		RenderStrategicResources(gameData, city.Value);
		RenderLuxuries(gameData, city.Value);
	}

	// Redraws the city screen after the engine changed the city it shows.
	public void RefreshCity(City city) {
		if (!Visible || tileAssignmentLayer.city?.id != city?.id) {
			return;
		}
		EngineStorage.ReadGameData((GameData gameData) => RenderCity(gameData, city));
	}

	// Shows the city again after a LAN client replaced its game data with
	// the host's latest snapshot, or closes the screen if the city is gone.
	public void RefreshAfterGameReplaced() {
		if (!Visible || tileAssignmentLayer.city == null) {
			return;
		}
		EngineStorage.ReadGameData((GameData gameData) => {
			City city = gameData.cities.Find(c => c.id == tileAssignmentLayer.city.id);
			if (city == null || city.owner.id != EngineStorage.uiControllerID) {
				Hide();
				return;
			}
			RenderCity(gameData, city);
		});
	}

	private void RenderCity(GameData gameData, City city) {
		city.RecalculateCitizenMoods(gameData);
		tileAssignmentLayer.city = city;
		cityName.Text = city.name;
		RenderPopHeads(city);
		RenderCulture(city);
		RenderFoodDetails(city);
		RenderCommerceDetails(city);
		RenderProductionDetails(gameData, city);
		RenderExistingBuildings(city.GetBuildings());
		RenderStrategicResources(gameData, city);
		RenderLuxuries(gameData, city);
	}

	private void OnExit() {
		productionMenu.Hide();
		tileAssignmentLayer.city = null;
	}

	// The lists' containers may come with placeholder children from the
	// scene; clear them out once, before the first rows are made.
	private void ClearListPlaceholders() {
		if (listsCleared) {
			return;
		}
		listsCleared = true;
		foreach (Container container in new Container[] { strategicResources, luxuriesContainer, existingBuildings }) {
			foreach (Node child in container.GetChildren()) {
				container.RemoveChild(child);
				child.QueueFree();
			}
		}
	}

	private void RenderStrategicResources(GameData gameData, City city) {
		Dictionary<C7GameData.Resource, int> resourceCounter = city.GetStrategicResources(gameData);
		ClearListPlaceholders();

		int index = 0;
		foreach ((C7GameData.Resource resource, int count) in resourceCounter) {
			if (index == strategicResourceRows.Count) {
				VBoxContainer resourceContainer = new();
				resourceContainer.AddThemeConstantOverride("separation", 0);

				// Shown at 45x45, as if the texture's size were overridden.
				TextureRect resourceRect = new() {
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					CustomMinimumSize = new Vector2(45, 45),
				};

				Label resourceLabel = new() {
					HorizontalAlignment = HorizontalAlignment.Center
				};

				resourceContainer.AddChild(resourceRect);
				resourceContainer.AddChild(resourceLabel);

				strategicResources.AddChild(resourceContainer);
				strategicResourceRows.Add((resourceContainer, resourceRect, resourceLabel));
			}
			var row = strategicResourceRows[index++];
			row.rect.Texture = TextureLoader.Load("resources.large", resource, useCache: true);
			row.label.Text = count.ToString();
			row.box.Show();
		}
		for (int i = index; i < strategicResourceRows.Count; ++i) {
			strategicResourceRows[i].box.Hide();
		}
	}

	private void RenderLuxuries(GameData gameData, City city) {
		Dictionary<C7GameData.Resource, int> resourceCounter = city.GetLuxuries(gameData);
		ClearListPlaceholders();

		int index = 0;
		foreach ((C7GameData.Resource resource, int count) in resourceCounter) {
			if (index == luxuryRows.Count) {
				HBoxContainer resourceContainer = new();
				Label resourceCount = new();
				TextureRect resourceRect = new();

				resourceContainer.AddChild(resourceCount);
				resourceContainer.AddChild(resourceRect);

				luxuriesContainer.AddChild(resourceContainer);
				luxuryRows.Add((resourceContainer, resourceCount, resourceRect));
			}
			var row = luxuryRows[index++];
			row.label.Text = "(" + count.ToString() + ")";
			row.rect.Texture = TextureLoader.Load("resources.small", resource, useCache: true);
			row.box.Show();
		}
		for (int i = index; i < luxuryRows.Count; ++i) {
			luxuryRows[i].box.Hide();
		}
	}

	private void RenderExistingBuildings(List<CityBuilding> buildings) {
		ClearListPlaceholders();

		int index = 0;
		foreach (CityBuilding building in buildings) {
			if (index == buildingLabels.Count) {
				Label newLabel = new();
				existingBuildings.AddChild(newLabel);
				buildingLabels.Add(newLabel);
			}
			Label label = buildingLabels[index++];
			label.Text = building.building.name;
			label.Show();
		}
		for (int i = index; i < buildingLabels.Count; ++i) {
			buildingLabels[i].Hide();
		}
	}

	private void RenderFoodDetails(City city) {
		string growthStr;
		int foodLostPerTurn = 0;
		int foodSurplus = 0;
		int turnsUntilGrowth = city.TurnsUntilGrowth();
		if (turnsUntilGrowth == int.MaxValue) {
			growthStr = "Not growing.";
		} else if (turnsUntilGrowth == int.MinValue) {
			growthStr = "Starving!";
			foodLostPerTurn = Math.Abs(city.FoodGrowthPerTurn());
		} else {
			growthStr = $"Growth in {turnsUntilGrowth} turns.";
			foodSurplus = city.FoodGrowthPerTurn();
		}
		growthInLabel.Text = growthStr;
		foodLabel.Text = $"{city.CurrentFoodYield()} per turn";

		RenderFoodBox(city.FoodNeededToGrow(), city.foodStored, foodLostPerTurn, hasGranary: city.HasGranary());
		RenderFoodRow(city.FoodConsumedPerTurn(), foodSurplus);
	}

	private void RenderFoodRow(int foodEatenPerTurn, int foodSurplus) {
		foodRowCanvas.Clear();
		if (foodEatenPerTurn + foodSurplus <= 0) {
			return;
		}

		int width = (int)foodRowContainer.Size.X;
		int iconWidth = eatenFoodTexture.GetWidth();
		int spacerWidth = foodSurplus > 0 ? 100 : 0;
		int spacePerIcon = (width - spacerWidth) / (foodEatenPerTurn + foodSurplus);

		int xOffset = 0;
		for (int i = 0; i < foodEatenPerTurn; ++i) {
			foodRowCanvas.AddIcon(eatenFoodTexture, new Vector2(xOffset, 0));
			xOffset += Math.Min(spacePerIcon, iconWidth + 10);
		}

		xOffset = width - iconWidth;
		for (int i = 0; i < foodSurplus; ++i) {
			foodRowCanvas.AddIcon(foodTexture, new Vector2(xOffset, 0));
			xOffset -= Math.Min(spacePerIcon, iconWidth + 10);
		}
	}

	private void RenderFoodBox(int foodNeededToGrow, int foodStored, int foodLostPerTurn, bool hasGranary) {
		if (hasGranary && foodStored >= foodNeededToGrow / 2) {
			RenderFoodBoxWithGranary(foodNeededToGrow, foodStored, foodLostPerTurn);
		} else {
			RenderFoodBoxNoGranary(foodNeededToGrow, foodStored, foodLostPerTurn);
		}
	}

	private void RenderFoodBoxNoGranary(int foodNeededToGrow, int foodStored, int foodLostPerTurn) {
		foodInGranaryCanvas.Clear();
		foodInGranaryCanvas.CustomMinimumSize = Vector2.Zero;
		granaryLabel.Visible = false;
		foodInGranaryContainer.Visible = false;

		int width = 120;
		int height = 180;

		foodInBoxContainer.Columns = FoodBoxColumns(foodNeededToGrow);

		int itemsPerColumn = (int)Math.Ceiling((float)foodNeededToGrow / foodInBoxContainer.Columns);
		int iconSize = Math.Min(height / itemsPerColumn, width / foodInBoxContainer.Columns);

		int fullSquares = Math.Max(0, Math.Min(foodNeededToGrow, foodStored) - foodLostPerTurn);
		int lostSquares = Math.Max(0, foodLostPerTurn);
		int nonEmptySquares = fullSquares + lostSquares;
		RenderIconGrid(foodInBoxContainer, foodInBoxCanvas, iconSize,
			(foodTexture, fullSquares),
			(noFoodTexture, lostSquares),
			(emptyFoodTexture, foodNeededToGrow - nonEmptySquares));
	}

	// Hardcode the common 20/40/60 sizes, but support custom rules as well.
	private static int FoodBoxColumns(int foodNeededToGrow) {
		if (foodNeededToGrow == 20) {
			return 2;
		} else if (foodNeededToGrow == 40) {
			return 4;
		} else if (foodNeededToGrow == 60) {
			return 6;
		} else {
			return (int)Math.Ceiling(Math.Sqrt(foodNeededToGrow));
		}
	}

	private void RenderFoodBoxWithGranary(int foodNeededToGrow, int foodStored, int foodLostPerTurn) {
		granaryLabel.Visible = true;
		foodInGranaryContainer.Visible = true;

		int width = 120;
		int height = 80;

		foodInBoxContainer.Columns = FoodBoxColumns(foodNeededToGrow);
		foodInGranaryContainer.Columns = foodInBoxContainer.Columns;

		int itemsPerColumn = (int)Math.Ceiling((float)foodNeededToGrow / foodInBoxContainer.Columns) / 2;
		int iconSize = Math.Min(height / itemsPerColumn, width / foodInBoxContainer.Columns);

		// Start by filling the granary.
		int foodInGranary = foodNeededToGrow / 2;
		if (foodInGranary > foodStored) { throw new Exception($"not enough food {foodInGranary} {foodStored}"); }

		int foodLostInGranaryPerTurn = Math.Max(0, foodLostPerTurn - (foodStored - foodInGranary));

		RenderIconGrid(foodInGranaryContainer, foodInGranaryCanvas, iconSize,
			(foodTexture, foodInGranary - foodLostInGranaryPerTurn),
			(noFoodTexture, foodLostInGranaryPerTurn));

		// Now fill the rest of the box.
		foodStored -= foodInGranary;
		foodLostPerTurn -= foodLostInGranaryPerTurn;

		RenderIconGrid(foodInBoxContainer, foodInBoxCanvas, iconSize,
			(foodTexture, foodStored - foodLostPerTurn),
			(noFoodTexture, foodLostPerTurn),
			(emptyFoodTexture, foodNeededToGrow / 2 - foodStored));
	}

	private static IconCanvas AddIconCanvas(Control container) {
		foreach (Node child in container.GetChildren()) {
			container.RemoveChild(child);
			child.QueueFree();
		}
		IconCanvas canvas = new();
		container.AddChild(canvas);
		return canvas;
	}

	// Lays the icons out on the grid's single canvas exactly as the grid would
	// lay out one TextureRect per icon: each an iconSize square, ignoring the
	// texture's size but keeping its aspect ratio. Runs with a negative count
	// add nothing.
	private static void RenderIconGrid(GridContainer grid, IconCanvas canvas, int iconSize,
			params (Texture2D texture, int count)[] runs) {
		canvas.Clear();
		int columns = grid.Columns;
		int hSeparation = grid.GetThemeConstant("h_separation");
		int vSeparation = grid.GetThemeConstant("v_separation");

		int index = 0;
		foreach (var (texture, count) in runs) {
			Vector2 size = KeepAspectSize(texture, iconSize);
			for (int i = 0; i < count; ++i, ++index) {
				int column = index % columns, row = index / columns;
				canvas.AddIcon(texture, new Rect2(new Vector2(column * (iconSize + hSeparation), row * (iconSize + vSeparation)), size));
			}
		}

		if (index == 0) {
			canvas.CustomMinimumSize = Vector2.Zero;
		} else {
			int usedColumns = Math.Min(index, columns);
			int rows = (index + columns - 1) / columns;
			canvas.CustomMinimumSize = new Vector2(
				usedColumns * iconSize + (usedColumns - 1) * hSeparation,
				rows * iconSize + (rows - 1) * vSeparation);
		}
	}

	// The size TextureRect's KeepAspect stretch mode draws a texture at
	// inside a square of the given size.
	private static Vector2 KeepAspectSize(Texture2D texture, int iconSize) {
		int textureWidth = (int)(texture.GetWidth() * (float)iconSize / texture.GetHeight());
		int textureHeight = iconSize;
		if (textureWidth > iconSize) {
			textureWidth = iconSize;
			textureHeight = texture.GetHeight() * textureWidth / texture.GetWidth();
		}
		return new Vector2(textureWidth, textureHeight);
	}

	private void RenderCommerceDetails(City city) {
		CommerceBreakdown breakdown = city.CurrentCommerceYield();
		commerceTaxesDetails.Text = $"{breakdown.taxes} gold/turn to taxes";
		commerceScienceDetails.Text = $"{breakdown.beakers} gold/turn to science  ({breakdown.corrupted} corrupt)";
		commerceHappinessDetails.Text = $"{breakdown.happiness} gold/turn to happiness";
	}

	private void RenderProductionDetails(GameData gameData, City city) {
		CorruptableValue shields = city.CurrentProductionYield();
		RenderShieldBox(city.owner.ShieldCost(city.itemBeingProduced), city.shieldsStored);
		RenderShieldRow(shields.useful, shields.corrupt);
		productionLabel.Text = $"PRODUCTION: {shields.useful + shields.corrupt} per turn";
		int turnsUntilFinished = city.TurnsUntilProductionFinished();
		completeInLabel.Text = turnsUntilFinished == int.MaxValue ? "--" : $"Complete in {turnsUntilFinished} turns";

		foreach (Node child in productionButton.GetChildren()) {
			child.QueueFree();
		}

		int marginTop = 35;

		if (city.itemBeingProduced is UnitPrototype proto) {
			// A stand-in unit, only for its picture, so it doesn't use up a game ID.
			var unit = proto.GetInstance(ID.None(proto.name), proto, city.owner);
			AnimationManager animationManager = mapView.game.animationController.civ3AnimData.forUnit(unit, MapUnit.AnimatedAction.DEFAULT).animationManager;
			ShaderMaterial material = PlayerTextureUtil.GetShaderMaterialForUnit(city.owner.GetPlayerColor());
			(ImageTexture baseImage, ImageTexture imageTint) = animationManager.GetAnimationFrameAndTintTextures(unit);

			// Add the base sprite.
			Sprite2D baseImageSprite = new();
			baseImageSprite.Texture = baseImage;
			baseImageSprite.Position = new Vector2(productionButton.TextureNormal.GetWidth() / 2.0f, marginTop);
			productionButton.AddChild(baseImageSprite);

			// Add the tint sprite, hooking up the shader.
			Sprite2D imageTintSprite = new Sprite2D();
			imageTintSprite.Texture = imageTint;
			imageTintSprite.Material = material;
			imageTintSprite.Position = baseImageSprite.Position;
			productionButton.AddChild(imageTintSprite);
		} else if (city.itemBeingProduced is Building b) {
			Sprite2D icon = new();
			icon.Texture = TextureLoader.Load("building_icons.large", b, useCache: true);
			icon.Position = new Vector2(productionButton.TextureNormal.GetWidth() / 2.0f, marginTop);
			productionButton.AddChild(icon);
		} else if (city.itemBeingProduced is Inflow inflow) {
			Sprite2D icon = new();
			icon.Texture = TextureLoader.Load("building_icons.large", inflow, useCache: true);
			icon.Position = new Vector2(productionButton.TextureNormal.GetWidth() / 2.0f, marginTop);
			productionButton.AddChild(icon);
		}

		Label productionButtonLabel = new();
		productionButton.AddChild(productionButtonLabel);
		productionButtonLabel.SetPosition(new Vector2(0, 65));
		productionButtonLabel.SetTextAndCenterLabel($"{city.itemBeingProduced.name}");

		productionMenu.AddItems(gameData, city, (IProducible p) => {
			new MsgChooseProduction(city.id, p.name).send();
		});
	}

	private void RenderShieldRow(int goodShields, int corruptShields) {
		shieldRowCanvas.Clear();
		// E.g. a polluted city center and tiles that make no shields.
		if (goodShields + corruptShields <= 0) {
			return;
		}

		int width = (int)shieldRowContainer.Size.X;
		int iconWidth = shieldTexture.GetWidth();
		int spacerWidth = corruptShields > 0 ? 100 : 0;
		int spacePerIcon = (width - spacerWidth) / (goodShields + corruptShields);

		int xOffset = 0;
		for (int i = 0; i < corruptShields; ++i) {
			shieldRowCanvas.AddIcon(corruptShieldTexture, new Vector2(xOffset, 0));
			xOffset += Math.Min(spacePerIcon, iconWidth);
		}

		xOffset = width - iconWidth;
		for (int i = 0; i < goodShields; ++i) {
			shieldRowCanvas.AddIcon(shieldTexture, new Vector2(xOffset, 0));
			xOffset -= Math.Min(spacePerIcon, iconWidth);
		}
	}

	private void RenderShieldBox(int shieldCost, int shieldsInBox) {
		if (shieldCost <= 0) {
			shieldsInBoxCanvas.Clear();
			shieldsInBoxCanvas.CustomMinimumSize = Vector2.Zero;
			return;
		}

		int width = (int)shieldsInBoxContainer.GetParent<CenterContainer>().Size.X;
		int height = (int)shieldsInBoxContainer.GetParent<CenterContainer>().Size.Y;

		// Set the columns before working out how many rows they make.
		shieldsInBoxContainer.Columns = (int)Math.Ceiling(Math.Sqrt(shieldCost));
		int itemsPerColumn = (int)Math.Ceiling((float)shieldCost / shieldsInBoxContainer.Columns);
		int iconSize = Math.Min(height / itemsPerColumn, width / shieldsInBoxContainer.Columns);

		RenderIconGrid(shieldsInBoxContainer, shieldsInBoxCanvas, iconSize,
			(shieldTexture, Math.Min(shieldCost, shieldsInBox)),
			(emptyShieldTexture, shieldCost - shieldsInBox));
	}

	private void RenderCulture(City city) {
		culturePerTurn.Text = $"{city.GetCulturePerTurn()}/turn";

		int nextCultureExpansion = (int)Math.Pow(10, city.GetBorderExpansionLevel());
		totalCulture.Text = $"Total: {city.GetCulture()}/{nextCultureExpansion}";
	}

	private void RenderPopHeads(City city) {
		// Reuse the heads and effects already made, hiding any left over.
		headsUsed = 0;
		effectsUsed = 0;

		int eraNum = city.owner.EraIndex();

		// The pop head textures are 50 x 50, but have a 1px border on all sides
		//
		// The texture file has 16 rows of the default citizen, in groups of 4
		// per era (content, happy, resisting, unhappy). There are 10 columns,
		// the first 5 are male heads of different regions for civs, the other 5
		// are female heads.
		//
		// After the 16 rows of default citizens there is one row per specialist
		// type, and again 10 columns per row. This time they are
		// (ancient, middle, industrial, modern, blank) for male and female heads
		//
		// TODO: handle per-civ regions
		// TODO: handle male/female citizens

		// Start by splitting the default residents from the specialists, since
		// they are spaced apart in the UI.
		List<CityResident> happyResidents =
			city.residents.FindAll(x => x.citizenType.IsDefaultCitizen && x.mood == CityResident.Mood.Happy);
		List<CityResident> contentResidents =
			city.residents.FindAll(x => x.citizenType.IsDefaultCitizen && x.mood == CityResident.Mood.Content);
		List<CityResident> unhappyResidents =
			city.residents.FindAll(x => x.citizenType.IsDefaultCitizen && x.mood == CityResident.Mood.Unhappy);
		List<CityResident> specialists = city.residents.FindAll(x => !x.citizenType.IsDefaultCitizen);

		// Leave a 1 head gap if we have specialists.
		int width = city.residents.Count * PopHead.HEAD_SIZE;
		if (specialists.Count > 0) {
			width += PopHead.HEAD_SIZE;
		}

		// Leave a 1 head gap between each section of moods.
		int numMoodsPresent = (happyResidents.Count > 0 ? 1 : 0)
			+ (contentResidents.Count > 0 ? 1: 0)
			+ (unhappyResidents.Count > 0 ? 1: 0);
		width += (numMoodsPresent - 1) * PopHead.HEAD_SIZE;

		// Track the x position of each head so that we're centered in the screen
		int xPos = background.Texture.GetWidth() / 2 + -width / 2;

		// Add each of the default citizens. These are buttons with the idea that
		// we can eventually support clicking on the heads to view details, such
		// as the reason for unhappiness.
		foreach (CityResident cr in happyResidents) {
			xPos = AddDefaultCitizen(cr, xPos, eraNum);
		}
		if (happyResidents.Count > 0 && (contentResidents.Count > 0 || unhappyResidents.Count > 0)) {
			xPos += PopHead.HEAD_SIZE;
		}
		foreach (CityResident cr in contentResidents) {
			xPos = AddDefaultCitizen(cr, xPos, eraNum);
		}
		if (contentResidents.Count > 0 && unhappyResidents.Count > 0) {
			xPos += PopHead.HEAD_SIZE;
		}
		foreach (CityResident cr in unhappyResidents) {
			xPos = AddDefaultCitizen(cr, xPos, eraNum);
		}

		// Add space before specialists.
		xPos += PopHead.HEAD_SIZE;

		// Add each of the specialists.
		//
		// TODO: Render the specialist effect (like a smiley for entertainers)
		// in the corner of the head.
		foreach (CityResident cr in specialists) {
			TextureButton tb = NextPopHead();
			tb.TextureNormal = PopHead.GetTexture(cr, eraNum);
			tb.SetPosition(new Vector2(xPos, POP_HEAD_OFFSET_Y));

			int residentIndex = city.residents.IndexOf(cr);
			specialistOfHead[tb] = (city, residentIndex);

			float iconOffset = 0.0f;
			foreach (KeyValuePair<string, int> entry in GetSpecialistEffectInfo(cr)) {
				if (!effectIcons.TryGetValue(entry.Key, out ImageTexture texture)) continue;
				for (int i = 0; i < entry.Value; i++) {
					TextureRect icon = NextPopHeadEffect();
					icon.Texture = texture;

					float iconHalfWidth = icon.Texture.GetWidth() / 2.0f;
					float iconXPos = tb.Position.X + iconHalfWidth * i + iconOffset;
					float iconYPos = tb.Position.Y + PopHead.HEAD_SIZE - 10;

					icon.SetPosition(new Vector2(iconXPos, iconYPos));

					// If multiple effects are applicable we need to
					// calculate the total offset that comes before each type.
					// The offset is only calculated on the last iteration
					// because we don't want any extra offset between effects of the same type.
					if (i == entry.Value - 1) {
						iconOffset += iconHalfWidth * entry.Value;
					}
				}
			}

			xPos += PopHead.HEAD_SIZE;
		}

		for (int i = headsUsed; i < popHeads.Count; ++i) {
			popHeads[i].Hide();
			specialistOfHead.Remove(popHeads[i]);
		}
		for (int i = effectsUsed; i < popHeadEffects.Count; ++i) {
			popHeadEffects[i].Hide();
		}
	}

	private TextureButton NextPopHead() {
		if (headsUsed == popHeads.Count) {
			TextureButton head = new();
			// Pressing a specialist cycles it to the next kind.
			head.Pressed += () => {
				if (specialistOfHead.TryGetValue(head, out var specialist)) {
					new MsgCycleSpecialist(specialist.city, specialist.residentIndex).send();
				}
			};
			popHeadLayer.AddChild(head);
			popHeads.Add(head);
		}
		TextureButton tb = popHeads[headsUsed++];
		specialistOfHead.Remove(tb);
		tb.Show();
		return tb;
	}

	private TextureRect NextPopHeadEffect() {
		if (effectsUsed == popHeadEffects.Count) {
			TextureRect effect = new();
			popHeadEffectLayer.AddChild(effect);
			popHeadEffects.Add(effect);
		}
		TextureRect icon = popHeadEffects[effectsUsed++];
		icon.Show();
		return icon;
	}

	private void SwitchToNextCity() {
		EngineStorage.ReadGameData((GameData gameData) => {
			City currentCity = tileAssignmentLayer.city;
			List<City> cities = currentCity.owner.cities;
			City nextCity = cities[(cities.IndexOf(currentCity) + 1) % cities.Count];
			nextCity.RecalculateCitizenMoods(gameData);
			OnShowCityScreenLocked(gameData, new ParameterWrapper<City>(nextCity));
		});
	}

	private void SwitchToPreviousCity() {
		EngineStorage.ReadGameData((GameData gameData) => {
			City currentCity = tileAssignmentLayer.city;
			List<City> cities = currentCity.owner.cities;
			City previousCity = cities[(cities.IndexOf(currentCity) + cities.Count - 1) % cities.Count];
			previousCity.RecalculateCitizenMoods(gameData);
			OnShowCityScreenLocked(gameData, new ParameterWrapper<City>(previousCity));
		});
	}

	private int AddDefaultCitizen(CityResident cr, int xPos, int eraNum) {
		TextureButton tb = NextPopHead();
		tb.TextureNormal = PopHead.GetTexture(cr, eraNum);
		tb.SetPosition(new Vector2(xPos, POP_HEAD_OFFSET_Y));
		return xPos + PopHead.HEAD_SIZE;
	}

	private Dictionary<string, int> GetSpecialistEffectInfo(CityResident cityResident) {
		Dictionary<string, int> specialistEffectInfo = new();
		specialistEffectInfo.TryAdd(LUXURIES, cityResident.citizenType.Luxuries);
		specialistEffectInfo.TryAdd(TAXES, cityResident.citizenType.Taxes);
		specialistEffectInfo.TryAdd(RESEARCH, cityResident.citizenType.Research);
		specialistEffectInfo.TryAdd(CORRUPTION, cityResident.citizenType.Corruption);
		specialistEffectInfo.TryAdd(CONSTRUCTION, cityResident.citizenType.Construction);

		return specialistEffectInfo;
	}
}

// Draws a set of icons with a single node, in place of one TextureRect per
// icon.
public partial class IconCanvas : Control {
	private readonly List<(Texture2D texture, Rect2 rect)> icons = new();

	public IconCanvas() {
		// Like the TextureRects it replaces.
		MouseFilter = MouseFilterEnum.Pass;
	}

	public void Clear() {
		icons.Clear();
		QueueRedraw();
	}

	// Adds an icon drawn at its texture's size.
	public void AddIcon(Texture2D texture, Vector2 position) {
		AddIcon(texture, new Rect2(position, texture.GetSize()));
	}

	public void AddIcon(Texture2D texture, Rect2 rect) {
		icons.Add((texture, rect));
		QueueRedraw();
	}

	public override void _Draw() {
		foreach (var (texture, rect) in icons) {
			DrawTextureRect(texture, rect, false);
		}
	}
}
