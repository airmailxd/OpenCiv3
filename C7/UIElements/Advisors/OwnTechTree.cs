using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Godot;

// The science advisor's tree for rules whose advances aren't Civ3's
// (Rules.OwnTechTree, as MDJ's): Civ3's era backgrounds have the arrows of
// Civ3's own tree drawn into them, which point at nothing there. So the
// arrows are taken out of the backgrounds, drawn between the advances' boxes
// from their prerequisites instead, and a page with advances placed off it
// (MDJ places those behind its barrier advances at 999, 999) is laid out by
// how deep each advance is in the page's tree.
public static class OwnTechTree {
	// The part of a background where the tree is drawn, as shares of its
	// size: the parchment inside the frame, below the title, left of the
	// advisor.
	private const float TreeLeft = 0.06f, TreeTop = 0.12f, TreeRight = 0.97f, TreeBottom = 0.92f;

	public static Rect2 TreeArea(Vector2 size) {
		return new Rect2(size.X * TreeLeft, size.Y * TreeTop, size.X * (TreeRight - TreeLeft), size.Y * (TreeBottom - TreeTop));
	}

	private static readonly Dictionary<ImageTexture, ImageTexture> cleaned = new();

	// The background without its arrows: in its tree area, a pixel much
	// darker than the parchment around it (the arrows are thin dark lines)
	// takes the color of the parchment around it.
	public static ImageTexture WithoutArrows(ImageTexture background) {
		if (background == null) {
			return null;
		}
		if (cleaned.TryGetValue(background, out ImageTexture done)) {
			return done;
		}
		Image image = background.GetImage();
		image.Convert(Image.Format.Rgba8);
		int w = image.GetWidth(), h = image.GetHeight();
		Rect2 area = TreeArea(new Vector2(w, h));
		int x0 = (int)area.Position.X, y0 = (int)area.Position.Y, x1 = (int)area.End.X, y1 = (int)area.End.Y;
		int aw = x1 - x0, ah = y1 - y0;
		float[] lum = new float[aw * ah];
		Color[] colors = new Color[aw * ah];
		for (int y = 0; y < ah; y++) {
			for (int x = 0; x < aw; x++) {
				Color c = image.GetPixel(x0 + x, y0 + y);
				colors[y * aw + x] = c;
				lum[y * aw + x] = c.R * 0.3f + c.G * 0.55f + c.B * 0.15f;
			}
		}
		// The parchment's brightness around each pixel: the median of a
		// coarse grid of samples, which the thin lines hardly touch.
		const int cell = 16;
		int gw = (aw + cell - 1) / cell, gh = (ah + cell - 1) / cell;
		float[] paper = new float[gw * gh];
		for (int gy = 0; gy < gh; gy++) {
			for (int gx = 0; gx < gw; gx++) {
				List<float> samples = new();
				for (int y = gy * cell; y < Math.Min(ah, (gy + 1) * cell); y++) {
					for (int x = gx * cell; x < Math.Min(aw, (gx + 1) * cell); x++) {
						samples.Add(lum[y * aw + x]);
					}
				}
				samples.Sort();
				paper[gy * gw + gx] = samples[samples.Count / 2];
			}
		}
		bool[] line = new bool[aw * ah];
		for (int y = 0; y < ah; y++) {
			for (int x = 0; x < aw; x++) {
				line[y * aw + x] = lum[y * aw + x] < paper[(y / cell) * gw + x / cell] * 0.82f;
			}
		}
		// Grow the lines by a pixel, for their soft edges, and fill them from
		// the parchment around, pass by pass from the outside in.
		bool[] grown = (bool[])line.Clone();
		for (int y = 1; y < ah - 1; y++) {
			for (int x = 1; x < aw - 1; x++) {
				if (line[y * aw + x]) {
					for (int dy = -1; dy <= 1; dy++) {
						for (int dx = -1; dx <= 1; dx++) {
							grown[(y + dy) * aw + x + dx] = true;
						}
					}
				}
			}
		}
		bool[] missing = grown;
		for (int pass = 0; pass < 12 && missing.Any(m => m); pass++) {
			bool[] next = (bool[])missing.Clone();
			for (int y = 0; y < ah; y++) {
				for (int x = 0; x < aw; x++) {
					if (!missing[y * aw + x]) {
						continue;
					}
					float r = 0, g = 0, b = 0;
					int n = 0;
					for (int dy = -2; dy <= 2; dy++) {
						for (int dx = -2; dx <= 2; dx++) {
							int xx = x + dx, yy = y + dy;
							if (xx < 0 || yy < 0 || xx >= aw || yy >= ah || missing[yy * aw + xx]) {
								continue;
							}
							Color c = colors[yy * aw + xx];
							r += c.R;
							g += c.G;
							b += c.B;
							n++;
						}
					}
					if (n > 0) {
						colors[y * aw + x] = new Color(r / n, g / n, b / n);
						next[y * aw + x] = false;
					}
				}
			}
			missing = next;
		}
		for (int y = 0; y < ah; y++) {
			for (int x = 0; x < aw; x++) {
				image.SetPixel(x0 + x, y0 + y, colors[y * aw + x]);
			}
		}
		ImageTexture result = ImageTexture.CreateFromImage(image);
		cleaned[background] = result;
		return result;
	}

