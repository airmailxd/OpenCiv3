using System.Runtime.CompilerServices;
using QueryCiv3;
using QueryCiv3.Biq;
using Xunit;

namespace EngineTests;

public class BiqSectionSizeTests {
	// The BIQ reader in QueryCiv3 splits dynamic sections into fixed-size chunks using the
	// [SECTION]_LEN_* constants; each constant is an offset bracket into the section's struct.
	// The sum of the constants must therefore equal the struct's size. A mismatch means a
	// Buffer.MemoryCopy can write past the end of the struct's backing memory. This happened
	// for CITY (38 + 36 = 74 vs. sizeof(CITY) = 70), writing 4 bytes past every city array
	// element and into the GC heap past the last one.
	// The sizes are compared with Unsafe.SizeOf, which is the size the readers use (sizeof(T)); Marshal.SizeOf
	// is the size of the marshalled struct, which differs for structs with bool fields.
	[Fact]
	public void DynamicSectionLengthConstants_SumToStructSize() {
		Assert.Equal(BiqData.GOVT_LEN_1 + BiqData.GOVT_LEN_2, Unsafe.SizeOf<GOVT>());
		Assert.Equal(BiqData.TERR_LEN_1 + BiqData.TERR_LEN_2, Unsafe.SizeOf<TERR>());
		Assert.Equal(BiqData.RACE_LEN_1 + BiqData.RACE_LEN_2 + BiqData.RACE_LEN_3 + BiqData.RACE_LEN_4, Unsafe.SizeOf<RACE>());
		Assert.Equal(BiqData.CITY_LEN_1 + BiqData.CITY_LEN_2, Unsafe.SizeOf<CITY>());
		Assert.Equal(BiqData.WMAP_LEN_1 + BiqData.WMAP_LEN_2, Unsafe.SizeOf<WMAP>());
		Assert.Equal(BiqData.PRTO_LEN_1 + BiqData.PRTO_LEN_2, Unsafe.SizeOf<PRTO>());
		Assert.Equal(BiqData.LEAD_LEN_1 + BiqData.LEAD_LEN_2 + BiqData.LEAD_LEN_3, Unsafe.SizeOf<LEAD>());
		Assert.Equal(BiqData.RULE_LEN_1 + BiqData.RULE_LEN_2 + BiqData.RULE_LEN_3, Unsafe.SizeOf<RULE>());
		Assert.Equal(BiqData.GAME_LEN_1 + BiqData.GAME_LEN_2 + BiqData.GAME_LEN_3, Unsafe.SizeOf<GAME>());
	}

	// SavData reads its LEAD and CITY structs in chunks in the same way
	[Fact]
	public void SavLengthConstants_SumToStructSize() {
		Assert.Equal(SavData.LEAD_LEN_1 + SavData.LEAD_LEN_2 + SavData.LEAD_LEN_3 + SavData.LEAD_LEN_4, Unsafe.SizeOf<QueryCiv3.Sav.LEAD>());
		Assert.Equal(SavData.CITY_LEN_1 + SavData.CITY_LEN_2 + SavData.CITY_LEN_3, Unsafe.SizeOf<QueryCiv3.Sav.CITY>());
	}
}
