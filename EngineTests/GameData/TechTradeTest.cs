using System.Collections.Generic;
using C7GameData;
using Xunit;

namespace EngineTests.GameData;

// Techs can only be traded to a player who could research them themselves.
public class TechTradeTest {
	[Fact]
	public void OnlyTechsTheRecipientCouldResearchAreTradable() {
		Tech alphabet = new() { id = ID.FromString("tech-1"), Name = "Alphabet" };
		Tech writing = new() { id = ID.FromString("tech-2"), Name = "Writing", Prerequisites = { alphabet } };
		Tech literature = new() { id = ID.FromString("tech-3"), Name = "Literature", Prerequisites = { writing } };
		List<Tech> techs = [alphabet, writing, literature];

		Player giver = new() { id = ID.FromString("player-1") };
		giver.knownTechs.UnionWith([alphabet.id, writing.id, literature.id]);
		Player recipient = new() { id = ID.FromString("player-2") };

		// The recipient knows nothing, so only Alphabet is researchable.
		Assert.Equal([alphabet], giver.GetTechsTradableTo(recipient, techs));

		// Once Alphabet is known, Writing opens up but Literature does not.
		recipient.knownTechs.Add(alphabet.id);
		Assert.Equal([writing], giver.GetTechsTradableTo(recipient, techs));

		// Nothing the giver doesn't know can be traded.
		Assert.Empty(recipient.GetTechsTradableTo(giver, techs));
	}

	[Fact]
	public void TechsFromALaterEraAreNotTradable() {
		Tech ancient = new() { id = ID.FromString("tech-1"), Name = "Ancient", EraCivilopediaName = EraUtils.ANCIENT_TIMES_CVLPD };
		Tech medieval = new() { id = ID.FromString("tech-2"), Name = "Medieval", EraCivilopediaName = EraUtils.MIDDLE_AGES_CVLPD };
		List<Tech> techs = [ancient, medieval];

		Player giver = new() { id = ID.FromString("player-1"), eraCivilopediaName = EraUtils.MIDDLE_AGES_CVLPD };
		giver.knownTechs.UnionWith([ancient.id, medieval.id]);
		Player recipient = new() { id = ID.FromString("player-2"), eraCivilopediaName = EraUtils.ANCIENT_TIMES_CVLPD };

		Assert.Equal([ancient], giver.GetTechsTradableTo(recipient, techs));
	}
}