	// Whether the advances of a page need laying out: any is placed off the
	// page's tree area.
	public static bool NeedsLayout(IEnumerable<Tech> techs, Vector2 size) {
		Rect2 area = TreeArea(size);
		return techs.Any(t => !area.HasPoint(new Vector2(t.X, t.Y)));
	}

	// Where each advance of a page goes: a column by how deep it is in the
	// page's tree (how many of the page's advances lead to it), and the rows
	// of each column spread down the tree area, in the order the advances
	// are listed.
	public static Dictionary<Tech, Vector2> Layout(List<Tech> techs, Vector2 size, Vector2 boxSize) {
		HashSet<Tech> onPage = techs.ToHashSet();
		Dictionary<Tech, int> depth = new();
		int Depth(Tech t, int guard = 0) {
			if (depth.TryGetValue(t, out int d)) {
				return d;
			}
			int deepest = -1;
			if (guard < 64) {
				foreach (Tech p in t.Prerequisites.Where(onPage.Contains)) {
					deepest = Math.Max(deepest, Depth(p, guard + 1));
				}
			}
			depth[t] = deepest + 1;
			return deepest + 1;
		}
		foreach (Tech t in techs) {
			Depth(t);
		}
		Rect2 area = TreeArea(size);
		int columns = depth.Values.DefaultIfEmpty(0).Max() + 1;
		float colWidth = area.Size.X / columns;
		Dictionary<Tech, Vector2> at = new();
		foreach (IGrouping<int, Tech> column in techs.GroupBy(t => depth[t])) {
			List<Tech> col = column.ToList();
			float rowHeight = area.Size.Y / col.Count;
			for (int i = 0; i < col.Count; i++) {
				float x = area.Position.X + column.Key * colWidth + (colWidth - boxSize.X) / 2;
				float y = area.Position.Y + i * rowHeight + (rowHeight - boxSize.Y) / 2;
				at[col[i]] = new Vector2(Math.Max(area.Position.X, x), Math.Max(area.Position.Y, y));
			}
		}
		return at;
	}
}

// The arrows between the boxes of a page: from each prerequisite on the
// page to the advance it leads to, right side to left side, in the brown of
// Civ3's.
public partial class TechTreeArrows : Control {
	private static readonly Color ArrowColor = new(0.45f, 0.2f, 0.08f);

	private readonly Dictionary<Tech, TechBox> boxes = new();

	public TechTreeArrows() {
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public void Show(Dictionary<Tech, TechBox> page) {
		boxes.Clear();
		foreach ((Tech t, TechBox b) in page) {
			boxes[t] = b;
		}
		Visible = true;
		QueueRedraw();
	}

	public void Clear() {
		boxes.Clear();
		Visible = false;
		QueueRedraw();
	}

	public override void _Draw() {
		foreach ((Tech tech, TechBox box) in boxes) {
			foreach (Tech prereq in tech.Prerequisites) {
				if (!boxes.TryGetValue(prereq, out TechBox from)) {
					continue;
				}
				Vector2 start = from.Position + new Vector2(from.Size.X, from.Size.Y / 2);
				Vector2 end = box.Position + new Vector2(0, box.Size.Y / 2);
				if (end.X <= start.X + 12) {
					// Not to its right: straight across.
					DrawLine(start, end, ArrowColor, 2);
				} else {
					float midX = start.X + Math.Max(8, (end.X - start.X) / 2);
					DrawPolyline([start, new Vector2(midX, start.Y), new Vector2(midX, end.Y), end - new Vector2(7, 0)], ArrowColor, 2);
				}
				DrawColoredPolygon([end, end + new Vector2(-9, -5), end + new Vector2(-9, 5)], ArrowColor);
			}
		}
	}
}
