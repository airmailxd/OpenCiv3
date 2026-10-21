using Godot;
using Serilog;
using C7Engine;

/**
 * Handles managing the advisor screens.
 * Showing them, hiding them... maybe some other things eventually.
 * This is part of the effort to de-centralize from Game.cs and be more event driven.
 */
public partial class Advisors : CenterContainer {
	private ILogger log = LogManager.ForContext<Advisors>();

	private string latest = C7Action.ShowDomesticAdvisor;

	[Export] public DomesticAdvisor domesticAdvisor;
	[Export] public TradeAdvisor tradeAdvisor;
	[Export] public MilitaryAdvisor militaryAdvisor;
	[Export] public ForeignAdvisor foreignAdvisor;
	[Export] public CulturalAdvisor culturalAdvisor;
	[Export] public ScienceAdvisor scienceAdvisor;

	// Civ3's hidden developer advisor, built here rather than in the scene.
	private SorenAdvisor sorenAdvisor;

	public override void _Ready() {
		sorenAdvisor = new SorenAdvisor { Name = "SorenAdvisor", Visible = false };
		AddChild(sorenAdvisor);
		Hide();
	}

	private void OnShowGameView(string gameView) {
		HideAdvisors();
		Hide();
	}

	/// <summary>
	/// Switches to the given advisor, as when its face in the sidebar is clicked.
	/// </summary>
	public void ShowAdvisor(AdvisorHead.Advisor advisor) {
		OnShowSpecificAdvisor(advisor switch {
			AdvisorHead.Advisor.Domestic => C7Action.ShowDomesticAdvisor,
			AdvisorHead.Advisor.Trade => C7Action.ShowTradeAdvisor,
			AdvisorHead.Advisor.Military => C7Action.ShowMilitaryAdvisor,
			AdvisorHead.Advisor.Foreign => C7Action.ShowForeignAdvisor,
			AdvisorHead.Advisor.Culture => C7Action.ShowCulturalAdvisor,
			AdvisorHead.Advisor.Science => C7Action.ShowScienceAdvisor,
			_ => throw new System.ArgumentOutOfRangeException(nameof(advisor)),
		});
	}

	private void OnShowSpecificAdvisor(string advisorType) {
		log.Information("Showing {Advisor}", advisorType);
		if (advisorType != latest) {
			latest = advisorType;
			HideAdvisors();
		}

		// An advisor that fails to fill itself in is still shown, and the
		// failure logged to the game's log. Before, it reached only Godot's
		// own log and the advisor never appeared (owner's report, 2026-10-12).
		try {
			ShowAdvisorScreen(advisorType);
		} catch (System.Exception e) {
			log.Error(e, "Couldn't fill in {Advisor}", advisorType);
		}

		Show();
	}

	private void ShowAdvisorScreen(string advisorType) {
		switch (advisorType) {
			case C7Action.ShowDomesticAdvisor:
				domesticAdvisor.ShowAdvisor();
				break;
			case C7Action.ShowTradeAdvisor:
				tradeAdvisor.ShowAdvisor();
				break;
			case C7Action.ShowForeignAdvisor:
				foreignAdvisor.ShowAdvisor();
				break;
			case C7Action.ShowMilitaryAdvisor:
				militaryAdvisor.ShowAdvisor();
				break;
			case C7Action.ShowCulturalAdvisor:
				culturalAdvisor.ShowAdvisor();
				break;
			case C7Action.ShowScienceAdvisor:
				scienceAdvisor.ShowAdvisor();
				break;
			case C7Action.ShowSorenAdvisor:
				sorenAdvisor.ShowAdvisor();
				break;
			default:
				log.Warning("Unknown advisor type: " + advisorType);
				break;
		}
	}

	/// <summary>
	/// Call after the game data was replaced (e.g. by a LAN snapshot). The
	/// advisors hold objects of the old game, so let go of them and draw the
	/// advisor on show again from the new game.
	/// </summary>
	public void RefreshAfterGameReplaced() {
		domesticAdvisor.ForgetGameObjects();
		scienceAdvisor.RefreshAfterGameReplaced();
		if (Visible) {
			OnShowSpecificAdvisor(latest);
		}
	}

	private void HideAdvisors() {
		domesticAdvisor.Hide();
		tradeAdvisor.Hide();
		militaryAdvisor.Hide();
		foreignAdvisor.Hide();
		culturalAdvisor.Hide();
		scienceAdvisor.Hide();
		sorenAdvisor.Hide();
	}
}
