using Godot;
using Serilog;
using C7Engine;

/**
 * Handles managing the advisor screens.
 * Showing them, hiding them... maybe some other things eventually.
 * This is part of the effort to de-centralize from Game.cs and be more event driven.
 */
public partial class GameViews : CenterContainer {
	private ILogger log = LogManager.ForContext<GameViews>();

	[Export] public WondersView wondersView;
	[Export] public VictoryStatusView victoryStatusView;
	[Export] public PalaceView palaceView;
	[Export] public SpaceRaceView spaceRaceView;
	[Export] public DemographicsView demographicsView;

	public override void _Ready() {
		Hide();
	}

	private void OnShowSpecificAdvisor(string advisor) {
		HideGameViews();
		Hide();
	}

	private void OnShowGameView(string gameView) {
		log.Information("Showing {GameView}", gameView);
		HideGameViews();

		// As with the advisors, a view that fails to fill itself in is still
		// shown, and the failure logged to the game's log rather than only to
		// Godot's own (owner's report, 2026-10-12).
		try {
			ShowGameViewScreen(gameView);
		} catch (System.Exception e) {
			log.Error(e, "Couldn't fill in {GameView}", gameView);
		}

		Show();
	}

	private void ShowGameViewScreen(string gameView) {
		switch (gameView) {
			case C7Action.ShowWondersView:
				wondersView.ShowView();
				break;
			case C7Action.ShowVictoryStatusView:
				victoryStatusView.ShowView();
				break;
			case C7Action.ShowPalaceView:
				palaceView.ShowView();
				break;
			case C7Action.ShowSpaceRaceView:
				spaceRaceView.ShowView();
				break;
			case C7Action.ShowDemographicsView:
				demographicsView.ShowView();
				break;
			default:
				log.Warning("Unknown game view: " + gameView);
				break;
		}
	}

	private void HideGameViews() {
		wondersView.Hide();
		victoryStatusView.Hide();
		palaceView.Hide();
		spaceRaceView.Hide();
		demographicsView.Hide();
	}
}
