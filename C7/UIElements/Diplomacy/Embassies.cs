using System.Text;

// Text for the popups about embassies and missions. The embassies and the
// missions themselves are on the Espionage screen (see EspionageScreen).
public static class Embassies {
	// The longest line the popups show before wrapping.
	private const int LineLength = 70;

	// Breaks the lines of the text that are too long for a popup at their
	// spaces, since the popups' labels don't wrap.
	public static string Wrap(string text) {
		StringBuilder result = new();
		foreach (string line in text.Split('\n')) {
			if (result.Length > 0) {
				result.Append('\n');
			}
			int lineStart = result.Length;
			foreach (string word in line.Split(' ')) {
				if (result.Length > lineStart && result.Length - lineStart + 1 + word.Length > LineLength) {
					result.Append("\n  ");
					lineStart = result.Length - 2;
				} else if (result.Length > lineStart) {
					result.Append(' ');
				}
				result.Append(word);
			}
		}
		return result.ToString();
	}
}
