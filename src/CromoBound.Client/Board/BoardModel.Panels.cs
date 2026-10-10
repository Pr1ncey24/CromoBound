using CromoBound.Engine.Decisions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.Views;
using CromoBound.Models.Cards;

namespace CromoBound.Client.Board;

public sealed partial class BoardModel
{
    /// <summary>The decisions the turn doesn't handle: the pre-game steps, the prompts, the undo answer, a later part's choices, and
    /// waiting for the opponent (who decides is in <see cref="PlayerView.Deciding"/>; the decision itself only reaches its player).</summary>
    private void Other(PlayerView view, Build build)
    {
        var opponent = build.Opponent;
        var game = view.GameNumber;
        switch (view.Decision)
        {
            case ResolveManuallyDecision resolve:
                Panel = new ResolvePanel(build.Book.NameOf(resolve.CardId), build.Book.DefaultPrinting(resolve.CardId), resolve.Text);
                break;
            case TurnPointDecision point:
                Panel = new TurnPointPanel(PointTitle(point.Point), [.. point.Cards.Select(build.Find).OfType<CardView>().Select(c => build.Card(c, this))]);
                break;
            case ConfirmUndoDecision:
                Panel = new UndoPanel(opponent, ActivityLog.LastAction(view, build.Book, opponent));
                break;
            case PickBattlefieldDecision pick:
                Panel = new PickBattlefieldPanel(game, Games(view), [.. pick.Choices.Where(c => c.Player == view.Viewer).SelectMany(c => c.Printings)
                    .Select(p => new PrintingOption(p, PrintingName(p, build)))], false, opponent);
                break;
            case ChoosePlayOrderDecision:
                Panel = new PlayOrderPanel(game, false, opponent);
                break;
            case SideboardDecision sideboard:
                var choice = sideboard.Choices.FirstOrDefault(c => c.Player == view.Viewer);
                Panel = new SideboardPanel(game, Rows(choice?.Main, build), Rows(choice?.Sideboard, build), false, opponent);
                break;
            case MulliganDecision mulligan:
                Panel = new MulliganPanel(game, [.. mulligan.Hand.Select(build.Find).OfType<CardView>().Select(c => build.Card(c, this))], false, opponent);
                break;
            case null when view.Deciding.Count > 0:
                Panel = view.Stage switch
                {
                    MatchStage.PickBattlefields => new PickBattlefieldPanel(game, Games(view), [], true, opponent),
                    MatchStage.PlayOrder => new PlayOrderPanel(game, true, opponent),
                    MatchStage.Sideboarding => new SideboardPanel(game, [], [], true, opponent),
                    MatchStage.Mulligan => new MulliganPanel(game, [], true, opponent),
                    _ => null,
                };
                UndoWaiting = view.DecisionKind == "ConfirmUndo";
                break;
            case { } later:
                Panel = new LaterPanel(view.DecisionKind ?? later.GetType().Name, What(later));
                break;
        }
    }

    private static int Games(PlayerView view) => view.Format == MatchFormat.Bo3 ? 3 : 1;

    private static string PointTitle(TurnPoint point) => point switch
    {
        TurnPoint.StartOfBeginning => "Start of your Beginning Phase",
        TurnPoint.StartOfMain => "Start of your Main Phase",
        _ => "End of your turn",
    };

    private static string What(PendingDecision decision) => decision switch
    {
        ChooseShowdownDecision => "choose where the showdown happens",
        AssignDamageDecision => "assign combat damage",
        ChooseTargetsDecision => "choose targets",
        ChoosePlayerDecision => "choose a player",
        ChooseCardsDecision => "choose cards",
        OptionalDecision => "answer a yes or no question",
        OrderTriggersDecision => "order triggered abilities",
        _ => "make a choice",
    };

    private static string PrintingName(string printing, Build build) =>
        build.Book.PrintingOf(printing) is { } known ? build.Book.NameOf(known.CardId) : CardBook.Unknown;

    private static List<DeckRow> Rows(IReadOnlyList<DeckEntry>? entries, Build build) =>
        [.. (entries ?? []).Select(e => new DeckRow(e.Printing, PrintingName(e.Printing, build), e.Count))];
}
