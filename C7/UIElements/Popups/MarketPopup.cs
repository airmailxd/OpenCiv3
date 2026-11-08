using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Godot;

// The Mercenary Market (see C7Engine.Staging): the units on offer, each with
// a box for the player's bid and a list of the cities it may go to if they
// win it, their capital to begin with; a map of their cities with the ones
// chosen picked out; and a countdown. The bids go in when the player is done
// or time runs out; until then they can't add up to more than the player's
// gold. No one sees anyone else's bids.
public partial class MarketPopup : Popup {
	private const int Width = 820;
	private const int RowHeight = 44;
	private const int IconSize = 36;
	private const int RowsTop = 100;
	private const int MapHeight = 230;
	private const int CityListWidth = 210;

	private readonly Player player;
	private readonly List<string> offers;
	private readonly int gold;
	private readonly List<City> cities;
	private readonly Action<List<int>, List<City>> bid;
	private readonly List<SpinBox> boxes = new();
	private readonly List<OptionButton> cityLists = new();
	private MarketMap map;
	private Label total;
	private Label clock;
	private double secondsLeft;
	private bool sent;

	public MarketPopup(Player player, List<string> offers, int gold, int seconds, List<City> cities, Action<List<int>, List<City>> bid) {
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins(top: 60);
		this.player = player;
		this.offers = offers;
		this.gold = gold;
		this.cities = cities ?? new();
		this.bid = bid;
		secondsLeft = seconds;
	}

	private int MapTop => RowsTop + offers.Count * RowHeight + 8;
	private int Height => MapTop + MapHeight + 64;

	// The player may look around the map while bidding.
	public override bool LetsMapMove => true;

	// The market closes only when the player is done or time runs out.
	public override bool OnEscape() {
		return false;
	}

	public override void _Ready() {
		base._Ready();
		AddTexture(Width, Height);
		AddBackground(Width, Height);
		AddHeader("Mercenary Market", 10);

		Label message = new() {
			Text = $"{AdvisorUtils.CivTitle(player)}: mercenaries offer their swords. Bids are secret; the highest bid for each wins it. "
				+ "Choose the city each will report to if we win it.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(Width - 50, 0),
		};
		message.SetPosition(new Vector2(25, 48));
		AddChild(message);

		for (int i = 0; i < offers.Count; i++) {
			UnitPrototype unit = EngineStorage.gameData?.unitPrototypes.Find(p => p.name == offers[i]);
			HBoxContainer row = new() { CustomMinimumSize = new Vector2(Width - 50, RowHeight) };
			row.SetPosition(new Vector2(25, RowsTop + i * RowHeight));
			row.AddThemeConstantOverride("separation", 10);
			if (unit != null) {
				row.AddChild(new TextureRect {
					Texture = TextureLoader.Load("unit_icons", new ItemContext(unit, player), useCache: true),
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					CustomMinimumSize = new Vector2(IconSize, IconSize),
					SizeFlagsVertical = SizeFlags.ShrinkCenter,
				});
			}
			string stats = unit == null ? "" : $"  {unit.attack}/{unit.defense}/{unit.movement}";
			row.AddChild(new Label {
				Text = offers[i] + stats,
				VerticalAlignment = VerticalAlignment.Center,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				ClipText = true,
			});
			row.AddChild(new Label { Text = "Bid:", VerticalAlignment = VerticalAlignment.Center });
			SpinBox box = new() {
				MinValue = 0,
				MaxValue = gold,
				Step = 1,
				Rounded = true,
				Suffix = "gold",
				CustomMinimumSize = new Vector2(130, 0),
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
			};
			box.ValueChanged += _ => KeepWithinGold(box);
			boxes.Add(box);
			row.AddChild(box);
			row.AddChild(new Label { Text = "to", VerticalAlignment = VerticalAlignment.Center });
			OptionButton list = CityList(unit);
			cityLists.Add(list);
			row.AddChild(list);
			AddChild(row);
		}

		map = new MarketMap(player, cities) {
			Position = new Vector2(25, MapTop),
			Size = new Vector2(Width - 50, MapHeight),
			CustomMinimumSize = new Vector2(Width - 50, MapHeight),
		};
		AddChild(map);
		ShowChosen();

		total = new Label { CustomMinimumSize = new Vector2(260, 0) };
		total.SetPosition(new Vector2(25, Height - 46));
		AddChild(total);
		clock = new Label { HorizontalAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(200, 0) };
		clock.SetPosition(new Vector2(Width - 380, Height - 46));
		AddChild(clock);
		Civ3MenuButton done = new() { Text = "Done", FontSize = 14 };
		done.SetPosition(new Vector2(Width - 150, Height - 54));
		done.Pressed += Send;
		AddChild(done);
		ShowTotals();
	}

