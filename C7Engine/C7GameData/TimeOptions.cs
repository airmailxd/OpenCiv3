using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;
using C7Engine;
using Serilog;

namespace C7GameData;

public enum TimeUnit {
	Years,
	Months,
	Weeks,
	Days,
	Hours,
}

public sealed class TimeOptions {
	public TimeUnit baseUnit { get; init; } = TimeUnit.Years;
	public string negativeLabel { get; init; } = "BC";
	public string positiveLabel { get; init; } = "AD";
	public int startYear { get; init; } = -4000;
	public int startMonth { get; init; } = 1;

	public int startWeek { get; init; } = 1;
	// custom
	public int startDay { get; init; } = 1;
	// custom
	public int startHour { get; init; } = 12;
	// Row 0 holds the number of turns in each segment, row 1 the time units
	// that pass each turn of that segment. After the last segment, time
	// passes at 1 time unit per turn.
	//
	// Setting it clears the cached display times. Don't change the array in
	// place once dates have been displayed; set a new one instead.
	public int[,] timeScale {
		get => _timeScale;
		set {
			_timeScale = value;
			DisplayTime.Clear();
		}
	}
	private int[,] _timeScale = new int[,] { { 25, 25, 40, 50, 100, 100, 100, 50000 }, { 50, 40, 25, 20, 10, 5, 2, 1 } };

	public int turnLimit { get; init; } = 540;

	[JsonIgnore]
	public int currentYear { get; private set; } = -1;
	[JsonIgnore]
	public int currentMonth { get; private set; } = -1;
	[JsonIgnore]
	public int currentWeek { get; private set; } = -1;
	[JsonIgnore]
	// custom
	public int currentDay { get; private set; } = -1;
	[JsonIgnore]
	// custom
	public int currentHour { get; private set; } = -1;

	// used to cache the turn number to display text value
	private readonly Dictionary<int, string> DisplayTime = [];


	/// <summary>
	/// Get the corresponding NON-normalized time unit number based on the turn number,
	/// taking into account the various time intervals from the timeSpan array.<br/><br/>
	/// In a standard game, for years as a base time unit, turn 0 will return -4000, turn 1 will return -3950, etc<br/><br/>
	/// For months as a base time unit (and 1 unit intervals) turn 2 will be month 3, turn 14 will be month 15, etc<br/><br/>
	/// For weeks as a base time unit (and 1 unit intervals) turn 2 will be week 3, turn 54 will be week 55, etc<br/><br/>
	/// This has no side effects. (It used to also set the current* properties to the result, but those are
	/// scratch values for the Lua display text function, which sets them itself, and calls for other turns,
	/// such as the culture calculations, would overwrite them.)
	/// </summary>
	/// <param name="current"></param>
	/// <returns></returns>
	/// <exception cref="Exception"></exception>
	public int GetRawNumber(int current) {
		int[,] scale = timeScale;
		int segments = scale.GetLength(1);

		// The turns in the segments before the current one, and the time
		// units that passed in them.
		int turnsBefore = 0;
		int unitsBefore = 0;
		for (int i = 0; i < segments; ++i) {
			int turns = scale[0, i];
			if (current <= turnsBefore + turns) {
				// e.x. if the current turn is 26, we are in the second
				// segment of a std game, 1 turn in, so 25*50 years from the
				// first segment plus 1*40 from this one have passed.
				return GetStartingPoint() + unitsBefore + (current - turnsBefore) * scale[1, i];
			}
			turnsBefore += turns;
			unitsBefore += turns * scale[1, i];
		}

		// Past the last segment we default to 1 time unit per turn.
		return GetStartingPoint() + unitsBefore + (current - turnsBefore);
	}

	/// <summary>
	/// Get the turn number from the current unit time.<br/>
	/// So, assuming a std game<br/>
	/// -3950 will return 1, etc<br/>
	/// month 5, Year 1, will return 4, month 3 year 2 will return 14, etc<br/>
	/// week 3 will return 3, and week 53 will  return 1, etc<br/>
	/// </summary>
	/// <param name="time"></param>
	/// <returns></returns>
	/// <exception cref="Exception"></exception>
	public int GetTurnFromRaw(int time) {
		int current = GetStartingPoint();
		int turn = 0;
		var extra = 0;
		for (int i = 0; i < timeScale.GetLength(1); i++) {
			var j = 1;
			if (time == current) return turn;
			while (current + extra < time && j <= timeScale[0, i]) {
				extra += timeScale[1, i];
				++turn;
				if (current + extra == time)
					return turn;
				++j;
			}
			// Account for all the turns of this segment, even those skipped
			// because the time was already passed.
			turn += Math.Max(0, timeScale[0, i] - (j - 1));
			extra += Math.Max(0, timeScale[0, i] - (j - 1)) * timeScale[1, i];
		}

		// Past the last segment, as in GetRawNumber, each turn is 1 time unit.
		if (time >= current + extra) {
			return turn + (time - current - extra);
		}

		throw new Exception($"The current time {time} is unknown to us");
	}

	public string GetDisplayTime(int current) {
		if (DisplayTime.TryGetValue(current, out var value)) return value;
		var time = GetRawNumber(current);
		var displayText = GetDisplayTimeFromRaw(time);
		DisplayTime[current] = displayText;
		return displayText;
	}

	public string GetDisplayTimeFromRaw(int time) {
		string label = "PLACEHOLDER_TIME";
		var displayTimeLabelFunction = EngineStorage.gameData.luaBehaviorEngine.ImportFunc<Func<int, string>>("gameplay.time.display_text");
		label = displayTimeLabelFunction.Invoke(time);
		return label;
	}

	private int GetStartingPoint() {
		return baseUnit switch {
			TimeUnit.Years => startYear,
			TimeUnit.Months => startMonth,
			TimeUnit.Weeks => startWeek,
			TimeUnit.Days => startDay,
			TimeUnit.Hours => startHour,
			_ => throw new InvalidEnumArgumentException($"{baseUnit} is not a valid TimeUnit")
		};
	}

	[LuaMethod]
	public void SetTimeUnitCurrent(TimeUnit timeUnit, int value) {
		Log.Information("Setting unit {TimeUnit} to {Value}", timeUnit, value);
		switch (timeUnit) {
			case TimeUnit.Years:
				currentYear = value;
				break;
			case TimeUnit.Months:
				currentMonth = value;
				break;
			case TimeUnit.Weeks:
				currentWeek = value;
				break;
			case TimeUnit.Days:
				currentDay = value;
				break;
			case TimeUnit.Hours:
				currentHour = value;
				break;
			default:
				throw new InvalidEnumArgumentException($"{baseUnit} is not a valid TimeUnit");
		}
	}

	[LuaMethod]
	public string GetAbbrMonthNameByIndex(int index) {
		return CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(index);
	}
	[LuaMethod]
	public string GetMonthNameByIndex(int index) {
		return CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(index);
	}
}
