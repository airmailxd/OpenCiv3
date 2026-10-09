using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace C7Relay;

// The public list of games: tidying what hosts say of their games, and
// choosing which listings to give a player looking for one.
internal static class GameList {
	// The listing as the relay keeps it: its text without control characters
	// and with runs of whitespace made one space, cut to length, and its
	// numbers within reason. Null if it has no name left.
	public static GameListing Tidy(GameListing listing) {
		if (listing == null) {
			return null;
		}
		string name = TidyText(listing.name, RelayProtocol.MaxGameNameLength);
		if (name == null) {
			return null;
		}
		int seatsTotal = Math.Clamp(listing.seatsTotal, 1, 64);
		double? turnSeconds = listing.turnSeconds is double seconds && double.IsFinite(seconds) && seconds > 0
			? Math.Min(seconds, 7 * 24 * 3600)
			: null;
		return listing with {
			name = name,
			description = TidyText(listing.description, RelayProtocol.MaxGameDescriptionLength),
			seatsTotal = seatsTotal,
			seatsTaken = Math.Clamp(listing.seatsTaken, 0, seatsTotal),
			turn = Math.Clamp(listing.turn, 0, 1_000_000),
			turnSeconds = turnSeconds,
			mapSize = TidyText(listing.mapSize, RelayProtocol.MaxListingTextLength),
			hostName = TidyText(listing.hostName, RelayProtocol.MaxListingTextLength),
			locale = TidyText(listing.locale, 16),
		};
	}

	// The text without control or formatting characters (which could reorder
	// or hide what's shown), with whitespace collapsed and trimmed, and cut
	// to the length; null if nothing is left.
	public static string TidyText(string text, int maxLength) {
		if (text == null) {
			return null;
		}
		StringBuilder tidy = new(Math.Min(text.Length, maxLength));
		bool space = false;
		foreach (char c in text) {
			if (char.IsWhiteSpace(c)) {
				space = tidy.Length > 0;
				continue;
			}
			UnicodeCategory category = char.GetUnicodeCategory(c);
			if (category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.OtherNotAssigned
				or UnicodeCategory.PrivateUse) {
				continue;
			}
			if (space) {
				tidy.Append(' ');
				space = false;
			}
			tidy.Append(c);
			if (tidy.Length >= maxLength) {
				break;
			}
		}
		// Don't leave half of a surrogate pair at the end.
		if (tidy.Length > 0 && char.IsHighSurrogate(tidy[^1])) {
			tidy.Length--;
		}
		return tidy.Length == 0 ? null : tidy.ToString();
	}

	// What a player asked for of the list.
	public sealed record Query(string version = null, bool notFull = false, bool noPassword = false, int? limit = null);

	// The listings that match, those with open seats first, up to the
	// limit; and how many matched in all.
	public static (List<PublicGame> games, int total) Choose(IEnumerable<PublicGame> listed, Query query, int maxGames) {
		List<PublicGame> matching = listed
			.Where(g => query.version == null || g.gameVersion == query.version)
			.Where(g => !query.notFull || g.seatsOpen > 0)
			.Where(g => !query.noPassword || !g.game.hasPassword)
			.ToList();
		int limit = Math.Clamp(query.limit ?? maxGames, 0, maxGames);
		return (matching.OrderByDescending(g => g.seatsOpen > 0).Take(limit).ToList(), matching.Count);
	}
}