	// The cities the unit may go to, the capital first and chosen. A ship
	// sent to an inland city puts to sea at the nearest coast, which the
	// list says.
	private OptionButton CityList(UnitPrototype unit) {
		bool ship = unit != null && unit.IsSeaUnit();
		OptionButton list = new() {
			CustomMinimumSize = new Vector2(CityListWidth, 0),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			ClipText = true,
			FitToLongestItem = false,
		};
		for (int i = 0; i < cities.Count; i++) {
			City city = cities[i];
			string note = city.IsCapital() ? " (capital)" : "";
			if (ship && !city.location.NeighborsOcean()) {
				note += " (inland)";
			}
			list.AddItem(city.name + note, i);
		}
		if (cities.Count > 0) {
			list.Select(0);
		}
		if (ship) {
			list.TooltipText = "Sent to an inland city, it will put to sea at the nearest coast.";
		}
		list.ItemSelected += _ => ShowChosen();
		return list;
	}

	// The city chosen for each offer, in order.
	private List<City> Chosen() {
		return cityLists.Select(l => l.Selected >= 0 && l.Selected < cities.Count ? cities[l.Selected] : null).ToList();
	}

	private void ShowChosen() {
		map?.SetChosen(Chosen().Where(c => c != null));
	}

	// The bids together can't come to more than the player's gold: the one
	// just changed is held down to what's left.
	private void KeepWithinGold(SpinBox changed) {
		int others = boxes.Where(b => b != changed).Sum(b => (int)b.Value);
		if (others + (int)changed.Value > gold) {
			changed.SetValueNoSignal(Math.Max(0, gold - others));
		}
		ShowTotals();
	}

	private void ShowTotals() {
		int bids = boxes.Sum(b => (int)b.Value);
		total.Text = $"Bidding {bids} of {gold} gold";
		clock.Text = $"Closes in {Math.Max(0, (int)Math.Ceiling(secondsLeft))}s";
	}

	public override void _Process(double delta) {
		if (sent) {
			return;
		}
		secondsLeft -= delta;
		ShowTotals();
		if (secondsLeft <= 0) {
			Send();
		}
	}

	private void Send() {
		if (sent) {
			return;
		}
		sent = true;
		// A value typed but not yet entered still counts.
		foreach (SpinBox box in boxes) {
			box.Apply();
		}
		List<int> amounts = boxes.Select(b => (int)b.Value).ToList();
		List<City> chosen = Chosen();
		GetParent<PopupOverlay>().OnHidePopup();
		bid(amounts, chosen);
	}
}

// The Mercenary Market's map: the land around the player's cities as the
// minimap colours it, with each city named, the ones chosen for a unit in
// gold. It shows only what the player knows.
public partial class MarketMap : Control {
	private static readonly Color Unknown = new(0.05f, 0.05f, 0.05f);
	private static readonly Color Border = new(0.12f, 0.09f, 0.04f);
	private static readonly Color CityColor = Colors.White;
	private static readonly Color ChosenColor = new(1f, 0.82f, 0.2f);
	private const int FontSize = 13;
	// How far past the outermost cities the map shows, in half tiles.
	private const float Pad = 10;

	private readonly Player player;
	private readonly List<City> cities;
	private HashSet<City> chosen = new();
	private ImageTexture texture;
	// The part of the map shown, in half tiles (see MiniMap).
	private float x0, y0, spanX, spanY;

