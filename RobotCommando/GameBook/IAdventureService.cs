namespace RobotCommando.GameBook;

public interface IAdventureService
{
    AdventureViewState Snapshot { get; }

    IState<AdventureViewState> ViewState { get; }

    Task StartNewGame(CancellationToken cancellationToken = default);

    Task SelectChoice(string actionId, CancellationToken cancellationToken = default);

    Task PickUpItem(string actionId, CancellationToken cancellationToken = default);

    Task TakeRobot(string actionId, CancellationToken cancellationToken = default);

    Task Fight(string actionId, CancellationToken cancellationToken = default);

    Task Escape(string actionId, CancellationToken cancellationToken = default);
}
