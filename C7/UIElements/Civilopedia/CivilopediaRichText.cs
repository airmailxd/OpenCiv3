using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using C7GameData;
using Godot;

// Turns the lines of a Civilopedia article into BBCode for a RichTextLabel:
// bold text, links to other entries as [url=key], and the rows of columns
// separated by tabs (like a government's statistics) as tables.
public static class CivilopediaRichText {
	public static readonly Color TextColor = new("101010");
	public static readonly Color LinkColor = new("1a1ad0");

	private static readonly Regex Tabs = new("\t+");

	// hasEntry says whether a link's key leads anywhere; links that don't
	// are shown as plain text.
	public static string ToBBCode(IReadOnlyList<string> lines, Func<string, bool> hasEntry) {
		StringBuilder result = new();
		int i = 0;
		while (i < lines.Count) {
			if (IsTableRow(lines, i)) {
				List<string[]> rows = new();
				while (i < lines.Count && IsTableRow(lines, i)) {
					rows.Add(Tabs.Split(lines[i]));
					i++;
				}
				AppendTable(result, rows, hasEntry);
				continue;
			}
			AppendLine(result, lines[i], hasEntry);
			result.Append('\n');
			i++;
		}
		return result.ToString().TrimEnd('\n');
	}

	// A line with columns, or a heading line (like "Unit Support") between
	// lines with columns.
	private static bool IsTableRow(IReadOnlyList<string> lines, int i) {
		if (lines[i].Contains('\t')) {
			return true;
		}
		return lines[i].Trim().Length > 0
			&& i > 0 && lines[i - 1].Contains('\t')
			&& i + 1 < lines.Count && lines[i + 1].Contains('\t');
	}

	private static void AppendTable(StringBuilder result, List<string[]> rows, Func<string, bool> hasEntry) {
		int columns = 1;
		foreach (string[] row in rows) {
			columns = Math.Max(columns, row.Length);
		}
		result.Append($"[table={columns}]");
		foreach (string[] row in rows) {
			for (int c = 0; c < columns; c++) {
				result.Append("[cell padding=0,0,24,0]");
				if (c < row.Length) {
					AppendLine(result, row[c], hasEntry);
				}
				result.Append("[/cell]");
			}
		}
		result.Append("[/table]\n");
	}

	public static void AppendLine(StringBuilder result, string line, Func<string, bool> hasEntry) {
		foreach (CivilopediaMarkup.Span span in CivilopediaMarkup.Tokenize(line)) {
			string text = Escape(span.Text);
			if (span.Bold) {
				text = $"[b]{text}[/b]";
			}
			if (span.IsLink && hasEntry(span.LinkKey)) {
				text = $"[url={span.LinkKey}][color=#{LinkColor.ToHtml(false)}]{text}[/color][/url]";
			}
			result.Append(text);
		}
	}

	public static string Escape(string text) {
		return text.Replace("[", "[lb]");
	}

	public static RichTextLabel MakeLabel(int fontSize = 13) {
		RichTextLabel label = new() {
			BbcodeEnabled = true,
			ScrollActive = true,
			FitContent = false,
			SelectionEnabled = false,
			MouseFilter = Control.MouseFilterEnum.Pass,
			MetaUnderlined = true,
		};
		label.AddThemeFontOverride("normal_font", Fonts.Regular);
		label.AddThemeFontOverride("bold_font", Fonts.Bold);
		label.AddThemeFontOverride("italics_font", Fonts.Italic);
		label.AddThemeFontSizeOverride("normal_font_size", fontSize);
		label.AddThemeFontSizeOverride("bold_font_size", fontSize);
		label.AddThemeFontSizeOverride("italics_font_size", fontSize);
		label.AddThemeColorOverride("default_color", TextColor);
		label.AddThemeConstantOverride("line_separation", 1);
		label.AddThemeConstantOverride("table_h_separation", 0);
		return label;
	}

	public static class Fonts {
		private static FontFile regular, bold, italic;
		public static FontFile Regular => regular ??= ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Regular.ttf");
		public static FontFile Bold => bold ??= ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Bold.ttf");
		public static FontFile Italic => italic ??= ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Italic.ttf");
	}
}