	public MarketMap(Player player, List<City> cities) {
		this.player = player;
		this.cities = cities;
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public void SetChosen(IEnumerable<City> cities) {
		chosen = cities.ToHashSet();
		QueueRedraw();
	}

	public override void _Ready() {
		GameData gameData = EngineStorage.gameData;
		if (gameData?.map == null || cities.Count == 0) {
			return;
		}
		int width = Math.Max(1, (int)Size.X), height = Math.Max(1, (int)Size.Y);
		FrameCities(gameData.map, (float)width / height);
		texture = ImageTexture.CreateFromImage(Paint(gameData, width, height));
	}

	// Picks the part of the map to show: the cities with some land around
	// them, widened to the map's shape on screen, where a half tile across
	// is twice as wide as one down. On a map that wraps, the cities are
	// framed across the widest stretch without one.
	private void FrameCities(GameMap map, float aspect) {
		int mapWidth = map.numTilesWide, mapHeight = map.numTilesTall;
		List<float> xs = cities.Select(c => (float)c.location.XCoordinate + 1).OrderBy(x => x).ToList();
		List<float> ys = cities.Select(c => (float)c.location.YCoordinate + 1).ToList();
		float left = xs.First(), right = xs.Last();
		if (map.wrapHorizontally && xs.Count > 1) {
			float widest = xs.First() + mapWidth - xs.Last();
			for (int i = 0; i + 1 < xs.Count; i++) {
				if (xs[i + 1] - xs[i] > widest) {
					widest = xs[i + 1] - xs[i];
					left = xs[i + 1];
					right = xs[i] + mapWidth;
				}
			}
		}
		x0 = left - Pad;
		spanX = right - left + 2 * Pad;
		y0 = ys.Min() - Pad;
		spanY = ys.Max() - ys.Min() + 2 * Pad;

		// Widen it to fit the box.
		if (2 * spanX / spanY < aspect) {
			float wider = aspect * spanY / 2;
			x0 -= (wider - spanX) / 2;
			spanX = wider;
		} else {
			float taller = 2 * spanX / aspect;
			y0 -= (taller - spanY) / 2;
			spanY = taller;
		}
		// Keep it on a map that doesn't wrap, where it fits.
		if (!map.wrapHorizontally && spanX <= mapWidth) {
			x0 = Math.Clamp(x0, 0, mapWidth - spanX);
		}
		if (!map.wrapVertically && spanY <= mapHeight) {
			y0 = Math.Clamp(y0, 0, mapHeight - spanY);
		}
	}

	// The map as the minimap colours it, without the cities.
	private Image Paint(GameData gameData, int width, int height) {
		GameMap map = gameData.map;
		List<MiniMapLayer> layers = [new BaseLandMiniLayer(), new TerrainMiniLayer(), new PlayerColorMiniLayer()];
		foreach (MiniMapLayer layer in layers) {
			layer.Configure(gameData);
		}
		TileKnowledge knowledge = player.tileKnowledge;
		Dictionary<Tile, uint> colors = new();
		byte[] pixels = new byte[width * height * 4];
		byte[] tileColor = new byte[4];
		for (int py = 0; py < height; py++) {
			float my = y0 + (py + 0.5f) / height * spanY;
			for (int px = 0; px < width; px++) {
				float mx = x0 + (px + 0.5f) / width * spanX;
				Tile tile = MiniMap.TileShownAt(map, mx, my);
				uint color;
				if (tile == null || tile == Tile.NONE || !knowledge.knownTiles.Contains(tile)) {
					color = Pack(Unknown);
				} else if (!colors.TryGetValue(tile, out color)) {
					Array.Clear(tileColor);
					foreach (MiniMapLayer layer in layers) {
						layer.DrawTile(tileColor, 0, tile);
					}
					tileColor[3] = 255;
					color = BitConverter.ToUInt32(tileColor, 0);
					colors[tile] = color;
				}
				BitConverter.TryWriteBytes(pixels.AsSpan((py * width + px) * 4, 4), color);
			}
		}
		return Image.CreateFromData(width, height, false, Image.Format.Rgba8, pixels);
	}

	private static uint Pack(Color c) {
		return BitConverter.ToUInt32([(byte)c.R8, (byte)c.G8, (byte)c.B8, 255], 0);
	}

	// Where the city is drawn, in the box.
	private Vector2 PositionOf(City city, int mapWidth, bool wraps) {
		float dx = city.location.XCoordinate + 1 - x0;
		if (wraps) {
			dx = ((dx % mapWidth) + mapWidth) % mapWidth;
		}
		float dy = city.location.YCoordinate + 1 - y0;
		return new Vector2(dx / spanX * Size.X, dy / spanY * Size.Y);
	}

	// The cities, each a square with its name beside it: the chosen ones
	// first, so theirs are never hidden, then the capital, then the rest
	// biggest first. A name that would cover one already written is left
	// out.
	public override void _Draw() {
		Rect2 box = new(Vector2.Zero, Size);
		if (texture != null) {
			DrawTextureRect(texture, box, false);
		} else {
			DrawRect(box, Unknown);
		}
		GameMap map = EngineStorage.gameData?.map;
		if (map != null && texture != null) {
			Font font = GetThemeDefaultFont();
			List<Rect2> written = new();
			IEnumerable<City> order = cities
				.OrderByDescending(c => chosen.Contains(c))
				.ThenByDescending(c => c.IsCapital())
				.ThenByDescending(c => c.residents.Count);
			foreach (City city in order.Reverse()) {
				Vector2 at = PositionOf(city, map.numTilesWide, map.wrapHorizontally);
				bool picked = chosen.Contains(city);
				float side = picked ? 9 : 6;
				Rect2 square = new(at - new Vector2(side, side) / 2, new Vector2(side, side));
				DrawRect(square.Grow(1), Border);
				DrawRect(square, picked ? ChosenColor : CityColor);
			}
			foreach (City city in order) {
				Vector2 at = PositionOf(city, map.numTilesWide, map.wrapHorizontally);
				bool picked = chosen.Contains(city);
				Vector2 size = font.GetStringSize(city.name, HorizontalAlignment.Left, -1, FontSize);
				// Right of the square, else left, below or above it,
				// whichever is clear and inside the box.
				float ascent = font.GetAscent(FontSize);
				Vector2[] tries = [
					at + new Vector2(7, ascent / 2 - 2),
					at + new Vector2(-7 - size.X, ascent / 2 - 2),
					at + new Vector2(-size.X / 2, 6 + ascent),
					at + new Vector2(-size.X / 2, -7),
				];
				Rect2 AreaAt(Vector2 p) => new(p - new Vector2(0, ascent), size);
				bool Fits(Vector2 p) => box.Encloses(AreaAt(p)) && !written.Any(r => r.Intersects(AreaAt(p)));
				Vector2? clear = tries.Cast<Vector2?>().FirstOrDefault(p => Fits(p.Value));
				if (clear == null && !picked) {
					continue;
				}
				Vector2 textAt = clear ?? tries[0];
				written.Add(AreaAt(textAt));
				DrawStringOutline(font, textAt, city.name, HorizontalAlignment.Left, -1, FontSize, 4, Border);
				DrawString(font, textAt, city.name, HorizontalAlignment.Left, -1, FontSize, picked ? ChosenColor : CityColor);
			}
		}
		DrawRect(box, Border, false, 2);
	}
}

// The Mercenary Market's results: who won each unit, and what the player
// paid for theirs. Nobody's bids are shown.
public partial class MarketResultsPopup : Popup {
	private const int Width = 560;
	private const int RowHeight = 40;
	private const int IconSize = 32;

