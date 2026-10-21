using System;
using System.Runtime.InteropServices;

namespace QueryCiv3.Sav {
	// One event of the replay. Civ3 FUN_00598bf0 records an event's type, the
	// civ (a LEAD index), the tile and a text, and for a territory change the
	// tile's previous owner (Civ3 FUN_005e2e50; FUN_00599350 reads it back to
	// play the replay backwards). Civ3 writes the 10 bytes from the type on
	// (DataHeader, which is 10), so the 3 after PreviousOwner are whatever
	// was in memory.
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct RPLE {
		private fixed byte HeaderText[4];
		public int DataHeader;
		public byte EventType;
		public byte CivID;
		public short XLocation;
		public short YLocation;
		public byte PreviousOwner;
		private fixed byte UnknownBuffer2[3];

		// RPLE ends in a null-terminated string, which is variable length, so it can't be included here
	}
}
