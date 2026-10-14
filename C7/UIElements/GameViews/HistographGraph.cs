using System;
using System.Collections.Generic;
using C7GameData;
using Godot;

// The graph itself: the axes, and a line for each civ.
public partial class HistographGraph : Control {
	private List<(Color color, List<HistographPoint> points)> lines = new();
	private int maxValue = 1;
	private bool byYears;
	private Func<int, string> dateText;

	private const float AxisLeft = 48, AxisBottom = 26;

	public void SetLines(List<(Color, List<HistographPoint>)> lines, int maxValue, bool byYears, Func<int, string> dateText) {
		this.lines = lines;
		this.maxValue = Math.Max(1, maxValue);
		this.byYears = byYears;
		this.dateText = dateText;
		QueueRedraw();
	}

	private int X(HistographPoint p) => byYears ? p.date : p.turn;

	public override void _Draw() {
		Font font = GetThemeDefaultFont();
		const int fontSize = 12;
		Rect2 plot = new(AxisLeft, 0, Size.X - AxisLeft - 8, Size.Y - AxisBottom);
		Color ink = new(0.15f, 0.12f, 0.08f);
		Color grid = new(0.15f, 0.12f, 0.08f, 0.15f);

		int minX = int.MaxValue, maxX = int.MinValue;
		foreach ((_, List<HistographPoint> points) in lines) {
			foreach (HistographPoint p in points) {
				minX = Math.Min(minX, X(p));
				maxX = Math.Max(maxX, X(p));
			}
		}
		if (minX > maxX) {
			minX = 0;
			maxX = 1;
		}
		if (maxX == minX) {
			maxX = minX + 1;
		}

		// The value axis, with a few round steps.
		int step = NiceStep(maxValue / 5.0);
		int top = (maxValue + step - 1) / step * step;
		for (int v = 0; v <= top; v += step) {
			float y = plot.End.Y - plot.Size.Y * v / top;
			DrawLine(new Vector2(plot.Position.X, y), new Vector2(plot.End.X, y), grid);
			DrawString(font, new Vector2(0, y + 4), v.ToString(), HorizontalAlignment.Right, AxisLeft - 6, fontSize, ink);
		}
		DrawLine(plot.Position, new Vector2(plot.Position.X, plot.End.Y), ink, 1.5f);
		DrawLine(new Vector2(plot.Position.X, plot.End.Y), plot.End, ink, 1.5f);

		// The time axis: the first and last turn or year, and the middle.
		foreach (int x in new[] { minX, (minX + maxX) / 2, maxX }) {
			float px = plot.Position.X + plot.Size.X * (x - minX) / (maxX - minX);
			string text = byYears && dateText != null ? dateText(x) : $"Turn {x + 1}";
			// Centered under its point, but kept inside the graph: the last
			// date ran past the paper's edge.
			float width = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X;
			float left = Mathf.Clamp(px - width / 2, 0, Mathf.Max(0, Size.X - width));
			DrawString(font, new Vector2(left, plot.End.Y + 18), text, HorizontalAlignment.Left, -1, fontSize, ink);
		}

		foreach ((Color color, List<HistographPoint> points) in lines) {
			if (points.Count == 0) {
				continue;
			}
			Vector2[] polyline = new Vector2[points.Count];
			for (int i = 0; i < points.Count; ++i) {
				polyline[i] = new Vector2(
					plot.Position.X + plot.Size.X * (X(points[i]) - minX) / (maxX - minX),
					plot.End.Y - plot.Size.Y * points[i].value / top);
			}
			if (polyline.Length == 1) {
				DrawCircle(polyline[0], 2.5f, color);
			} else {
				// A dark edge keeps light civ colors visible on the paper.
				DrawPolyline(polyline, new Color(0, 0, 0, 0.35f), 4f, true);
				DrawPolyline(polyline, color, 2f, true);
			}
		}
	}

	// 1, 2, 5, 10, 20, 50, ... at least the given size.
	private static int NiceStep(double raw) {
		int step = 1;
		while (true) {
			foreach (int m in new[] { 1, 2, 5 }) {
				if (step * m >= raw) {
					return step * m;
				}
			}
			step *= 10;
		}
	}
}