	private readonly Player player;
	private readonly List<MarketResult> results;

	public MarketResultsPopup(Player player, List<MarketResult> results) {
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins(top: 100);
		this.player = player;
		this.results = results;
	}

	private int Height => 130 + results.Count * RowHeight;

	public override void _Ready() {
		base._Ready();
		AddTexture(Width, Height);
		AddBackground(Width, Height);
		AddHeader("Mercenary Market", 10);

		for (int i = 0; i < results.Count; i++) {
			MarketResult result = results[i];
			UnitPrototype unit = EngineStorage.gameData?.unitPrototypes.Find(p => p.name == result.unitType);
			HBoxContainer row = new() { CustomMinimumSize = new Vector2(Width - 50, RowHeight) };
			row.SetPosition(new Vector2(25, 55 + i * RowHeight));
			row.AddThemeConstantOverride("separation", 10);
			if (unit != null) {
				row.AddChild(new TextureRect {
					Texture = TextureLoader.Load("unit_icons", new ItemContext(unit, player), useCache: true),
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					CustomMinimumSize = new Vector2(IconSize, IconSize),
					SizeFlagsVertical = SizeFlags.ShrinkCenter,
				});
			}
			string outcome;
			if (result.winners.Count == 0) {
				outcome = "unsold";
			} else {
				string who = string.Join(", ", result.winners.Select(w => w == player ? "us" : AdvisorUtils.CivTitle(w)));
				outcome = (result.winners.Count > 1 ? "tied, one each to " : "won by ") + who;
				if (result.winners.Contains(player)) {
					outcome += $" (we paid {result.price} gold)";
				}
			}
			row.AddChild(new Label {
				Text = $"{result.unitType}: {outcome}",
				VerticalAlignment = VerticalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			});
			AddChild(row);
		}

		AddConfirmButton(new Vector2(Width - 66, Height - 66), () => GetParent<PopupOverlay>().OnHidePopup());
	}
}
